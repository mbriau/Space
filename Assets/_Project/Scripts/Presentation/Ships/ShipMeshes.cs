using UnityEngine;
using UnityEngine.Rendering;

namespace Space.Presentation.Ships
{
    /// <summary>Turns generated geometry into Unity meshes and holds the shared materials of the hangar.</summary>
    public static class ShipMeshes
    {
        /// <summary>Paint in the colours, emission mask in their alpha, outline normals in UV channel 3.</summary>
        public static Mesh ToMesh(ShipMeshData data, string name)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = data.VertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(data.Positions);
            mesh.SetNormals(data.Normals);
            mesh.SetColors(data.Colors);
            if (data.OutlineNormals.Count == data.VertexCount)
                mesh.SetUVs(3, data.OutlineNormals);
            mesh.SetTriangles(data.Indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh _quad;

        /// <summary>Unit quad centred on its pivot, for the camera-facing halos.</summary>
        public static Mesh Quad
        {
            get
            {
                if (_quad != null)
                    return _quad;
                _quad = new Mesh { name = "HaloQuad" };
                _quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
                _quad.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
                _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                // The shader moves the vertices to face the camera: keep generous bounds.
                _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
                return _quad;
            }
        }

        static Material _toon, _halo, _line;

        public static Material Toon => _toon != null ? _toon : _toon = Create("Space/ToonShip", "ShipToon");
        public static Material Halo => _halo != null ? _halo : _halo = Create("Space/EngineHalo", "EngineHalo");
        public static Material Line => _line != null ? _line : _line = Create("Space/AdditiveLine", "AdditiveLine");

        static Material Create(string shaderName, string materialName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader {shaderName} introuvable : vérifier Assets/_Project/Art/Shaders/Resources.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return new Material(shader) { name = materialName, enableInstancing = true };
        }
    }
}
