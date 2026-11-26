using Unity.Burst;
using Unity.Entities;

namespace Space.Core.Ships
{
    /// <summary>
    /// Runs first in every tick: copies the current simulation transform into the previous one,
    /// so presentation can interpolate between the last two ticks.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct StorePreviousTransformSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new StoreJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct StoreJob : IJobEntity
        {
            void Execute(in SimPosition position, ref SimPreviousPosition previousPosition,
                         in SimRotation rotation, ref SimPreviousRotation previousRotation)
            {
                previousPosition.Value = position.Value;
                previousRotation.Value = rotation.Value;
            }
        }
    }
}
