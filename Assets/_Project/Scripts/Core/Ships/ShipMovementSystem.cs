using Space.Core.Combat;
using Space.Core.Orders;
using Unity.Burst;
using Unity.Entities;

namespace Space.Core.Ships
{
    /// <summary>
    /// Executes the head of each ship's order queue once per simulation tick. MoveTo orders are
    /// removed when the ship arrives. With no order left, a ship holds its formation slot if it has
    /// one, otherwise it brakes to a stop.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(StorePreviousTransformSystem))]
    [BurstCompile]
    public partial struct ShipMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MoveJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct MoveJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref SimPosition position, ref SimRotation rotation, ref ShipVelocity velocity,
                         in ShipMovementStats baseStats, in PowerGrid power, in FollowTarget follow,
                         DynamicBuffer<ShipCommand> commands)
            {
                // Engines run in proportion to their power.
                var engines = DamageModel.EngineFactor(power);
                var stats = baseStats;
                stats.MaxSpeed *= engines;
                stats.Acceleration *= engines;

                // Orders this system does not execute yet (Dock, Undock) are dropped so they cannot block the queue.
                while (commands.Length > 0 && commands[0].Type != ShipCommandType.MoveTo)
                    commands.RemoveAt(0);

                if (commands.Length > 0)
                {
                    var arrived = ShipSteering.MoveTowards(ref position.Value, ref rotation.Value, ref velocity.Linear,
                                                           commands[0].Point, stats, DeltaTime);
                    if (arrived)
                        commands.RemoveAt(0);
                    return;
                }

                if (follow.Valid)
                {
                    ShipSteering.MoveTowards(ref position.Value, ref rotation.Value, ref velocity.Linear,
                                             follow.Point, follow.Velocity, stats, DeltaTime);
                    return;
                }

                ShipSteering.Brake(ref position.Value, rotation.Value, ref velocity.Linear, stats, DeltaTime);
            }
        }
    }
}
