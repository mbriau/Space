using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Orders
{
    public enum ShipCommandType : byte
    {
        None = 0,

        /// <summary>Fly to <see cref="ShipCommand.Point"/> and stop there.</summary>
        MoveTo = 1,

        /// <summary>Dock at a space base (upgrades, drones). Not implemented yet.</summary>
        Dock = 2,

        /// <summary>Leave the base the ship is docked at. Not implemented yet.</summary>
        Undock = 3,
    }

    /// <summary>
    /// One order in a ship's queue. The player, the AI and (later) the network all issue
    /// orders through this buffer: the simulation never reads input directly.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ShipCommand : IBufferElementData
    {
        public ShipCommandType Type;

        /// <summary>Target entity (station, ship), or Entity.Null.</summary>
        public Entity Target;

        /// <summary>Target point in simulation space, used when Target is null.</summary>
        public double3 Point;

        /// <summary>Generic payload, meaning depends on Type.</summary>
        public int Argument;

        /// <summary>Simulation tick at which the order was issued (needed for networking and replays).</summary>
        public uint IssuedTick;
    }
}
