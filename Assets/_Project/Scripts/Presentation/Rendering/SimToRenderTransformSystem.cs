using Space.Core.Ships;
using Space.Core.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Writes LocalTransform from the simulation state: interpolates between the last two ticks
    /// and converts double-precision positions to float relative to the floating origin.
    /// The simulation never reads LocalTransform.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct SimToRenderTransformSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationTime>();
            state.RequireForUpdate<FloatingOrigin>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new InterpolateJob
            {
                Alpha = SystemAPI.GetSingleton<SimulationTime>().InterpolationAlpha,
                Origin = SystemAPI.GetSingleton<FloatingOrigin>().Origin,
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct InterpolateJob : IJobEntity
        {
            public float Alpha;
            public double3 Origin;

            void Execute(in SimPosition position, in SimPreviousPosition previousPosition,
                         in SimRotation rotation, in SimPreviousRotation previousRotation,
                         ref LocalTransform transform)
            {
                var simPosition = math.lerp(previousPosition.Value, position.Value, Alpha);
                transform.Position = (float3)(simPosition - Origin);
                transform.Rotation = math.slerp(previousRotation.Value, rotation.Value, Alpha);
            }
        }
    }
}
