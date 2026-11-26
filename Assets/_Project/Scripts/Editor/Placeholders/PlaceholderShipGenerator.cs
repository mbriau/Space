using System.Collections.Generic;
using System.IO;
using Space.Authoring;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace Space.Editor.Placeholders
{
    /// <summary>
    /// Generates placeholder ship prefabs: a lofted hull, engines and surface "greebles".
    /// Not meant to look good, but to have realistic triangle counts, draw setup and LODs
    /// so performance measurements are meaningful from the start.
    /// Menu: Space > Generate Placeholder Ships.
    /// </summary>
    public static class PlaceholderShipGenerator
    {
        const string Root = "Assets/_Project/Art/Placeholder";

        public readonly struct ShipClass
        {
            public readonly string Name;
            public readonly float Length, Width, Height;
            /// <summary>Superellipse exponent of the hull section: 2 = ellipse, higher = boxier.</summary>
            public readonly float Boxiness;
            public readonly int TriangleBudget;
            public readonly int Engines;
            public readonly Color Color;
            public readonly float MaxSpeed, Acceleration, TurnRate;

            public ShipClass(string name, float length, float width, float height, float boxiness,
                int triangleBudget, int engines, Color color, float maxSpeed, float acceleration, float turnRate)
            {
                Name = name; Length = length; Width = width; Height = height; Boxiness = boxiness;
                TriangleBudget = triangleBudget; Engines = engines; Color = color;
                MaxSpeed = maxSpeed; Acceleration = acceleration; TurnRate = turnRate;
            }
        }

        public static readonly ShipClass[] Classes =
        {
            new("Drone",         4f,   1.2f, 0.6f, 2.0f,  1_500, 1, new Color(0.60f, 0.80f, 0.90f), 120f, 40f, 180f),
            new("Cargo",        40f,  10f,   8f,   4.0f, 12_000, 2, new Color(0.85f, 0.70f, 0.30f),  60f,  6f,  30f),
            new("PirateFighter",12f,   6f,   2f,   2.2f,  4_000, 2, new Color(0.80f, 0.25f, 0.20f), 140f, 35f, 120f),
            new("Corvette",     30f,   6f,   4f,   2.5f, 10_000, 2, new Color(0.40f, 0.60f, 0.40f), 100f, 18f,  70f),
            new("Frigate",      80f,  14f,  10f,   3.0f, 25_000, 3, new Color(0.50f, 0.55f, 0.70f),  70f,  8f,  35f),
            new("Capital",     300f,  50f,  35f,   3.5f, 80_000, 6, new Color(0.55f, 0.55f, 0.60f),  35f,  2f,  10f),
        };

        /// <summary>Share of the triangle budget kept by each LOD, and the screen height at which it switches.</summary>
        static readonly float[] LodBudgetFactors = { 1f, 0.35f, 0.08f };
        static readonly float[] LodScreenHeights = { 0.30f, 0.08f, 0.01f };

        [MenuItem("Space/Generate Placeholder Ships")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Meshes"));
            Directory.CreateDirectory(Path.Combine(Root, "Materials"));
            Directory.CreateDirectory(Path.Combine(Root, "Prefabs"));
            AssetDatabase.Refresh();

            var report = new System.Text.StringBuilder("Placeholder ships generated:\n");
            for (var i = 0; i < Classes.Length; i++)
            {
                var tris = Generate(Classes[i], seed: 1000 + i);
                report.AppendLine($"  {Classes[i].Name}: LOD0 {tris[0]} / LOD1 {tris[1]} / LOD2 {tris[2]} triangles");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        static int[] Generate(ShipClass shipClass, int seed)
        {
            var material = GetOrCreateMaterial(shipClass);
            var greebles = PlanGreebles(shipClass, seed);
            var triangleCounts = new int[LodBudgetFactors.Length];
            var meshes = new Mesh[LodBudgetFactors.Length];

            for (var lod = 0; lod < LodBudgetFactors.Length; lod++)
            {
                var builder = BuildLod(shipClass, greebles, LodBudgetFactors[lod]);
                triangleCounts[lod] = builder.TriangleCount;
                meshes[lod] = SaveMesh(builder.ToMesh($"{shipClass.Name}_LOD{lod}"));
            }

            var root = new GameObject(shipClass.Name);
            try
            {
                var lods = new LOD[meshes.Length];
                for (var lod = 0; lod < meshes.Length; lod++)
                {
                    var child = new GameObject($"LOD{lod}");
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = meshes[lod];
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    lods[lod] = new LOD(LodScreenHeights[lod], new Renderer[] { renderer });
                }

                var lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();

                var ship = root.AddComponent<ShipAuthoring>();
                ship.MaxSpeed = shipClass.MaxSpeed;
                ship.Acceleration = shipClass.Acceleration;
                ship.TurnRateDegrees = shipClass.TurnRate;

                PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Prefabs/{shipClass.Name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return triangleCounts;
        }

        struct Greeble
        {
            public float T;      // position along the hull, 0 = back, 1 = front
            public float Angle;  // position around the hull
            public Vector3 Size;
            public float Yaw;
        }

        /// <summary>
        /// Greebles are planned once per ship, sorted largest first, so lower LODs keep the
        /// biggest ones and the silhouette stays stable between LODs.
        /// </summary>
        static List<Greeble> PlanGreebles(ShipClass c, int seed)
        {
            var random = new Random(seed);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            // Upper bound: the whole LOD0 budget in boxes of 12 triangles.
            var count = c.TriangleBudget / 12;
            var scale = Mathf.Min(c.Width, c.Height);
            var list = new List<Greeble>(count);
            for (var i = 0; i < count; i++)
            {
                var size = scale * Range(0.02f, 0.12f) * Mathf.Pow((float)random.NextDouble(), 2f) + scale * 0.01f;
                list.Add(new Greeble
                {
                    T = Range(0.08f, 0.92f),
                    Angle = Range(0f, Mathf.PI * 2f),
                    Size = new Vector3(size * Range(0.5f, 2f), size * Range(0.3f, 1f), size * Range(0.5f, 3f)),
                    Yaw = random.NextDouble() < 0.7 ? 0f : Range(0f, 90f),
                });
            }
            list.Sort((a, b) => b.Size.sqrMagnitude.CompareTo(a.Size.sqrMagnitude));
            return list;
        }

        static MeshBuilder BuildLod(ShipClass c, List<Greeble> greebles, float budgetFactor)
        {
            var builder = new MeshBuilder();
            var budget = Mathf.Max(60, Mathf.RoundToInt(c.TriangleBudget * budgetFactor));

            // Hull: about 35 % of the budget, 2 triangles per quad.
            var hullTris = budget * 0.35f;
            var sides = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(hullTris / 2f)), 8, 256);
            var rings = Mathf.Clamp(Mathf.RoundToInt(hullTris / (2f * sides)) + 1, 4, 512);
            builder.AddLoft(BuildHullRings(c, rings, sides));

            // Engines at the back: about 10 % of the budget.
            var engineSegments = Mathf.Clamp(Mathf.RoundToInt(budget * 0.10f / (4f * c.Engines)), 6, 64);
            var engineRadius = Mathf.Min(c.Width, c.Height) * 0.18f;
            for (var e = 0; e < c.Engines; e++)
            {
                var x = c.Engines == 1 ? 0f : Mathf.Lerp(-c.Width * 0.3f, c.Width * 0.3f, e / (float)(c.Engines - 1));
                var center = new Vector3(x, 0f, -c.Length * 0.5f - engineRadius * 0.5f);
                builder.AddCylinder(center, engineRadius, engineRadius * 2f, engineSegments, Quaternion.identity);
            }

            // Greebles fill the rest of the budget, largest first.
            var remaining = budget - builder.TriangleCount;
            var greebleCount = Mathf.Clamp(remaining / 12, 0, greebles.Count);
            for (var i = 0; i < greebleCount; i++)
            {
                var g = greebles[i];
                var (point, normal) = HullSurface(c, g.T, g.Angle);
                var rotation = Quaternion.LookRotation(Vector3.forward, normal) * Quaternion.Euler(0f, g.Yaw, 0f);
                builder.AddBox(point + normal * (g.Size.y * 0.4f), g.Size, rotation);
            }

            return builder;
        }

        static Vector3[][] BuildHullRings(ShipClass c, int ringCount, int sides)
        {
            var rings = new Vector3[ringCount][];
            for (var r = 0; r < ringCount; r++)
            {
                var t = r / (float)(ringCount - 1);
                var ring = new Vector3[sides];
                for (var s = 0; s < sides; s++)
                {
                    var angle = s * Mathf.PI * 2f / sides;
                    ring[s] = HullSurface(c, t, angle).point;
                }
                rings[r] = ring;
            }
            return rings;
        }

        /// <summary>Hull section scale along the ship: blunt back, tapered nose.</summary>
        static float Profile(float t)
        {
            var back = Mathf.SmoothStep(0.65f, 1f, Mathf.Clamp01(t / 0.15f));
            var nose = Mathf.Sqrt(Mathf.Clamp01((1f - t) / 0.35f));
            return Mathf.Max(0.05f, Mathf.Min(back, nose));
        }

        static (Vector3 point, Vector3 normal) HullSurface(ShipClass c, float t, float angle)
        {
            var scale = Profile(t);
            var w = c.Width * 0.5f * scale;
            var h = c.Height * 0.5f * scale;
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            var e = 2f / c.Boxiness;
            var x = w * Mathf.Sign(cos) * Mathf.Pow(Mathf.Abs(cos), e);
            var y = h * Mathf.Sign(sin) * Mathf.Pow(Mathf.Abs(sin), e);
            var z = Mathf.Lerp(-c.Length * 0.5f, c.Length * 0.5f, t);

            // Approximate outward normal of the section (ignores the slope along Z).
            var normal = new Vector3(x / (w * w), y / (h * h), 0f).normalized;
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
            return (new Vector3(x, y, z), normal);
        }

        static Mesh SaveMesh(Mesh mesh)
        {
            var path = $"{Root}/Meshes/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material GetOrCreateMaterial(ShipClass c)
        {
            var path = $"{Root}/Materials/{c.Name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = c.Name, enableInstancing = true };
            material.SetColor("_BaseColor", c.Color);
            material.SetColor("_Color", c.Color);
            material.SetFloat("_Smoothness", 0.35f);
            material.SetFloat("_Metallic", 0.6f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
