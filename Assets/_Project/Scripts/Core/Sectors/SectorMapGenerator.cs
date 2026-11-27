using System.Collections.Generic;
using Unity.Mathematics;

namespace Space.Core.Sectors
{
    /// <summary>
    /// Builds a sector map from a seed: a start, 3 or 4 columns of 2 or 3 beacons, an exit. Each
    /// beacon leads to 1 or 2 beacons of the next column, and a few neighbours of one column are
    /// linked both ways: the shortest way out takes one jump per column, side links make detours
    /// for more rewards. Every sector has at least one shop off the first column and a few elites.
    /// Deterministic for a seed.
    /// </summary>
    public static class SectorMapGenerator
    {
        public const int MinMiddleColumns = 3;
        public const int MaxMiddleColumns = 4;
        public const int MinPerColumn = 2;
        public const int MaxPerColumn = 3;
        public const float SideLinkChance = 0.35f;
        public const float EliteChance = 0.18f;
        public const int MaxShops = 2;

        /// <param name="levelCount">Size of the level pool: fights draw their level from it.</param>
        public static SectorMap Generate(uint seed, int levelCount)
        {
            var random = new Random(seed == 0 ? 1u : seed);
            var map = new SectorMap();
            var middle = random.NextInt(MinMiddleColumns, MaxMiddleColumns + 1);

            var columns = new List<List<int>>();
            columns.Add(new List<int> { Add(map, BeaconKind.Start, 0, 0.5f) });
            for (var c = 1; c <= middle; c++)
            {
                var count = random.NextInt(MinPerColumn, MaxPerColumn + 1);
                var column = new List<int>(count);
                for (var r = 0; r < count; r++)
                    column.Add(Add(map, BeaconKind.Combat, c, count == 1 ? 0.5f : (r + 0.5f) / count));
                columns.Add(column);
            }
            columns.Add(new List<int> { Add(map, BeaconKind.Exit, middle + 1, 0.5f) });
            map.Start = columns[0][0];
            map.Exit = columns[middle + 1][0];

            // Forward links: each beacon to the one or two nearest of the next column, then every
            // beacon of the next column gets at least one way in.
            for (var c = 0; c <= middle; c++)
            {
                var here = columns[c];
                var next = columns[c + 1];
                foreach (var from in here)
                {
                    var nearest = Nearest(map, next, map.Beacons[from].Row);
                    Link(map, from, nearest.x);
                    if (nearest.y >= 0 && random.NextFloat() < 0.6f)
                        Link(map, from, nearest.y);
                }
                foreach (var to in next)
                {
                    var linked = false;
                    foreach (var from in here)
                        linked |= map.AreLinked(from, to);
                    if (!linked)
                        Link(map, Nearest(map, here, map.Beacons[to].Row).x, to);
                }
            }

            // Side links between neighbours of one column, both ways.
            for (var c = 1; c <= middle; c++)
            {
                var column = columns[c];
                for (var r = 0; r + 1 < column.Count; r++)
                {
                    if (random.NextFloat() >= SideLinkChance)
                        continue;
                    Link(map, column[r], column[r + 1]);
                    Link(map, column[r + 1], column[r]);
                }
            }

            // Shops: one or two, never in the first column; elites: a few, never in the first column.
            var candidates = new List<int>();
            for (var c = 2; c <= middle; c++)
                candidates.AddRange(columns[c]);
            var shops = math.min(candidates.Count, random.NextInt(1, MaxShops + 1));
            for (var i = 0; i < shops; i++)
            {
                var index = random.NextInt(candidates.Count);
                map.Beacons[candidates[index]].Kind = BeaconKind.Shop;
                candidates.RemoveAt(index);
            }
            foreach (var id in candidates)
                if (random.NextFloat() < EliteChance)
                    map.Beacons[id].Kind = BeaconKind.Elite;

            // Levels of the fights.
            foreach (var beacon in map.Beacons)
                if ((beacon.Kind == BeaconKind.Combat || beacon.Kind == BeaconKind.Elite) && levelCount > 0)
                    beacon.Level = random.NextInt(levelCount);

            return map;
        }

        static int Add(SectorMap map, BeaconKind kind, int column, float row)
        {
            map.Beacons.Add(new Beacon { Kind = kind, Column = column, Row = row });
            return map.Beacons.Count - 1;
        }

        static void Link(SectorMap map, int from, int to)
        {
            var links = map.Beacons[from].Links;
            if (from != to && !links.Contains(to))
                links.Add(to);
        }

        /// <summary>The nearest beacon of a column to a height, and the second nearest (-1 if none).</summary>
        static int2 Nearest(SectorMap map, List<int> column, float row)
        {
            var best = new int2(-1, -1);
            var bestDistance = new float2(float.MaxValue, float.MaxValue);
            foreach (var id in column)
            {
                var d = math.abs(map.Beacons[id].Row - row);
                if (d < bestDistance.x)
                {
                    best = new int2(id, best.x);
                    bestDistance = new float2(d, bestDistance.x);
                }
                else if (d < bestDistance.y)
                {
                    best.y = id;
                    bestDistance.y = d;
                }
            }
            return best;
        }
    }
}
