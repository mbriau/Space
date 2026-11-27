using System.Collections.Generic;
using System.IO;
using Space.Presentation.Ships;
using AuraShape = Space.Presentation.Ships.AuraShape;
using UnityEditor;
using UnityEngine;

namespace Space.Editor.ShipArt
{
    /// <summary>
    /// Turns a generated, textured ship model (one fused mesh, imported from a .glb) into game art:
    /// the turret heads are cut out (the islands of geometry inside a box around each turret), the
    /// rest is the hull. Everything is moved to ship space (+Z forward) and scaled to the design's
    /// length, saved as mesh assets with a URP Lit material, and registered in the
    /// <see cref="ShipArtLibrary"/> in Resources. Menu: Space &gt; Build Ship Art.
    /// </summary>
    public static class ShipArtBuilder
    {
        const string LibraryPath = "Assets/_Project/Resources/" + ShipArtLibrary.ResourceName + ".asset";

        /// <summary>A turret to cut out, in the source model's units (length 1, +Z forward once its node rotation is applied).</summary>
        public readonly struct HeadSpec
        {
            public readonly string Name;
            public readonly TurretKind Kind;
            /// <summary>Where the head turns: on top of its pedestal, which stays on the hull.</summary>
            public readonly Vector3 Pivot;
            /// <summary>Islands whose centre falls in this box belong to the head.</summary>
            public readonly Bounds Box;

            public HeadSpec(string name, TurretKind kind, Vector3 pivot, Vector3 boxMin, Vector3 boxMax)
            {
                Name = name; Kind = kind; Pivot = pivot;
                Box = new Bounds((boxMin + boxMax) * 0.5f, boxMax - boxMin);
            }
        }

        public readonly struct ShipSpec
        {
            public readonly string DesignId;
            public readonly string Source;
            public readonly string OutputFolder;
            public readonly float Length;
            public readonly HeadSpec[] Heads;

            public ShipSpec(string designId, string source, string outputFolder, float length, HeadSpec[] heads)
            {
                DesignId = designId; Source = source; OutputFolder = outputFolder; Length = length; Heads = heads;
            }
        }

        /// <summary>
        /// The Remorqueur generated from its concept: a single gun at the bow, a twin cannon before the
        /// bridge and a missile pod on the rear deck. Slots follow this order (front to back).
        /// </summary>
        public static readonly ShipSpec Remorqueur = new(
            "remorqueur",
            "Assets/_Project/Art/Concepts/Generated/Remorqueur3D/remorqueur_tripo_Assets/selected.glb",
            "Assets/_Project/Art/Ships/Remorqueur",
            50f,
            new[]
            {
                new HeadSpec("Gun", TurretKind.Laser, new Vector3(-0.003f, 0.031f, 0.367f),
                    new Vector3(-0.05f, 0.035f, 0.33f), new Vector3(0.05f, 0.12f, 0.48f)),
                new HeadSpec("TwinCannon", TurretKind.Twin, new Vector3(-0.004f, 0.049f, 0.118f),
                    new Vector3(-0.06f, 0.05f, 0.075f), new Vector3(0.06f, 0.12f, 0.27f)),
                new HeadSpec("MissilePod", TurretKind.Missile, new Vector3(-0.006f, 0.048f, -0.236f),
                    new Vector3(-0.06f, 0.05f, -0.30f), new Vector3(0.06f, 0.14f, -0.17f)),
            });

        /// <summary>Ready-made models of the asset packs, by id: prefab path and scale (nose along +Z).</summary>
        public static readonly (string Id, string Prefab, float Scale)[] Models =
        {
            ("forcebadger35", "Assets/UltimateSpaceshipsCreator/Prefabs/Examples/ForceBadger/ForceBadger35.prefab", 3f),
        };

        [MenuItem("Space/Build Ship Art")]
        public static void BuildAll()
        {
            Build(Remorqueur);
            foreach (var (id, prefab, scale) in Models)
                RegisterModel(id, prefab, scale);
        }

        /// <summary>Registers a ready-made model prefab under an id in the <see cref="ShipArtLibrary"/>.</summary>
        public static void RegisterModel(string id, string prefabPath, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Modèle introuvable : {prefabPath}");
                return;
            }
            var art = new Space.Presentation.Ships.ShipArt { DesignId = id, Model = prefab, ModelScale = scale };
            var bounds = ModelBounds(art, out _);
            art.TurretSize = bounds.size.z * 0.06f;
            art.Slots.Add(TopOfHull(prefab, scale, 0f, bounds));
            art.Aura = AuraFromColliders(prefab, scale);

            var library = LoadOrCreateLibrary();
            library.Ships.RemoveAll(s => s == null || s.DesignId == id);
            library.Ships.Add(art);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"{id} : {bounds.size} m, emplacement {art.Slots[0]}, aura de {art.Aura.Count} forme(s)");
        }

        /// <summary>
        /// A turret slot on top of a model: the highest point of the hull along the centre line, at
        /// <paramref name="share"/> of the length from the middle (+ forward).
        /// </summary>
        static Vector3 TopOfHull(GameObject prefab, float scale, float share, Bounds bounds)
        {
            var z = bounds.center.z + share * bounds.size.z;
            var top = float.NegativeInfinity;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                var matrix = Matrix4x4.Scale(Vector3.one * scale) * prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var p = matrix.MultiplyPoint3x4(vertex);
                    if (Mathf.Abs(p.x - bounds.center.x) < bounds.size.x * 0.08f && Mathf.Abs(p.z - z) < bounds.size.z * 0.06f)
                        top = Mathf.Max(top, p.y);
                }
            }
            return new Vector3(bounds.center.x, float.IsNegativeInfinity(top) ? bounds.max.y : top, z);
        }

        /// <summary>
        /// The shield aura of a model: one ellipsoid per collider of the prefab (sphere, capsule or box),
        /// a little larger than the collider, in ship space.
        /// </summary>
        static List<AuraShape> AuraFromColliders(GameObject prefab, float scale)
        {
            const float margin = 1.12f;
            var shapes = new List<AuraShape>();
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                var toShip = Matrix4x4.Scale(Vector3.one * scale) * prefab.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                var lossy = toShip.lossyScale;
                Vector3 center, size;
                switch (collider)
                {
                    case SphereCollider sphere:
                        center = sphere.center;
                        size = Vector3.one * sphere.radius * 2f;
                        break;
                    case CapsuleCollider capsule:
                        center = capsule.center;
                        size = Vector3.one * capsule.radius * 2f;
                        size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                        break;
                    case BoxCollider box:
                        // The ellipsoid around a box: its half-diagonals, a little rounder.
                        center = box.center;
                        size = box.size * 1.3f;
                        break;
                    default:
                        continue;
                }
                shapes.Add(new AuraShape
                {
                    Center = toShip.MultiplyPoint3x4(center),
                    Size = Vector3.Scale(size, lossy) * margin,
                    Euler = toShip.rotation.eulerAngles,
                });
            }
            return shapes;
        }

        /// <summary>Bounds of a model art once scaled, in ship space, and its triangle count.</summary>
        public static Bounds ModelBounds(Space.Presentation.Ships.ShipArt art, out int triangles)
        {
            triangles = 0;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var first = true;
            foreach (var filter in art.Model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                triangles += filter.sharedMesh.triangles.Length / 3;
                var matrix = Matrix4x4.Scale(Vector3.one * art.ModelScale) * art.Model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = matrix.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        public static void Build(ShipSpec spec)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Source);
            if (source == null)
            {
                Debug.LogError($"Modèle introuvable : {spec.Source} (le paquet glTFast est-il installé ?)");
                return;
            }
            var filter = source.GetComponentInChildren<MeshFilter>();
            var sourceRenderer = filter.GetComponent<Renderer>();
            var mesh = filter.sharedMesh;
            var toShip = filter.transform.localToWorldMatrix;

            Directory.CreateDirectory(spec.OutputFolder);

            // Every vertex in ship space, in the source units (length 1).
            var positions = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = toShip.MultiplyPoint3x4(positions[i]);
                if (i < normals.Length)
                    normals[i] = toShip.MultiplyVector(normals[i]).normalized;
            }
            var triangles = mesh.triangles;
            var islands = Islands(positions, triangles, out var islandCenters);

            // Each triangle goes to the head whose box holds its island's centre, or stays on the hull.
            var owner = new int[islandCenters.Count];
            for (var island = 0; island < owner.Length; island++)
            {
                owner[island] = -1;
                for (var h = 0; h < spec.Heads.Length; h++)
                    if (spec.Heads[h].Box.Contains(islandCenters[island]))
                        owner[island] = h;
            }

            var scale = spec.Length / Mathf.Max(1e-4f, LengthOf(positions));
            var hull = Extract(positions, normals, uvs, triangles, islands, owner, -1, Vector3.zero, scale, $"{spec.DesignId}_Hull");
            var material = SaveMaterial(spec, sourceRenderer.sharedMaterial);
            var art = new Space.Presentation.Ships.ShipArt
            {
                DesignId = spec.DesignId,
                Hull = SaveMesh(spec, hull),
                Material = material,
            };

            for (var h = 0; h < spec.Heads.Length; h++)
            {
                var head = spec.Heads[h];
                var headMesh = Extract(positions, normals, uvs, triangles, islands, owner, h, head.Pivot, scale, $"{spec.DesignId}_{head.Name}");
                art.Slots.Add(head.Pivot * scale);
                art.Turrets.Add(new TurretArt
                {
                    Kind = head.Kind,
                    Head = SaveMesh(spec, headMesh),
                    Muzzles = Muzzles(positions, triangles, islands, owner, h, head.Pivot, scale),
                    Size = Mathf.Max(headMesh.bounds.size.x, headMesh.bounds.size.z),
                });
                Debug.Log($"{spec.DesignId} : tourelle {head.Name}, {headMesh.triangles.Length / 3} triangles");
            }
            Debug.Log($"{spec.DesignId} : coque {hull.triangles.Length / 3} triangles, {hull.bounds.size} m");

            var library = LoadOrCreateLibrary();
            library.Ships.RemoveAll(s => s == null || s.DesignId == spec.DesignId);
            library.Ships.Add(art);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        static float LengthOf(Vector3[] positions)
        {
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var p in positions)
            {
                min = Mathf.Min(min, p.z);
                max = Mathf.Max(max, p.z);
            }
            return max - min;
        }

        /// <summary>Connected pieces of the mesh (vertices welded by position), and the centre of each.</summary>
        static int[] Islands(Vector3[] positions, int[] triangles, out List<Vector3> centers)
        {
            var welded = new Dictionary<Vector3Int, int>();
            var id = new int[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var key = Vector3Int.RoundToInt(positions[i] * 100000f);
                if (!welded.TryGetValue(key, out var n))
                {
                    n = welded.Count;
                    welded[key] = n;
                }
                id[i] = n;
            }

            var parent = new int[welded.Count];
            for (var i = 0; i < parent.Length; i++)
                parent[i] = i;
            int Find(int a)
            {
                while (parent[a] != a)
                {
                    parent[a] = parent[parent[a]];
                    a = parent[a];
                }
                return a;
            }
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = Find(id[triangles[t]]);
                parent[Find(id[triangles[t + 1]])] = a;
                parent[Find(id[triangles[t + 2]])] = a;
            }

            // One island index per triangle, and the bounds of each island's triangle centres.
            var islandOf = new Dictionary<int, int>();
            var bounds = new List<Bounds>();
            var result = new int[triangles.Length / 3];
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var root = Find(id[triangles[t]]);
                var center = (positions[triangles[t]] + positions[triangles[t + 1]] + positions[triangles[t + 2]]) / 3f;
                if (!islandOf.TryGetValue(root, out var island))
                {
                    island = bounds.Count;
                    islandOf[root] = island;
                    bounds.Add(new Bounds(center, Vector3.zero));
                }
                else
                {
                    var b = bounds[island];
                    b.Encapsulate(center);
                    bounds[island] = b;
                }
                result[t / 3] = island;
            }
            centers = bounds.ConvertAll(b => b.center);
            return result;
        }

        /// <summary>The triangles owned by <paramref name="part"/> (-1 = hull), relative to <paramref name="pivot"/> and scaled.</summary>
        static Mesh Extract(Vector3[] positions, Vector3[] normals, Vector2[] uvs, int[] triangles, int[] islands, int[] owner,
                            int part, Vector3 pivot, float scale, string name)
        {
            var remap = new Dictionary<int, int>();
            var outPositions = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var outTriangles = new List<int>();
            for (var t = 0; t < triangles.Length; t += 3)
            {
                if (owner[islands[t / 3]] != part)
                    continue;
                for (var k = 0; k < 3; k++)
                {
                    var v = triangles[t + k];
                    if (!remap.TryGetValue(v, out var n))
                    {
                        n = outPositions.Count;
                        remap[v] = n;
                        outPositions.Add((positions[v] - pivot) * scale);
                        outNormals.Add(v < normals.Length ? normals[v] : Vector3.up);
                        outUvs.Add(v < uvs.Length ? uvs[v] : Vector2.zero);
                    }
                    outTriangles.Add(n);
                }
            }

            var mesh = new Mesh
            {
                name = name,
                indexFormat = outPositions.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(outPositions);
            mesh.SetNormals(outNormals);
            mesh.SetUVs(0, outUvs);
            mesh.SetTriangles(outTriangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// Muzzles of a head, in head space: the front tip of every barrel (an island much longer than
        /// wide along Z), or the middle of the head's front face when it has no barrel (launchers).
        /// </summary>
        static List<Vector3> Muzzles(Vector3[] positions, int[] triangles, int[] islands, int[] owner, int part, Vector3 pivot, float scale)
        {
            var islandBounds = new Dictionary<int, Bounds>();
            var all = new Bounds();
            var any = false;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var island = islands[t / 3];
                if (owner[island] != part)
                    continue;
                for (var k = 0; k < 3; k++)
                {
                    var p = positions[triangles[t + k]];
                    if (islandBounds.TryGetValue(island, out var b))
                        b.Encapsulate(p);
                    else
                        b = new Bounds(p, Vector3.zero);
                    islandBounds[island] = b;
                    if (any)
                        all.Encapsulate(p);
                    else
                        all = new Bounds(p, Vector3.zero);
                    any = true;
                }
            }

            var muzzles = new List<Vector3>();
            foreach (var b in islandBounds.Values)
                if (b.size.z > 2.5f * Mathf.Max(b.size.x, b.size.y))
                    muzzles.Add((new Vector3(b.center.x, b.center.y, b.max.z) - pivot) * scale);
            if (muzzles.Count == 0 && any)
                muzzles.Add((new Vector3(all.center.x, all.center.y, all.max.z) - pivot) * scale);
            return muzzles;
        }

        static Mesh SaveMesh(ShipSpec spec, Mesh mesh)
        {
            var path = $"{spec.OutputFolder}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>A URP Lit material with the model's colour texture: worn paint is matte, a little metallic.</summary>
        static Material SaveMaterial(ShipSpec spec, Material source)
        {
            var path = $"{spec.OutputFolder}/{spec.DesignId}_Lit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"{spec.DesignId}_Lit" };
                AssetDatabase.CreateAsset(material, path);
            }

            Texture baseColor = null;
            if (source != null)
                foreach (var property in source.GetTexturePropertyNames())
                    if (property.ToLowerInvariant().Contains("basecolor") || property == "_BaseMap" || property == "_MainTex")
                        baseColor ??= source.GetTexture(property);
            material.SetTexture("_BaseMap", baseColor);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0.35f);
            material.SetFloat("_Smoothness", 0.3f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static ShipArtLibrary LoadOrCreateLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<ShipArtLibrary>(LibraryPath);
            if (library != null)
                return library;
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            library = ScriptableObject.CreateInstance<ShipArtLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            return library;
        }
    }
}
