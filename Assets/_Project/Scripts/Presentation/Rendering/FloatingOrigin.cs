using Unity.Entities;
using Unity.Mathematics;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Singleton: the simulation-space point that maps to (0,0,0) in render space.
    /// Rendering positions are (SimPosition - Origin) cast to float, so they stay precise
    /// close to the camera no matter how far the ship is from the world centre.
    /// </summary>
    public struct FloatingOrigin : IComponentData
    {
        public double3 Origin;

        /// <summary>Distance from the origin (metres) beyond which the origin is moved to the anchor.</summary>
        public double RecenterDistance;
    }

    /// <summary>Tag on the entity the floating origin follows (usually the camera's focus ship).</summary>
    public struct FloatingOriginAnchor : IComponentData { }
}
