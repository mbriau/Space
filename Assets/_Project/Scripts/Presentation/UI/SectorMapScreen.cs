using System;
using Space.Core.Sectors;
using Space.Presentation.Game;
using UnityEngine;

namespace Space.Presentation.UI
{
    /// <summary>
    /// The sector map between two levels (provisional IMGUI, drawn by <see cref="LevelRunner"/>):
    /// beacons in columns from the start (left) to the exit (right), the links between them, the
    /// delivery deadline. A click on a reachable beacon jumps there.
    /// </summary>
    public sealed class SectorMapScreen
    {
        public const float Width = 820f;
        public const float Height = 520f;
        const float Node = 46f;

        static readonly Color LinkColor = new(0.45f, 0.5f, 0.6f, 0.8f);
        static readonly Color ReachableLink = new(1f, 0.85f, 0.4f, 0.95f);
        static readonly Color CombatColor = new(0.75f, 0.8f, 0.9f);
        static readonly Color EliteColor = new(1f, 0.6f, 0.25f);
        static readonly Color ShopColor = new(0.45f, 0.9f, 0.5f);
        static readonly Color ExitColor = new(0.4f, 0.75f, 1f);
        static readonly Color CurrentColor = new(1f, 0.9f, 0.35f);

        GUIStyle _node;
        GUIStyle _caption;

        /// <summary>Draws the map in <paramref name="area"/>; returns the beacon clicked, or -1.</summary>
        public int Draw(Rect area, RunState run, LevelPool pool)
        {
            MenuStyles.Ensure();
            _node ??= new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _caption ??= new GUIStyle(MenuStyles.Small) { alignment = TextAnchor.UpperCenter, wordWrap = true };

            var sector = run.Sector;
            var map = sector.Map;
            var columns = Math.Max(2, map.Columns);
            var field = new Rect(area.x + 50f, area.y + 30f, area.width - 100f, area.height - 80f);
            Vector2 Position(Beacon b) => new(
                field.x + field.width * b.Column / (columns - 1),
                field.y + field.height * b.Row);

            // Links first, under the beacons.
            if (Event.current.type == EventType.Repaint)
            {
                for (var i = 0; i < map.Beacons.Count; i++)
                    foreach (var to in map.Beacons[i].Links)
                    {
                        var reachable = i == sector.Current && sector.CanJump(to);
                        Line(Position(map.Beacons[i]), Position(map.Beacons[to]), reachable ? ReachableLink : LinkColor, reachable ? 3f : 1.5f);
                    }
            }

            var clicked = -1;
            for (var i = 0; i < map.Beacons.Count; i++)
            {
                var beacon = map.Beacons[i];
                var center = Position(beacon);
                var rect = new Rect(center.x - Node * 0.5f, center.y - Node * 0.5f, Node, Node);
                var known = sector.IsKindKnown(i);
                var visited = sector.Visited.Contains(i);
                var canJump = sector.CanJump(i);

                if (i == sector.Current)
                    MenuStyles.Fill(new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f), CurrentColor);

                var previousColor = GUI.backgroundColor;
                var previousEnabled = GUI.enabled;
                GUI.backgroundColor = known ? KindColor(beacon.Kind) : CombatColor;
                if (visited && i != sector.Current)
                    GUI.backgroundColor *= 0.55f;
                GUI.enabled = canJump || i == sector.Current;
                if (GUI.Button(rect, new GUIContent(known ? Letter(beacon.Kind) : "?", Tooltip(beacon, known, pool)), _node) && canJump)
                    clicked = i;
                GUI.enabled = previousEnabled;
                GUI.backgroundColor = previousColor;

                GUI.Label(new Rect(center.x - 60f, rect.yMax + 2f, 120f, 34f), pool.Environment(beacon), _caption);
            }

            // Hovered beacon, then the legend.
            var legend = new Rect(area.x + 16f, area.yMax - 40f, area.width - 32f, 40f);
            GUI.Label(new Rect(legend.x, legend.y - 22f, legend.width, 22f), GUI.tooltip, MenuStyles.Text);
            GUI.Label(legend, "D : départ · C : combat · E : élite (plus dur, mieux payé) · M : magasin · S : sortie (livraison) · ? : inconnu. " +
                              "Les liens en jaune sont tes destinations possibles.", MenuStyles.Muted);
            return clicked;
        }

        static string Letter(BeaconKind kind) => kind switch
        {
            BeaconKind.Start => "D",
            BeaconKind.Elite => "E",
            BeaconKind.Shop => "M",
            BeaconKind.Exit => "S",
            _ => "C",
        };

        static Color KindColor(BeaconKind kind) => kind switch
        {
            BeaconKind.Elite => EliteColor,
            BeaconKind.Shop => ShopColor,
            BeaconKind.Exit => ExitColor,
            _ => CombatColor,
        };

        static string Tooltip(Beacon beacon, bool known, LevelPool pool)
        {
            if (!known)
                return $"{pool.Environment(beacon)} : on ne sait pas encore ce qui t'y attend.";
            var level = pool.ForBeacon(beacon);
            return beacon.Kind switch
            {
                BeaconKind.Shop => "Station marchande : magasin et revente, sans combat.",
                BeaconKind.Start => "Point d'entrée du secteur.",
                BeaconKind.Exit => $"Sortie : {level?.DisplayName}. La gagner livre la cargaison.",
                BeaconKind.Elite => $"Élite : {level?.DisplayName}, ennemis doublés, récompense doublée et plus rare.",
                _ => $"Combat : {level?.DisplayName}.",
            };
        }

        /// <summary>A straight line, drawn as a rotated strip.</summary>
        static void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            var matrix = GUI.matrix;
            var delta = b - a;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, a);
            MenuStyles.Fill(new Rect(a.x, a.y - width * 0.5f, delta.magnitude, width), color);
            GUI.matrix = matrix;
        }
    }
}
