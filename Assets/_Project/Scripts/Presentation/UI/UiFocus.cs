using System.Collections.Generic;
using UnityEngine;

namespace Space.Presentation.UI
{
    /// <summary>
    /// Whether a full-screen menu has the focus: world controls (selection, orders, camera, power
    /// keys) ignore the mouse and keyboard while it is set. HUD panels also register their screen
    /// area each frame, so a click on one of their buttons is not taken as a click in space.
    /// </summary>
    public static class UiFocus
    {
        public static bool MenuOpen;

        /// <summary>A scripted sequence (level intro) is playing: no HUD, no controls.</summary>
        public static bool Cinematic;

        /// <summary>World controls (selection, orders, camera, power, time) are off.</summary>
        public static bool BlocksWorld => MenuOpen || Cinematic;

        static readonly List<Rect> HudRects = new();
        static int _hudFrame = -1;

        /// <summary>Registers a HUD panel drawn this frame, in GUI coordinates (origin top left).</summary>
        public static void AddHudRect(Rect guiRect)
        {
            if (_hudFrame != Time.frameCount)
            {
                _hudFrame = Time.frameCount;
                HudRects.Clear();
            }
            HudRects.Add(guiRect);
        }

        /// <summary>The pointer (screen coordinates, origin bottom left) is over a HUD panel.</summary>
        public static bool PointerOverHud(Vector2 screenPosition)
        {
            // Panels register from OnGUI, after Update: last frame's areas are the current ones.
            if (_hudFrame < Time.frameCount - 1)
                return false;
            var guiPoint = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            foreach (var rect in HudRects)
                if (rect.Contains(guiPoint))
                    return true;
            return false;
        }
    }
}
