using System.Collections.Generic;
using System.IO;
using Space.Presentation.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Space.Editor.Characters
{
    /// <summary>
    /// Builds the talking portrait of the pilot: cuts the mouth and eye patches out of the generated
    /// variants (feathered edges, so they melt into the base picture), then a demo scene that plays the
    /// voice line in a loop. Menu: Space &gt; Portrait de la pilote.
    /// </summary>
    public static class PortraitBuilder
    {
        const string Folder = "Assets/_Project/Art/Characters/Pilote";
        const string Base = Folder + "/pilote_portrait.png";
        const string Patches = Folder + "/Patches";
        const string ScenePath = "Assets/_Project/Scenes/PilotePortrait.unity";
        const string Voice = "Assets/_Project/Audio/Voix/Pilote/pilote_contact_pirate";

        /// <summary>Mouth patch region (pixels, origin top left): the lips and the top of the chin, clear of the microphone.</summary>
        public static readonly RectInt MouthRegion = new(668, 482, 94, 96);

        /// <summary>Eyes patch region: both eyes, below the brows.</summary>
        public static readonly RectInt EyesRegion = new(640, 362, 165, 60);

        const int Feather = 12;

        static readonly (string Shape, string File)[] MouthVariants =
        {
            ("X", "bouche_X_repos"), ("A", "bouche_A_mbp"), ("B", "bouche_B_dents"), ("C", "bouche_C_e"),
            ("D", "bouche_D_a"), ("E", "bouche_E_o"), ("F", "bouche_F_ou"),
        };

        [MenuItem("Space/Portrait de la pilote")]
        public static void BuildAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildPatches();
            BuildScene();
        }

        public static void BuildPatches()
        {
            Directory.CreateDirectory(Patches);
            foreach (var (shape, file) in MouthVariants)
                Cut($"{Folder}/Variants/{file}.png", MouthRegion, $"{Patches}/bouche_{shape}.png");
            Cut($"{Folder}/Variants/yeux_fermes.png", EyesRegion, $"{Patches}/yeux_fermes.png");
            AssetDatabase.Refresh();
        }

        /// <summary>Copies a region of an image into a PNG whose alpha fades to 0 over <see cref="Feather"/> pixels at the edges.</summary>
        static void Cut(string sourcePath, RectInt region, string destination)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(sourcePath));
            var patch = new Texture2D(region.width, region.height, TextureFormat.RGBA32, false);
            for (var y = 0; y < region.height; y++)
            for (var x = 0; x < region.width; x++)
            {
                // Texture rows go up, the region is given with y going down.
                var color = source.GetPixel(region.x + x, source.height - 1 - (region.y + region.height - 1 - y));
                var edge = Mathf.Min(Mathf.Min(x, region.width - 1 - x), Mathf.Min(y, region.height - 1 - y));
                var a = Mathf.Clamp01(edge / (float)Feather);
                color.a = a * a * (3f - 2f * a);
                patch.SetPixel(x, y, color);
            }
            patch.Apply();
            File.WriteAllBytes(destination, patch.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(patch);
        }

        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 0.52f;
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";

            var root = new GameObject("Pilote");
            var rig = root.AddComponent<PortraitRig>();
            rig.Base = Layer(root.transform, "Base", new RectInt(0, 0, 1024, 1024), 0, AssetDatabase.LoadAssetAtPath<Texture>(Base));
            rig.Eyes = Layer(root.transform, "Yeux", EyesRegion, 1, null);
            rig.Mouth = Layer(root.transform, "Bouche", MouthRegion, 2, null);
            rig.EyesClosed = AssetDatabase.LoadAssetAtPath<Texture>($"{Patches}/yeux_fermes.png");
            rig.Mouths = new List<PortraitRig.MouthPatch>();
            foreach (var (shape, _) in MouthVariants)
                rig.Mouths.Add(new PortraitRig.MouthPatch { Shape = shape, Texture = AssetDatabase.LoadAssetAtPath<Texture>($"{Patches}/bouche_{shape}.png") });
            rig.Line = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Voice}.wav");
            rig.LipSync = AssetDatabase.LoadAssetAtPath<TextAsset>($"{Voice}.lipsync.json");

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Portrait de la pilote : {ScenePath} (Play pour l'entendre)");
        }

        static PortraitLayer Layer(Transform parent, string name, RectInt region, int order, Texture texture)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var layer = go.AddComponent<PortraitLayer>();
            layer.RegionPx = new Rect(region.x, region.y, region.width, region.height);
            layer.Order = order;
            layer.Texture = texture;
            layer.Grid = order == 0 ? 48 : 8;
            return layer;
        }
    }
}
