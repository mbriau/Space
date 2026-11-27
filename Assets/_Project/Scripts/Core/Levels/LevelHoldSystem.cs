using Space.Core.Ships;
using Unity.Burst;
using Unity.Entities;

namespace Space.Core.Levels
{
    /// <summary>
    /// Holds the level clock while <see cref="LevelHold.Clock"/> is set: each tick pushes the start
    /// and the end of the level back, before anything is spawned or the objective is checked.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(StorePreviousTransformSystem))]
    [UpdateBefore(typeof(LevelSpawnSystem))]
    [BurstCompile]
    public partial struct LevelHoldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LevelState>();
            state.RequireForUpdate<LevelHold>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.GetSingleton<LevelHold>().Clock)
                return;
            var level = SystemAPI.GetSingletonRW<LevelState>();
            if (level.ValueRO.Status == LevelStatus.Running)
                LevelHoldMath.HoldOneTick(ref level.ValueRW);
        }
    }
}
