using Space.Core.Combat;
using Space.Core.Orders;
using Space.Core.Ships;
using Space.Core.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Levels
{
    /// <summary>
    /// Instantiates the ships of the running level when their delay has elapsed. Spawning happens in
    /// the simulation, on ticks, so a level unfolds the same way every time.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(StorePreviousTransformSystem))]
    [UpdateBefore(typeof(FormationSystem))]
    public partial struct LevelSpawnSystem : ISystem
    {
        EntityQuery _flagships;

        public void OnCreate(ref SystemState state)
        {
            _flagships = SystemAPI.QueryBuilder().WithAll<PlayerFlagship>().Build();
            state.RequireForUpdate<LevelState>();
            state.RequireForUpdate<ShipLibraryEntry>();
            state.RequireForUpdate<SimulationTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var levelEntity = SystemAPI.GetSingletonEntity<LevelState>();
            var level = SystemAPI.GetComponent<LevelState>(levelEntity);
            if (level.Status != LevelStatus.Running)
                return;

            var tick = SystemAPI.GetSingleton<SimulationTime>().Tick;
            var em = state.EntityManager;

            // Copy the library: instantiating invalidates buffers.
            var library = SystemAPI.GetSingletonBuffer<ShipLibraryEntry>(true).ToNativeArray(Allocator.Temp);
            var spawns = em.GetBuffer<LevelSpawn>(levelEntity).ToNativeArray(Allocator.Temp);
            var changed = false;

            // Flagship first, so escorts spawned on the same tick can form up on it.
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < spawns.Length; i++)
                {
                    var spawn = spawns[i];
                    var isFlagship = spawn.Role == SpawnRole.Flagship;
                    if (spawn.Done || (pass == 0) != isFlagship || tick < level.StartTick + spawn.DelayTicks)
                        continue;

                    var prefab = Find(library, spawn.Ship);
                    if (prefab != Entity.Null)
                        Spawn(em, prefab, spawn);

                    spawn.Done = true;
                    spawns[i] = spawn;
                    changed = true;
                }
            }

            if (changed)
                em.GetBuffer<LevelSpawn>(levelEntity).CopyFrom(spawns);
        }

        static Entity Find(NativeArray<ShipLibraryEntry> library, FixedString64Bytes name)
        {
            foreach (var entry in library)
                if (entry.Name == name)
                    return entry.Prefab;
            return Entity.Null;
        }

        void Spawn(EntityManager em, Entity prefab, LevelSpawn spawn)
        {
            var entity = em.Instantiate(prefab);
            var position = spawn.Position;
            var rotation = quaternion.identity;

            switch (spawn.Role)
            {
                case SpawnRole.Flagship:
                    em.AddComponent<PlayerFlagship>(entity);
                    break;

                case SpawnRole.Escort:
                {
                    var leader = Entity.Null;
                    if (!_flagships.IsEmpty)
                    {
                        using var flagships = _flagships.ToEntityArray(Allocator.Temp);
                        leader = flagships[0];
                    }
                    if (leader != Entity.Null)
                    {
                        var leaderRotation = em.GetComponentData<SimRotation>(leader).Value;
                        rotation = leaderRotation;
                        position = em.GetComponentData<SimPosition>(leader).Value + (double3)math.mul(leaderRotation, spawn.Offset);
                    }
                    var slot = new FormationSlot { Leader = leader, Offset = spawn.Offset };
                    if (em.HasComponent<FormationSlot>(entity))
                        em.SetComponentData(entity, slot);
                    else
                        em.AddComponentData(entity, slot);
                    break;
                }

                case SpawnRole.Enemy:
                    em.AddComponent<PirateAI>(entity);
                    // Face the level origin, where the player starts.
                    var toOrigin = -(float3)spawn.Position;
                    if (math.lengthsq(toOrigin) > 1e-4f)
                        rotation = quaternion.LookRotationSafe(math.normalize(toOrigin), math.up());
                    break;
            }

            em.SetComponentData(entity, new SimPosition { Value = position });
            em.SetComponentData(entity, new SimPreviousPosition { Value = position });
            em.SetComponentData(entity, new SimRotation { Value = rotation });
            em.SetComponentData(entity, new SimPreviousRotation { Value = rotation });
            em.SetComponentData(entity, new ShipVelocity());
            em.GetBuffer<ShipCommand>(entity).Clear();
        }
    }
}
