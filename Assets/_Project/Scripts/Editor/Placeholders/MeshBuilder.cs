using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Space.Editor.Placeholders
{
    /// <summary>Minimal mesh builder for procedural placeholder geometry.</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _vertices = new();
        readonly List<int> _triangles = new();

        public int TriangleCount => _triangles.Count / 3;

        void Tri(int a, int b, int c)
        {
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
        }

        void Quad(int a, int b, int c, int d)
        {
            Tri(a, b, c);
            Tri(a, c, d);
        }

        /// <summary>Box with its own vertices per face, so normals stay flat.</summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation)
        {
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var i = _vertices.Count;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
                Quad(i, i + 1, i + 2, i + 3);
            }
            Face(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));     // +Z
            Face(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1)); // -Z
            Face(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1));     // +X
            Face(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1)); // -X
            Face(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1));     // +Y
            Face(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1)); // -Y
        }

        /// <summary>Capped cylinder along the given rotation's forward axis.</summary>
        public void AddCylinder(Vector3 center, float radius, float length, int segments, Quaternion rotation)
        {
            segments = Mathf.Max(3, segments);
            var half = length * 0.5f;
            var start = _vertices.Count;
            for (var i = 0; i < segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                var p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                _vertices.Add(center + rotation * (p + Vector3.back * half));
                _vertices.Add(center + rotation * (p + Vector3.forward * half));
            }
            for (var i = 0; i < segments; i++)
            {
                var j = (i + 1) % segments;
                Quad(start + i * 2, start + j * 2, start + j * 2 + 1, start + i * 2 + 1);
            }
            AddCap(start, segments, center + rotation * (Vector3.back * half), true);
            AddCap(start + 1, segments, center + rotation * (Vector3.forward * half), false);
        }

        void AddCap(int firstRingVertex, int segments, Vector3 capCenter, bool back)
        {
            var c = _vertices.Count;
            _vertices.Add(capCenter);
            for (var i = 0; i < segments; i++)
            {
                var a = firstRingVertex + i * 2;
                var b = firstRingVertex + ((i + 1) % segments) * 2;
                if (back) Tri(c, b, a); else Tri(c, a, b);
            }
        }

        /// <summary>
        /// Closed loft: rings[r][s] are points around the hull at successive stations along Z.
        /// Rings must be ordered from back (-Z) to front (+Z) and wound counter-clockwise seen from +Z.
        /// </summary>
        public void AddLoft(Vector3[][] rings)
        {
            var ringCount = rings.Length;
            var sides = rings[0].Length;
            var start = _vertices.Count;
            foreach (var ring in rings)
                _vertices.AddRange(ring);

            for (var r = 0; r < ringCount - 1; r++)
            for (var s = 0; s < sides; s++)
            {
                var n = (s + 1) % sides;
                var a = start + r * sides + s;
                var b = start + r * sides + n;
                var c = start + (r + 1) * sides + n;
                var d = start + (r + 1) * sides + s;
                Quad(a, b, c, d);
            }

            // End caps as fans.
            var backCenter = _vertices.Count;
            _vertices.Add(Average(rings[0]));
            var frontCenter = _vertices.Count;
            _vertices.Add(Average(rings[ringCount - 1]));
            for (var s = 0; s < sides; s++)
            {
                var n = (s + 1) % sides;
                Tri(backCenter, start + n, start + s);
                var f = start + (ringCount - 1) * sides;
                Tri(frontCenter, f + s, f + n);
            }
        }

        static Vector3 Average(Vector3[] points)
        {
            var sum = Vector3.zero;
            foreach (var p in points) sum += p;
            return sum / points.Length;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(_vertices);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.Optimize();
            return mesh;
        }
    }
}
