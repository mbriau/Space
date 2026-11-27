using System.Collections.Generic;
using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// A ship drawn with its finished art (<see cref="ShipArt"/>): the textured hull with a turret head
    /// per fitted weapon on the slot pivots, or a ready-made model with generated turrets on its slots
    /// and a faint shield aura shaped on its colliders. Same turret API as <see cref="ShipModel"/>.
    /// </summary>
    public class ShipArtModel : MonoBehaviour
    {
        [Tooltip("Aura brightness with a full shield (it fades with the charge).")]
        public float AuraIntensity = 0.35f;

        [Tooltip("Extra brightness of the whole aura right after a hit.")]
        public float HitFlash = 1.2f;

        public ShipArt Art { get; private set; }

        /// <summary>One entry per slot; null where no weapon is fitted.</summary>
        public readonly List<ShipTurret> Turrets = new();

        readonly List<Mesh> _meshes = new();
        readonly List<MeshRenderer> _aura = new();
        Material _auraMaterial;
        MeshCollider _shellCollider;
        float _flash;
        static Mesh _sphere;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

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
            if (art.Model != null)
            {
                // A ready-made model: only its looks (its colliders would mean nothing to the simulation).
                var model = Instantiate(art.Model, transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * art.ModelScale;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                    Destroy(collider);
                BuildAura(art);
            }
            else
            {
                var hull = new GameObject("Hull");
                hull.transform.SetParent(transform, false);
                hull.AddComponent<MeshFilter>().sharedMesh = art.Hull;
                hull.AddComponent<MeshRenderer>().sharedMaterial = art.Material;
                BuildAura(art);
            }
            for (var i = 0; i < art.Slots.Count; i++)
                Turrets.Add(null);
        }

        void BuildAura(ShipArt art)
        {
            if (!art.HasAura)
                return;
            var shader = Shader.Find("Space/ShieldAura");
            if (shader == null)
                return;
            _auraMaterial = new Material(shader) { name = "ShieldAura" };

            if (art.AuraShell != null)
            {
                // The hull pushed outward: the aura hugs the whole silhouette. Its collider finds where
                // shots strike it.
                var shell = new GameObject("Aura", typeof(MeshFilter), typeof(MeshRenderer));
                shell.transform.SetParent(transform, false);
                shell.GetComponent<MeshFilter>().sharedMesh = art.AuraShell;
                var shellRenderer = shell.GetComponent<MeshRenderer>();
                shellRenderer.sharedMaterial = _auraMaterial;
                shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
                _aura.Add(shellRenderer);
                _shellCollider = shell.AddComponent<MeshCollider>();
                _shellCollider.sharedMesh = art.AuraShell;
                return;
            }

            foreach (var shape in art.Aura)
            {
                var go = new GameObject("Aura", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = shape.Center;
                go.transform.localRotation = Quaternion.Euler(shape.Euler);
                go.transform.localScale = shape.Size;
                go.GetComponent<MeshFilter>().sharedMesh = Sphere;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _auraMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _aura.Add(renderer);
            }
        }

        /// <summary>The aura follows the shield: brighter with the charge, gone when it is down.</summary>
        public void SetShield(float charge, bool up)
        {
            if (_auraMaterial == null)
                return;
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 3f);
            var intensity = up ? AuraIntensity * (0.35f + 0.65f * Mathf.Clamp01(charge)) + _flash * HitFlash : 0f;
            _auraMaterial.SetFloat(IntensityId, intensity);
            foreach (var renderer in _aura)
                renderer.enabled = intensity > 0.001f;
        }

        /// <summary>A shot struck the shield: the whole aura lights up for an instant.</summary>
        public void FlashShield() => _flash = 1f;

        /// <summary>
        /// Where a shot coming from <paramref name="from"/> (world) meets the aura, and the surface's
        /// outward normal. False when the model has no aura.
        /// </summary>
        public bool TryShieldHit(Vector3 from, out Vector3 point, out Vector3 normal)
        {
            point = transform.position;
            normal = Vector3.up;
            if (_shellCollider != null)
            {
                // Toward the centre of the ship, from where the shot came, onto the shell.
                var ray = new Ray(from, (transform.position - from).normalized);
                if (_shellCollider.Raycast(ray, out var hit, Vector3.Distance(from, transform.position) * 2f))
                {
                    point = hit.point;
                    normal = hit.normal;
                    return true;
                }
                return false;
            }
            if (Art == null || Art.Aura == null || Art.Aura.Count == 0)
                return false;

            var best = float.PositiveInfinity;
            foreach (var shape in Art.Aura)
            {
                // Into the ellipsoid's own space, where it is a unit sphere.
                var toLocal = Matrix4x4.TRS(transform.TransformPoint(shape.Center), transform.rotation * Quaternion.Euler(shape.Euler), Vector3.Scale(transform.lossyScale, shape.Size * 0.5f)).inverse;
                var origin = toLocal.MultiplyPoint3x4(from);
                var direction = toLocal.MultiplyVector(transform.TransformPoint(shape.Center) - from).normalized;
                var b = Vector3.Dot(origin, direction);
                var c = origin.sqrMagnitude - 1f;
                var discriminant = b * b - c;
                if (discriminant < 0f)
                    continue;
                var t = -b - Mathf.Sqrt(discriminant);
                if (t < 0f)
                    continue;
                var local = origin + direction * t;
                var world = toLocal.inverse.MultiplyPoint3x4(local);
                var distance = (world - from).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    point = world;
                    // The normal of a scaled sphere: the inverse transpose of the ellipsoid's matrix.
                    normal = toLocal.transpose.MultiplyVector(local).normalized;
                }
            }
            return best < float.PositiveInfinity;
        }

        /// <summary>Puts the turret matching <paramref name="kind"/> on a slot, or empties it with null.</summary>
        public ShipTurret SetTurret(int slot, TurretKind? kind)
        {
            if (slot < 0 || slot >= Turrets.Count)
                return null;
            if (Turrets[slot] != null)
            {
                Destroy(Turrets[slot].gameObject);
                Turrets[slot] = null;
            }
            if (kind == null)
                return null;

            // Cut-out heads of a textured hull, or a generated turret on a ready-made model.
            var head = Art.BestTurret(kind.Value);
            if (head == null && Art.Model == null)
                return null;

            var root = new GameObject($"Turret {slot + 1}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Art.Slots[slot];
            var turret = root.AddComponent<ShipTurret>();
            turret.Kind = kind.Value;

            var headObject = new GameObject("Head");
            headObject.transform.SetParent(root.transform, false);
            if (head != null)
            {
                headObject.AddComponent<MeshFilter>().sharedMesh = head.Head;
                headObject.AddComponent<MeshRenderer>().sharedMaterial = Art.Material;
                turret.Size = head.Size;
                turret.Muzzles.AddRange(head.Muzzles);
            }
            else
            {
                var meshes = TurretGenerator.Build(kind.Value, Art.TurretSize, ShipDesigns.PirateIron);
                AddRenderer(root, meshes.Base, $"{Art.DesignId}_Turret{slot}_Base");
                headObject.transform.localPosition = new Vector3(0f, meshes.PivotHeight, 0f);
                AddRenderer(headObject, meshes.Head, $"{Art.DesignId}_Turret{slot}_Head");
                turret.Size = Art.TurretSize;
                turret.Muzzles.AddRange(meshes.Muzzles);
            }
            turret.Head = headObject.transform;
            Turrets[slot] = turret;
            return turret;
        }

        void AddRenderer(GameObject go, ShipMeshData data, string meshName)
        {
            var mesh = ShipMeshes.ToMesh(data, meshName);
            _meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = ShipMeshes.Toon;
        }

        static Mesh Sphere
        {
            get
            {
                if (_sphere != null)
                    return _sphere;
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _sphere = primitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primitive);
                return _sphere;
            }
        }

        void OnDestroy()
        {
            foreach (var mesh in _meshes)
                if (mesh != null)
                    Destroy(mesh);
            if (_auraMaterial != null)
                Destroy(_auraMaterial);
        }
    }
}
