using Space.Core.Orders;
using Space.Core.Ships;
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

                AddComponent<Ship>(entity);
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
