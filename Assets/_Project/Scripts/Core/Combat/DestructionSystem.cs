using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space.Core.Combat
{
    /// <summary>Destroys ships whose hull reached zero (with their child entities, through LinkedEntityGroup).</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(ImpactSystem))]
    [BurstCompile]
    public partial struct DestructionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (hull, entity) in SystemAPI.Query<RefRO<Hull>>().WithEntityAccess())
            {
                if (hull.ValueRO.Current <= 0f)
                    commands.DestroyEntity(entity);
            }
            commands.Playback(state.EntityManager);
        }
    }
}
