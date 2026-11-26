using System.Collections.Generic;
using Space.Core.Equipment;
using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// A generated ship in the scene: the hull, one turret per fitted weapon on its hardpoints, and
    /// a glowing halo in every nozzle. Weapons can be swapped at any time (<see cref="SetWeapon"/>).
    /// </summary>
    public class ShipModel : MonoBehaviour
    {
        [Tooltip("Halo diameter as a multiple of the nozzle glow radius.")]
        public float HaloScale = 4.5f;

        [Tooltip("How much the halos flicker (share of their size).")]
        [Range(0f, 0.3f)] public float Flicker = 0.06f;

        public GeneratedShip Ship { get; private set; }
        public float Radius { get; private set; }

        /// <summary>One entry per hardpoint; null where no weapon is fitted.</summary>
        public readonly List<ShipTurret> Turrets = new();
        readonly List<Transform> _halos = new();
        readonly List<float> _haloSizes = new();
        readonly List<Mesh> _meshes = new();
        MaterialPropertyBlock _block;

        public static ShipModel Create(in ShipDesign design, Transform parent)
        {
            var go = new GameObject(design.DisplayName);
            go.transform.SetParent(parent, false);
            var model = go.AddComponent<ShipModel>();
            model.Build(ShipGenerator.Generate(design));
            return model;
        }

        void Build(GeneratedShip ship)
        {
            Ship = ship;
            // Same rule as the game: half of the largest dimension.
            var size = ship.Bounds.size;
            Radius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f;

            var hull = new GameObject("Hull");
            hull.transform.SetParent(transform, false);
            AddRenderer(hull, ship.Hull, $"{ship.Design.Id}_Hull");

            _block ??= new MaterialPropertyBlock();
            var glow = ship.Design.Palette.Glow;
            foreach (var engine in ship.Engines)
            {
                var halo = new GameObject("Halo");
                halo.transform.SetParent(transform, false);
                halo.transform.localPosition = engine.Position - new Vector3(0f, 0f, engine.Radius * 0.2f);
                halo.transform.localScale = Vector3.one * engine.Radius * HaloScale;
                halo.AddComponent<MeshFilter>().sharedMesh = ShipMeshes.Quad;
                var renderer = halo.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ShipMeshes.Halo;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _block.SetColor("_Color", new Color(glow.r, glow.g, glow.b, 1f));
                renderer.SetPropertyBlock(_block);
                _halos.Add(halo.transform);
                _haloSizes.Add(engine.Radius * HaloScale);
            }

            for (var i = 0; i < ship.Hardpoints.Count; i++)
                Turrets.Add(null);
            var weapons = ship.Design.DefaultWeapons;
            for (var i = 0; i < ship.Hardpoints.Count; i++)
                SetWeapon(i, weapons != null && i < weapons.Length ? weapons[i] : null);
        }

        void AddRenderer(GameObject go, ShipMeshData data, string meshName)
        {
            var mesh = ShipMeshes.ToMesh(data, meshName);
            _meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = ShipMeshes.Toon;
        }

        /// <summary>Weapon id fitted on a hardpoint, or null.</summary>
        public string WeaponAt(int slot) => slot < Turrets.Count && Turrets[slot] != null ? Turrets[slot].Weapon.Id : null;

        /// <summary>Fits a weapon (catalogue id) on a hardpoint, or empties it with null.</summary>
        public void SetWeapon(int slot, string itemId)
        {
            if (slot < 0 || slot >= Ship.Hardpoints.Count)
                return;
            if (Turrets[slot] != null)
            {
                Destroy(Turrets[slot].gameObject);
                Turrets[slot] = null;
            }

            var item = string.IsNullOrEmpty(itemId) ? null : ItemCatalog.Get(itemId);
            if (item == null || !ItemCategories.IsWeapon(item.Category))
                return;

            var hardpoint = Ship.Hardpoints[slot];
            var kind = TurretVisuals.KindFor(item);
            var size = hardpoint.Size * TurretVisuals.SizeFactor(item);
            var meshes = TurretGenerator.Build(kind, size, Ship.Design.Palette);

            var root = new GameObject($"Turret {slot + 1}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = hardpoint.Position;
            AddRenderer(root, meshes.Base, $"{Ship.Design.Id}_Turret{slot}_Base");

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, meshes.PivotHeight, 0f);
            AddRenderer(head, meshes.Head, $"{Ship.Design.Id}_Turret{slot}_Head");

            var turret = root.AddComponent<ShipTurret>();
            turret.Head = head.transform;
            turret.Kind = kind;
            turret.Size = size;
            turret.Weapon = item;
            turret.Muzzles.AddRange(meshes.Muzzles);
            // Stagger the first shots so the turrets do not all fire on the same frame.
            turret.Cooldown = 0.5f + slot * 0.6f;
            Turrets[slot] = turret;
        }

        void Update()
        {
            if (Flicker <= 0f)
                return;
            var t = Time.time * 18f;
            for (var i = 0; i < _halos.Count; i++)
            {
                var noise = Mathf.PerlinNoise(t, i * 3.7f) - 0.5f;
                _halos[i].localScale = Vector3.one * _haloSizes[i] * (1f + noise * 2f * Flicker);
            }
        }

        void OnDestroy()
        {
            foreach (var mesh in _meshes)
                if (mesh != null)
                    Destroy(mesh);
        }
    }
}
