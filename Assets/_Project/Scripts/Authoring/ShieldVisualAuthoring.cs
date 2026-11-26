using Space.Presentation.Rendering;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Space.Authoring
{
    /// <summary>
    /// On the shield bubble, a child of a ship prefab. Its renderer needs a transparent URP material;
    /// the bubble's colour and opacity are then driven per instance from the ship's shield.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShieldVisualAuthoring : MonoBehaviour
    {
        public Color Color = new Color(0.35f, 0.65f, 1f, 1f);

        class Baker : Baker<ShieldVisualAuthoring>
        {
            public override void Bake(ShieldVisualAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                var parent = authoring.transform.parent;
                var ship = parent != null ? GetEntity(parent, TransformUsageFlags.Dynamic) : Entity.Null;

                var c = authoring.Color;
                AddComponent(entity, new ShieldVisual { Ship = ship, Color = new Unity.Mathematics.float4(c.r, c.g, c.b, c.a) });
                AddComponent(entity, new URPMaterialPropertyBaseColor { Value = new Unity.Mathematics.float4(0f, 0f, 0f, 0f) });
            }
        }
    }
}
