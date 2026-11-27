using Space.Core.Ships;
using Space.Core.Time;
using Unity.Burst;
using Unity.Entities;

namespace Space.Core.Combat
{
    /// <summary>
    /// Applies projectiles that reach their target this tick: shield first, then hull. Each impact
    /// is reported to presentation (explosion, shield flash, camera shake).
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponSystem))]
    [BurstCompile]
    public partial struct ImpactSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationTime>();
            state.RequireForUpdate<ShotEventQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Main-thread work: wait for this tick's weapon and movement jobs first.
            state.CompleteDependency();

            var tick = SystemAPI.GetSingleton<SimulationTime>().Tick;
            var queue = SystemAPI.GetSingletonEntity<ShotEventQueue>();
            var inFlight = SystemAPI.GetBuffer<ProjectileInFlight>(queue);
            var impacts = SystemAPI.GetBuffer<ImpactEvent>(queue);

            var hulls = SystemAPI.GetComponentLookup<Hull>(false);
            var shields = SystemAPI.GetComponentLookup<Shield>(false);
            var positions = SystemAPI.GetComponentLookup<SimPosition>(true);
            var teams = SystemAPI.GetComponentLookup<Team>(true);
            var radii = SystemAPI.GetComponentLookup<ShipRadius>(true);

            for (var i = inFlight.Length - 1; i >= 0; i--)
            {
                var projectile = inFlight[i];
                if (projectile.ImpactTick > tick)
                    continue;
                inFlight.RemoveAtSwapBack(i);

                var target = projectile.Target;
                if (!projectile.Hit || !hulls.HasComponent(target) || hulls[target].Current <= 0f)
                    continue;

                var hull = hulls[target];
                var shield = shields.HasComponent(target) ? shields[target] : default;
                var hullBefore = hull.Current;
                var shieldBefore = shield.Current;
                var shieldWasUp = shield.IsUp;
                if (shieldWasUp)
                    shield.LastHitTick = tick;

                var weapon = new Weapon { Type = projectile.Type, Damage = projectile.Damage, ShieldDamage = projectile.ShieldDamage };
                DamageModel.ApplyHit(weapon, ref shield, ref hull, tick, SimulationClockBootstrapSystem.TickRate);
                hulls[target] = hull;
                if (shields.HasComponent(target))
                    shields[target] = shield;

                impacts.Add(new ImpactEvent
                {
                    Position = positions.HasComponent(target) ? positions[target].Value : default,
                    Type = projectile.Type,
                    HullDamage = hullBefore - hull.Current,
                    ShieldDamage = shieldBefore - shield.Current,
                    ShieldWasUp = shieldWasUp,
                    TargetTeam = teams.HasComponent(target) ? teams[target].Value : (byte)0,
                    TargetRadius = radii.HasComponent(target) ? radii[target].Value : 10f,
                    Target = target,
                    From = projectile.From,
                });
            }
        }
    }
}
