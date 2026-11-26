using System.Collections.Generic;
using Space.Presentation.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Space.Editor.Rendering
{
    /// <summary>
    /// Puts the DinV nebula and star fields behind the level: imports the textures so they can tile
    /// (Repeat), then replaces the camera's SpaceDust with a <see cref="SpaceBackdrop"/>.
    /// Menu: Space &gt; Install Space Backdrop.
    /// </summary>
    public static class SpaceBackdropSetup
    {
        const string Folder = "Assets/DinV/Dynamic Space Background/Sprites";
        const string Nebula = Folder + "/Nebula Blue.png";
        const string StarsSmall = Folder + "/Stars Small_1.png";
        const string StarsBig = Folder + "/Stars-Big_1.png";
        const string LevelScene = "Assets/_Project/Scenes/Level.unity";

        [MenuItem("Space/Install Space Backdrop")]
        public static void Install()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            ImportTextures();
            InstallInScene(LevelScene);
        }

        public static void ImportTextures()
        {
            foreach (var path in new[] { Nebula, StarsSmall, StarsBig })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null)
                {
                    Debug.LogError($"Texture introuvable : {path}");
                    continue;
                }
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
        }

        public static void InstallInScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError($"Pas de caméra principale dans {scenePath}");
                return;
            }

            var dust = camera.GetComponent<SpaceDust>();
            if (dust != null)
                Object.DestroyImmediate(dust, true);

            var backdrop = camera.GetComponent<SpaceBackdrop>() ?? camera.gameObject.AddComponent<SpaceBackdrop>();
            backdrop.Layers = new List<SpaceBackdrop.Layer>
            {
                // Far away: barely moves, drifts very slowly.
                new() { Name = "Nébuleuse", Texture = AssetDatabase.LoadAssetAtPath<Texture>(Nebula), Tiling = 1f,
                        MetresPerTile = 60000f, Drift = new Vector2(0.0015f, 0.0004f) },
                // Small stars: middle distance.
                new() { Name = "Petites étoiles", Texture = AssetDatabase.LoadAssetAtPath<Texture>(StarsSmall), Tiling = 1.4f,
                        MetresPerTile = 5000f, Drift = new Vector2(0.004f, 0.001f), Additive = true, Tint = new Color(1f, 1f, 1f, 0.85f) },
                // Big stars: the nearest layer, the fastest.
                new() { Name = "Grandes étoiles", Texture = AssetDatabase.LoadAssetAtPath<Texture>(StarsBig), Tiling = 1f,
                        MetresPerTile = 1500f, Drift = new Vector2(0.009f, 0.002f), Additive = true },
            };
            EditorUtility.SetDirty(camera.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Fond spatial installé sur {camera.name} ({scenePath})");
        }
    }
}
