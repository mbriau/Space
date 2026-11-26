using Unity.Entities;
using Unity.Mathematics;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// On the shield bubble entity (child of a ship): which ship's shield it shows and its colour.
    /// <see cref="ShieldVisualSystem"/> drives the bubble's per-instance colour from the shield state.
    /// </summary>
    public struct ShieldVisual : IComponentData
    {
        public Entity Ship;
        /// <summary>Bubble colour (RGB; alpha is driven by the shield).</summary>
        public float4 Color;
    }
}
