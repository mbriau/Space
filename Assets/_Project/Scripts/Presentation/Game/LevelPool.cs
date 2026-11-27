using System.Collections.Generic;
using Space.Core.Levels;
using Space.Core.Sectors;
using UnityEngine;

namespace Space.Presentation.Game
{
    /// <summary>
    /// The levels the sector maps draw their fights from: every level linked from the first one
    /// (<see cref="LevelDefinition.NextLevels"/>), the first excepted. The exit of a sector is the
    /// best-paid level; an elite fight is a level with its enemies doubled (<see cref="EliteLevels"/>).
    /// </summary>
    public sealed class LevelPool
    {
        public readonly List<LevelDefinition> Levels = new();
        public LevelDefinition Exit;

        readonly Dictionary<LevelDefinition, LevelDefinition> _elites = new();

        public int Count => Levels.Count;

        public static LevelPool Collect(LevelDefinition first)
        {
            var pool = new LevelPool();
            var seen = new HashSet<LevelDefinition> { first };
            var queue = new Queue<LevelDefinition>();
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                var level = queue.Dequeue();
                if (level == null || level.NextLevels == null)
                    continue;
                foreach (var next in level.NextLevels)
                {
                    if (next == null || !seen.Add(next))
                        continue;
                    pool.Levels.Add(next);
                    queue.Enqueue(next);
                }
            }
            if (pool.Levels.Count == 0 && first != null)
                pool.Levels.Add(first);

            foreach (var level in pool.Levels)
                if (pool.Exit == null || level.RewardCredits > pool.Exit.RewardCredits ||
                    level.RewardCredits == pool.Exit.RewardCredits && level.Enemies.Length > pool.Exit.Enemies.Length)
                    pool.Exit = level;
            return pool;
        }

        /// <summary>The level fought at a beacon, or null where there is no fight (start, shop).</summary>
        public LevelDefinition ForBeacon(Beacon beacon) => beacon.Kind switch
        {
            BeaconKind.Exit => Exit,
            BeaconKind.Combat => Get(beacon.Level),
            BeaconKind.Elite => EliteOf(Get(beacon.Level)),
            _ => null,
        };

        /// <summary>Kind of place shown on the map (asteroid field, station...).</summary>
        public string Environment(Beacon beacon) => beacon.Kind switch
        {
            BeaconKind.Shop => "Station marchande",
            BeaconKind.Start => "Point d'entrée",
            _ => ForBeacon(beacon)?.Environment ?? "",
        };

        LevelDefinition Get(int index) => index >= 0 && index < Levels.Count ? Levels[index] : Exit;

        LevelDefinition EliteOf(LevelDefinition level)
        {
            if (level == null)
                return null;
            if (_elites.TryGetValue(level, out var elite) && elite != null)
                return elite;
            elite = Object.Instantiate(level);
            elite.name = level.name + "_Elite";
            elite.DisplayName = level.DisplayName + " (élite)";
            elite.Enemies = EliteLevels.Reinforce(level.Enemies);
            elite.RewardCredits = level.RewardCredits * EliteLevels.RewardFactor;
            _elites[level] = elite;
            return elite;
        }
    }
}
