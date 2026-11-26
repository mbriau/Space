using System;
using System.Collections;
using NUnit.Framework;
using Space.Presentation.Ships;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Space.PlayMode.Tests
{
    /// <summary>
    /// Opens the hangar in an empty scene, lets every ship fire at the target for a moment and captures
    /// it twice: with the game's tactical camera and with a close three-quarter view. The CI prints
    /// the images in its log (base64 between markers), like <see cref="TacticalViewCaptureTests"/>.
    /// </summary>
    public class HangarCaptureTests
    {
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureEveryShip()
        {
            // A fresh scene next to the test runner's own: unloading the runner's scene would stop
            // this coroutine for good (and with it the timeout), so it stays loaded.
            var scene = SceneManager.CreateScene("HangarTest");
            SceneManager.SetActiveScene(scene);

            var viewer = new GameObject("Hangar").AddComponent<ShipViewer>();
            yield return null;
            Assert.IsNotNull(viewer.Model, "aucun vaisseau affiché");
            Assert.IsNotNull(viewer.Range.Target, "pas de cible");

            for (var i = 0; i < ShipDesigns.All.Count; i++)
            {
                viewer.Select(i);
                viewer.Mode = ShipViewer.CameraMode.Tactical;
                var id = ShipDesigns.All[i].Id;
                // Long enough for the first shots to cross the 300 m to the target.
                yield return WaitSeconds(2.2f);
                Capture(viewer.ViewCamera, $"hangar-{id}-tactique");
                viewer.SetFreeCamera(140f, 18f, 2.6f);
                yield return null;
                Capture(viewer.ViewCamera, $"hangar-{id}-libre");
            }

            Assert.Greater(viewer.Range.ShotsFired, 10, "les tourelles n'ont presque pas tiré");
            UnityEngine.Object.Destroy(viewer.ViewCamera.gameObject);
            UnityEngine.Object.Destroy(viewer.gameObject);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            // Real time, bounded by a frame count too, so a stalled clock cannot hang the CI.
            var waited = 0f;
            for (var frames = 0; waited < seconds && frames < 2000; frames++)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static void Capture(Camera camera, string name)
        {
            Assert.IsNotNull(camera, "aucune caméra");
            const int width = 960, height = 540;
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

            var jpg = texture.EncodeToJPG(85);
            UnityEngine.Object.Destroy(texture);
            target.Release();
            UnityEngine.Object.Destroy(target);

            var base64 = Convert.ToBase64String(jpg);
            const int chunk = 6000;
            for (int i = 0, n = 0; i < base64.Length; i += chunk, n++)
                Debug.Log($"CAPTURE {name} {n:000} {base64.Substring(i, Math.Min(chunk, base64.Length - i))}");
            Debug.Log($"CAPTURE {name} END");
        }
    }
}
