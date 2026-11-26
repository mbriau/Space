using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Orders
{
    public enum ShipCommandType : byte
    {
        None = 0,
        MoveTo = 1,
        Dock = 2,
        Undock = 3,
        Load = 4,
        Unload = 5,
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

        /// <summary>Generic payload (quantity, cargo type id...), meaning depends on Type.</summary>
        public int Argument;

        /// <summary>Simulation tick at which the order was issued (needed for networking and replays).</summary>
        public uint IssuedTick;
    }
}
