using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// How the layers of the space backdrop scroll. Each layer is a texture tiled over the view; it
    /// slides opposite to the camera's travel (near layers faster: parallax), turns with the camera
    /// like a distant sky and rolls against the camera's roll. Pure functions, unit tested.
    /// </summary>
    public static class BackdropMath
    {
        /// <summary>
        /// Texture offset for a camera move of <paramref name="travel"/> metres: the layer slides the
        /// other way, one texture tile every <paramref name="metresPerTile"/> metres, along the camera's
        /// right and up axes (a move along the view axis does not scroll it).
        /// </summary>
        public static Vector2 TravelScroll(Vector3 travel, Vector3 cameraRight, Vector3 cameraUp, float metresPerTile)
        {
            if (metresPerTile <= 0f)
                return Vector2.zero;
            return new Vector2(Vector3.Dot(travel, cameraRight), Vector3.Dot(travel, cameraUp)) / metresPerTile;
        }

        /// <summary>
        /// Texture offset for a camera turn (yaw, pitch in degrees): turning by a whole field of view
        /// moves the layer by the tiles that fill the view, so the sky stays put as the camera turns.
        /// </summary>
        public static Vector2 TurnScroll(float yawDegrees, float pitchDegrees, float horizontalFov, float verticalFov, Vector2 tilesInView)
        {
            var x = horizontalFov > 0f ? yawDegrees / horizontalFov * tilesInView.x : 0f;
            var y = verticalFov > 0f ? -pitchDegrees / verticalFov * tilesInView.y : 0f;
            return new Vector2(x, y);
        }

        /// <summary>
        /// A screen-space scroll expressed along a layer turned by <paramref name="layerRollDegrees"/>
        /// on screen (the layers turn against the camera's roll to stay level with the world).
        /// </summary>
        public static Vector2 ToLayer(Vector2 screen, float layerRollDegrees)
        {
            var a = layerRollDegrees * Mathf.Deg2Rad;
            var c = Mathf.Cos(a);
            var s = Mathf.Sin(a);
            return new Vector2(screen.x * c + screen.y * s, -screen.x * s + screen.y * c);
        }

        /// <summary>Side of a square layer that still covers a view of this size whatever its roll.</summary>
        public static float RollCover(float width, float height) => Mathf.Sqrt(width * width + height * height);

        /// <summary>Keeps an offset in [0, 1) so it never loses float precision over a long flight.</summary>
        public static Vector2 Wrap(Vector2 offset) => new(offset.x - Mathf.Floor(offset.x), offset.y - Mathf.Floor(offset.y));

        /// <summary>Horizontal field of view in degrees from the vertical one and the aspect ratio.</summary>
        public static float HorizontalFov(float verticalFov, float aspect) =>
            2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
    }
}
