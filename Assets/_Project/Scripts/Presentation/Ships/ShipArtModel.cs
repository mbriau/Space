using System.Collections.Generic;
using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// A ship drawn with its finished art (<see cref="ShipArt"/>): the textured hull and a turret head
    /// per fitted weapon on the slot pivots. Same turret API as <see cref="ShipModel"/>.
    /// </summary>
    public class ShipArtModel : MonoBehaviour
    {
        public ShipArt Art { get; private set; }

        /// <summary>One entry per slot; null where no weapon is fitted.</summary>
        public readonly List<ShipTurret> Turrets = new();

        public static ShipArtModel Create(ShipArt art, Transform parent)
        {
            var go = new GameObject(art.DesignId);
            go.transform.SetParent(parent, false);
            var model = go.AddComponent<ShipArtModel>();
            model.Build(art);
            return model;
        }

        void Build(ShipArt art)
        {
            Art = art;
            var hull = new GameObject("Hull");
            hull.transform.SetParent(transform, false);
            hull.AddComponent<MeshFilter>().sharedMesh = art.Hull;
            hull.AddComponent<MeshRenderer>().sharedMaterial = art.Material;
            for (var i = 0; i < art.Slots.Count; i++)
                Turrets.Add(null);
        }

        /// <summary>Puts the head matching <paramref name="kind"/> on a slot, or empties it with null.</summary>
        public ShipTurret SetTurret(int slot, TurretKind? kind)
        {
            if (slot < 0 || slot >= Turrets.Count)
                return null;
            if (Turrets[slot] != null)
            {
                Destroy(Turrets[slot].gameObject);
                Turrets[slot] = null;
            }
            var head = kind == null ? null : Art.BestTurret(kind.Value);
            if (head == null)
                return null;

            var root = new GameObject($"Turret {slot + 1}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Art.Slots[slot];

            var headObject = new GameObject("Head");
            headObject.transform.SetParent(root.transform, false);
            headObject.AddComponent<MeshFilter>().sharedMesh = head.Head;
            headObject.AddComponent<MeshRenderer>().sharedMaterial = Art.Material;

            var turret = root.AddComponent<ShipTurret>();
            turret.Head = headObject.transform;
            turret.Kind = kind.Value;
            turret.Size = head.Size;
            turret.Muzzles.AddRange(head.Muzzles);
            Turrets[slot] = turret;
            return turret;
        }
    }
}
