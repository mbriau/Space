using System.Collections.Generic;
using Space.Core.Levels;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space.Authoring
{
    /// <summary>
    /// Lists the ship prefabs levels can spawn. Put one in the level sub scene: it bakes to a
    /// singleton buffer of entity prefabs, looked up by the prefab's name.
    /// </summary>
    public class ShipLibraryAuthoring : MonoBehaviour
    {
        public List<GameObject> Ships = new();

        class Baker : Baker<ShipLibraryAuthoring>
        {
            public override void Bake(ShipLibraryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var library = AddBuffer<ShipLibraryEntry>(entity);
                foreach (var prefab in authoring.Ships)
                {
                    if (prefab == null)
                        continue;
                    library.Add(new ShipLibraryEntry
                    {
                        Name = new FixedString64Bytes(prefab.name),
                        Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic),
                    });
                }
            }
        }
    }
}
