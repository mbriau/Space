using Space.Core.Ships;
using Space.Core.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Writes LocalTransform from the simulation state: interpolates between the last two ticks,
    /// adds the enemies' idle sway, bends the straight lane onto the course (<see cref="RenderSpace"/>)
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
            state.RequireForUpdate<RenderSpace>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<SimulationTime>();
            new InterpolateJob
            {
                Alpha = time.InterpolationAlpha,
                // The moment the interpolated state stands for: between the last two ticks.
                Time = time.ElapsedTime - (1.0 - time.InterpolationAlpha) * time.TickDuration,
                Render = SystemAPI.GetSingleton<RenderSpace>(),
                Sways = SystemAPI.GetComponentLookup<IdleSway>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct InterpolateJob : IJobEntity
        {
            public float Alpha;
            public double Time;
            public RenderSpace Render;
            [ReadOnly] public ComponentLookup<IdleSway> Sways;

            void Execute(Entity entity, in SimPosition position, in SimPreviousPosition previousPosition,
                         in SimRotation rotation, in SimPreviousRotation previousRotation,
                         ref LocalTransform transform)
            {
                var simPosition = math.lerp(previousPosition.Value, position.Value, Alpha);
                var simRotation = math.slerp(previousRotation.Value, rotation.Value, Alpha);
                if (Sways.TryGetComponent(entity, out var sway) && sway.Heave > 0f)
                {
                    CourseMath.Sway(sway, Time, out var offset, out var tilt);
                    simPosition += Render.LaneOffset(offset);
                    simRotation = math.mul(simRotation, tilt);
                }
                Render.ToRender(simPosition, simRotation, out var renderPosition, out var renderRotation);
                transform.Position = renderPosition;
                transform.Rotation = renderRotation;
            }
        }
    }
}
