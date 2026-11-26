using System.Collections.Generic;
using Space.Core.Combat;
using Space.Core.Time;
using Space.Presentation.Controls;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Impacts: an expanding burst sized by the damage dealt (orange on the hull, blue on a shield),
    /// and a camera shake when one of the player's ships takes hull damage. Each hit should be felt.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ImpactEffects : MonoBehaviour
    {
        [Tooltip("Unlit transparent material with vertex colours (Sprites/Default works).")]
        public Material Material;

        public Color HullColor = new Color(1f, 0.55f, 0.15f, 1f);
        public Color ShieldColor = new Color(0.45f, 0.7f, 1f, 1f);
        [Min(0.1f)] public float Duration = 0.8f;

        [Tooltip("Camera shake in metres per point of hull damage taken by a player ship.")]
        public float ShakePerDamage = 0.05f;
        public float WidthPerMetreOfDistance = 0.004f;

        struct Burst
        {
            public LineRenderer Outer;
            public LineRenderer Inner;
            public Vector3 Position;
            public float Radius;
            public float Age;
            public Color Color;
        }

        const int Segments = 48;

        readonly List<Burst> _bursts = new();
        Camera _camera;
        ShipCameraRig _rig;
        World _world;
        EntityQuery _queue;
        EntityQuery _origin;
        EntityQuery _time;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _rig = GetComponent<ShipCameraRig>();
            if (Material == null)
                Material = new Material(Shader.Find("Sprites/Default"));
        }

        void LateUpdate()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;
            if (world != _world)
            {
                _world = world;
                var em = world.EntityManager;
                _queue = em.CreateEntityQuery(ComponentType.ReadOnly<ShotEventQueue>());
                _origin = em.CreateEntityQuery(ComponentType.ReadOnly<FloatingOrigin>());
                _time = em.CreateEntityQuery(ComponentType.ReadOnly<SimulationTime>());
            }

            ReadImpacts();
            Animate();
        }

        void ReadImpacts()
        {
            if (_queue.IsEmpty)
                return;

            var origin = _origin.IsEmpty ? double3.zero : _origin.GetSingleton<FloatingOrigin>().Origin;
            var impacts = _world.EntityManager.GetBuffer<ImpactEvent>(_queue.GetSingletonEntity());
            foreach (var impact in impacts)
            {
                var position = (Vector3)(float3)(impact.Position - origin);
                var onHull = impact.HullDamage > impact.ShieldDamage * 0.5f || !impact.ShieldWasUp;
                var damage = impact.HullDamage + impact.ShieldDamage;
                var radius = impact.TargetRadius * 0.6f + damage * 0.25f;
                Play(position, radius, onHull ? HullColor : ShieldColor);

                if (impact.TargetTeam == Team.Player && impact.HullDamage > 0f && _rig != null)
                    _rig.Shake(impact.HullDamage * ShakePerDamage);
            }
            impacts.Clear();
        }

        void Play(Vector3 position, float radius, Color color)
        {
            _bursts.Add(new Burst
            {
                Outer = CreateRing(), Inner = CreateRing(),
                Position = position, Radius = radius, Color = color,
            });
        }

        void Animate()
        {
            // Bursts follow simulated time: they freeze on pause like everything else.
            var dt = Time.deltaTime;
            if (!_time.IsEmpty)
            {
                var time = _time.GetSingleton<SimulationTime>();
                dt = time.Paused ? 0f : dt * Mathf.Min(time.TimeScale, 2f);
            }

            var cameraPosition = _camera.transform.position;
            for (var i = _bursts.Count - 1; i >= 0; i--)
            {
                var burst = _bursts[i];
                burst.Age += dt;
                var t = burst.Age / Duration;
                if (t >= 1f)
                {
                    Destroy(burst.Outer.gameObject);
                    Destroy(burst.Inner.gameObject);
                    _bursts.RemoveAt(i);
                    continue;
                }

                var width = Vector3.Distance(cameraPosition, burst.Position) * WidthPerMetreOfDistance;
                var ease = 1f - (1f - t) * (1f - t);
                SetRing(burst.Outer, burst.Position, burst.Radius * Mathf.Lerp(0.2f, 1f, ease), width * 1.5f, burst.Color, 1f - t);
                SetRing(burst.Inner, burst.Position, burst.Radius * Mathf.Lerp(0.05f, 0.45f, ease), width * 3f,
                        Color.Lerp(Color.white, burst.Color, t), Mathf.Clamp01(1f - t * 2f));
                _bursts[i] = burst;
            }
        }

        static void SetRing(LineRenderer ring, Vector3 center, float radius, float width, Color color, float alpha)
        {
            for (var s = 0; s < Segments; s++)
            {
                var angle = s * Mathf.PI * 2f / Segments;
                ring.SetPosition(s, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
            ring.widthMultiplier = width;
            color.a *= alpha;
            ring.startColor = ring.endColor = color;
        }

        LineRenderer CreateRing()
        {
            var go = new GameObject("Impact");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.sharedMaterial = Material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            return line;
        }
    }
}
