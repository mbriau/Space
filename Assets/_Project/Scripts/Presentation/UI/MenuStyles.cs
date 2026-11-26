using UnityEngine;

namespace Space.Presentation.UI
{
    /// <summary>Shared IMGUI styles of the menus (provisional UI until the UI Toolkit pass).</summary>
    public static class MenuStyles
    {
        public static GUIStyle Title, Heading, Text, Small, Muted, Button, Tab, TabActive, Slot, SlotSelected, Price, PriceTooHigh, Panel;

        public static readonly Color Background = new(0.03f, 0.04f, 0.08f, 0.96f);
        public static readonly Color PanelColor = new(0.08f, 0.1f, 0.16f, 0.95f);
        public static readonly Color Accent = new(0.35f, 0.75f, 1f);
        public static readonly Color Warning = new(1f, 0.45f, 0.35f);

        static Texture2D _white;

        public static void Ensure()
        {
            if (Title != null)
                return;
            Title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            Heading = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
            Text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            Small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            Muted = new GUIStyle(Small) { normal = { textColor = new Color(0.65f, 0.7f, 0.78f) } };
            Button = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(10, 10, 6, 6) };
            Tab = new GUIStyle(GUI.skin.button) { fontSize = 15, fixedHeight = 32, padding = new RectOffset(14, 14, 4, 4) };
            TabActive = new GUIStyle(Tab) { fontStyle = FontStyle.Bold, normal = { textColor = Accent }, hover = { textColor = Accent } };
            Slot = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 13, wordWrap = true, padding = new RectOffset(6, 6, 4, 4), imagePosition = ImagePosition.ImageLeft };
            SlotSelected = new GUIStyle(Slot) { fontStyle = FontStyle.Bold, normal = { textColor = Accent }, hover = { textColor = Accent } };
            Price = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            PriceTooHigh = new GUIStyle(Price) { normal = { textColor = Warning } };
            Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
        }

        public static void Fill(Rect rect, Color color)
        {
            if (_white == null)
                _white = Texture2D.whiteTexture;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
        }
    }
}
