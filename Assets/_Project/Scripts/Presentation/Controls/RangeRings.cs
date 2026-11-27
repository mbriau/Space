using UnityEngine;

namespace Space.Presentation.Controls
{
    /// <summary>
    /// Concentric distance rings on the game plane, centred on the player's main ship, so the player
    /// keeps a sense of position and distance whatever ship the camera follows. Labels show each radius.
    /// </summary>
    [RequireComponent(typeof(PlayerControls))]
    public class RangeRings : MonoBehaviour
    {
        [Tooltip("Ring radii in metres.")]
        public float[] Radii = { 100f, 250f, 500f, 1000f, 2000f };

        [Tooltip("Unlit transparent material with vertex colours (Sprites/Default works).")]
        public Material Material;

        public Color Color = new Color(0.35f, 0.75f, 1f, 0.35f);

        [Tooltip("Line width as a share of the camera distance, so rings look the same at any zoom.")]
        public float WidthPerMetreOfDistance = 0.0025f;

        [Min(16)] public int Segments = 192;

        PlayerControls _controls;
        Camera _camera;
        LineRenderer[] _rings;
        Transform _root;
        bool _visible;
        GUIStyle _label;

        void Awake()
        {
            _controls = GetComponent<PlayerControls>();
            _camera = GetComponent<Camera>();
            if (Material == null)
                Material = new Material(Shader.Find("Sprites/Default"));

            _root = new GameObject("Range Rings").transform;
            _rings = new LineRenderer[Radii.Length];
            for (var i = 0; i < Radii.Length; i++)
                _rings[i] = CreateRing(Radii[i], i);
        }

        void OnDestroy()
        {
            if (_root != null)
                Destroy(_root.gameObject);
        }

        LineRenderer CreateRing(float radius, int index)
        {
            var go = new GameObject($"Ring {radius:0} m");
            go.transform.SetParent(_root, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.sharedMaterial = Material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;

            // Outer rings fade a little so the closest distances read first.
            var color = Color;
            color.a *= Mathf.Lerp(1f, 0.5f, Radii.Length > 1 ? index / (float)(Radii.Length - 1) : 0f);
            line.startColor = line.endColor = color;

            line.positionCount = Segments;
            for (var s = 0; s < Segments; s++)
            {
                var angle = s * Mathf.PI * 2f / Segments;
                line.SetPosition(s, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return line;
        }

        void LateUpdate()
        {
            _visible = _controls.TryGetMainShipFrame(out var center, out var rotation, out _);
            _root.gameObject.SetActive(_visible);
            if (!_visible)
                return;

            // On the game plane, tilted with the course; turned with the ship's heading only.
            _root.SetPositionAndRotation(center, rotation);

            // The far side of a ring is about (camera distance + radius) away: size the line for an
            // average of near and far sides so big rings do not break into dashes at a grazing angle.
            var cameraDistance = Vector3.Distance(_camera.transform.position, center);
            for (var i = 0; i < _rings.Length; i++)
                _rings[i].widthMultiplier = (cameraDistance + Radii[i] * 0.6f) * WidthPerMetreOfDistance;
        }

        void OnGUI()
        {
            if (!_visible)
                return;

            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 12 };
            GUI.color = new Color(Color.r, Color.g, Color.b, 0.9f);

            // Label each ring where it crosses the camera's right-hand side on the plane.
            var right = Vector3.ProjectOnPlane(_camera.transform.right, _root.up).normalized;
            for (var i = 0; i < Radii.Length; i++)
            {
                var screen = _camera.WorldToScreenPoint(_root.position + right * Radii[i]);
                if (screen.z <= 0f)
                    continue;
                var text = Radii[i] >= 1000f ? $"{Radii[i] / 1000f:0.#} km" : $"{Radii[i]:0} m";
                GUI.Label(new Rect(screen.x + 4f, Screen.height - screen.y - 9f, 80f, 18f), text, _label);
            }
            GUI.color = Color.white;
        }
    }
}
