using Space.Core.Combat;
using Space.Core.Ships;
using Space.Core.Time;
using Space.Presentation.Controls;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
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
        World _hullWorld;
        EntityQuery _hullQuery;
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

            GUI.Label(new Rect(16, Screen.height - 48, Screen.width - 32, 22),
                "Clic gauche / Tab : sélectionner   Clic droit : déplacer (Maj : ajouter) ou imposer une cible   " +
                "F : mode de tir   Puissance : Q W E R (Maj : retirer)");
            GUI.Label(new Rect(16, Screen.height - 28, Screen.width - 32, 22),
                "Molette : zoom   Milieu ou Alt + clic gauche : orbite   Espace : pause   1/2/3 : vitesse");

            if (Event.current.type == EventType.Repaint)
                DrawHullBars();

            if (Controls == null || !Controls.TryGetSelection(out var selection))
                return;

            var shieldText = selection.ShieldMax <= 0f ? "aucun"
                : selection.ShieldBrokenSeconds > 0f ? $"BRISÉ ({selection.ShieldBrokenSeconds:0} s)"
                : !selection.ShieldUp ? "hors tension"
                : $"{selection.Shield:0} / {selection.ShieldMax:0}";
            var text = $"{selection.Name}{(selection.IsPlayerShip ? "" : " (ennemi)")}\n" +
                       $"Coque : {selection.Hull:0} / {selection.HullMax:0}\n" +
                       $"Bouclier : {shieldText}\n" +
                       $"Vitesse : {selection.Speed:0.0} m/s · ordres : {selection.OrderCount}";
            if (selection.WeaponCount > 0)
                text += $"\n{selection.WeaponSummary}\nTir {FireModeLabel(selection.FireMode)}" +
                        (selection.HasAttackTarget ? " · cible imposée" : "");
            var lines = 5 + selection.WeaponCount;
            var infoHeight = 18 * lines + 16;
            var infoRect = new Rect(16, Screen.height - 64 - infoHeight, 320, infoHeight);
            GUI.Box(infoRect, text, _panel);

            if (selection.HasPower)
                DrawPowerPanel(selection, new Rect(Screen.width - 16 - 330, Screen.height - 64 - 176, 330, 176));

            DrawBrackets(selection.RenderPosition, selection.Radius, MarkerColor);
            if (selection.HasDestination)
                DrawBrackets(selection.DestinationRenderPosition, 0f, DestinationColor);
        }

        static readonly (PowerSystem System, string Label, string Key)[] PowerRows =
        {
            (PowerSystem.Shield, "Bouclier", "Q"),
            (PowerSystem.EnergyWeapons, "Armes à énergie", "W"),
            (PowerSystem.PhysicalWeapons, "Armes physiques", "E"),
            (PowerSystem.Engines, "Moteurs", "R"),
        };

        /// <summary>
        /// Reactor output and the power of each subsystem as rows of cells; − and + buttons move one
        /// unit (enemy ships are shown read-only).
        /// </summary>
        void DrawPowerPanel(PlayerControls.SelectionInfo selection, Rect area)
        {
            var grid = selection.Power;
            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.85f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var x = area.x + 10;
            var y = area.y + 8;
            GUI.Label(new Rect(x, y, area.width - 20, 22),
                $"Réacteur : {grid.Allocated} / {grid.Reactor} utilisés · {grid.Free} libre(s)");
            DrawCells(new Rect(x, y + 22, area.width - 20, 10), grid.Reactor, grid.Allocated, new Color(1f, 0.8f, 0.3f));
            y += 42;

            foreach (var (system, label, key) in PowerRows)
            {
                var capacity = grid.Capacity(system);
                if (capacity == 0)
                    continue;

                GUI.Label(new Rect(x, y, 150, 22), $"{label} [{key}]");
                if (selection.IsPlayerShip)
                {
                    if (GUI.Button(new Rect(x + 150, y + 1, 22, 20), "−"))
                        Controls.ChangePower(system, -1);
                    if (GUI.Button(new Rect(x + 286, y + 1, 22, 20), "+"))
                        Controls.ChangePower(system, +1);
                }
                DrawCells(new Rect(x + 176, y + 5, 106, 12), capacity, grid.Get(system), new Color(0.35f, 0.8f, 1f));
                y += 28;
            }
        }

        /// <summary>A row of <paramref name="count"/> cells, the first <paramref name="filled"/> lit.</summary>
        static void DrawCells(Rect area, int count, int filled, Color color)
        {
            if (count <= 0)
                return;
            const float gap = 3f;
            var width = (area.width - gap * (count - 1)) / count;
            for (var i = 0; i < count; i++)
            {
                GUI.color = i < filled ? color : new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(new Rect(area.x + i * (width + gap), area.y, width, area.height), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        static string FireModeLabel(FireMode mode) => mode switch
        {
            FireMode.TriggerHappy => "nerveux",
            FireMode.Conservative => "prudent",
            _ => "normal",
        };

        /// <summary>A small hull bar above every ship: green for the player's ships, red for enemies.</summary>
        void DrawHullBars()
        {
            if (_world == null || !_world.IsCreated)
                return;
            if (_hullQuery == default || _hullWorld != _world)
            {
                _hullWorld = _world;
                _hullQuery = _world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Hull>(), ComponentType.ReadOnly<Team>(),
                                                                    ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<ShipRadius>(),
                                                                    ComponentType.ReadOnly<Shield>());
            }

            using var hulls = _hullQuery.ToComponentDataArray<Hull>(Allocator.Temp);
            using var shields = _hullQuery.ToComponentDataArray<Shield>(Allocator.Temp);
            using var teams = _hullQuery.ToComponentDataArray<Team>(Allocator.Temp);
            using var transforms = _hullQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            using var radii = _hullQuery.ToComponentDataArray<ShipRadius>(Allocator.Temp);
            var pixelsPerMetreAtUnitDistance = Screen.height * 0.5f / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            for (var i = 0; i < hulls.Length; i++)
            {
                var screen = _camera.WorldToScreenPoint(transforms[i].Position);
                if (screen.z <= 0f)
                    continue;

                var radiusPixels = Mathf.Max(12f, radii[i].Value * pixelsPerMetreAtUnitDistance / screen.z);
                const float width = 36f;
                const float height = 4f;
                var x = screen.x - width * 0.5f;
                var y = Screen.height - screen.y - radiusPixels - 10f;
                var fraction = hulls[i].Max > 0f ? Mathf.Clamp01(hulls[i].Current / hulls[i].Max) : 0f;

                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(x - 1, y - 1, width + 2, height + 2), Texture2D.whiteTexture);
                GUI.color = teams[i].Value == Team.Player ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.35f, 0.25f);
                GUI.DrawTexture(new Rect(x, y, width * fraction, height), Texture2D.whiteTexture);

                // Shield bar just above the hull bar.
                var shield = shields[i];
                if (shield.Max > 0f && shield.IsUp)
                {
                    GUI.color = new Color(0.4f, 0.65f, 1f);
                    GUI.DrawTexture(new Rect(x, y - height - 2f, width * Mathf.Clamp01(shield.Current / shield.Max), height), Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;
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
