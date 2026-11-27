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
        public Color EnemyColor = new Color(1f, 0.35f, 0.3f, 0.85f);
        readonly System.Collections.Generic.List<Vector4> _enemies = new();

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
            // A level intro plays: the scene and the comms frames only.
            if (UiFocus.Cinematic)
                return;
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
                "Clic gauche / Tab : sélectionner   Clic droit sur un ennemi : imposer une cible   V : caméra libre / tactique   " +
                "Bouclier Q / moteurs R (Maj : retirer) · armes : sur leurs cartes");
            GUI.Label(new Rect(16, Screen.height - 28, Screen.width - 32, 22),
                "Molette : zoom   Milieu ou Alt + clic gauche : orbite   Espace : pause   1/2/3 : vitesse   " +
                "I : vaisseau   C : catalogue");

            if (Event.current.type == EventType.Repaint)
            {
                DrawHullBars();
                // Enemies are far away in the tactical view: always mark them.
                if (Controls != null)
                {
                    Controls.CollectEnemies(_enemies);
                    foreach (var e in _enemies)
                        DrawBrackets(new Vector3(e.x, e.y, e.z), e.w, EnemyColor);
                }
            }

            // Weapons of the player's ship, bottom left; reactor power, bottom right.
            const float powerWidth = 330f;
            WeaponPanel.Draw(Controls, new Vector2(16, Screen.height - 60), Screen.width - 48 - powerWidth);

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
            if (selection.HasAttackTarget)
                text += "\nCible imposée";
            var lines = selection.HasAttackTarget ? 6 : 5;
            var infoRect = new Rect(16, 70, 300, 18 * lines + 16);
            GUI.Box(infoRect, text, _panel);
            UiFocus.AddHudRect(infoRect);

            if (selection.HasPower)
            {
                var powerRect = new Rect(Screen.width - 16 - powerWidth, Screen.height - 60 - PowerPanelHeight, powerWidth, PowerPanelHeight);
                DrawPowerPanel(selection, powerRect);
                UiFocus.AddHudRect(powerRect);
            }

            DrawBrackets(selection.RenderPosition, selection.Radius, MarkerColor);
            if (selection.HasDestination)
                DrawBrackets(selection.DestinationRenderPosition, 0f, DestinationColor);
        }

        static readonly (PowerSystem System, string Label, string Key, Color Color)[] PowerRows =
        {
            (PowerSystem.Shield, "Bouclier", "Q", new Color(0.35f, 0.7f, 1f)),
            (PowerSystem.Engines, "Moteurs", "R", new Color(0.4f, 0.95f, 0.6f)),
        };

        static readonly Color WeaponsPowerColor = new(1f, 0.65f, 0.25f);

        /// <summary>Height of the power panel.</summary>
        const float PowerPanelHeight = 150f;

        /// <summary>
        /// Reactor output as cells coloured by consumer (shield, weapons, engines, free), then the
        /// shield and the engines as rows of cells with − and + buttons (enemy ships are shown
        /// read-only). Weapons are switched on and off on their cards.
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
                $"Réacteur : {grid.Allocated} / {grid.Reactor} utilisés · {grid.Free} libre{(grid.Free > 1 ? "s" : "")}");
            DrawReactorCells(new Rect(x, y + 24, area.width - 20, 14), grid);
            y += 44;

            // Legend of the reactor cells.
            var legendX = x;
            foreach (var (label, value, color) in new[]
                     {
                         ("Bouclier", grid.Shield, PowerRows[0].Color), ("Armes", grid.Weapons, WeaponsPowerColor),
                         ("Moteurs", grid.Engines, PowerRows[1].Color),
                     })
            {
                FillRect(new Rect(legendX, y + 6, 10, 10), color);
                GUI.Label(new Rect(legendX + 14, y, 90, 22), $"{label} {value}");
                legendX += 100;
            }
            GUI.color = Color.white;
            y += 30;

            foreach (var (system, label, key, color) in PowerRows)
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
                DrawCells(new Rect(x + 176, y + 5, 106, 12), capacity, grid.Get(system), color);
                y += 28;
            }
        }

        /// <summary>One cell per reactor unit: shield, then weapons, then engines, then the free units.</summary>
        static void DrawReactorCells(Rect area, PowerGrid grid)
        {
            var count = (int)grid.Reactor;
            if (count <= 0)
                return;
            const float gap = 3f;
            var width = (area.width - gap * (count - 1)) / count;
            for (var i = 0; i < count; i++)
            {
                var color = i < grid.Shield ? PowerRows[0].Color
                    : i < grid.Shield + grid.Weapons ? WeaponsPowerColor
                    : i < grid.Allocated ? PowerRows[1].Color
                    : new Color(1f, 1f, 1f, 0.12f);
                FillRect(new Rect(area.x + i * (width + gap), area.y, width, area.height), color);
            }
            GUI.color = Color.white;
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

        static readonly Color ShieldColor = new(0.35f, 0.7f, 1f);
        static readonly Color ShieldDownColor = new(0.6f, 0.62f, 0.7f);
        static readonly Color PlayerHullColor = new(0.3f, 1f, 0.5f);
        static readonly Color EnemyHullColor = new(1f, 0.35f, 0.25f);

        /// <summary>
        /// Shield and hull bars above every ship, sized with the ship on screen: the shield (blue) over
        /// the hull (green for the player's ships, red for enemies). A broken shield blinks grey, an
        /// unpowered one shows an empty frame, and a hull under a third blinks.
        /// </summary>
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
            var tick = TryGetTime(out var time) ? time.Tick : 0u;
            var blinkOn = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.35f;

            for (var i = 0; i < hulls.Length; i++)
            {
                var screen = _camera.WorldToScreenPoint(transforms[i].Position);
                if (screen.z <= 0f)
                    continue;

                var radiusPixels = Mathf.Max(12f, radii[i].Value * pixelsPerMetreAtUnitDistance / screen.z);
                var width = Mathf.Clamp(radiusPixels * 2.2f, 90f, 170f);
                const float hullHeight = 9f;
                const float shieldHeight = 7f;
                const float pad = 2f;
                var shield = shields[i];
                var hasShield = shield.Max > 0f;
                var total = hullHeight + (hasShield ? shieldHeight + pad : 0f);

                var x = screen.x - width * 0.5f;
                var bottom = Screen.height - screen.y - radiusPixels - 12f;
                var top = bottom - total;

                // Dark backing so the bars read over the stars and the ship.
                FillRect(new Rect(x - pad, top - pad, width + 2 * pad, total + 2 * pad), new Color(0f, 0f, 0f, 0.65f));

                var y = top;
                if (hasShield)
                {
                    var bar = new Rect(x, y, width, shieldHeight);
                    FillRect(bar, new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, 0.15f));
                    if (shield.BrokenUntilTick > tick)
                    {
                        if (blinkOn)
                            FillRect(bar, ShieldDownColor);
                    }
                    else if (shield.IsUp)
                    {
                        FillRect(new Rect(x, y, width * Mathf.Clamp01(shield.Current / shield.Max), shieldHeight), ShieldColor);
                    }
                    y += shieldHeight + pad;
                }

                var fraction = hulls[i].Max > 0f ? Mathf.Clamp01(hulls[i].Current / hulls[i].Max) : 0f;
                var hullColor = teams[i].Value == Team.Player ? PlayerHullColor : EnemyHullColor;
                FillRect(new Rect(x, y, width, hullHeight), new Color(hullColor.r, hullColor.g, hullColor.b, 0.15f));
                if (fraction > 1f / 3f || blinkOn)
                    FillRect(new Rect(x, y, width * fraction, hullHeight), hullColor);
            }
            GUI.color = Color.white;
        }

        static void FillRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
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
