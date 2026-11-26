using Space.Core.Combat;
using Space.Core.Orders;
using Space.Core.Ships;
using Space.Core.Time;
using Space.Presentation.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space.Presentation.Controls
{
    /// <summary>
    /// Mouse and keyboard controls for the local player (M1):
    /// left click or Tab selects a ship; right click orders the selected ship to move to the clicked
    /// point on the game plane (Shift appends to its queue), or to attack the enemy ship under the
    /// pointer; F cycles the fire mode of its weapons. Orders go through the same components as the
    /// AI's (<see cref="ShipCommand"/> queue, <see cref="AttackTarget"/>).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerControls : MonoBehaviour
    {
        [Tooltip("Name of the player's main ship when no ship carries the PlayerFlagship tag (Sandbox).")]
        [UnityEngine.Serialization.FormerlySerializedAs("InitialSelection")]
        public string MainShipName = "Cargo";

        [Tooltip("Smallest click radius around a ship, in pixels, so distant ships stay clickable.")]
        [Min(4f)] public float MinPickRadiusPixels = 18f;

        /// <summary>What presentation needs to know about the selected ship this frame.</summary>
        public struct SelectionInfo
        {
            public Entity Entity;
            public Vector3 RenderPosition;
            public float Radius;
            public float Speed;
            public string Name;
            public bool IsPlayerShip;
            public float Hull;
            public float HullMax;
            public float Shield;
            public float ShieldMax;
            public bool ShieldUp;
            public float ShieldBrokenSeconds;
            public bool HasPower;
            public PowerGrid Power;
            public int WeaponCount;
            public string WeaponSummary;
            public FireMode FireMode;
            public bool HasAttackTarget;
            public int OrderCount;
            public bool HasDestination;
            public Vector3 DestinationRenderPosition;
        }

        Camera _camera;
        World _world;
        EntityQuery _ships;
        EntityQuery _selected;
        EntityQuery _flagships;
        EntityQuery _time;
        EntityQuery _origin;
        Entity _mainShip;
        Entity _nameCacheEntity;
        string _nameCache;

        void Awake()
        {
            _camera = GetComponent<Camera>();
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
                _ships = em.CreateEntityQuery(ComponentType.ReadOnly<Ship>(), ComponentType.ReadOnly<LocalToWorld>(),
                                              ComponentType.ReadOnly<ShipRadius>());
                _selected = em.CreateEntityQuery(ComponentType.ReadOnly<Selected>(), ComponentType.ReadOnly<LocalToWorld>());
                _flagships = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerFlagship>(), ComponentType.ReadOnly<LocalToWorld>());
                _time = em.CreateEntityQuery(ComponentType.ReadOnly<SimulationTime>());
                _origin = em.CreateEntityQuery(ComponentType.ReadOnly<FloatingOrigin>());
                _mainShip = Entity.Null;
            }
            return true;
        }

        void Update()
        {
            if (!EnsureWorld())
                return;

            var em = _world.EntityManager;
            TrackMainShip(em);

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame)
                    SelectNext(em, keyboard.shiftKey.isPressed ? -1 : 1);
                if (keyboard.fKey.wasPressedThisFrame)
                    CycleFireMode(em);

                // Power: Q shield, W energy weapons, E physical weapons, R engines; Shift takes a unit back.
                var step = keyboard.shiftKey.isPressed ? -1 : 1;
                if (keyboard.qKey.wasPressedThisFrame) ChangePower(PowerSystem.Shield, step);
                if (keyboard.wKey.wasPressedThisFrame) ChangePower(PowerSystem.EnergyWeapons, step);
                if (keyboard.eKey.wasPressedThisFrame) ChangePower(PowerSystem.PhysicalWeapons, step);
                if (keyboard.rKey.wasPressedThisFrame) ChangePower(PowerSystem.Engines, step);
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var pointer = mouse.position.ReadValue();

            // Alt + left drag orbits the camera (ShipCameraRig), so it must not change the selection.
            // A click in empty space keeps the current selection, so the camera never loses its target.
            var altHeld = keyboard != null && keyboard.altKey.isPressed;
            if (mouse.leftButton.wasPressedThisFrame && !altHeld)
            {
                var picked = Pick(em, pointer);
                if (picked != Entity.Null)
                    Select(em, picked);
            }

            if (mouse.rightButton.wasPressedThisFrame && TryGetOrderableSelection(em, out var ship))
            {
                var picked = Pick(em, pointer);
                if (picked != Entity.Null && IsEnemyOf(em, picked, ship))
                    em.SetComponentData(ship, new AttackTarget { Value = picked });
                else
                    IssueMoveOrder(em, ship, pointer, keyboard != null && keyboard.shiftKey.isPressed);
            }
        }

        /// <summary>
        /// Follows the player's main ship (PlayerFlagship tag, or <see cref="MainShipName"/>): selects
        /// it when it appears (new level) and whenever nothing is selected.
        /// </summary>
        void TrackMainShip(EntityManager em)
        {
            if (!IsLiveShip(em, _mainShip))
            {
                _mainShip = FindMainShip(em);
                if (_mainShip != Entity.Null)
                    Select(em, _mainShip);
            }
            else if (_selected.IsEmpty)
            {
                Select(em, _mainShip);
            }
        }

        Entity FindMainShip(EntityManager em)
        {
            if (!_flagships.IsEmpty)
            {
                using var flagships = _flagships.ToEntityArray(Allocator.Temp);
                return flagships[0];
            }
            return FindByName(em, MainShipName);
        }

        /// <summary>Returns the selected ship, if any. Used by the camera rig and the HUD.</summary>
        public bool TryGetSelection(out SelectionInfo info)
        {
            info = default;
            if (!EnsureWorld() || _selected.IsEmpty)
                return false;

            var em = _world.EntityManager;
            using var entities = _selected.ToEntityArray(Allocator.Temp);
            var entity = entities[0];

            info.Entity = entity;
            info.RenderPosition = em.GetComponentData<LocalToWorld>(entity).Position;
            info.Radius = em.HasComponent<ShipRadius>(entity) ? em.GetComponentData<ShipRadius>(entity).Value : 1f;
            info.Speed = em.HasComponent<ShipVelocity>(entity) ? math.length(em.GetComponentData<ShipVelocity>(entity).Linear) : 0f;
            info.Name = GetName(em, entity);
            info.IsPlayerShip = IsPlayerShip(em, entity);

            if (em.HasComponent<Hull>(entity))
            {
                var hull = em.GetComponentData<Hull>(entity);
                info.Hull = hull.Current;
                info.HullMax = hull.Max;
            }

            if (em.HasComponent<Shield>(entity))
            {
                var shield = em.GetComponentData<Shield>(entity);
                info.Shield = shield.Current;
                info.ShieldMax = shield.Max;
                info.ShieldUp = shield.IsUp;
                var tick = _time.IsEmpty ? 0u : _time.GetSingleton<SimulationTime>().Tick;
                if (shield.BrokenUntilTick > tick)
                    info.ShieldBrokenSeconds = (shield.BrokenUntilTick - tick) / SimulationClockBootstrapSystem.TickRate;
            }

            if (em.HasComponent<PowerGrid>(entity))
            {
                info.HasPower = true;
                info.Power = em.GetComponentData<PowerGrid>(entity);
            }

            if (em.HasBuffer<Weapon>(entity))
            {
                var weapons = em.GetBuffer<Weapon>(entity, isReadOnly: true);
                info.WeaponCount = weapons.Length;
                if (weapons.Length > 0)
                    info.FireMode = weapons[0].Mode;
                info.WeaponSummary = SummarizeWeapons(weapons);
            }

            if (em.HasComponent<AttackTarget>(entity))
            {
                var target = em.GetComponentData<AttackTarget>(entity).Value;
                info.HasAttackTarget = target != Entity.Null && em.Exists(target);
            }

            if (em.HasBuffer<ShipCommand>(entity))
            {
                var commands = em.GetBuffer<ShipCommand>(entity, isReadOnly: true);
                info.OrderCount = commands.Length;
                if (commands.Length > 0 && commands[0].Type == ShipCommandType.MoveTo)
                {
                    info.HasDestination = true;
                    info.DestinationRenderPosition = (float3)(commands[0].Point - GetOrigin());
                }
            }
            return true;
        }

        /// <summary>Render-space position of the player's main ship (centre of the range rings).</summary>
        public bool TryGetMainShipPosition(out Vector3 renderPosition)
        {
            renderPosition = default;
            if (!EnsureWorld() || _mainShip == Entity.Null)
                return false;

            var em = _world.EntityManager;
            if (!IsLiveShip(em, _mainShip) || !em.HasComponent<LocalToWorld>(_mainShip))
                return false;

            renderPosition = em.GetComponentData<LocalToWorld>(_mainShip).Position;
            return true;
        }

        static readonly System.Text.StringBuilder SummaryBuilder = new();

        static string SummarizeWeapons(DynamicBuffer<Weapon> weapons)
        {
            SummaryBuilder.Clear();
            for (var i = 0; i < weapons.Length; i++)
            {
                if (i > 0)
                    SummaryBuilder.Append('\n');
                var weapon = weapons[i];
                SummaryBuilder.Append(weapon.Type switch
                {
                    DamageType.Ion => "Ionique",
                    DamageType.Laser => "Laser (énergie)",
                    DamageType.Kinetic => "Canon (physique)",
                    _ => "Missiles",
                });
                if (weapon.Ammo >= 0)
                    SummaryBuilder.Append(" · ").Append(weapon.Ammo).Append(" mun.");
                var charge = weapon.Cooldown > 0f ? 1f - weapon.CooldownLeft / weapon.Cooldown : 1f;
                SummaryBuilder.Append(weapon.Ammo == 0 ? " · vide"
                    : charge >= 1f ? (weapon.CurrentTarget != Entity.Null ? " · tire" : " · prête")
                    : $" · {charge * 100f:0} %");
            }
            return SummaryBuilder.ToString();
        }

        string GetName(EntityManager em, Entity entity)
        {
            if (entity != _nameCacheEntity)
            {
                _nameCacheEntity = entity;
                _nameCache = em.HasComponent<ShipName>(entity) ? em.GetComponentData<ShipName>(entity).Value.ToString() : "?";
            }
            return _nameCache;
        }

        double3 GetOrigin()
        {
            return _origin.IsEmpty ? double3.zero : _origin.GetSingleton<FloatingOrigin>().Origin;
        }

        /// <summary>
        /// A destroyed entity can linger with only its cleanup components (rendering), so
        /// <see cref="EntityManager.Exists"/> is not enough: a live ship still has its <see cref="Ship"/> tag.
        /// </summary>
        static bool IsLiveShip(EntityManager em, Entity entity)
        {
            return entity != Entity.Null && em.Exists(entity) && em.HasComponent<Ship>(entity);
        }

        static bool IsPlayerShip(EntityManager em, Entity entity)
        {
            return !em.HasComponent<Team>(entity) || em.GetComponentData<Team>(entity).Value == Team.Player;
        }

        static bool IsEnemyOf(EntityManager em, Entity candidate, Entity ship)
        {
            return em.HasComponent<Team>(candidate) && em.HasComponent<Team>(ship)
                && em.GetComponentData<Team>(candidate).Value != em.GetComponentData<Team>(ship).Value;
        }

        /// <summary>The selected ship, if the player may give it orders.</summary>
        bool TryGetOrderableSelection(EntityManager em, out Entity ship)
        {
            ship = Entity.Null;
            if (_selected.IsEmpty)
                return false;
            using var entities = _selected.ToEntityArray(Allocator.Temp);
            ship = entities[0];
            return IsPlayerShip(em, ship);
        }

        Entity FindByName(EntityManager em, string shipName)
        {
            if (string.IsNullOrEmpty(shipName) || _ships.IsEmpty)
                return Entity.Null;

            var wanted = new FixedString64Bytes(shipName);
            using var entities = _ships.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (em.HasComponent<ShipName>(entity) && em.GetComponentData<ShipName>(entity).Value == wanted)
                    return entity;
            }
            return Entity.Null;
        }

        /// <summary>Tab / Shift+Tab: cycles through the player's ships in a stable order.</summary>
        void SelectNext(EntityManager em, int step)
        {
            if (_ships.IsEmpty)
                return;

            using var all = _ships.ToEntityArray(Allocator.Temp);
            var own = new NativeList<Entity>(all.Length, Allocator.Temp);
            foreach (var entity in all)
                if (IsPlayerShip(em, entity))
                    own.Add(entity);
            if (own.Length == 0)
                return;
            own.Sort(new EntityIndexComparer());

            var current = -1;
            if (!_selected.IsEmpty)
            {
                using var selected = _selected.ToEntityArray(Allocator.Temp);
                current = own.IndexOf(selected[0]);
            }

            var next = current < 0 ? 0 : (current + step + own.Length) % own.Length;
            Select(em, own[next]);
            own.Dispose();
        }

        struct EntityIndexComparer : System.Collections.Generic.IComparer<Entity>
        {
            public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index);
        }

        /// <summary>F: Normal → Trigger happy → Conservative, for every weapon of the selected ship.</summary>
        void CycleFireMode(EntityManager em)
        {
            if (!TryGetOrderableSelection(em, out var ship) || !em.HasBuffer<Weapon>(ship))
                return;

            var weapons = em.GetBuffer<Weapon>(ship);
            if (weapons.Length == 0)
                return;

            var next = (FireMode)(((int)weapons[0].Mode + 1) % 3);
            for (var i = 0; i < weapons.Length; i++)
            {
                var weapon = weapons[i];
                weapon.Mode = next;
                weapons[i] = weapon;
            }
        }

        /// <summary>
        /// Moves one power unit into (delta &gt; 0) or out of (delta &lt; 0) a subsystem of the selected
        /// ship. Used by the keyboard shortcuts and by the power panel's buttons.
        /// </summary>
        public bool ChangePower(PowerSystem system, int delta)
        {
            if (!EnsureWorld())
                return false;
            var em = _world.EntityManager;
            if (!TryGetOrderableSelection(em, out var ship) || !em.HasComponent<PowerGrid>(ship))
                return false;

            var grid = em.GetComponentData<PowerGrid>(ship);
            var changed = delta > 0 ? PowerMath.TryAdd(ref grid, system) : PowerMath.TryRemove(ref grid, system);
            if (changed)
                em.SetComponentData(ship, grid);
            return changed;
        }

        /// <summary>Closest ship under the pointer, using each ship's projected radius.</summary>
        Entity Pick(EntityManager em, Vector2 pointer)
        {
            var best = Entity.Null;
            var bestScore = float.MaxValue;
            var pixelsPerMetreAtUnitDistance = Screen.height * 0.5f / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            using var entities = _ships.ToEntityArray(Allocator.Temp);
            using var transforms = _ships.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            using var radii = _ships.ToComponentDataArray<ShipRadius>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var screen = _camera.WorldToScreenPoint(transforms[i].Position);
                if (screen.z <= 0f)
                    continue;

                var pickRadius = Mathf.Max(MinPickRadiusPixels, radii[i].Value * pixelsPerMetreAtUnitDistance / screen.z);
                var score = Vector2.Distance(pointer, screen) / pickRadius;
                if (score <= 1f && score < bestScore)
                {
                    bestScore = score;
                    best = entities[i];
                }
            }
            return best;
        }

        /// <summary>Selects <paramref name="entity"/> and makes it the floating-origin anchor.</summary>
        void Select(EntityManager em, Entity entity)
        {
            em.RemoveComponent<Selected>(_selected);
            if (entity == Entity.Null)
                return;

            using (var anchorQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FloatingOriginAnchor>()))
                em.RemoveComponent<FloatingOriginAnchor>(anchorQuery);

            em.AddComponent<Selected>(entity);
            em.AddComponent<FloatingOriginAnchor>(entity);
        }

        void IssueMoveOrder(EntityManager em, Entity ship, Vector2 pointer, bool append)
        {
            if (!em.HasBuffer<ShipCommand>(ship))
                return;

            // Destination on the game plane through the ship.
            var shipPosition = (Vector3)em.GetComponentData<LocalToWorld>(ship).Position;
            var ray = _camera.ScreenPointToRay(pointer);
            if (Mathf.Abs(ray.direction.y) < 1e-4f)
                return;
            var distance = (shipPosition.y - ray.origin.y) / ray.direction.y;
            if (distance <= 0f)
                return; // clicked above the horizon

            var renderPoint = ray.origin + ray.direction * distance;
            var simPoint = (double3)(float3)renderPoint + GetOrigin();
            var tick = _time.IsEmpty ? 0u : _time.GetSingleton<SimulationTime>().Tick;

            var commands = em.GetBuffer<ShipCommand>(ship);
            if (!append)
                commands.Clear();
            commands.Add(new ShipCommand
            {
                Type = ShipCommandType.MoveTo,
                Target = Entity.Null,
                Point = simPoint,
                IssuedTick = tick,
            });
        }
    }
}
