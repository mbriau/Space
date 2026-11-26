using Space.Core.Orders;
using Space.Core.Ships;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Authoring
{
    /// <summary>Turns a ship prefab into a simulated ship entity.</summary>
    [DisallowMultipleComponent]
    public class ShipAuthoring : MonoBehaviour
    {
        [Min(0f)] public float MaxSpeed = 50f;
        [Min(0f)] public float Acceleration = 10f;
        [Min(0f)] public float TurnRateDegrees = 45f;

        class Baker : Baker<ShipAuthoring>
        {
            public override void Bake(ShipAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var t = authoring.transform;
                var position = new double3(t.position.x, t.position.y, t.position.z);
                quaternion rotation = t.rotation;

                // LODGroup.size is the largest dimension of the ship's bounds.
                var lodGroup = GetComponent<LODGroup>();
                var radius = lodGroup != null ? lodGroup.size * 0.5f : 1f;

                AddComponent<Ship>(entity);
                AddComponent(entity, new ShipName { Value = new FixedString64Bytes(authoring.name) });
                AddComponent(entity, new ShipRadius { Value = radius });
                AddComponent(entity, new SimPosition { Value = position });
                AddComponent(entity, new SimPreviousPosition { Value = position });
                AddComponent(entity, new SimRotation { Value = rotation });
                AddComponent(entity, new SimPreviousRotation { Value = rotation });
                AddComponent(entity, new ShipVelocity());
                AddComponent(entity, new ShipMovementStats
                {
                    MaxSpeed = authoring.MaxSpeed,
                    Acceleration = authoring.Acceleration,
                    TurnRateRadians = math.radians(authoring.TurnRateDegrees),
                });
                AddBuffer<ShipCommand>(entity);
            }
        }
    }
}
