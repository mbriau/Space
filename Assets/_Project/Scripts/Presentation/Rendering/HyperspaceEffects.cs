using System.Collections.Generic;
using Space.Core.Combat;
using Space.Core.Ships;
using Space.Core.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Hyperspace arrival: a long light streak collapsing onto the ship along its heading, then a
    /// flash ring expanding around it. Played once for each ship tagged <see cref="HyperspaceArrival"/>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class HyperspaceEffects : MonoBehaviour
    {
        [Tooltip("Unlit transparent material with vertex colours (Sprites/Default works).")]
        public Material Material;

        public Color Color = new Color(0.75f, 0.85f, 1f, 1f);
        [Min(0.1f)] public float Duration = 0.9f;
        [Min(10f)] public float StreakLength = 1500f;
        [Min(1f)] public float RingRadiusInShipRadii = 6f;
        public float WidthPerMetreOfDistance = 0.008f;

        struct Effect
        {
            public LineRenderer Streak;
            public LineRenderer Ring;
            public Vector3 Position;
            public Vector3 Forward;
            public float Radius;
            public float Age;
        }

        const int RingSegments = 64;
        const uint RecentTicks = 20;

        readonly List<Effect> _effects = new();
        readonly HashSet<Entity> _played = new();
        Camera _camera;
        World _world;
        EntityQuery _arrivals;
        EntityQuery _time;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (Material == null)
                Material = new Material(Shader.Find("Sprites/Default"));
        }

        void LateUpdate()
        {
            DetectArrivals();
            Animate();
        }

        void DetectArrivals()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;
            if (world != _world)
            {
                _world = world;
                _arrivals = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<HyperspaceArrival>(),
                                                                  ComponentType.ReadOnly<LocalToWorld>(),
                                                                  ComponentType.ReadOnly<ShipRadius>());
                _time = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SimulationTime>());
                _played.Clear();
            }
            if (_arrivals.IsEmpty || _time.IsEmpty)
                return;

            var tick = _time.GetSingleton<SimulationTime>().Tick;
            using var entities = _arrivals.ToEntityArray(Allocator.Temp);
            using var arrivals = _arrivals.ToComponentDataArray<HyperspaceArrival>(Allocator.Temp);
            using var transforms = _arrivals.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            using var radii = _arrivals.ToComponentDataArray<ShipRadius>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (tick - arrivals[i].Tick > RecentTicks || !_played.Add(entities[i]))
                    continue;
                Play(transforms[i].Position, transforms[i].Forward, radii[i].Value);
            }

            if (_played.Count > 512)
                _played.Clear();
        }

        void Play(Vector3 position, Vector3 forward, float radius)
        {
            var streak = CreateLine("Hyperspace streak", 2, false);
            var ring = CreateLine("Hyperspace ring", RingSegments, true);
            _effects.Add(new Effect
            {
                Streak = streak, Ring = ring, Position = position,
                Forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward,
                Radius = radius, Age = 0f,
            });
        }

        void Animate()
        {
            for (var i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];
                effect.Age += Time.unscaledDeltaTime;
                var t = effect.Age / Duration;
                if (t >= 1f)
                {
                    Destroy(effect.Streak.gameObject);
                    Destroy(effect.Ring.gameObject);
                    _effects.RemoveAt(i);
                    continue;
                }

                var width = Vector3.Distance(_camera.transform.position, effect.Position) * WidthPerMetreOfDistance;

                // Streak: collapses onto the ship during the first 40 % of the effect.
                var collapse = Mathf.Clamp01(t / 0.4f);
                var tail = effect.Position - effect.Forward * StreakLength * (1f - collapse);
                effect.Streak.SetPosition(0, tail);
                effect.Streak.SetPosition(1, effect.Position);
                effect.Streak.widthMultiplier = width * 3f;
                SetColor(effect.Streak, 1f - collapse * 0.5f);

                // Ring: expands and fades once the ship is out of hyperspace.
                var ringT = Mathf.Clamp01((t - 0.3f) / 0.7f);
                var ringRadius = effect.Radius * RingRadiusInShipRadii * Mathf.SmoothStep(0.2f, 1f, ringT);
                for (var s = 0; s < RingSegments; s++)
                {
                    var angle = s * Mathf.PI * 2f / RingSegments;
                    effect.Ring.SetPosition(s, effect.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius);
                }
                effect.Ring.widthMultiplier = width;
                SetColor(effect.Ring, ringT > 0f ? 1f - ringT : 0f);

                _effects[i] = effect;
            }
        }

        void SetColor(LineRenderer line, float alpha)
        {
            var color = Color;
            color.a *= alpha;
            line.startColor = line.endColor = color;
        }

        LineRenderer CreateLine(string name, int points, bool loop)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points;
            line.sharedMaterial = Material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            return line;
        }
    }
}
