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
    /// Impacts: on a shield, an electric splash on its surface where the shot struck; on the hull, an
    /// expanding burst sized by the damage dealt; a camera shake when one of the player's ships takes
    /// hull damage. Each hit should be felt.
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
                _origin = em.CreateEntityQuery(ComponentType.ReadOnly<RenderSpace>());
                _time = em.CreateEntityQuery(ComponentType.ReadOnly<SimulationTime>());
            }

            ReadImpacts();
            Animate();
        }

        void ReadImpacts()
        {
            if (_queue.IsEmpty)
                return;

            var space = _origin.IsEmpty ? RenderSpace.Flat(double3.zero) : _origin.GetSingleton<RenderSpace>();
            var impacts = _world.EntityManager.GetBuffer<ImpactEvent>(_queue.GetSingletonEntity());
            foreach (var impact in impacts)
            {
                var position = (Vector3)space.ToRender(impact.Position);
                var onHull = impact.HullDamage > impact.ShieldDamage * 0.5f || !impact.ShieldWasUp;
                var damage = impact.HullDamage + impact.ShieldDamage;
                var radius = impact.TargetRadius * 0.6f + damage * 0.25f;
                if (impact.ShieldWasUp && impact.ShieldDamage > 0f)
                    PlayShieldArc(impact, space, damage);
                else
                    Play(position, radius, onHull ? HullColor : ShieldColor);

                if (impact.TargetTeam == Team.Player && impact.HullDamage > 0f && _rig != null)
                    _rig.Shake(impact.HullDamage * ShakePerDamage);
            }
            impacts.Clear();
        }

        [Header("Impacts sur bouclier")]
        [Tooltip("Lifetime of the electric splash where a shot strikes a shield, in seconds.")]
        [Min(0.05f)] public float ArcDuration = 0.55f;

        [Tooltip("Size of the splash as a share of the ship's radius (grows a little with the damage).")]
        public float ArcSize = 0.9f;

        struct Arc
        {
            public Transform Quad;
            public Material Material;
            public float Age;
        }

        readonly List<Arc> _arcs = new();
        Mesh _arcQuad;
        Shader _arcShader;
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int ReachId = Shader.PropertyToID("_Reach");

        /// <summary>
        /// A shot strikes a shield: an electric splash on the shield's surface, on the side the shot
        /// came from, following the ship.
        /// </summary>
        void PlayShieldArc(in ImpactEvent impact, in RenderSpace space, float damage)
        {
            var from = (Vector3)space.ToRender(impact.From);
            var presenter = TurretPresenter.Instance;
            Vector3 point, normal;
            float radius;
            Transform anchor;
            if (presenter == null || !presenter.TryGetShieldHit(impact.Target, from, out point, out normal, out radius, out anchor))
            {
                var center = (Vector3)space.ToRender(impact.Position);
                normal = (from - center).normalized;
                radius = impact.TargetRadius;
                point = center + normal * radius;
                anchor = null;
            }

            _arcShader ??= Shader.Find("Space/ElectricArc");
            if (_arcShader == null)
                return;
            _arcQuad ??= BuildQuad();

            var go = new GameObject("ShieldArc", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetPositionAndRotation(point + normal * Mathf.Max(1f, radius * 0.06f),Quaternion.LookRotation(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up));
            go.transform.localScale = Vector3.one * radius * ArcSize * (1f + Mathf.Clamp01(damage / 120f) * 0.6f);
            if (anchor != null)
                go.transform.SetParent(anchor, true);
            go.GetComponent<MeshFilter>().sharedMesh = _arcQuad;
            var material = new Material(_arcShader) { name = "ShieldArc" };
            material.SetColor("_Color", ShieldColor);
            material.SetFloat("_Seed", UnityEngine.Random.Range(0f, 100f));
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _arcs.Add(new Arc { Quad = go.transform, Material = material });
        }

        void AnimateArcs(float dt)
        {
            for (var i = _arcs.Count - 1; i >= 0; i--)
            {
                var arc = _arcs[i];
                arc.Age += dt;
                var t = arc.Age / ArcDuration;
                if (t >= 1f || arc.Quad == null)
                {
                    if (arc.Quad != null)
                        Destroy(arc.Quad.gameObject);
                    Destroy(arc.Material);
                    _arcs.RemoveAt(i);
                    continue;
                }
                // The arcs shoot out at once, then crackle and fade.
                arc.Material.SetFloat(ReachId, Mathf.Clamp01(t * 6f));
                arc.Material.SetFloat(FadeId, 1f - t * t);
                _arcs[i] = arc;
            }
        }

        static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "ShieldArcQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
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

            AnimateArcs(dt);
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
