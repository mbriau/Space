using System.IO;
using System.Linq;
using Space.Presentation.Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Space.Editor.Hangar
{
    /// <summary>
    /// Menu Space > Hangar: opens the ship viewer scene, creating it on first use (an empty scene with
    /// a <see cref="ShipViewer"/>, which sets up its own camera and light). Press Play to browse.
    /// </summary>
    public static class HangarMenu
    {
        public const string ScenePath = "Assets/_Project/Scenes/Hangar.unity";

        [MenuItem("Space/Hangar (visionneuse de vaisseaux)")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!File.Exists(ScenePath))
                CreateScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Hangar").AddComponent<ShipViewer>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            // In the build settings too, after the levels, so it can be loaded from a menu later.
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log($"Scène du hangar créée : {ScenePath}");
        }
    }
}
