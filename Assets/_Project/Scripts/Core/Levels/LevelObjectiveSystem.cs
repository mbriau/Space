using Space.Core.Combat;
using Space.Core.Time;
using Unity.Burst;
using Unity.Entities;

namespace Space.Core.Levels
{
    /// <summary>
    /// Survival objective: lost when the flagship is destroyed, won when the timer runs out or,
    /// if the level allows it, when every enemy has been destroyed.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct LevelObjectiveSystem : ISystem
    {
        EntityQuery _flagships;
        EntityQuery _enemies;

        public void OnCreate(ref SystemState state)
        {
            _flagships = SystemAPI.QueryBuilder().WithAll<PlayerFlagship>().Build();
            _enemies = SystemAPI.QueryBuilder().WithAll<PirateAI>().Build();
            state.RequireForUpdate<LevelState>();
            state.RequireForUpdate<SimulationTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var levelEntity = SystemAPI.GetSingletonEntity<LevelState>();
            var level = SystemAPI.GetComponentRW<LevelState>(levelEntity);
            if (level.ValueRO.Status != LevelStatus.Running)
                return;

            var tick = SystemAPI.GetSingleton<SimulationTime>().Tick;
            var spawns = SystemAPI.GetBuffer<LevelSpawn>(levelEntity);

            var flagshipPending = false;
            var enemiesPending = false;
            foreach (var spawn in spawns)
            {
                if (spawn.Done)
                    continue;
                flagshipPending |= spawn.Role == SpawnRole.Flagship;
                enemiesPending |= spawn.Role == SpawnRole.Enemy;
            }

            if (!flagshipPending && _flagships.IsEmpty)
                level.ValueRW.Status = LevelStatus.Lost;
            else if (tick >= level.ValueRO.EndTick)
                level.ValueRW.Status = LevelStatus.Won;
            else if (level.ValueRO.WinWhenEnemiesDestroyed && !enemiesPending && _enemies.IsEmpty)
                level.ValueRW.Status = LevelStatus.Won;
        }
    }
}
