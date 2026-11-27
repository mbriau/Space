using System.Collections.Generic;
using UnityEngine;

namespace Space.Presentation.Controls
{
    /// <summary>
    /// Placement of the tactical camera, shared by the game (<see cref="ShipCameraRig"/>) and the
    /// hangar's firing range: over the shoulder of our ship, on the side away from the enemies,
    /// slightly above and astern, looking past it toward them, then backed off just enough for our
    /// ship and every enemy to fit on screen.
    /// </summary>
    public static class TacticalFraming
    {
        public struct Settings
        {
            /// <summary>Distance behind our ship, in ship radii.</summary>
            public float Back;
            /// <summary>Height above our ship, in ship radii.</summary>
            public float Height;
            /// <summary>Offset toward the stern, in ship radii.</summary>
            public float Astern;
            /// <summary>Where the camera looks, between our ship (0) and the enemies (1).</summary>
            public float LookShare;
            /// <summary>Share of the half field of view the framed points may use.</summary>
            public float Margin;

            public static Settings Default => new() { Back = 1.7f, Height = 1f, Astern = 0.4f, LookShare = 0.3f, Margin = 0.85f };
        }

        /// <param name="enemies">Enemy positions (xyz) and radii (w), in render space.</param>
        /// <param name="zoom">Wheel factor: 1 by default, larger is further.</param>
        public static void Place(Vector3 ship, Quaternion shipRotation, float radius, IReadOnlyList<Vector4> enemies,
            float fieldOfView, float aspect, float zoom, in Settings settings, out Vector3 position, out Quaternion rotation)
            => Place(ship, shipRotation, radius, enemies, fieldOfView, aspect, zoom, settings, Vector3.up, out position, out rotation);

        /// <param name="up">Up of the game plane as the camera sees it (tilted when the course banks).</param>
        public static void Place(Vector3 ship, Quaternion shipRotation, float radius, IReadOnlyList<Vector4> enemies,
            float fieldOfView, float aspect, float zoom, in Settings settings, Vector3 up, out Vector3 position, out Quaternion rotation)
        {
            up = up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.up;
            var forward = Vector3.ProjectOnPlane(shipRotation * Vector3.forward, up);
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.ProjectOnPlane(Vector3.forward, up).normalized;

            // Direction of the fight, on the plane: toward the enemies' centre, or ahead if none.
            var toEnemies = forward * radius * 8f;
            if (enemies.Count > 0)
            {
                var centre = Vector3.zero;
                foreach (var e in enemies)
                    centre += new Vector3(e.x, e.y, e.z);
                centre /= enemies.Count;
                toEnemies = Vector3.ProjectOnPlane(centre - ship, up);
                if (toEnemies.sqrMagnitude < 1f)
                    toEnemies = forward * radius * 8f;
            }
            var direction = toEnemies.normalized;

            var anchor = ship - direction * radius * settings.Back * zoom - forward * radius * settings.Astern * zoom
                         + up * radius * settings.Height * zoom;
            var lookAt = ship + toEnemies * settings.LookShare;

            var halfV = fieldOfView * 0.5f * Mathf.Deg2Rad * settings.Margin;
            var halfH = Mathf.Atan(Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect) * settings.Margin;
            position = anchor;
            rotation = Quaternion.LookRotation(lookAt - position, up);
            for (var i = 0; i < 24 && !Fits(position, rotation, ship, radius, enemies, halfH, halfV); i++)
            {
                position += (anchor - lookAt).normalized * radius * 0.6f + up * radius * 0.1f;
                rotation = Quaternion.LookRotation(lookAt - position, up);
            }
        }

        static bool Fits(Vector3 position, Quaternion rotation, Vector3 ship, float radius, IReadOnlyList<Vector4> enemies, float halfH, float halfV)
        {
            var inverse = Quaternion.Inverse(rotation);
            bool Inside(Vector3 point, float pointRadius)
            {
                var local = inverse * (point - position);
                if (local.z <= pointRadius)
                    return false;
                var h = Mathf.Atan2(Mathf.Abs(local.x) + pointRadius, local.z);
                var v = Mathf.Atan2(Mathf.Abs(local.y) + pointRadius, local.z);
                return h <= halfH && v <= halfV;
            }

            if (!Inside(ship, radius))
                return false;
            foreach (var e in enemies)
                if (!Inside(new Vector3(e.x, e.y, e.z), e.w))
                    return false;
            return true;
        }
    }
}
