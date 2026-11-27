using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Specks of dust floating in space around the camera, fixed in the simulation's frame: as the
    /// ships cruise on their fixed course, the dust drifts past and makes the speed readable. Points
    /// are kept in simulation coordinates (double precision) so floating-origin shifts never move them.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SpaceDust : MonoBehaviour
    {
        [Min(16)] public int Count = 700;
        [Tooltip("Radius of the dust volume around the camera, in metres.")]
        [Min(10f)] public float Radius = 450f;
        public Vector2 SizeRange = new(1.2f, 3.5f);
        public Color Color = new(0.7f, 0.8f, 1f, 1f);

        double3[] _points;
        float[] _sizes;
        Matrix4x4[] _matrices;
        Mesh _mesh;
        Material _material;
        Unity.Mathematics.Random _random = new(0x5eed1234u);
        World _world;
        EntityQuery _origin;

        void Awake()
        {
            _mesh = BuildOctahedron();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _material = new Material(shader) { color = Color, enableInstancing = true };
            if (_material.HasProperty("_BaseColor"))
                _material.SetColor("_BaseColor", Color);
            _matrices = new Matrix4x4[Count];
        }

        double3 Origin()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return double3.zero;
            if (world != _world)
            {
                _world = world;
                _origin = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RenderSpace>());
            }
            return _origin.IsEmpty ? double3.zero : _origin.GetSingleton<RenderSpace>().RenderOrigin;
        }

        void LateUpdate()
        {
            var origin = Origin();
            var centre = origin + (double3)(float3)transform.position;

            if (_points == null)
            {
                _points = new double3[Count];
                _sizes = new float[Count];
                for (var i = 0; i < Count; i++)
                {
                    _points[i] = centre + (double3)(_random.NextFloat3Direction() * Radius * math.pow(_random.NextFloat(), 1f / 3f));
                    _sizes[i] = _random.NextFloat(SizeRange.x, SizeRange.y);
                }
            }

            var radiusSq = (double)Radius * Radius;
            for (var i = 0; i < Count; i++)
            {
                // Dust left behind reappears on the far side of the volume.
                var offset = _points[i] - centre;
                if (math.lengthsq(offset) > radiusSq)
                    _points[i] = centre - offset * 0.98;
                var render = (Vector3)(float3)(_points[i] - origin);
                _matrices[i] = Matrix4x4.TRS(render, Quaternion.identity, Vector3.one * _sizes[i]);
            }

            var parameters = new RenderParams(_material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshInstanced(parameters, _mesh, 0, _matrices, Count);
        }

        static Mesh BuildOctahedron()
        {
            var mesh = new Mesh { name = "DustSpeck" };
            mesh.vertices = new[]
            {
                new Vector3(0, 0.5f, 0), new Vector3(0, -0.5f, 0), new Vector3(0.5f, 0, 0),
                new Vector3(-0.5f, 0, 0), new Vector3(0, 0, 0.5f), new Vector3(0, 0, -0.5f),
            };
            mesh.triangles = new[] { 0, 4, 2, 0, 2, 5, 0, 5, 3, 0, 3, 4, 1, 2, 4, 1, 5, 2, 1, 3, 5, 1, 4, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
