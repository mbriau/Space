using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Levels
{
    public enum LevelStatus : byte
    {
        None = 0,
        Running = 1,
        Won = 2,
        Lost = 3,
    }

    /// <summary>
    /// Singleton describing the level being played. Set up by the game flow from a
    /// <see cref="LevelDefinition"/>; updated by <see cref="LevelObjectiveSystem"/>.
    /// </summary>
    public struct LevelState : IComponentData
    {
        public LevelStatus Status;
        public uint StartTick;

        /// <summary>Survival objective: the level is won at this tick if the flagship is still alive.</summary>
        public uint EndTick;

        /// <summary>The level is also won once every enemy (spawned or still to come) is destroyed.</summary>
        public bool WinWhenEnemiesDestroyed;

        /// <summary>Seed of every random draw in the level (shots...), so a level can be replayed exactly.</summary>
        public uint RunSeed;

        /// <summary>Speed at which the flagship cruises along its fixed course (m/s).</summary>
        public float CruiseSpeed;
    }

    /// <summary>
    /// Scripted pause of the level, set by the presentation during an intro (camera flyover,
    /// dialogue): the same order for every player, like the power grid. Lives on the
    /// <see cref="LevelState"/> entity.
    /// </summary>
    public struct LevelHold : IComponentData
    {
        /// <summary>The level clock stands still: no enemy arrives and the survival timer waits.</summary>
        public bool Clock;

        /// <summary>No weapon fires (they still charge).</summary>
        public bool CeaseFire;
    }

    /// <summary>The level clock while it is held. Pure functions, unit tested.</summary>
    public static class LevelHoldMath
    {
        /// <summary>
        /// One held tick: the start and the end of the level slide one tick later, so every delay
        /// (enemy arrivals, survival time) is counted from when the clock runs again.
        /// </summary>
        public static void HoldOneTick(ref LevelState level)
        {
            level.StartTick++;
            level.EndTick++;
        }
    }

    public enum SpawnRole : byte
    {
        /// <summary>The player's main ship.</summary>
        Flagship = 0,

        /// <summary>Player ship holding a formation slot around the flagship (drones).</summary>
        Escort = 1,

        /// <summary>Hostile ship driven by the pirate AI.</summary>
        Enemy = 2,
    }

    /// <summary>A ship the level spawns, possibly after a delay. Buffer on the <see cref="LevelState"/> entity.</summary>
    public struct LevelSpawn : IBufferElementData
    {
        public FixedString64Bytes Ship;
        public SpawnRole Role;
        public uint DelayTicks;

        /// <summary>Spawn position of the flagship, relative to the level origin.</summary>
        public double3 Position;

        /// <summary>Enemy: authored distance and angle (the side it comes from).</summary>
        public float Distance;
        public float AngleDegrees;

        /// <summary>Formation offset in the flagship's frame (Escort).</summary>
        public float3 Offset;

        public bool Done;
    }

    /// <summary>Ship prefabs the levels can spawn, by name. Buffer on a singleton baked from the level sub scene.</summary>
    public struct ShipLibraryEntry : IBufferElementData
    {
        public FixedString64Bytes Name;
        public Entity Prefab;
    }
}
