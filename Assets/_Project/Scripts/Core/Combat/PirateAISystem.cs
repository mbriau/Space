using Space.Core.Orders;
using Space.Core.Ships;
using Space.Core.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Combat
{
    /// <summary>
    /// Simple pirate behaviour: every half second, pick the nearest player ship and order a move to a
    /// point on a circle around it, inside weapon range, a little further round each time. The AI
    /// issues the same <see cref="ShipCommand"/> orders as the player.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(ShipMovementSystem))]
    [UpdateAfter(typeof(StorePreviousTransformSystem))]
    [BurstCompile]
    public partial struct PirateAISystem : ISystem
    {
        /// <summary>Ticks between two decisions (staggered per ship).</summary>
        const uint DecisionInterval = 10;

        /// <summary>Angle travelled round the target at each decision, in radians.</summary>
        const float OrbitStep = 0.6f;

        EntityQuery _prey;

        public void OnCreate(ref SystemState state)
        {
            _prey = SystemAPI.QueryBuilder().WithAll<Ship, Team, SimPosition>().WithNone<PirateAI>().Build();
            state.RequireForUpdate<SimulationTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new DecideJob
            {
                Tick = SystemAPI.GetSingleton<SimulationTime>().Tick,
                PreyTeams = _prey.ToComponentDataArray<Team>(state.WorldUpdateAllocator),
                PreyPositions = _prey.ToComponentDataArray<SimPosition>(state.WorldUpdateAllocator),
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(PirateAI))]
        partial struct DecideJob : IJobEntity
        {
            public uint Tick;
            [ReadOnly] public NativeArray<Team> PreyTeams;
            [ReadOnly] public NativeArray<SimPosition> PreyPositions;

            void Execute(Entity entity, in SimPosition position, in Team team, in DynamicBuffer<Weapon> weapons,
                         DynamicBuffer<ShipCommand> commands)
            {
                if ((Tick + (uint)entity.Index) % DecisionInterval != 0)
                    return;

                var nearest = -1;
                var nearestDistanceSq = double.MaxValue;
                for (var i = 0; i < PreyPositions.Length; i++)
                {
                    if (PreyTeams[i].Value == team.Value)
                        continue;
                    var distanceSq = math.distancesq(position.Value, PreyPositions[i].Value);
                    if (distanceSq < nearestDistanceSq)
                    {
                        nearestDistanceSq = distanceSq;
                        nearest = i;
                    }
                }

                commands.Clear();
                if (nearest < 0)
                    return; // nothing left to attack: brake and wait

                var range = weapons.Length > 0 ? weapons[0].Range : 300f;
                var target = PreyPositions[nearest].Value;
                var fromTarget = (float3)(position.Value - target);
                var angle = math.atan2(fromTarget.z, fromTarget.x) + OrbitStep;
                var orbit = range * 0.7f;
                var point = target + new double3(math.cos(angle) * orbit, 0.0, math.sin(angle) * orbit);

                commands.Add(new ShipCommand { Type = ShipCommandType.MoveTo, Point = point, IssuedTick = Tick });
            }
        }
    }
}
