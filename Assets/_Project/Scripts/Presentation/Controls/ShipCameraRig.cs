using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using Space.Presentation.UI;

namespace Space.Presentation.Controls
{
    /// <summary>
    /// Camera of the tactical combat (default): close over the shoulder of the player's ship, on the
    /// side away from the enemies, framing our ship tightly in the foreground while keeping every
    /// enemy on screen. The wheel brings it closer or further. V switches to the free orbit camera
    /// around the selected ship (M1, US1): middle mouse drag (or Alt + left drag) orbits, the wheel
    /// zooms from 20 m to 2 km.
    /// Ship positions are already relative to the floating origin, so the camera stays precise
    /// however far the ship travels.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(PlayerControls))]
    public class ShipCameraRig : MonoBehaviour
    {
        [Min(1f)] public float MinDistance = 20f;
        [Min(1f)] public float MaxDistance = 2000f;
        public float Distance = 150f;
        public float Yaw = 30f;
        [Range(-89f, 89f)] public float Pitch = 25f;

        [Tooltip("Degrees per pixel of mouse movement.")]
        public float OrbitSpeed = 0.25f;

        [Tooltip("Zoom factor per wheel notch.")]
        [Min(1.01f)] public float ZoomStep = 1.15f;

        [Tooltip("How quickly the zoom reaches its target (higher is snappier).")]
        public float ZoomSharpness = 12f;

        [Tooltip("First framing: camera distance in ship radii, wide enough to see the fight around the ship.")]
        [Min(1f)] public float FirstFramingRadii = 12f;

        [Tooltip("Duration of the camera glide from one ship to the next, in seconds.")]
        [Min(0f)] public float TransitionDuration = 0.5f;

        [Tooltip("How much the zoom follows the size of a newly selected ship (0 = not at all, 1 = proportionally).")]
        [Range(0f, 1f)] public float SizeInfluence = 0.25f;

        [Tooltip("Largest zoom change caused by selecting another ship (only the player zooms further).")]
        [Min(1f)] public float MaxAutoZoomFactor = 1.4f;

        public enum Mode { Tactical, Orbit }

        [Header("Caméra tactique")]
        public Mode CameraMode = Mode.Tactical;

        [Tooltip("Distance behind our ship, in ship radii (before the wheel factor).")]
        [Min(1f)] public float TacticalBack = 3.4f;

        [Tooltip("Height above our ship, in ship radii.")]
        public float TacticalHeight = 1.1f;

        [Tooltip("Offset toward the stern, in ship radii: the camera looks along the hull a little.")]
        public float TacticalAstern = 1.2f;

        [Tooltip("Where the camera looks, between our ship (0) and the enemies (1).")]
        [Range(0f, 1f)] public float TacticalLookShare = 0.35f;

        [Tooltip("Share of the half field of view the framed points may use (margin at the screen edge).")]
        [Range(0.5f, 1f)] public float TacticalMargin = 0.85f;

        [Tooltip("How quickly the tactical camera follows its ideal placement.")]
        public float TacticalSharpness = 3f;

        PlayerControls _controls;
        Camera _camera;
        float _tacticalZoom = 1f;
        bool _tacticalPlaced;
        Vector3 _tacticalPosition;
        Quaternion _tacticalRotation = Quaternion.identity;
        readonly System.Collections.Generic.List<Vector4> _enemies = new();
        Vector3 _focus;
        float _targetDistance;
        Entity _framedEntity;
        float _framedRadius;
        Vector3 _transitionStart;
        float _transitionTime = float.MaxValue;

        void Awake()
        {
            _controls = GetComponent<PlayerControls>();
            _camera = GetComponent<Camera>();
            _targetDistance = Distance;
        }

        void LateUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && !UiFocus.MenuOpen && keyboard.vKey.wasPressedThisFrame)
            {
                CameraMode = CameraMode == Mode.Tactical ? Mode.Orbit : Mode.Tactical;
                _tacticalPlaced = false;
            }

            if (CameraMode == Mode.Tactical && _controls.TryGetMainShipFrame(out var shipPosition, out var shipRotation, out var shipRadius))
            {
                UpdateTactical(shipPosition, shipRotation, shipRadius);
                return;
            }

            if (_controls.TryGetSelection(out var selection))
            {
                if (selection.Entity != _framedEntity)
                    OnSelectionChanged(selection);

                // Glide from the previous focus to the new ship, then follow it exactly.
                _transitionTime += Time.unscaledDeltaTime;
                var t = TransitionDuration > 0f ? Mathf.Clamp01(_transitionTime / TransitionDuration) : 1f;
                _focus = Vector3.Lerp(_transitionStart, selection.RenderPosition, Mathf.SmoothStep(0f, 1f, t));
            }

            ReadInput();

            Distance = Mathf.Lerp(Distance, _targetDistance, 1f - Mathf.Exp(-ZoomSharpness * Time.unscaledDeltaTime));
            var rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * Distance + ShakeOffset(), rotation);
        }

        /// <summary>
        /// Over-the-shoulder framing: the camera sits behind our ship, on the side away from the
        /// enemies, slightly above and astern, and looks past it toward them. It then backs off just
        /// enough for every enemy (and our whole ship) to fit on screen.
        /// </summary>
        void UpdateTactical(Vector3 ship, Quaternion shipRotation, float radius)
        {
            if (!UiFocus.MenuOpen)
            {
                var scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
                if (scroll > 0f) _tacticalZoom /= ZoomStep;
                else if (scroll < 0f) _tacticalZoom *= ZoomStep;
                _tacticalZoom = Mathf.Clamp(_tacticalZoom, 0.5f, 6f);
            }

            _controls.CollectEnemies(_enemies);
            var forward = shipRotation * Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;

            // Direction of the fight, on the plane: toward the enemies' centre, or ahead if none.
            var toEnemies = forward * radius * 8f;
            if (_enemies.Count > 0)
            {
                var centre = Vector3.zero;
                foreach (var e in _enemies)
                    centre += new Vector3(e.x, e.y, e.z);
                centre /= _enemies.Count;
                toEnemies = centre - ship;
                toEnemies.y = 0f;
                if (toEnemies.sqrMagnitude < 1f)
                    toEnemies = forward * radius * 8f;
            }
            var direction = toEnemies.normalized;

            var back = radius * TacticalBack * _tacticalZoom;
            var anchor = ship - direction * back - forward * radius * TacticalAstern * _tacticalZoom
                         + Vector3.up * radius * TacticalHeight * _tacticalZoom;
            var lookAt = ship + toEnemies * TacticalLookShare;

            // Back off (and rise a little) until our ship and every enemy fit in the view.
            var fov = _camera != null ? _camera.fieldOfView : 60f;
            var aspect = _camera != null ? _camera.aspect : 16f / 9f;
            var halfV = fov * 0.5f * Mathf.Deg2Rad * TacticalMargin;
            var halfH = Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * aspect) * TacticalMargin;
            var position = anchor;
            var rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            for (var i = 0; i < 24 && !Fits(position, rotation, ship, radius, halfH, halfV); i++)
            {
                position += (anchor - lookAt).normalized * radius * 0.6f + Vector3.up * radius * 0.1f;
                rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            }

            if (!_tacticalPlaced)
            {
                _tacticalPosition = position;
                _tacticalRotation = rotation;
                _tacticalPlaced = true;
            }
            else
            {
                var k = 1f - Mathf.Exp(-TacticalSharpness * Time.unscaledDeltaTime);
                _tacticalPosition = Vector3.Lerp(_tacticalPosition, position, k);
                _tacticalRotation = Quaternion.Slerp(_tacticalRotation, rotation, k);
            }

            // Keep the orbit camera consistent for when the player switches to it.
            _focus = ship;
            Distance = _targetDistance = Vector3.Distance(_tacticalPosition, ship);
            var euler = _tacticalRotation.eulerAngles;
            Yaw = euler.y;
            Pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), -89f, 89f);

            transform.SetPositionAndRotation(_tacticalPosition + ShakeOffset(), _tacticalRotation);
        }

        bool Fits(Vector3 position, Quaternion rotation, Vector3 ship, float radius, float halfH, float halfV)
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
            foreach (var e in _enemies)
                if (!Inside(new Vector3(e.x, e.y, e.z), e.w))
                    return false;
            return true;
        }

        float _shake;

        /// <summary>Shakes the camera; the amplitude (metres, scaled with the zoom) decays quickly.</summary>
        public void Shake(float amplitude)
        {
            _shake = Mathf.Min(MaxShake, Mathf.Max(_shake, amplitude));
        }

        [Tooltip("Largest camera shake, in metres at 100 m of zoom.")]
        public float MaxShake = 3f;

        Vector3 ShakeOffset()
        {
            if (_shake <= 0.01f)
                return Vector3.zero;
            _shake *= Mathf.Exp(-6f * Time.unscaledDeltaTime);
            var scale = _shake * Distance / 100f;
            var t = Time.unscaledTime * 40f;
            return new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f) * 2f * scale;
        }

        /// <summary>
        /// Keeps yaw and pitch, and keeps the zoom almost the same: slightly closer for a smaller
        /// ship, slightly further for a bigger one. Only the player changes the zoom a lot.
        /// </summary>
        void OnSelectionChanged(PlayerControls.SelectionInfo selection)
        {
            var first = _framedEntity == Entity.Null;
            _framedEntity = selection.Entity;
            _transitionStart = first ? selection.RenderPosition : _focus;
            _transitionTime = first ? float.MaxValue : 0f;

            if (first)
                _targetDistance = Mathf.Clamp(selection.Radius * FirstFramingRadii, MinDistance, MaxDistance);
            else if (_framedRadius > 0f && selection.Radius > 0f)
            {
                var factor = Mathf.Pow(selection.Radius / _framedRadius, SizeInfluence);
                factor = Mathf.Clamp(factor, 1f / MaxAutoZoomFactor, MaxAutoZoomFactor);
                _targetDistance = Mathf.Clamp(_targetDistance * factor, MinDistance, MaxDistance);
            }
            _framedRadius = selection.Radius;
        }

        void ReadInput()
        {
            if (UiFocus.MenuOpen)
                return;

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var keyboard = Keyboard.current;
            var altHeld = keyboard != null && keyboard.altKey.isPressed;
            if (mouse.middleButton.isPressed || (altHeld && mouse.leftButton.isPressed))
            {
                var delta = mouse.delta.ReadValue();
                Yaw += delta.x * OrbitSpeed;
                Pitch = Mathf.Clamp(Pitch - delta.y * OrbitSpeed, -89f, 89f);
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0f)
                _targetDistance /= ZoomStep;
            else if (scroll < 0f)
                _targetDistance *= ZoomStep;
            _targetDistance = Mathf.Clamp(_targetDistance, MinDistance, MaxDistance);
        }
    }
}
