using Unity.Burst;
using Unity.Entities;

namespace Space.Core.Ships
{
    /// <summary>Steers ships on a <see cref="CruiseCourse"/>: they hold their lane at cruise speed.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(StorePreviousTransformSystem))]
    [UpdateBefore(typeof(FormationSystem))]
    [BurstCompile]
    public partial struct CruiseSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new CruiseJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct CruiseJob : IJobEntity
        {
            void Execute(in CruiseCourse course, in SimPosition position, ref FollowTarget follow)
            {
                follow.Point = Engagement.LanePoint(position.Value, course.LaneOrigin, course.Heading);
                follow.Velocity = course.Heading * course.Speed;
                follow.Valid = true;
            }
        }
    }
}
