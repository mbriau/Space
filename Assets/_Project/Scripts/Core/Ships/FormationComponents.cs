using Unity.Entities;
using Unity.Mathematics;

namespace Space.Core.Ships
{
    /// <summary>
    /// Place of a ship in its leader's formation: an offset in the leader's frame (X right, Z forward).
    /// With no order in its queue, the ship holds that slot and moves with the leader.
    /// </summary>
    public struct FormationSlot : IComponentData
    {
        public Entity Leader;
        public float3 Offset;
    }

    /// <summary>Where the formation slot is this tick (computed by <see cref="FormationSystem"/>).</summary>
    public struct FollowTarget : IComponentData
    {
        public double3 Point;
        public float3 Velocity;
        public bool Valid;
    }
}
