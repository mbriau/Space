using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// The space behind the battle: a nebula and star fields drawn behind everything, filling the view.
    /// Each layer slides opposite to the camera's travel in the simulation (near star fields faster
    /// than far ones, the nebula barely: parallax), drifts slowly on its own, and turns with the
    /// camera like a distant sky. Travel is measured in simulation coordinates, so floating-origin
    /// shifts never make it jump.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SpaceBackdrop : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            public string Name = "Layer";
            public Texture Texture;
            public Color Tint = Color.white;

            [Tooltip("Texture tiles across the view (more = smaller details).")]
            public float Tiling = 1f;

            [Tooltip("Metres of camera travel to slide one tile: small = near, fast layer; 0 = fixed.")]
            public float MetresPerTile;

            [Tooltip("Slow drift of its own, in tiles per second.")]
            public Vector2 Drift;

            [Tooltip("Adds light (stars) instead of covering what is behind (nebula).")]
            public bool Additive;

            [NonSerialized] public Vector2 Offset;
            [NonSerialized] public Material Material;
            [NonSerialized] public Transform Quad;
        }

        public List<Layer> Layers = new();

        [Tooltip("Distance of the backdrop in front of the camera, as a share of the far clip plane.")]
        [Range(0.1f, 0.95f)] public float Depth = 0.8f;

        Camera _camera;
        Mesh _quad;
        bool _hasLast;
        double3 _lastPosition;
        Vector3 _lastEuler;
        World _world;
        EntityQuery _origin;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _quad = BuildQuad();
            var shader = Shader.Find("Space/BackdropLayer");
            for (var i = 0; i < Layers.Count; i++)
            {
                var layer = Layers[i];
                layer.Material = new Material(shader) { name = $"Backdrop {layer.Name}", renderQueue = 1000 + i };
                layer.Material.SetTexture("_MainTex", layer.Texture);
                layer.Material.SetColor("_Color", layer.Tint);
                layer.Material.SetFloat("_SrcBlend", (float)(layer.Additive ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.One));
                layer.Material.SetFloat("_DstBlend", (float)(layer.Additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.Zero));

                var go = new GameObject($"Backdrop {layer.Name}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = _quad;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = layer.Material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                layer.Quad = go.transform;
                // Each layer starts at its own place in its texture.
                layer.Offset = new Vector2(0.37f * i, 0.61f * i);
            }
        }

        double3 SimulationPosition()
        {
            var origin = double3.zero;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                if (world != _world)
                {
                    _world = world;
                    _origin = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FloatingOrigin>());
                }
                if (!_origin.IsEmpty)
                    origin = _origin.GetSingleton<FloatingOrigin>().Origin;
            }
            return origin + (double3)(float3)transform.position;
        }

        void LateUpdate()
        {
            var position = SimulationPosition();
            var euler = transform.eulerAngles;
            var travel = _hasLast ? (Vector3)(float3)(position - _lastPosition) : Vector3.zero;
            // A jump (level change, teleport) is not a flight: no scroll for it.
            if (travel.sqrMagnitude > 2000f * 2000f)
                travel = Vector3.zero;
            var yaw = _hasLast ? Mathf.DeltaAngle(_lastEuler.y, euler.y) : 0f;
            var pitch = _hasLast ? Mathf.DeltaAngle(_lastEuler.x, euler.x) : 0f;
            _lastPosition = position;
            _lastEuler = euler;
            _hasLast = true;

            // The quads fill the view at the backdrop's distance, a little larger to hide the edges.
            var distance = _camera.farClipPlane * Depth;
            var height = 2f * distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            var width = height * _camera.aspect;
            var horizontalFov = BackdropMath.HorizontalFov(_camera.fieldOfView, _camera.aspect);
            var right = transform.right;
            var up = transform.up;

            foreach (var layer in Layers)
            {
                if (layer.Quad == null)
                    continue;
                layer.Quad.localPosition = new Vector3(0f, 0f, distance);
                layer.Quad.localRotation = Quaternion.identity;
                layer.Quad.localScale = new Vector3(width, height, 1f);

                // Square texels: the tiling follows the screen's aspect.
                var tiles = new Vector2(layer.Tiling * _camera.aspect, layer.Tiling);
                layer.Offset += BackdropMath.TravelScroll(travel, right, up, layer.MetresPerTile)
                                + BackdropMath.TurnScroll(yaw, pitch, horizontalFov, _camera.fieldOfView, tiles)
                                + layer.Drift * Time.deltaTime;
                layer.Offset = BackdropMath.Wrap(layer.Offset);
                layer.Material.SetVector("_MainTex_ST", new Vector4(tiles.x, tiles.y, layer.Offset.x, layer.Offset.y));
            }
        }

        static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "BackdropQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 1f));
            return mesh;
        }

        void OnDestroy()
        {
            if (_quad != null)
                Destroy(_quad);
            foreach (var layer in Layers)
                if (layer.Material != null)
                    Destroy(layer.Material);
        }
    }
}
