using System;
using System.Collections.Generic;

namespace Space.Core.Sectors
{
    public enum BeaconKind : byte
    {
        /// <summary>Where the sector is entered (the first level of a run, or nothing).</summary>
        Start = 0,
        Combat = 1,

        /// <summary>A harder fight for a better reward.</summary>
        Elite = 2,

        /// <summary>A station with a shop, no fight.</summary>
        Shop = 3,

        /// <summary>The way out of the sector: a slightly harder fight.</summary>
        Exit = 4,
    }

    /// <summary>One place of a sector map.</summary>
    [Serializable]
    public sealed class Beacon
    {
        public BeaconKind Kind;

        /// <summary>Column, from 0 (start) to the exit's column: the map is crossed from left to right.</summary>
        public int Column;

        /// <summary>Height in its column, 0 (top) to 1 (bottom), for drawing.</summary>
        public float Row;

        /// <summary>Index of the level fought there in the level pool, or -1 (start, shop, exit: chosen by the game).</summary>
        public int Level = -1;

        /// <summary>Beacons reachable in one jump: the next column, and sometimes a neighbour in the same column.</summary>
        public List<int> Links = new();
    }

    /// <summary>
    /// A sector: beacons in columns, entered at the start (column 0) and left through the exit (last
    /// column). Built by <see cref="SectorMapGenerator"/>; plain data so the run can be saved.
    /// </summary>
    [Serializable]
    public sealed class SectorMap
    {
        public List<Beacon> Beacons = new();
        public int Start;
        public int Exit;

        public int Columns => Beacons.Count == 0 ? 0 : Beacons[Exit].Column + 1;

        public bool AreLinked(int from, int to) =>
            from >= 0 && from < Beacons.Count && Beacons[from].Links.Contains(to);

        /// <summary>Fewest jumps from a beacon to the exit (-1 if it cannot be reached).</summary>
        public int JumpsToExit(int from, ICollection<int> closed = null)
        {
            if (from == Exit)
                return 0;
            var distance = new int[Beacons.Count];
            for (var i = 0; i < distance.Length; i++)
                distance[i] = -1;
            var queue = new Queue<int>();
            distance[from] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in Beacons[current].Links)
                {
                    if (distance[next] >= 0 || closed != null && closed.Contains(next))
                        continue;
                    distance[next] = distance[current] + 1;
                    if (next == Exit)
                        return distance[next];
                    queue.Enqueue(next);
                }
            }
            return -1;
        }
    }
}
