using System.Collections.Generic;
using Space.Core.Levels;
using Space.Editor.Characters;
using Space.Presentation.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Space.Editor.Game
{
    /// <summary>
    /// Sets up the intro of "Premier contact": two raiders arriving to port one second apart, the
    /// portraits' prefabs, and a <see cref="LevelIntro"/> on the level runner with Kaïa's and
    /// Silas's lines. Menu: Space &gt; Intro de Premier contact.
    /// </summary>
    public static class LevelIntroSetup
    {
        const string LevelPath = "Assets/_Project/Data/Levels/L00_PremierContact.asset";
        const string ScenePath = "Assets/_Project/Scenes/Level.unity";
        const string Pilote = "Assets/_Project/Audio/Voix/Pilote/";
        const string Pirate = "Assets/_Project/Audio/Voix/Pirate/";

        [MenuItem("Space/Intro de Premier contact")]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            SetupLevel();
            InstallInScene();
        }

        /// <summary>Two raiders on the port side; their delays count from the moment Kaïa picks them up.</summary>
        public static void SetupLevel()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
            level.Description = "Deux raiders pirates sortent d'hyperespace sur ta route.";
            level.Enemies = new[]
            {
                // The first ahead, the second astern and a little further out (within half its cannon's range).
                new LevelDefinition.EnemySpawn { Ship = "Raider", DelaySeconds = 3.4f, Distance = 190f, AngleDegrees = -35f, Along = 130f },
                new LevelDefinition.EnemySpawn { Ship = "Raider", DelaySeconds = 4.4f, Distance = 260f, AngleDegrees = -35f, Along = -110f },
            };
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
        }

        public static void InstallInScene()
        {
            var kaia = AssetDatabase.LoadAssetAtPath<GameObject>(PortraitBuilder.PrefabPath(PortraitBuilder.Pilote))
                       ?? PortraitBuilder.BuildPrefab(PortraitBuilder.Pilote);
            var silas = AssetDatabase.LoadAssetAtPath<GameObject>(PortraitBuilder.PrefabPath(PortraitBuilder.Pirate))
                        ?? PortraitBuilder.BuildPrefab(PortraitBuilder.Pirate);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null)
            {
                Debug.LogError($"Pas de LevelRunner dans {ScenePath}");
                return;
            }

            var intro = runner.GetComponent<LevelIntro>() ?? runner.gameObject.AddComponent<LevelIntro>();
            intro.Level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
            intro.LeftPortrait = kaia;
            intro.RightPortrait = silas;
            intro.Story = Line(0, Pilote + "pilote_histoire_cuirasses");
            intro.Detection = Line(0, Pilote + "pilote_detection");
            intro.Dialogue = new List<LevelIntro.Line>
            {
                Line(0, Pilote + "pilote_appel_pirates"),
                Line(1, Pirate + "pirate_reponse_kaia"),
            };
            runner.Intro = intro;

            // The scene had no listener: nothing could be heard.
            var camera = Camera.main;
            if (camera != null && camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(intro);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Intro de Premier contact installée.");
        }

        /// <summary>A line from its files: audio (.wav), Rhubarb track (.lipsync.json) and text (.txt, the subtitle).</summary>
        static LevelIntro.Line Line(int speaker, string basePath)
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(basePath + ".txt");
            return new LevelIntro.Line
            {
                Speaker = speaker,
                Audio = AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + ".wav"),
                LipSync = AssetDatabase.LoadAssetAtPath<TextAsset>(basePath + ".lipsync.json"),
                Subtitle = text != null ? text.text.Trim() : "",
            };
        }
    }
}
