using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using Space.Presentation.UI;

namespace Space.Presentation.Controls
{
    /// <summary>
    /// Third-person orbit camera around the selected ship (M1, US1).
    /// Middle mouse drag (or Alt + left drag) orbits, the wheel zooms from 20 m to 2 km.
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

        PlayerControls _controls;
        Vector3 _focus;
        float _targetDistance;
        Entity _framedEntity;
        float _framedRadius;
        Vector3 _transitionStart;
        float _transitionTime = float.MaxValue;

        void Awake()
        {
            _controls = GetComponent<PlayerControls>();
            _targetDistance = Distance;
        }

        void LateUpdate()
        {
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
