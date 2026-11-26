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
    /// Mouse controls for the local player (M1): left click selects a ship, right click orders the
    /// selected ship to move to the clicked point on the horizontal plane through the ship
    /// (Shift + right click appends to its order queue). Orders go through the ship's
    /// <see cref="ShipCommand"/> buffer, like every other order source.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerControls : MonoBehaviour
    {
        [Tooltip("Name of the player's main ship: selected when the level starts, centre of the range rings.")]
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
            public int OrderCount;
            public bool HasDestination;
            public Vector3 DestinationRenderPosition;
        }

        Camera _camera;
        World _world;
        EntityQuery _ships;
        EntityQuery _selected;
        EntityQuery _time;
        EntityQuery _origin;
        bool _initialSelectionDone;
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
                _time = em.CreateEntityQuery(ComponentType.ReadOnly<SimulationTime>());
                _origin = em.CreateEntityQuery(ComponentType.ReadOnly<FloatingOrigin>());
                _initialSelectionDone = false;
                _mainShip = Entity.Null;
            }
            return true;
        }

        void Update()
        {
            if (!EnsureWorld())
                return;

            var em = _world.EntityManager;
            if (!_initialSelectionDone)
            {
                _mainShip = FindByName(em, MainShipName);
                if (_mainShip != Entity.Null)
                {
                    Select(em, _mainShip);
                    _initialSelectionDone = true;
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                SelectNext(em, keyboard.shiftKey.isPressed ? -1 : 1);

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

            if (mouse.rightButton.wasPressedThisFrame)
            {
                var append = keyboard != null && keyboard.shiftKey.isPressed;
                IssueMoveOrder(em, pointer, append);
            }
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

        /// <summary>Render-space position of the player's main ship (centre of the range rings).</summary>
        public bool TryGetMainShipPosition(out Vector3 renderPosition)
        {
            renderPosition = default;
            if (!EnsureWorld() || _mainShip == Entity.Null)
                return false;

            var em = _world.EntityManager;
            if (!em.Exists(_mainShip) || !em.HasComponent<LocalToWorld>(_mainShip))
                return false;

            renderPosition = em.GetComponentData<LocalToWorld>(_mainShip).Position;
            return true;
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

        /// <summary>Tab / Shift+Tab: cycles through the ships in a stable order.</summary>
        void SelectNext(EntityManager em, int step)
        {
            if (_ships.IsEmpty)
                return;

            using var entities = _ships.ToEntityArray(Allocator.Temp);
            entities.Sort(new EntityIndexComparer());

            var current = -1;
            if (!_selected.IsEmpty)
            {
                using var selected = _selected.ToEntityArray(Allocator.Temp);
                current = entities.IndexOf(selected[0]);
            }

            var next = current < 0 ? 0 : (current + step + entities.Length) % entities.Length;
            Select(em, entities[next]);
        }

        struct EntityIndexComparer : System.Collections.Generic.IComparer<Entity>
        {
            public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index);
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

        /// <summary>Selects <paramref name="entity"/> (or clears the selection) and makes it the floating-origin anchor.</summary>
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

        void IssueMoveOrder(EntityManager em, Vector2 pointer, bool append)
        {
            if (_selected.IsEmpty)
                return;

            using var entities = _selected.ToEntityArray(Allocator.Temp);
            var entity = entities[0];
            if (!em.HasBuffer<ShipCommand>(entity))
                return;

            // Destination on the horizontal plane through the ship.
            var shipPosition = (Vector3)em.GetComponentData<LocalToWorld>(entity).Position;
            var ray = _camera.ScreenPointToRay(pointer);
            if (Mathf.Abs(ray.direction.y) < 1e-4f)
                return;
            var distance = (shipPosition.y - ray.origin.y) / ray.direction.y;
            if (distance <= 0f)
                return; // clicked above the horizon

            var renderPoint = ray.origin + ray.direction * distance;
            var simPoint = (double3)(float3)renderPoint + GetOrigin();
            var tick = _time.IsEmpty ? 0u : _time.GetSingleton<SimulationTime>().Tick;

            var commands = em.GetBuffer<ShipCommand>(entity);
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
