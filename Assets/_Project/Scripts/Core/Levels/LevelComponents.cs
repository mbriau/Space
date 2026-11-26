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

        /// <summary>Spawn position (Flagship, Enemy), relative to the level origin.</summary>
        public double3 Position;

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
