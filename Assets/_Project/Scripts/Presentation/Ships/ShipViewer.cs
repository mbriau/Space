using System.Collections.Generic;
using System.Linq;
using Space.Core.Equipment;
using Space.Presentation.Controls;
using Space.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// Hangar: browse the generated ships, fit weapons on their three hardpoints and watch them fire
    /// at an invincible target. The camera starts as in the game (tactical, over the shoulder);
    /// V switches to the free camera (right or middle drag, or Alt + left drag, to orbit; wheel to
    /// zoom). ← / → change ship, F starts or stops firing.
    /// Sets up its own camera, light and ambient when the scene has none: an empty scene with this
    /// component is enough (menu Space > Hangar).
    /// </summary>
    public class ShipViewer : MonoBehaviour
    {
        public enum CameraMode { Tactical, Free }

        public CameraMode Mode = CameraMode.Tactical;

        [Tooltip("Degrees per pixel of mouse movement in the free camera.")]
        public float OrbitSpeed = 0.25f;

        [Min(1.01f)] public float ZoomStep = 1.15f;

        public Color Background = new(0.035f, 0.045f, 0.09f);

        public int Selected { get; private set; } = -1;
        public ShipModel Model { get; private set; }
        public FiringRange Range { get; private set; }
        public Camera ViewCamera { get; private set; }

        float _tacticalZoom = 1f;
        float _yaw = 140f, _pitch = 20f, _distance = 80f;
        int _pickerSlot = -1;
        Vector2 _pickerScroll;
        Rect _leftPanel, _pickerPanel;

        void Awake()
        {
            SetUpScene();
            Range = gameObject.AddComponent<FiringRange>();
        }

        void Start()
        {
            if (Selected < 0)
                Select(0);
        }

        void SetUpScene()
        {
            ViewCamera = Camera.main;
            if (ViewCamera == null)
            {
                var go = new GameObject("Hangar Camera") { tag = "MainCamera" };
                ViewCamera = go.AddComponent<Camera>();
            }
            ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            ViewCamera.backgroundColor = Background;
            ViewCamera.fieldOfView = 60f;
            ViewCamera.nearClipPlane = 0.5f;
            ViewCamera.farClipPlane = 5000f;

            if (!FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l => l.type == LightType.Directional))
            {
                var sun = new GameObject("Hangar Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.15f;
                sun.color = new Color(1f, 0.97f, 0.92f);
                sun.shadows = LightShadows.None;
                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.36f, 0.5f);
        }

        /// <summary>Shows another ship (index in <see cref="ShipDesigns.All"/>).</summary>
        public void Select(int index)
        {
            var count = ShipDesigns.All.Count;
            index = ((index % count) + count) % count;
            if (Model != null)
                Destroy(Model.gameObject);
            Selected = index;
            Model = ShipModel.Create(ShipDesigns.All[index], transform);
            Range.Ship = Model;
            _distance = Model.Radius * 3.2f;
            _pickerSlot = -1;
        }

        /// <summary>Free camera placement, for the tests and screenshots.</summary>
        public void SetFreeCamera(float yaw, float pitch, float distanceInRadii)
        {
            Mode = CameraMode.Free;
            _yaw = yaw;
            _pitch = pitch;
            if (Model != null)
                _distance = Model.Radius * distanceInRadii;
        }

        void LateUpdate()
        {
            ReadInput();
            if (Model == null || ViewCamera == null)
                return;

            if (Mode == CameraMode.Tactical)
            {
                var enemies = new List<Vector4>();
                if (Range.Target != null)
                {
                    var p = Range.Target.position;
                    enemies.Add(new Vector4(p.x, p.y, p.z, Range.TargetRadius));
                }
                TacticalFraming.Place(Model.transform.position, Model.transform.rotation, Model.Radius, enemies,
                    ViewCamera.fieldOfView, ViewCamera.aspect, _tacticalZoom, TacticalFraming.Settings.Default,
                    out var position, out var rotation);
                ViewCamera.transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                var focus = Model.transform.TransformPoint(Model.Ship.Bounds.center);
                var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                ViewCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * _distance, rotation);
            }
        }

        bool PointerOverUi()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return false;
            var p = mouse.position.ReadValue();
            var gui = new Vector2(p.x, Screen.height - p.y);
            return _leftPanel.Contains(gui) || (_pickerSlot >= 0 && _pickerPanel.Contains(gui));
        }

        void ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.vKey.wasPressedThisFrame)
                    Mode = Mode == CameraMode.Tactical ? CameraMode.Free : CameraMode.Tactical;
                if (keyboard.fKey.wasPressedThisFrame)
                    Range.Firing = !Range.Firing;
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                    Select(Selected + 1);
                if (keyboard.leftArrowKey.wasPressedThisFrame)
                    Select(Selected - 1);
                if (keyboard.escapeKey.wasPressedThisFrame)
                    _pickerSlot = -1;
            }

            var mouse = Mouse.current;
            if (mouse == null || PointerOverUi())
                return;

            var scroll = mouse.scroll.ReadValue().y;
            if (Mode == CameraMode.Tactical)
            {
                if (scroll > 0f) _tacticalZoom /= ZoomStep;
                else if (scroll < 0f) _tacticalZoom *= ZoomStep;
                _tacticalZoom = Mathf.Clamp(_tacticalZoom, 0.5f, 6f);
                return;
            }

            if (scroll > 0f) _distance /= ZoomStep;
            else if (scroll < 0f) _distance *= ZoomStep;
            var radius = Model != null ? Model.Radius : 20f;
            _distance = Mathf.Clamp(_distance, radius * 1.2f, 2000f);

            var alt = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            if (mouse.rightButton.isPressed || mouse.middleButton.isPressed || (alt && mouse.leftButton.isPressed))
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * OrbitSpeed;
                _pitch = Mathf.Clamp(_pitch - delta.y * OrbitSpeed, -85f, 85f);
            }
        }

        // ------------------------------------------------------------------ Menu (IMGUI, provisional)

        static readonly ItemCategory[] WeaponCategories = { ItemCategory.EnergyWeapon, ItemCategory.PhysicalWeapon, ItemCategory.Missile };

        static string CategoryName(ItemCategory category) => category switch
        {
            ItemCategory.EnergyWeapon => "Armes à énergie",
            ItemCategory.PhysicalWeapon => "Armes physiques",
            _ => "Missiles",
        };

        static string KindName(TurretKind kind) => kind switch
        {
            TurretKind.Twin => "canons jumeaux",
            TurretKind.Heavy => "canon lourd",
            TurretKind.Rail => "rail",
            TurretKind.Laser => "émetteur laser",
            TurretKind.Ion => "projecteur ionique",
            _ => "lance-missiles",
        };

        void OnGUI()
        {
            MenuStyles.Ensure();
            const float width = 330f;
            var height = Mathf.Min(Screen.height - 20f, 700f);
            _leftPanel = new Rect(10f, 10f, width, height);
            MenuStyles.Fill(_leftPanel, MenuStyles.PanelColor);

            GUILayout.BeginArea(new Rect(_leftPanel.x + 12f, _leftPanel.y + 10f, width - 24f, height - 20f));
            GUILayout.Label("Hangar", MenuStyles.Title);
            GUILayout.Label("Vaisseaux moyens : combat et cargaison", MenuStyles.Muted);
            GUILayout.Space(6f);

            for (var i = 0; i < ShipDesigns.All.Count; i++)
            {
                var design = ShipDesigns.All[i];
                var style = i == Selected ? MenuStyles.SlotSelected : MenuStyles.Slot;
                if (GUILayout.Button($"{design.DisplayName}  ·  {design.Role}", style, GUILayout.Height(26f)))
                    Select(i);
            }

            if (Model != null)
            {
                GUILayout.Space(10f);
                var ship = Model.Ship;
                var size = ship.Bounds.size;
                GUILayout.Label($"{ship.Design.DisplayName} : {size.z:0} × {size.x:0} × {size.y:0} m, soute ≈ {ship.CargoVolume:0} m³", MenuStyles.Small);
                GUILayout.Label($"Peinture « {ship.Design.Palette.Name} », {ship.Engines.Count} tuyère(s), {ship.Hull.TriangleCount} triangles", MenuStyles.Muted);

                GUILayout.Space(8f);
                GUILayout.Label("Armes (cliquer pour changer)", MenuStyles.Heading);
                for (var slot = 0; slot < Model.Turrets.Count; slot++)
                {
                    var turret = Model.Turrets[slot];
                    var label = turret == null ? "— vide —" : $"{turret.Weapon.Name}  ({KindName(turret.Kind)})";
                    var style = slot == _pickerSlot ? MenuStyles.SlotSelected : MenuStyles.Slot;
                    if (GUILayout.Button($"{slot + 1}. {label}", style, GUILayout.Height(28f)))
                        _pickerSlot = _pickerSlot == slot ? -1 : slot;
                }
            }

            GUILayout.FlexibleSpace();
            var camera = Mode == CameraMode.Tactical ? "tactique (comme en jeu)" : "libre";
            GUILayout.Label($"Caméra {camera} — V pour changer", MenuStyles.Small);
            GUILayout.Label($"Tir {(Range.Firing ? "en cours" : "arrêté")} — F · ← / → vaisseau · molette zoom · clic droit + glisser : orbite (caméra libre)", MenuStyles.Muted);
            GUILayout.EndArea();

            if (_pickerSlot >= 0 && Model != null)
                DrawPicker();
        }

        void DrawPicker()
        {
            const float width = 360f;
            var height = Mathf.Min(Screen.height - 20f, 700f);
            _pickerPanel = new Rect(_leftPanel.xMax + 8f, 10f, width, height);
            MenuStyles.Fill(_pickerPanel, MenuStyles.PanelColor);
            GUILayout.BeginArea(new Rect(_pickerPanel.x + 10f, _pickerPanel.y + 10f, width - 20f, height - 20f));
            GUILayout.Label($"Emplacement {_pickerSlot + 1}", MenuStyles.Heading);
            var current = Model.WeaponAt(_pickerSlot);
            if (GUILayout.Button("Retirer l'arme", MenuStyles.Button))
            {
                Model.SetWeapon(_pickerSlot, null);
                _pickerSlot = -1;
            }

            _pickerScroll = GUILayout.BeginScrollView(_pickerScroll);
            foreach (var category in WeaponCategories)
            {
                GUILayout.Space(6f);
                GUILayout.Label(CategoryName(category), MenuStyles.Small);
                foreach (var item in ItemCatalog.InCategory(category))
                {
                    var style = item.Id == current ? MenuStyles.SlotSelected : MenuStyles.Slot;
                    if (GUILayout.Button($"{item.Name}  ({KindName(TurretVisuals.KindFor(item))})", style, GUILayout.Height(24f)))
                    {
                        Model.SetWeapon(_pickerSlot, item.Id);
                        _pickerSlot = -1;
                        break;
                    }
                }
                if (_pickerSlot < 0)
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
