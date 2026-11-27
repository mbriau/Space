using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Space.Core.Sectors
{
    /// <summary>
    /// The player's way through one sector: where the ship is, where it has been, how many jumps it
    /// took. In phase 1 the sector is a delivery contract: delivering within the deadline pays a
    /// bonus, each jump over it takes a part of the bonus away (<see cref="DeliveryBonus"/>).
    /// </summary>
    [Serializable]
    public sealed class SectorRun
    {
        /// <summary>Jumps allowed on top of the shortest way out.</summary>
        public const int SpareJumps = 2;
        public const int BaseBonus = 150;
        public const int BonusLostPerLateJump = 50;

        public SectorMap Map;
        public int Current;
        public int Jumps;
        public int Deadline;
        public List<int> Visited = new();

        public SectorRun() { }

        public SectorRun(SectorMap map)
        {
            Map = map;
            Current = map.Start;
            Visited.Add(map.Start);
            Deadline = map.JumpsToExit(map.Start) + SpareJumps;
        }

        public Beacon CurrentBeacon => Map.Beacons[Current];
        public bool AtExit => Current == Map.Exit;
        public int JumpsLeft => Deadline - Jumps;

        /// <summary>A beacon can be reached if it is linked to the current one, not visited yet, and the exit stays reachable from it.</summary>
        public bool CanJump(int to) =>
            !AtExit && Map.AreLinked(Current, to) && !Visited.Contains(to) && Map.JumpsToExit(to, Visited) >= 0;

        public bool Jump(int to)
        {
            if (!CanJump(to))
                return false;
            Current = to;
            Jumps++;
            Visited.Add(to);
            return true;
        }

        /// <summary>
        /// What the ship knows of a beacon: its kind shows once it is next to the ship, visited, a
        /// shop (stations are on the charts) or the exit.
        /// </summary>
        public bool IsKindKnown(int beacon)
        {
            var kind = Map.Beacons[beacon].Kind;
            return kind == BeaconKind.Shop || kind == BeaconKind.Exit || kind == BeaconKind.Start ||
                   Visited.Contains(beacon) || Map.AreLinked(Current, beacon);
        }

        /// <summary>Bonus paid on delivery after <paramref name="jumps"/> jumps for a deadline.</summary>
        public static int DeliveryBonus(int jumps, int deadline) =>
            math.max(0, BaseBonus - BonusLostPerLateJump * math.max(0, jumps - deadline));

        public int CurrentBonus => DeliveryBonus(Jumps, Deadline);
    }
}
