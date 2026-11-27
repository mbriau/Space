using System.Collections.Generic;
using System.IO;
using Space.Presentation.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Space.Editor.Characters
{
    /// <summary>
    /// Builds the talking portraits: cuts the mouth and eye patches out of each character's generated
    /// variants (feathered edges, so they melt into the base picture), then demo scenes: the pilot
    /// alone, and a dialogue where the pirate answers her. Menus: Space &gt; Portraits.
    /// </summary>
    public static class PortraitBuilder
    {
        /// <summary>Everything needed to animate one character's portrait.</summary>
        public sealed class Character
        {
            public string Name;
            public string Folder;
            public string BaseFile;
            public string Voice;
            /// <summary>Mouth patch region (pixels, origin top left): the lips and around them.</summary>
            public RectInt MouthRegion;
            /// <summary>Eyes patch region: both eyes, below the brows.</summary>
            public RectInt EyesRegion;
            public PortraitLandmarks Landmarks;

            public string Patches => Folder + "/Patches";
            public string Base => $"{Folder}/{BaseFile}";
        }

        public static readonly Character Pilote = new()
        {
            Name = "Kaïa",
            Folder = "Assets/_Project/Art/Characters/Pilote",
            BaseFile = "pilote_portrait.png",
            Voice = "Assets/_Project/Audio/Voix/Pilote/pilote_contact_pirate",
            MouthRegion = new RectInt(668, 482, 94, 96),
            EyesRegion = new RectInt(640, 362, 165, 60),
            Landmarks = new PortraitLandmarks
            {
                ImageSize = 1024f, HeadCenter = new Vector2(680f, 400f), HeadRadii = new Vector2(260f, 250f),
                Neck = new Vector2(660f, 640f), TorsoLeft = 220f, TorsoRight = 900f, BorderPx = 40f,
            },
        };

        public static readonly Character Pirate = new()
        {
            Name = "Silas",
            Folder = "Assets/_Project/Art/Characters/Pirate",
            BaseFile = "pirate_portrait.png",
            Voice = "Assets/_Project/Audio/Voix/Pirate/pirate_reponse_kaia",
            MouthRegion = new RectInt(455, 405, 135, 85),
            EyesRegion = new RectInt(440, 285, 165, 55),
            Landmarks = new PortraitLandmarks
            {
                // The ellipse takes the big tricorn hat along with the head.
                ImageSize = 1024f, HeadCenter = new Vector2(600f, 270f), HeadRadii = new Vector2(330f, 300f),
                Neck = new Vector2(600f, 570f), TorsoLeft = 220f, TorsoRight = 1000f, BorderPx = 40f,
            },
        };

        const string PiloteScene = "Assets/_Project/Scenes/PilotePortrait.unity";
        const string DialogueScene = "Assets/_Project/Scenes/DialoguePirate.unity";
        const int Feather = 12;

        static readonly (string Shape, string File)[] MouthVariants =
        {
            ("X", "bouche_X_repos"), ("A", "bouche_A_mbp"), ("B", "bouche_B_dents"), ("C", "bouche_C_e"),
            ("D", "bouche_D_a"), ("E", "bouche_E_o"), ("F", "bouche_F_ou"),
        };

        [MenuItem("Space/Portraits/Pilote seule")]
        public static void BuildPiloteScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildPatches(Pilote);
            BuildSingleScene(Pilote, PiloteScene);
        }

        [MenuItem("Space/Portraits/Dialogue pilote et pirate")]
        public static void BuildDialogueSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildAll();
        }

        /// <summary>Patches of every character, their prefabs and both demo scenes (the dialogue stays open).</summary>
        public static void BuildAll()
        {
            BuildPatches(Pilote);
            BuildPatches(Pirate);
            BuildPrefab(Pilote);
            BuildPrefab(Pirate);
            BuildSingleScene(Pilote, PiloteScene);
            BuildDialogueScene();
        }

        /// <summary>Prefab path of a character's talking portrait (used by the level intro's comms).</summary>
        public static string PrefabPath(Character character) => $"{character.Folder}/Portrait_{Path.GetFileName(character.Folder)}.prefab";

        /// <summary>The talking portrait as a prefab: rig and layers, silent until told to speak.</summary>
        public static GameObject BuildPrefab(Character character)
        {
            var rig = AddPortrait(character, Vector3.zero);
            rig.AutoPlay = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(rig.gameObject, PrefabPath(character));
            Object.DestroyImmediate(rig.gameObject);
            return prefab;
        }

        public static void BuildPatches(Character character)
        {
            Directory.CreateDirectory(character.Patches);
            foreach (var (shape, file) in MouthVariants)
            {
                var source = $"{character.Folder}/Variants/{file}.png";
                if (File.Exists(source))
                    Cut(source, character.MouthRegion, $"{character.Patches}/bouche_{shape}.png");
            }
            Cut($"{character.Folder}/Variants/yeux_fermes.png", character.EyesRegion, $"{character.Patches}/yeux_fermes.png");
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

        static void BuildSingleScene(Character character, string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(0.52f);
            AddPortrait(character, Vector3.zero);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"Portrait : {path} (Play pour l'entendre)");
        }

        /// <summary>Kaïa on the left looking right, Silas on the right looking left: she calls, he answers.</summary>
        static void BuildDialogueScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(0.58f);
            var pilote = AddPortrait(Pilote, new Vector3(-0.53f, 0f, 0f));
            var pirate = AddPortrait(Pirate, new Vector3(0.53f, 0f, 0f));
            var dialogue = new GameObject("Dialogue").AddComponent<PortraitDialogue>();
            dialogue.Speakers = new List<PortraitRig> { pilote, pirate };
            EditorSceneManager.SaveScene(scene, DialogueScene);
            Debug.Log($"Dialogue : {DialogueScene} (Play pour l'entendre)");
        }

        static void AddCamera(float size)
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";
        }

        static PortraitRig AddPortrait(Character character, Vector3 position)
        {
            var root = new GameObject(character.Name);
            root.transform.position = position;
            var rig = root.AddComponent<PortraitRig>();
            rig.Landmarks = character.Landmarks;
            rig.Base = Layer(root.transform, "Base", new RectInt(0, 0, 1024, 1024), 0, AssetDatabase.LoadAssetAtPath<Texture>(character.Base));
            rig.Eyes = Layer(root.transform, "Yeux", character.EyesRegion, 1, null);
            rig.Mouth = Layer(root.transform, "Bouche", character.MouthRegion, 2, null);
            rig.EyesClosed = AssetDatabase.LoadAssetAtPath<Texture>($"{character.Patches}/yeux_fermes.png");
            rig.Mouths = new List<PortraitRig.MouthPatch>();
            foreach (var (shape, _) in MouthVariants)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture>($"{character.Patches}/bouche_{shape}.png");
                if (texture != null)
                    rig.Mouths.Add(new PortraitRig.MouthPatch { Shape = shape, Texture = texture });
            }
            rig.Line = AssetDatabase.LoadAssetAtPath<AudioClip>($"{character.Voice}.wav");
            rig.LipSync = AssetDatabase.LoadAssetAtPath<TextAsset>($"{character.Voice}.lipsync.json");
            return rig;
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
