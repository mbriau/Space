using System;
using System.Collections;
using NUnit.Framework;
using Space.Core.Combat;
using Space.Core.Time;
using Space.Presentation.Game;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Space.PlayMode.Tests
{
    /// <summary>
    /// Plays the first level for a while and captures what the camera sees, so the framing can be
    /// reviewed without a screen (the CI prints the images in its log, base64, between markers).
    /// </summary>
    public class TacticalViewCaptureTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Level.unity";

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureTheDuel()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            // Wait for the level to start (the ship library streams in with the sub scene).
            LevelRunner runner = null;
            var waited = 0f;
            while ((runner == null || !runner.HasStarted) && waited < 120f)
            {
                runner = UnityEngine.Object.FindAnyObjectByType<LevelRunner>();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(runner, "LevelRunner introuvable");
            Assert.IsTrue(runner.HasStarted, "le niveau n'a pas démarré");

            SetTimeScale(4f);
            yield return WaitSimulated(9f);
            Capture("arrivee");
            yield return WaitSimulated(16f);
            Capture("combat");
            yield return WaitSimulated(20f);
            Capture("combat2");
        }

        static void SetTimeScale(float scale)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var query = em.CreateEntityQuery(typeof(SimulationTime));
            var entity = query.GetSingletonEntity();
            var time = em.GetComponentData<SimulationTime>(entity);
            time.TimeScale = scale;
            time.Paused = false;
            em.SetComponentData(entity, time);
        }

        static IEnumerator WaitSimulated(float seconds)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var query = em.CreateEntityQuery(typeof(SimulationTime));
            var start = query.GetSingleton<SimulationTime>().ElapsedTime;
            var guard = 0f;
            while (query.GetSingleton<SimulationTime>().ElapsedTime - start < seconds && guard < 180f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static void Capture(string name)
        {
            var camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
            Assert.IsNotNull(camera, "aucune caméra");

            const int width = 1280, height = 720;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;

            var active = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = active;

            var png = texture.EncodeToJPG(80);
            UnityEngine.Object.Destroy(texture);
            target.Release();
            UnityEngine.Object.Destroy(target);

            var base64 = Convert.ToBase64String(png);
            const int chunk = 6000;
            for (int i = 0, n = 0; i < base64.Length; i += chunk, n++)
                Debug.Log($"CAPTURE {name} {n:000} {base64.Substring(i, Math.Min(chunk, base64.Length - i))}");
            Debug.Log($"CAPTURE {name} END");
        }
    }
}
