using Space.Presentation.Characters;
using UnityEngine;

namespace Space.Presentation.Game
{
    /// <summary>
    /// One speaker on the comms: their talking portrait, set apart far below the battle and filmed by
    /// its own camera into a texture, and the frame that shows it in a top corner of the screen.
    /// </summary>
    public sealed class CommsChannel
    {
        public enum Side { Left, Right }

        /// <summary>Far below the battle, out of the main camera's reach; scaled up for float precision.</summary>
        const float Depth = -20000f;
        const float Scale = 100f;

        public readonly PortraitRig Rig;
        public readonly RenderTexture Texture;
        public readonly string Name;
        public readonly string Role;
        public readonly Side Corner;
        public readonly Color Accent;

        readonly GameObject _root;
        float _since = float.MaxValue;
        bool _shown;

        public CommsChannel(GameObject portraitPrefab, Side corner, float slot, string name, string role, Color accent)
        {
            Name = name;
            Role = role;
            Corner = corner;
            Accent = accent;

            _root = new GameObject($"Comms {name}");
            _root.transform.position = new Vector3(slot * Scale * 3f, Depth, 0f);
            var portrait = Object.Instantiate(portraitPrefab, _root.transform);
            portrait.transform.localPosition = Vector3.zero;
            portrait.transform.localScale = Vector3.one * Scale;
            Rig = portrait.GetComponent<PortraitRig>();
            Rig.AutoPlay = false;

            Texture = new RenderTexture(512, 512, 16) { name = $"Comms {name}" };
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(_root.transform, false);
            camera.transform.localPosition = new Vector3(0f, 0f, -5f * Scale);
            camera.orthographic = true;
            camera.orthographicSize = 0.5f * Scale;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 10f * Scale;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = Texture;
        }

        /// <summary>The frame drops into its corner, then the picture fades in.</summary>
        public void Show()
        {
            if (_shown)
                return;
            _shown = true;
            _since = 0f;
        }

        /// <summary>The picture fades out, then the frame rises away.</summary>
        public void Hide()
        {
            if (!_shown)
                return;
            _shown = false;
            _since = 0f;
        }

        public bool Visible => _shown;

        public void Tick(float deltaTime) => _since += deltaTime;

        /// <summary>Drop (0 hidden, 1 in place) and picture visibility right now.</summary>
        public (float Drop, float Picture) State => _shown ? IntroMath.FrameIn(_since) : IntroMath.FrameOut(_since);

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root);
            if (Texture != null)
            {
                Texture.Release();
                Object.Destroy(Texture);
            }
        }
    }
}
