using Space.Core.Ships;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Creates the <see cref="FloatingOrigin"/> singleton and snaps it onto the anchor entity
    /// when the anchor drifts too far. Runs after the fixed-step simulation, before transforms.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(SimToRenderTransformSystem))]
    [BurstCompile]
    public partial struct FloatingOriginSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity(typeof(FloatingOrigin));
            state.EntityManager.SetComponentData(entity, new FloatingOrigin
            {
                Origin = double3.zero,
                RecenterDistance = 5000.0,
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var origin = SystemAPI.GetSingletonRW<FloatingOrigin>();

            foreach (var position in SystemAPI.Query<RefRO<SimPosition>>().WithAll<FloatingOriginAnchor>())
            {
                var offset = position.ValueRO.Value - origin.ValueRO.Origin;
                if (math.lengthsq(offset) > origin.ValueRO.RecenterDistance * origin.ValueRO.RecenterDistance)
                    origin.ValueRW.Origin = math.floor(position.ValueRO.Value);
                break; // a single anchor is expected
            }
        }
    }
}
