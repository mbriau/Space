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
    /// Tactical engagement: the flagship cruises on a fixed course; enemies drop out of hyperspace
    /// alongside it and keep station there, sailing parallel at the same speed.
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
            var enemyIndex = 0;
            foreach (var spawn in spawns)
                if (spawn.Role == SpawnRole.Enemy && spawn.Done)
                    enemyIndex++;

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
                        Spawn(em, prefab, spawn, tick, level.CruiseSpeed > 0f ? level.CruiseSpeed : Engagement.DefaultCruiseSpeed,
                              spawn.Role == SpawnRole.Enemy ? enemyIndex : 0);
                    if (spawn.Role == SpawnRole.Enemy)
                        enemyIndex++;

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

        Entity Flagship()
        {
            if (_flagships.IsEmpty)
                return Entity.Null;
            using var flagships = _flagships.ToEntityArray(Allocator.Temp);
            return flagships[0];
        }

        void Spawn(EntityManager em, Entity prefab, LevelSpawn spawn, uint tick, float cruiseSpeed, int enemyIndex)
        {
            var entity = em.Instantiate(prefab);
            var position = spawn.Position;
            var rotation = quaternion.identity;
            var arrivalVelocity = float3.zero;

            switch (spawn.Role)
            {
                case SpawnRole.Flagship:
                {
                    em.AddComponent<PlayerFlagship>(entity);
                    // Fixed course, straight ahead, already at cruise speed.
                    var heading = new float3(0f, 0f, 1f);
                    rotation = quaternion.LookRotationSafe(heading, math.up());
                    arrivalVelocity = heading * cruiseSpeed;
                    em.AddComponentData(entity, new CruiseCourse { Heading = heading, Speed = cruiseSpeed, LaneOrigin = position });
                    break;
                }

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
                        arrivalVelocity = em.GetComponentData<ShipVelocity>(leader).Linear;
                    }
                    var slot = new FormationSlot { Leader = leader, Offset = spawn.Offset };
                    if (em.HasComponent<FormationSlot>(entity))
                        em.SetComponentData(entity, slot);
                    else
                        em.AddComponentData(entity, slot);
                    break;
                }

                case SpawnRole.Enemy:
                {
                    em.AddComponent<PirateAI>(entity);
                    // Drops out of hyperspace alongside the flagship, on a parallel course at the same
                    // speed, and keeps that station (a formation slot on the flagship).
                    var longestRange = 0f;
                    if (em.HasBuffer<Weapon>(entity))
                        foreach (var weapon in em.GetBuffer<Weapon>(entity))
                            longestRange = math.max(longestRange, weapon.Range);
                    var distance = Engagement.StationDistance(spawn.Distance, longestRange);
                    var station = Engagement.EnemyStation(enemyIndex, distance, spawn.AngleDegrees);
                    var leader = Flagship();
                    if (leader != Entity.Null)
                    {
                        rotation = em.GetComponentData<SimRotation>(leader).Value;
                        position = em.GetComponentData<SimPosition>(leader).Value + (double3)math.mul(rotation, station);
                        arrivalVelocity = em.GetComponentData<ShipVelocity>(leader).Linear;
                    }
                    else
                    {
                        position = (double3)station;
                    }
                    var slot = new FormationSlot { Leader = leader, Offset = station };
                    if (em.HasComponent<FormationSlot>(entity))
                        em.SetComponentData(entity, slot);
                    else
                        em.AddComponentData(entity, slot);
                    em.AddComponentData(entity, new HyperspaceArrival { Tick = tick });
                    break;
                }
            }

            em.SetComponentData(entity, new SimPosition { Value = position });
            em.SetComponentData(entity, new SimPreviousPosition { Value = position });
            em.SetComponentData(entity, new SimRotation { Value = rotation });
            em.SetComponentData(entity, new SimPreviousRotation { Value = rotation });
            em.SetComponentData(entity, new ShipVelocity { Linear = arrivalVelocity });
            em.GetBuffer<ShipCommand>(entity).Clear();
        }
    }
}
