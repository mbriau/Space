using System.Collections.Generic;
using Space.Core.Combat;
using Space.Core.Time;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Projectiles in flight: each shot travels from the gun to its impact point in simulated time
    /// (it freezes on pause and speeds up with x2 / x4). Shells are short and heavy, energy bolts
    /// long and bright; misses fly on past the target and fade.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TracerRenderer : MonoBehaviour
    {
        [Tooltip("Unlit transparent material with vertex colours (Sprites/Default works).")]
        public Material Material;

        [Tooltip("Lasers: colour of the shooter's side.")]
        public Color PlayerColor = new Color(0.45f, 1f, 0.6f, 1f);
        public Color EnemyColor = new Color(1f, 0.3f, 0.25f, 1f);
        public Color IonColor = new Color(0.55f, 0.55f, 1f, 1f);
        public Color KineticColor = new Color(1f, 0.85f, 0.45f, 1f);
        public Color MissileColor = new Color(1f, 0.6f, 0.15f, 1f);
        [Range(0f, 1f)] public float MissAlpha = 0.5f;
        public float WidthPerMetreOfDistance = 0.003f;
        [Min(1)] public int MaxProjectiles = 128;

        struct Projectile
        {
            public LineRenderer Line;
            public Vector3 From;
            public Vector3 To;
            public float Flight;
            public float Age;
            public float Length;
            public float WidthFactor;
            public bool Hit;
            public Color Color;
        }

        /// <summary>Simulated seconds a miss keeps flying past its end point while fading.</summary>
        const float MissOvershoot = 0.6f;

        readonly List<Projectile> _active = new();
        readonly Stack<LineRenderer> _pool = new();
        Camera _camera;
        Transform _root;
        World _world;
        EntityQuery _shots;
        EntityQuery _origin;
        EntityQuery _time;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (Material == null)
                Material = new Material(Shader.Find("Sprites/Default"));
            _root = new GameObject("Projectiles").transform;
        }

        void OnDestroy()
        {
            if (_root != null)
                Destroy(_root.gameObject);
        }

        void LateUpdate()
        {
            if (!EnsureWorld())
                return;
            ReadShots();
            Animate(SimulatedDeltaTime());
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
                _shots = em.CreateEntityQuery(ComponentType.ReadOnly<ShotEventQueue>());
                _origin = em.CreateEntityQuery(ComponentType.ReadOnly<RenderSpace>());
                _time = em.CreateEntityQuery(ComponentType.ReadOnly<SimulationTime>());
            }
            return true;
        }

        float SimulatedDeltaTime()
        {
            if (_time.IsEmpty)
                return Time.deltaTime;
            var time = _time.GetSingleton<SimulationTime>();
            return time.Paused ? 0f : Time.deltaTime * time.TimeScale;
        }

        void ReadShots()
        {
            if (_shots.IsEmpty)
                return;

            var space = _origin.IsEmpty ? RenderSpace.Flat(double3.zero) : _origin.GetSingleton<RenderSpace>();
            var buffer = _world.EntityManager.GetBuffer<ShotEvent>(_shots.GetSingletonEntity());
            foreach (var shot in buffer)
            {
                if (_active.Count >= MaxProjectiles)
                    break;

                var line = _pool.Count > 0 ? _pool.Pop() : CreateLine();
                line.gameObject.SetActive(true);

                var (color, length, widthFactor) = shot.Type switch
                {
                    DamageType.Ion => (IonColor, 60f, 1f),
                    DamageType.Laser => (shot.Team == Team.Player ? PlayerColor : EnemyColor, 60f, 1f),
                    DamageType.Missile => (MissileColor, 14f, 3f),
                    _ => (KineticColor, 10f, 2.5f),
                };
                if (!shot.Hit)
                    color.a *= MissAlpha;

                // Projectiles leave from the turret's muzzle, which recoils.
                var from = (Vector3)space.ToRender(shot.From);
                var turrets = TurretPresenter.Instance;
                if (turrets != null)
                {
                    if (turrets.TryGetMuzzle(shot.Shooter, shot.WeaponIndex, out var muzzle))
                        from = muzzle;
                    turrets.NotifyFired(shot.Shooter, shot.WeaponIndex);
                }

                _active.Add(new Projectile
                {
                    Line = line,
                    From = from,
                    To = (Vector3)space.ToRender(shot.To),
                    Flight = Mathf.Max(0.05f, shot.FlightSeconds),
                    Length = length,
                    WidthFactor = widthFactor,
                    Hit = shot.Hit,
                    Color = color,
                });
            }
            buffer.Clear();
        }

        void Animate(float dt)
        {
            var cameraPosition = _camera.transform.position;
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                p.Age += dt;

                var end = p.Hit ? p.Flight : p.Flight + MissOvershoot;
                if (p.Age >= end)
                {
                    p.Line.gameObject.SetActive(false);
                    _pool.Push(p.Line);
                    _active.RemoveAt(i);
                    continue;
                }

                var direction = p.To - p.From;
                var distance = direction.magnitude;
                direction = distance > 1e-3f ? direction / distance : Vector3.forward;
                var travelled = distance * (p.Age / p.Flight);
                var head = p.From + direction * travelled;
                var tail = p.From + direction * Mathf.Max(0f, travelled - p.Length);

                p.Line.SetPosition(0, tail);
                p.Line.SetPosition(1, head);
                p.Line.widthMultiplier = Mathf.Max(0.3f, Vector3.Distance(cameraPosition, head) * WidthPerMetreOfDistance) * p.WidthFactor;

                var color = p.Color;
                if (p.Age > p.Flight)
                    color.a *= 1f - (p.Age - p.Flight) / MissOvershoot;
                p.Line.startColor = new Color(color.r, color.g, color.b, color.a * 0.2f);
                p.Line.endColor = color;
                _active[i] = p;
            }
        }

        LineRenderer CreateLine()
        {
            var go = new GameObject("Projectile");
            go.transform.SetParent(_root, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = Material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            return line;
        }
    }
}
