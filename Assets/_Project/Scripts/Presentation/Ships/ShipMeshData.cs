using System.Collections.Generic;
using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// Mesh under construction, kept as plain lists so the generator stays testable without a GPU.
    /// Vertex colour carries the paint (rgb) and the emission mask (a). Faces are flat-shaded unless
    /// stated otherwise; <see cref="ComputeOutlineNormals"/> then welds the hard edges into smooth
    /// normals that the toon shader uses to extrude its outline without gaps at the corners.
    /// Front faces follow Unity's convention: cross(b - a, c - a) points out of the surface.
    /// </summary>
    public sealed class ShipMeshData
    {
        public readonly List<Vector3> Positions = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Color> Colors = new();
        public readonly List<int> Indices = new();
        public readonly List<Vector3> OutlineNormals = new();

        public int VertexCount => Positions.Count;
        public int TriangleCount => Indices.Count / 3;

        int Vertex(Vector3 position, Vector3 normal, Color color)
        {
            Positions.Add(position);
            Normals.Add(normal);
            Colors.Add(color);
            return Positions.Count - 1;
        }

        static Vector3 NewellNormal(IReadOnlyList<Vector3> points)
        {
            var n = Vector3.zero;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                n.x += (a.y - b.y) * (a.z + b.z);
                n.y += (a.z - b.z) * (a.x + b.x);
                n.z += (a.x - b.x) * (a.y + b.y);
            }
            return n;
        }

        /// <summary>
        /// Flat convex polygon. The winding is fixed so the face looks toward <paramref name="outward"/>:
        /// callers only say which side is outside, never think about winding.
        /// </summary>
        public void AddFace(IReadOnlyList<Vector3> points, Vector3 outward, Color color)
        {
            if (points.Count < 3)
                return;
            // Newell's sum points like cross(b - a, c - a) for the same order of points.
            var n = NewellNormal(points);
            if (n.sqrMagnitude < 1e-10f)
                return;
            var reverse = Vector3.Dot(n, outward) < 0f;
            if (reverse)
                n = -n;
            n.Normalize();
            var start = Positions.Count;
            for (var i = 0; i < points.Count; i++)
                Vertex(points[reverse ? points.Count - 1 - i : i], n, color);
            for (var i = 1; i < points.Count - 1; i++)
            {
                Indices.Add(start);
                Indices.Add(start + i);
                Indices.Add(start + i + 1);
            }
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color)
        {
            // Skip collapsed corners so a pointed nose does not leave degenerate triangles.
            var points = new List<Vector3>(4) { a };
            foreach (var p in new[] { b, c, d })
                if ((p - points[^1]).sqrMagnitude > 1e-8f)
                    points.Add(p);
            if (points.Count > 3 && (points[^1] - points[0]).sqrMagnitude < 1e-8f)
                points.RemoveAt(points.Count - 1);
            AddFace(points, outward, color);
        }

        /// <summary>
        /// Closed tube through successive rings (same point count each). <paramref name="bandColors"/>
        /// gives one colour per span between two rings; the end caps take the colour of their span.
        /// </summary>
        public void AddLoft(IReadOnlyList<Vector3[]> rings, IReadOnlyList<Color> bandColors, bool capStart = true, bool capEnd = true)
        {
            var count = rings.Count;
            var sides = rings[0].Length;
            var centers = new Vector3[count];
            for (var r = 0; r < count; r++)
                centers[r] = Centroid(rings[r]);

            for (var r = 0; r < count - 1; r++)
            {
                var color = bandColors[Mathf.Min(r, bandColors.Count - 1)];
                var axisMid = (centers[r] + centers[r + 1]) * 0.5f;
                for (var s = 0; s < sides; s++)
                {
                    var n = (s + 1) % sides;
                    var a = rings[r][s];
                    var b = rings[r][n];
                    var c = rings[r + 1][n];
                    var d = rings[r + 1][s];
                    var center = (a + b + c + d) * 0.25f;
                    AddQuad(a, b, c, d, center - axisMid, color);
                }
            }

            if (capStart)
                AddFace(rings[0], centers[0] - centers[1], bandColors[0]);
            if (capEnd)
                AddFace(rings[count - 1], centers[count - 1] - centers[count - 2], bandColors[Mathf.Min(count - 2, bandColors.Count - 1)]);
        }

        /// <summary>
        /// Surface of revolution around the local Z axis of <paramref name="rotation"/>. The profile is a
        /// list of (radius, z) points walked with the outside of the surface on the outward side:
        /// from the back centre outwards, along the outer skin to the front, back in to the front centre.
        /// Each span has its own colour; spans are smooth around the axis and hard between each other.
        /// </summary>
        public void AddLathe(Vector3 origin, Quaternion rotation, IReadOnlyList<Vector2> profile, IReadOnlyList<Color> spanColors, int segments)
        {
            segments = Mathf.Max(3, segments);
            for (var i = 0; i < profile.Count - 1; i++)
            {
                var p0 = profile[i];
                var p1 = profile[i + 1];
                var dr = p1.x - p0.x;
                var dz = p1.y - p0.y;
                if (dr * dr + dz * dz < 1e-10f)
                    continue;
                var color = spanColors[Mathf.Min(i, spanColors.Count - 1)];
                var start = Positions.Count;
                for (var s = 0; s <= segments; s++)
                {
                    var angle = s * Mathf.PI * 2f / segments;
                    var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    // Outward normal of the span: the profile tangent turned a quarter towards the outside.
                    var normal = (radial * dz - Vector3.forward * dr).normalized;
                    var worldNormal = rotation * normal;
                    Vertex(origin + rotation * (radial * p0.x + Vector3.forward * p0.y), worldNormal, color);
                    Vertex(origin + rotation * (radial * p1.x + Vector3.forward * p1.y), worldNormal, color);
                }
                for (var s = 0; s < segments; s++)
                {
                    var a = start + s * 2;      // p0, angle s
                    var b = start + s * 2 + 2;  // p0, angle s + 1
                    var c = start + s * 2 + 3;  // p1, angle s + 1
                    var d = start + s * 2 + 1;  // p1, angle s
                    AddOrientedTriangle(a, b, c);
                    AddOrientedTriangle(a, c, d);
                }
            }
        }

        /// <summary>Adds a triangle wound so it faces along the average normal of its vertices.</summary>
        void AddOrientedTriangle(int a, int b, int c)
        {
            var pa = Positions[a];
            var face = Vector3.Cross(Positions[b] - pa, Positions[c] - pa);
            if (face.sqrMagnitude < 1e-12f)
                return;
            var n = Normals[a] + Normals[b] + Normals[c];
            Indices.Add(a);
            if (Vector3.Dot(face, n) >= 0f)
            {
                Indices.Add(b);
                Indices.Add(c);
            }
            else
            {
                Indices.Add(c);
                Indices.Add(b);
            }
        }

        /// <summary>Box with chamfered edges (all twelve), in the frame given by center and rotation.</summary>
        public void AddChamferBox(Vector3 center, Quaternion rotation, Vector3 size, float chamfer, Color color, Color? frontColor = null)
        {
            var h = size * 0.5f;
            var c = Mathf.Min(chamfer, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
            Vector3[] Ring(float z, float inset) => Octagon(h.x - inset, h.y - inset, c, c, z);
            var rings = new List<Vector3[]>
            {
                Ring(-h.z, c), Ring(-h.z + c, 0f), Ring(h.z - c, 0f), Ring(h.z, c),
            };
            Transform(rings, center, rotation);
            var front = frontColor ?? color;
            AddLoft(rings, new[] { color, color, front });
        }

        /// <summary>
        /// Octagonal section at depth z: half width w, half height h, with separate top and bottom
        /// chamfers. Points go counter-clockwise seen from +Z; <paramref name="topScale"/> narrows the
        /// top to give sloped sides.
        /// </summary>
        public static Vector3[] Octagon(float w, float h, float chamferTop, float chamferBottom, float z, float y = 0f, float topScale = 1f)
        {
            var ct = Mathf.Clamp(chamferTop, 0f, Mathf.Min(w, h) * 0.95f);
            var cb = Mathf.Clamp(chamferBottom, 0f, Mathf.Min(w, h) * 0.95f);
            var wt = w * topScale;
            return new[]
            {
                new Vector3(w, y - h + cb, z),
                new Vector3(wt, y + h - ct, z),
                new Vector3(wt - ct, y + h, z),
                new Vector3(-wt + ct, y + h, z),
                new Vector3(-wt, y + h - ct, z),
                new Vector3(-w, y - h + cb, z),
                new Vector3(-w + cb, y - h, z),
                new Vector3(w - cb, y - h, z),
            };
        }

        public static void Transform(List<Vector3[]> rings, Vector3 origin, Quaternion rotation)
        {
            foreach (var ring in rings)
                for (var i = 0; i < ring.Length; i++)
                    ring[i] = origin + rotation * ring[i];
        }

        /// <summary>Copies the vertices added since <paramref name="start"/>, mirrored across the X = 0 plane.</summary>
        public void MirrorX(int startVertex, int startIndex)
        {
            var vertexEnd = Positions.Count;
            var indexEnd = Indices.Count;
            var offset = vertexEnd - startVertex;
            for (var i = startVertex; i < vertexEnd; i++)
            {
                var p = Positions[i];
                var n = Normals[i];
                Vertex(new Vector3(-p.x, p.y, p.z), new Vector3(-n.x, n.y, n.z), Colors[i]);
            }
            // Mirroring flips the handedness: reverse each triangle to keep it facing out.
            for (var i = startIndex; i < indexEnd; i += 3)
            {
                Indices.Add(Indices[i] + offset);
                Indices.Add(Indices[i + 2] + offset);
                Indices.Add(Indices[i + 1] + offset);
            }
        }

        public (int vertex, int index) Mark() => (Positions.Count, Indices.Count);

        public void MirrorSince((int vertex, int index) mark) => MirrorX(mark.vertex, mark.index);

        /// <summary>
        /// Smooth normals for the outline pass: every vertex sharing a position (to the millimetre)
        /// gets the normalised sum of the distinct normals found there.
        /// </summary>
        public void ComputeOutlineNormals()
        {
            var sums = new Dictionary<(int, int, int), Vector3>();
            var seen = new Dictionary<(int, int, int), List<Vector3>>();
            var keys = new (int, int, int)[Positions.Count];
            for (var i = 0; i < Positions.Count; i++)
            {
                var p = Positions[i];
                var key = (Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));
                keys[i] = key;
                if (!seen.TryGetValue(key, out var list))
                {
                    list = new List<Vector3>(4);
                    seen[key] = list;
                    sums[key] = Vector3.zero;
                }
                var n = Normals[i];
                var duplicate = false;
                foreach (var other in list)
                    if (Vector3.Dot(other, n) > 0.999f) { duplicate = true; break; }
                if (duplicate)
                    continue;
                list.Add(n);
                sums[key] += n;
            }

            OutlineNormals.Clear();
            for (var i = 0; i < Positions.Count; i++)
            {
                var sum = sums[keys[i]];
                OutlineNormals.Add(sum.sqrMagnitude > 1e-8f ? sum.normalized : Normals[i]);
            }
        }

        public Bounds ComputeBounds()
        {
            if (Positions.Count == 0)
                return new Bounds();
            var min = Positions[0];
            var max = Positions[0];
            foreach (var p in Positions)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        static Vector3 Centroid(Vector3[] points)
        {
            var sum = Vector3.zero;
            foreach (var p in points)
                sum += p;
            return sum / points.Length;
        }
    }
}
