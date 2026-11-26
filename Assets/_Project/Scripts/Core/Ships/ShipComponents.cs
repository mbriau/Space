using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Ships
{
    /// <summary>Marks an entity as a ship (player, drone, NPC or enemy).</summary>
    public struct Ship : IComponentData { }

    /// <summary>
    /// Authoritative position in the simulation, in double precision (metres, world space).
    /// Rendering converts it to float relative to the floating origin.
    /// </summary>
    public struct SimPosition : IComponentData
    {
        public double3 Value;
    }

    /// <summary>Position at the previous tick, kept for render interpolation.</summary>
    public struct SimPreviousPosition : IComponentData
    {
        public double3 Value;
    }

    /// <summary>Authoritative orientation in the simulation.</summary>
    public struct SimRotation : IComponentData
    {
        public quaternion Value;
    }

    public struct SimPreviousRotation : IComponentData
    {
        public quaternion Value;
    }

    public struct ShipVelocity : IComponentData
    {
        public float3 Linear;
    }

    /// <summary>Movement characteristics, normally baked from a ship definition.</summary>
    public struct ShipMovementStats : IComponentData
    {
        public float MaxSpeed;
        public float Acceleration;
        public float TurnRateRadians;
    }
}
