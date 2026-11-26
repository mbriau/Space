using System.Collections.Generic;
using Space.Core.Combat;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Draws the shots of the last simulation ticks as short-lived tracer lines, then clears the
    /// <see cref="ShotEvent"/> queue. Hits are bright, misses faint.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TracerRenderer : MonoBehaviour
    {
        [Tooltip("Unlit transparent material with vertex colours (Sprites/Default works).")]
        public Material Material;

        public Color PlayerColor = new Color(0.45f, 0.9f, 1f, 1f);
        public Color EnemyColor = new Color(1f, 0.45f, 0.2f, 1f);
        [Range(0f, 1f)] public float MissAlpha = 0.35f;
        [Min(0.01f)] public float Lifetime = 0.15f;
        public float WidthPerMetreOfDistance = 0.002f;
        [Min(1)] public int MaxTracers = 128;

        struct Tracer
        {
            public LineRenderer Line;
            public float Age;
            public Color Color;
        }

        readonly List<Tracer> _active = new();
        readonly Stack<LineRenderer> _pool = new();
        Camera _camera;
        Transform _root;
        World _world;
        EntityQuery _shots;
        EntityQuery _origin;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (Material == null)
                Material = new Material(Shader.Find("Sprites/Default"));
            _root = new GameObject("Tracers").transform;
        }

        void OnDestroy()
        {
            if (_root != null)
                Destroy(_root.gameObject);
        }

        void LateUpdate()
        {
            Age();
            ReadShots();
        }

        void Age()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var tracer = _active[i];
                tracer.Age += Time.unscaledDeltaTime;
                if (tracer.Age >= Lifetime)
                {
                    tracer.Line.gameObject.SetActive(false);
                    _pool.Push(tracer.Line);
                    _active.RemoveAt(i);
                    continue;
                }

                var color = tracer.Color;
                color.a *= 1f - tracer.Age / Lifetime;
                tracer.Line.startColor = tracer.Line.endColor = color;
                _active[i] = tracer;
            }
        }

        void ReadShots()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;
            if (world != _world)
            {
                _world = world;
                _shots = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ShotEventQueue>());
                _origin = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FloatingOrigin>());
            }
            if (_shots.IsEmpty)
                return;

            var origin = _origin.IsEmpty ? double3.zero : _origin.GetSingleton<FloatingOrigin>().Origin;
            var buffer = world.EntityManager.GetBuffer<ShotEvent>(_shots.GetSingletonEntity());
            var cameraPosition = _camera.transform.position;

            foreach (var shot in buffer)
            {
                if (_active.Count >= MaxTracers)
                    break;

                var from = (Vector3)(float3)(shot.From - origin);
                var to = (Vector3)(float3)(shot.To - origin);
                var line = _pool.Count > 0 ? _pool.Pop() : CreateLine();
                line.gameObject.SetActive(true);
                line.SetPosition(0, from);
                line.SetPosition(1, to);
                line.widthMultiplier = Mathf.Max(0.3f, Vector3.Distance(cameraPosition, (from + to) * 0.5f) * WidthPerMetreOfDistance);

                var color = shot.Team == Team.Player ? PlayerColor : EnemyColor;
                if (!shot.Hit)
                    color.a *= MissAlpha;
                line.startColor = line.endColor = color;
                _active.Add(new Tracer { Line = line, Age = 0f, Color = color });
            }
            buffer.Clear();
        }

        LineRenderer CreateLine()
        {
            var go = new GameObject("Tracer");
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
