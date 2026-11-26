using Space.Core.Orders;
using Unity.Burst;
using Unity.Entities;

namespace Space.Core.Ships
{
    /// <summary>
    /// Executes the head of each ship's order queue once per simulation tick. MoveTo orders are
    /// removed when the ship arrives; with no order left, the ship brakes to a stop.
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
                         in ShipMovementStats stats, DynamicBuffer<ShipCommand> commands)
            {
                // Orders this system does not execute yet (Dock, Undock) are dropped so they cannot block the queue.
                while (commands.Length > 0 && commands[0].Type != ShipCommandType.MoveTo)
                    commands.RemoveAt(0);

                if (commands.Length == 0)
                {
                    ShipSteering.Brake(ref position.Value, rotation.Value, ref velocity.Linear, stats, DeltaTime);
                    return;
                }

                var arrived = ShipSteering.MoveTowards(ref position.Value, ref rotation.Value, ref velocity.Linear,
                                                       commands[0].Point, stats, DeltaTime);
                if (arrived)
                    commands.RemoveAt(0);
            }
        }
    }
}
