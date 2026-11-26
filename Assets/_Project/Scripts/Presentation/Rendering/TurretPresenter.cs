using System.Collections.Generic;
using Space.Core.Combat;
using Space.Core.Ships;
using Space.Core.Time;
using Space.Presentation.Ships;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Every weapon is a turret on the deck of its ship: a base, a rotating head and its barrel(s).
    /// Turrets turn toward their weapon's current target and recoil with a muzzle flash when they
    /// fire (<see cref="NotifyFired"/>, called by <see cref="TracerRenderer"/>, which also starts the
    /// projectiles at the muzzle). A ship with a <see cref="ShipVisual"/> is drawn as its hangar design
    /// (<see cref="ShipModel"/>: hull, engine halos, generated turrets on the design's hardpoints);
    /// the others get placeholder turrets built from primitives along their centre line.
    /// </summary>
    public class TurretPresenter : MonoBehaviour
    {
        public static TurretPresenter Instance { get; private set; }

        [Tooltip("Turret size as a share of the ship radius.")]
        [Range(0.03f, 0.3f)] public float SizeShare = 0.15f;

        [Tooltip("Turn rate of the turret heads, degrees per simulated second.")]
        public float TurnRate = 70f;

        public Color PlayerColor = new(0.75f, 0.8f, 0.88f);
        public Color EnemyColor = new(0.55f, 0.3f, 0.3f);
        public Color BarrelColor = new(0.22f, 0.23f, 0.26f);
        public Color EnergyTipColor = new(1f, 0.4f, 0.6f);
        public Color FlashColor = new(1f, 0.8f, 0.45f);

        sealed class Turret
        {
            public Transform Head;
            public Transform Barrel;
            public Transform Muzzle;
            public Transform Flash;
            public DamageType Type;
            public float Yaw;
            public float Recoil;
            public float BarrelRest;
        }

        sealed class ShipTurrets
        {
            public GameObject Root;
            public readonly List<Turret> Turrets = new();

            /// <summary>
            /// The turrets of the design model (generated or finished art) when the ship is drawn as a
            /// hangar design; null for placeholder turrets (<see cref="Turrets"/>).
            /// </summary>
            public List<ShipTurret> ModelTurrets;
            public int Signature;
            public bool Seen;
        }

        readonly Dictionary<Entity, ShipTurrets> _ships = new();
        readonly List<Entity> _gone = new();
        World _world;
        EntityQuery _query;
        EntityQuery _time;
        Material _playerMaterial, _enemyMaterial, _barrelMaterial, _tipMaterial, _flashMaterial;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            foreach (var ship in _ships.Values)
                if (ship.Root != null)
                    Destroy(ship.Root);
            _ships.Clear();
        }

        bool EnsureWorld()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;
            if (world != _world)
            {
                _world = world;
                var em = world.EntityManager;
                _query = em.CreateEntityQuery(ComponentType.ReadOnly<Ship>(), ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<ShipRadius>(), ComponentType.ReadOnly<Team>());
                _time = em.CreateEntityQuery(ComponentType.ReadOnly<SimulationTime>());
            }
            return true;
        }

        void LateUpdate()
        {
            if (!EnsureWorld())
                return;
            EnsureMaterials();

            var em = _world.EntityManager;
            var dt = 0f;
            if (!_time.IsEmpty)
            {
                var time = _time.GetSingleton<SimulationTime>();
                dt = time.Paused ? 0f : Time.deltaTime * time.TimeScale;
            }

            foreach (var ship in _ships.Values)
                ship.Seen = false;

            using (var entities = _query.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    var weapons = em.HasBuffer<Weapon>(entity) ? em.GetBuffer<Weapon>(entity, true) : default;
                    var weaponCount = weapons.IsCreated ? weapons.Length : 0;
                    var design = em.HasComponent<ShipVisual>(entity)
                        ? ShipDesigns.Get(em.GetComponentData<ShipVisual>(entity).DesignId.ToString())
                        : null;
                    if (design == null && weaponCount == 0)
                        continue;

                    var ltw = em.GetComponentData<LocalToWorld>(entity);
                    var radius = em.GetComponentData<ShipRadius>(entity).Value;
                    var team = em.GetComponentData<Team>(entity).Value;

                    var signature = Signature(weapons) * 31 + (design?.Id.GetHashCode() ?? 0);
                    if (!_ships.TryGetValue(entity, out var set) || set.Signature != signature || set.Root == null)
                    {
                        if (set?.Root != null)
                            Destroy(set.Root);
                        set = design != null ? BuildModel(design.Value, weapons) : Build(weapons, radius, team);
                        set.Signature = signature;
                        _ships[entity] = set;
                    }
                    set.Seen = true;
                    set.Root.transform.SetPositionAndRotation(ltw.Position, ltw.Rotation);

                    if (set.ModelTurrets != null)
                    {
                        for (var i = 0; i < set.ModelTurrets.Count && i < weaponCount; i++)
                            AnimateModelTurret(em, set.ModelTurrets[i], weapons[i], dt);
                    }
                    else
                    {
                        for (var i = 0; i < set.Turrets.Count && i < weaponCount; i++)
                            Animate(em, set.Turrets[i], weapons[i], dt);
                    }
                }
            }

            _gone.Clear();
            foreach (var pair in _ships)
                if (!pair.Value.Seen)
                    _gone.Add(pair.Key);
            foreach (var entity in _gone)
            {
                if (_ships[entity].Root != null)
                    Destroy(_ships[entity].Root);
                _ships.Remove(entity);
            }
        }

        static int Signature(DynamicBuffer<Weapon> weapons)
        {
            if (!weapons.IsCreated)
                return 0;
            var hash = weapons.Length;
            foreach (var w in weapons)
                hash = hash * 31 + (int)w.Type;
            return hash;
        }

        /// <summary>A turret of a hangar design: it aims at its weapon's target (or holds its bearing) and its recoil decays.</summary>
        static void AnimateModelTurret(EntityManager em, ShipTurret turret, in Weapon weapon, float dt)
        {
            if (turret == null)
                return;
            var target = weapon.CurrentTarget;
            var aim = target != Entity.Null && em.Exists(target) && em.HasComponent<LocalToWorld>(target)
                ? (Vector3)em.GetComponentData<LocalToWorld>(target).Position
                : turret.Head.position + turret.Forward * 100f;
            turret.Aim(aim, dt);
        }

        void Animate(EntityManager em, Turret turret, in Weapon weapon, float dt)
        {
            // Turn toward the current target, in the ship's frame (heads only yaw: everything is on one plane).
            var target = weapon.CurrentTarget;
            if (target != Entity.Null && em.Exists(target) && em.HasComponent<LocalToWorld>(target))
            {
                var targetPosition = (Vector3)em.GetComponentData<LocalToWorld>(target).Position;
                var parent = turret.Head.parent;
                var local = parent.InverseTransformDirection(targetPosition - turret.Head.position);
                var wanted = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                turret.Yaw = Mathf.MoveTowardsAngle(turret.Yaw, wanted, TurnRate * dt);
            }
            turret.Head.localRotation = Quaternion.Euler(0f, turret.Yaw, 0f);

            // Recoil and muzzle flash, decaying over a fraction of a second.
            turret.Recoil = Mathf.Max(0f, turret.Recoil - dt * 2.5f);
            var kick = turret.Recoil * turret.Recoil;
            var barrelPosition = turret.Barrel.localPosition;
            barrelPosition.z = turret.BarrelRest - kick * 0.35f;
            turret.Barrel.localPosition = barrelPosition;
            if (turret.Flash != null)
            {
                var flash = Mathf.Clamp01((turret.Recoil - 0.6f) / 0.4f);
                turret.Flash.gameObject.SetActive(flash > 0f);
                turret.Flash.localScale = Vector3.one * (0.5f + flash);
            }
        }

        /// <summary>A weapon of this ship has just fired: recoil and flash.</summary>
        public void NotifyFired(Entity ship, int weaponIndex)
        {
            if (!_ships.TryGetValue(ship, out var set) || weaponIndex < 0)
                return;
            if (set.ModelTurrets != null)
            {
                if (weaponIndex < set.ModelTurrets.Count && set.ModelTurrets[weaponIndex] != null)
                    set.ModelTurrets[weaponIndex].Fire();
            }
            else if (weaponIndex < set.Turrets.Count)
            {
                set.Turrets[weaponIndex].Recoil = 1f;
            }
        }

        /// <summary>World position of a weapon's muzzle, for projectiles to start from.</summary>
        public bool TryGetMuzzle(Entity ship, int weaponIndex, out Vector3 position)
        {
            position = default;
            if (!_ships.TryGetValue(ship, out var set) || weaponIndex < 0)
                return false;
            if (set.ModelTurrets != null)
            {
                if (weaponIndex >= set.ModelTurrets.Count || set.ModelTurrets[weaponIndex] == null)
                    return false;
                position = set.ModelTurrets[weaponIndex].NextMuzzle;
                return true;
            }
            if (weaponIndex >= set.Turrets.Count)
                return false;
            position = set.Turrets[weaponIndex].Muzzle.position;
            return true;
        }

        // ------------------------------------------------------------------ construction

        /// <summary>
        /// The hangar design of the ship, with a turret per weapon on the hardpoints in slot order
        /// (a ship with more weapons than hardpoints leaves the extra ones without a turret).
        /// </summary>
        static ShipTurrets BuildModel(in ShipDesign design, DynamicBuffer<Weapon> weapons)
        {
            var set = new ShipTurrets { Root = new GameObject($"Ship {design.DisplayName}") };
            set.Root.hideFlags = HideFlags.DontSave;
            var count = weapons.IsCreated ? weapons.Length : 0;

            // Finished art when the design has some (textured hull, heads cut out of the model).
            var art = ShipArtLibrary.For(design.Id);
            if (art != null)
            {
                var artModel = ShipArtModel.Create(art, set.Root.transform);
                for (var i = 0; i < count && i < artModel.Turrets.Count; i++)
                {
                    var w = weapons[i];
                    artModel.SetTurret(i, TurretVisuals.KindFor(w.Type, w.ProjectileSpeed, w.Damage, w.Cooldown));
                }
                set.ModelTurrets = artModel.Turrets;
                return set;
            }

            var model = ShipModel.Create(design, set.Root.transform, fitDefaultWeapons: false);
            for (var i = 0; i < count && i < model.Turrets.Count; i++)
            {
                var w = weapons[i];
                var kind = TurretVisuals.KindFor(w.Type, w.ProjectileSpeed, w.Damage, w.Cooldown);
                model.SetTurret(i, (kind, TurretVisuals.SizeFactor(1, w.PowerDraw)));
            }
            set.ModelTurrets = model.Turrets;
            return set;
        }

        ShipTurrets Build(DynamicBuffer<Weapon> weapons, float radius, byte team)
        {
            var set = new ShipTurrets { Root = new GameObject("Turrets") };
            set.Root.hideFlags = HideFlags.DontSave;
            var size = radius * SizeShare;
            var deck = radius * 0.2f;
            var count = weapons.Length;
            var hullMaterial = team == Team.Player ? _playerMaterial : _enemyMaterial;

            for (var i = 0; i < count; i++)
            {
                // Along the centre line of the deck, from the bow toward the stern.
                var z = count == 1 ? radius * 0.15f : Mathf.Lerp(radius * 0.4f, -radius * 0.35f, i / (float)(count - 1));
                var mount = new GameObject($"Turret{i}").transform;
                mount.SetParent(set.Root.transform, false);
                mount.localPosition = new Vector3(0f, deck, z);

                var turret = new Turret { Type = weapons[i].Type };
                Part(PrimitiveType.Cylinder, mount, new Vector3(0f, size * 0.12f, 0f), new Vector3(size * 1.2f, size * 0.12f, size * 1.2f), hullMaterial);

                turret.Head = new GameObject("Head").transform;
                turret.Head.SetParent(mount, false);
                turret.Head.localPosition = new Vector3(0f, size * 0.24f, 0f);

                float barrelLength, barrelWidth;
                switch (weapons[i].Type)
                {
                    case DamageType.Missile:
                        barrelLength = size * 0.7f;
                        barrelWidth = size * 0.8f;
                        Part(PrimitiveType.Cube, turret.Head, new Vector3(0f, size * 0.35f, 0f), new Vector3(size * 1.0f, size * 0.55f, size * 0.9f), hullMaterial);
                        break;
                    case DamageType.Kinetic:
                        barrelLength = size * 2.2f;
                        barrelWidth = size * 0.24f;
                        Part(PrimitiveType.Cube, turret.Head, new Vector3(0f, size * 0.3f, -size * 0.1f), new Vector3(size * 0.95f, size * 0.5f, size * 1.1f), hullMaterial);
                        break;
                    default:
                        barrelLength = size * 1.6f;
                        barrelWidth = size * 0.14f;
                        Part(PrimitiveType.Sphere, turret.Head, new Vector3(0f, size * 0.3f, 0f), new Vector3(size * 0.85f, size * 0.6f, size * 0.85f), hullMaterial);
                        break;
                }

                // Barrel: a pivot that slides back on recoil, holding the tube and the muzzle.
                turret.Barrel = new GameObject("Barrel").transform;
                turret.Barrel.SetParent(turret.Head, false);
                turret.BarrelRest = size * 0.3f;
                turret.Barrel.localPosition = new Vector3(0f, size * 0.32f, turret.BarrelRest);
                if (weapons[i].Type == DamageType.Missile)
                {
                    Part(PrimitiveType.Cube, turret.Barrel, new Vector3(0f, 0.04f * size, barrelLength * 0.5f),
                        new Vector3(barrelWidth, size * 0.45f, barrelLength), _barrelMaterial);
                }
                else
                {
                    var tube = Part(PrimitiveType.Cylinder, turret.Barrel, new Vector3(0f, 0f, barrelLength * 0.5f),
                        new Vector3(barrelWidth, barrelLength * 0.5f, barrelWidth), _barrelMaterial);
                    tube.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    if (weapons[i].Type != DamageType.Kinetic)
                        Part(PrimitiveType.Sphere, turret.Barrel, new Vector3(0f, 0f, barrelLength), Vector3.one * barrelWidth * 1.5f, _tipMaterial);
                }

                turret.Muzzle = new GameObject("Muzzle").transform;
                turret.Muzzle.SetParent(turret.Barrel, false);
                turret.Muzzle.localPosition = new Vector3(0f, 0f, barrelLength + barrelWidth);

                var flash = Part(PrimitiveType.Sphere, turret.Muzzle, Vector3.zero, Vector3.one * size * 0.6f, _flashMaterial);
                turret.Flash = new GameObject("Flash").transform;
                turret.Flash.SetParent(turret.Muzzle, false);
                flash.SetParent(turret.Flash, true);
                turret.Flash.gameObject.SetActive(false);

                set.Turrets.Add(turret);
            }
            return set;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void EnsureMaterials()
        {
            if (_playerMaterial != null)
                return;
            _playerMaterial = Lit(PlayerColor, 0.6f);
            _enemyMaterial = Lit(EnemyColor, 0.6f);
            _barrelMaterial = Lit(BarrelColor, 0.8f);
            _tipMaterial = Unlit(EnergyTipColor);
            _flashMaterial = Unlit(FlashColor);
        }

        static Material Lit(Color color, float metallic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.45f);
            return material;
        }

        static Material Unlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }
    }
}
