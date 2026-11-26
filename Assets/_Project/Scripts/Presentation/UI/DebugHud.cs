using Space.Core.Time;
using Space.Presentation.Controls;
using Unity.Entities;
using UnityEngine;

namespace Space.Presentation.UI
{
    /// <summary>
    /// Temporary HUD drawn with IMGUI until the UI Toolkit interface exists:
    /// pause and speed indicator (US7), selected ship panel and marker (US2), destination marker.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DebugHud : MonoBehaviour
    {
        public PlayerControls Controls;
        public Color MarkerColor = new Color(0.3f, 1f, 0.5f, 0.9f);
        public Color DestinationColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        Camera _camera;
        World _world;
        EntityQuery _time;
        GUIStyle _big;
        GUIStyle _panel;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (Controls == null)
                Controls = GetComponent<PlayerControls>();
        }

        bool TryGetTime(out SimulationTime time)
        {
            time = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;
            if (world != _world)
            {
                _world = world;
                _time = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SimulationTime>());
            }
            if (_time.IsEmpty)
                return false;
            time = _time.GetSingleton<SimulationTime>();
            return true;
        }

        void OnGUI()
        {
            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _panel ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, padding = new RectOffset(10, 10, 8, 8) };

            if (TryGetTime(out var time))
            {
                var label = time.Paused ? "PAUSE" : $"x{time.TimeScale:0}";
                GUI.color = time.Paused ? new Color(1f, 0.6f, 0.2f) : Color.white;
                GUI.Label(new Rect(16, 10, 300, 32), label, _big);
                GUI.color = Color.white;
                GUI.Label(new Rect(16, 40, 400, 22), $"Tick {time.Tick}   Temps {time.ElapsedTime:0.0} s");
            }

            GUI.Label(new Rect(16, Screen.height - 28, Screen.width - 32, 22),
                "Clic gauche / Tab : sélectionner   Clic droit : déplacer (Maj : ajouter)   Molette : zoom   " +
                "Milieu ou Alt + clic gauche : orbite   Espace : pause   1/2/3 : vitesse");

            if (Controls == null || !Controls.TryGetSelection(out var selection))
                return;

            GUI.Box(new Rect(16, Screen.height - 120, 260, 84),
                $"{selection.Name}\nVitesse : {selection.Speed:0.0} m/s\nOrdres en file : {selection.OrderCount}", _panel);

            DrawBrackets(selection.RenderPosition, selection.Radius, MarkerColor);
            if (selection.HasDestination)
                DrawBrackets(selection.DestinationRenderPosition, 0f, DestinationColor);
        }

        /// <summary>Square brackets around a world point, sized from its radius (minimum 12 px).</summary>
        void DrawBrackets(Vector3 worldPosition, float radius, Color color)
        {
            var screen = _camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f)
                return;

            var pixelsPerMetre = Screen.height * 0.5f / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / screen.z;
            var half = Mathf.Max(12f, radius * pixelsPerMetre);
            var x = screen.x;
            var y = Screen.height - screen.y;
            var length = Mathf.Max(6f, half * 0.4f);
            const float thickness = 2f;

            GUI.color = color;
            // Top-left, top-right, bottom-left, bottom-right corners.
            Corner(x - half, y - half, length, thickness, 1, 1);
            Corner(x + half, y - half, length, thickness, -1, 1);
            Corner(x - half, y + half, length, thickness, 1, -1);
            Corner(x + half, y + half, length, thickness, -1, -1);
            GUI.color = Color.white;
        }

        static void Corner(float x, float y, float length, float thickness, int dirX, int dirY)
        {
            var horizontal = new Rect(dirX > 0 ? x : x - length, dirY > 0 ? y : y - thickness, length, thickness);
            var vertical = new Rect(dirX > 0 ? x : x - thickness, dirY > 0 ? y : y - length, thickness, length);
            GUI.DrawTexture(horizontal, Texture2D.whiteTexture);
            GUI.DrawTexture(vertical, Texture2D.whiteTexture);
        }
    }
}
