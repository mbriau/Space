using Space.Core.Levels;
using Space.Core.Ships;
using Space.Core.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Combat
{
    /// <summary>
    /// Automatic weapons: each weapon picks a target (the ship's <see cref="AttackTarget"/> when it is
    /// valid, otherwise the nearest enemy in range), decides to fire from the estimated hit chance
    /// and its <see cref="FireMode"/>, then resolves the shot with a deterministic random draw.
    /// Runs on one thread: shots write the hull of other ships.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(ShipMovementSystem))]
    [BurstCompile]
    public partial struct WeaponSystem : ISystem
    {
        EntityQuery _targets;

        public void OnCreate(ref SystemState state)
        {
            _targets = SystemAPI.QueryBuilder()
                .WithAll<Ship, Team, SimPosition, ShipVelocity, ShipRadius, Hull>()
                .Build();

            var queue = state.EntityManager.CreateEntity(typeof(ShotEventQueue));
            state.EntityManager.AddBuffer<ShotEvent>(queue);
            state.EntityManager.SetName(queue, "ShotEvents");

            state.RequireForUpdate<SimulationTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<SimulationTime>();
            var seed = SystemAPI.TryGetSingleton<LevelState>(out var level) ? level.RunSeed : 1u;

            var job = new FireJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Tick = time.Tick,
                Seed = seed,
                Entities = _targets.ToEntityArray(state.WorldUpdateAllocator),
                Teams = _targets.ToComponentDataArray<Team>(state.WorldUpdateAllocator),
                Positions = _targets.ToComponentDataArray<SimPosition>(state.WorldUpdateAllocator),
                Velocities = _targets.ToComponentDataArray<ShipVelocity>(state.WorldUpdateAllocator),
                Radii = _targets.ToComponentDataArray<ShipRadius>(state.WorldUpdateAllocator),
                Hulls = SystemAPI.GetComponentLookup<Hull>(false),
                Shields = SystemAPI.GetComponentLookup<Shield>(false),
                Shots = SystemAPI.GetSingletonBuffer<ShotEvent>(),
            };
            state.Dependency = job.Schedule(state.Dependency);
        }

        [BurstCompile]
        partial struct FireJob : IJobEntity
        {
            public float DeltaTime;
            public uint Tick;
            public uint Seed;

            [ReadOnly] public NativeArray<Entity> Entities;
            [ReadOnly] public NativeArray<Team> Teams;
            [ReadOnly] public NativeArray<SimPosition> Positions;
            [ReadOnly] public NativeArray<ShipVelocity> Velocities;
            [ReadOnly] public NativeArray<ShipRadius> Radii;

            public ComponentLookup<Hull> Hulls;
            public ComponentLookup<Shield> Shields;
            public DynamicBuffer<ShotEvent> Shots;

            void Execute(Entity entity, in SimPosition position, in Team team, in AttackTarget attack,
                         in PowerGrid power, DynamicBuffer<Weapon> weapons)
            {
                if (Hulls.HasComponent(entity) && Hulls[entity].Current <= 0f)
                    return;

                for (var w = 0; w < weapons.Length; w++)
                {
                    var weapon = weapons[w];

                    // Weapons charge in proportion to their subsystem's power, and stay idle without it.
                    var charge = power.Factor(weapon.PowerSource);
                    weapon.CooldownLeft = math.max(0f, weapon.CooldownLeft - DeltaTime * charge);

                    var target = weapon.Enabled && weapon.Ammo != 0 && charge > 0f
                        ? ChooseTarget(position.Value, team.Value, attack.Value, weapon)
                        : -1;
                    weapon.CurrentTarget = target >= 0 ? Entities[target] : Entity.Null;

                    if (target >= 0 && weapon.CooldownLeft <= 0f)
                        TryFire(entity, w, ref weapon, position.Value, team.Value, target);

                    weapons[w] = weapon;
                }
            }

            float Effectiveness(in Weapon weapon, Entity target) =>
                DamageModel.Effectiveness(weapon, Shields.HasComponent(target) ? Shields[target] : default);

            /// <summary>
            /// Index of the target in the arrays, or -1. Each weapon prefers targets it hurts most
            /// (ion against raised shields, laser and cannon against weak or downed shields), then the
            /// closest; it ignores targets it cannot hurt. The ordered target wins when it qualifies.
            /// </summary>
            int ChooseTarget(double3 from, byte team, Entity ordered, in Weapon weapon)
            {
                var rangeSq = (double)weapon.Range * weapon.Range;
                var best = -1;
                var bestScore = double.MaxValue;
                for (var i = 0; i < Entities.Length; i++)
                {
                    var candidate = Entities[i];
                    if (Teams[i].Value == team || Hulls[candidate].Current <= 0f)
                        continue;
                    if (weapon.OrderedTargetOnly && candidate != ordered)
                        continue;

                    var distanceSq = math.distancesq(from, Positions[i].Value);
                    if (distanceSq > rangeSq)
                        continue;

                    var effectiveness = Effectiveness(weapon, candidate);
                    if (effectiveness <= 0f)
                        continue;

                    if (candidate == ordered)
                        return i;

                    // A target twice as well protected counts as twice as far.
                    var score = math.sqrt(distanceSq) / effectiveness;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                return best;
            }

            void TryFire(Entity shooter, int weaponIndex, ref Weapon weapon, double3 from, byte team, int target)
            {
                var toTarget = (float3)(Positions[target].Value - from);
                var distance = math.length(toTarget);
                var radius = Radii[target].Value;
                var probability = WeaponMath.HitProbability(distance, radius, toTarget, Velocities[target].Linear,
                                                            weapon.ProjectileSpeed, weapon.Spread)
                                  * WeaponMath.AccuracyFactor(weapon.Mode);

                if (!WeaponMath.ShouldFire(weapon.Mode, probability))
                    return;

                var random = WeaponMath.ShotRandom(Seed, Tick, shooter.Index, weaponIndex);
                var hit = random.NextFloat() < probability;
                var impact = Positions[target].Value;

                if (weapon.Ammo > 0)
                    weapon.Ammo--;

                if (hit)
                {
                    var targetEntity = Entities[target];
                    var hull = Hulls[targetEntity];
                    var shield = Shields.HasComponent(targetEntity) ? Shields[targetEntity] : default;
                    if (shield.IsUp)
                        shield.LastHitTick = Tick;
                    DamageModel.ApplyHit(weapon, ref shield, ref hull, Tick, SimulationClockBootstrapSystem.TickRate);
                    Hulls[targetEntity] = hull;
                    if (Shields.HasComponent(targetEntity))
                        Shields[targetEntity] = shield;
                }
                else
                {
                    // Show the miss passing beside the target.
                    var direction = distance > 1e-3f ? toTarget / distance : new float3(0f, 0f, 1f);
                    var side = math.normalizesafe(math.cross(direction, math.up()), new float3(1f, 0f, 0f));
                    var lateral = (radius * 1.2f + random.NextFloat(0f, radius * 2f)) * (random.NextBool() ? 1f : -1f);
                    impact += (double3)(side * lateral + direction * random.NextFloat(0f, radius * 3f));
                }

                Shots.Add(new ShotEvent { From = from, To = impact, Hit = hit, Team = team, Type = weapon.Type });
                weapon.CooldownLeft = weapon.Cooldown * WeaponMath.CooldownFactor(weapon.Mode);
            }
        }
    }
}
