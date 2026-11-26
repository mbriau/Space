using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Ships
{
    /// <summary>Marks an entity as a ship (player, drone, NPC or enemy).</summary>
    public struct Ship : IComponentData { }

    /// <summary>Display name of the ship.</summary>
    public struct ShipName : IComponentData
    {
        public FixedString64Bytes Value;
    }

    /// <summary>
    /// The generated model the presentation draws for this ship (a design of the hangar, by id), with
    /// its turrets on the design's hardpoints. Ships without it keep their baked placeholder mesh.
    /// </summary>
    public struct ShipVisual : IComponentData
    {
        public FixedString32Bytes DesignId;
    }

    /// <summary>Radius of a sphere enclosing the ship, in metres (picking, avoidance, arrival).</summary>
    public struct ShipRadius : IComponentData
    {
        public float Value;
    }

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
