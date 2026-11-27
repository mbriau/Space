using Space.Core.Combat;
using Space.Core.Equipment;
using Space.Presentation.Game;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space.Presentation.UI
{
    /// <summary>
    /// Equipment menus (provisional IMGUI): the player's ship and its equipment, the shop (only at a
    /// shop beacon) and the catalogue of every item by category. I: ship, M: shop, C: catalogue, Esc: close.
    /// Opening a menu during a level pauses the simulation; equipment changes only between levels.
    /// </summary>
    public partial class EquipmentMenus : MonoBehaviour
    {
        public enum Page { None, Ship, Shop, Catalog }

        public LevelRunner Runner;

        Page _page;
        bool _pausedByMenu;
        Vector2 _scroll;
        Vector2 _detailsScroll;

        // Changes to the loadout are applied after the GUI pass, so the layout of a pass never
        // changes halfway through (IMGUI requires the same controls in its layout and event passes).
        readonly System.Collections.Generic.List<System.Action> _deferred = new();

        void Defer(System.Action action) => _deferred.Add(action);

        public bool IsOpen => _page != Page.None;
        public Page Current => _page;

        RunState Run => Runner != null ? Runner.Run : null;

        void Awake()
        {
            if (Runner == null)
                Runner = GetComponent<LevelRunner>();
        }

        void OnDisable()
        {
            UiFocus.MenuOpen = false;
        }

        public void Open(Page page)
        {
            if (page == Page.Shop && (Run == null || !Run.ShopOpen))
                page = Page.Ship;
            if (_page == Page.None && page != Page.None && Runner != null && Runner.IsLevelRunning && !Runner.IsPaused)
            {
                Runner.SetPaused(true);
                _pausedByMenu = true;
            }
            _page = page;
            _scroll = Vector2.zero;
            _detailsScroll = Vector2.zero;
            UiFocus.MenuOpen = page != Page.None;
        }

        public void Close()
        {
            _page = Page.None;
            UiFocus.MenuOpen = false;
            if (_pausedByMenu && Runner != null && Runner.IsLevelRunning)
                Runner.SetPaused(false);
            _pausedByMenu = false;
        }

        void Toggle(Page page)
        {
            if (_page == page)
                Close();
            else
                Open(page);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || Runner == null || !Runner.HasStarted || UiFocus.Cinematic)
                return;
            if (keyboard.iKey.wasPressedThisFrame) Toggle(Page.Ship);
            if (keyboard.cKey.wasPressedThisFrame) Toggle(Page.Catalog);
            if (keyboard.mKey.wasPressedThisFrame && Run != null && Run.ShopOpen) Toggle(Page.Shop);
            if (keyboard.escapeKey.wasPressedThisFrame && IsOpen) Close();
        }

        void OnGUI()
        {
            if (!IsOpen || Run == null)
                return;
            MenuStyles.Ensure();
            GUI.depth = -10;

            var width = Mathf.Min(1080f, Screen.width - 40f);
            var height = Mathf.Min(680f, Screen.height - 40f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            MenuStyles.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
            MenuStyles.Fill(area, MenuStyles.Background);

            GUILayout.BeginArea(new Rect(area.x + 16, area.y + 12, area.width - 32, area.height - 24));
            DrawHeader();
            GUILayout.Space(8);
            switch (_page)
            {
                case Page.Ship: DrawShipPage(); break;
                case Page.Shop: DrawShopPage(); break;
                case Page.Catalog: DrawCatalogPage(); break;
            }
            GUILayout.EndArea();

            if (_deferred.Count > 0)
            {
                foreach (var action in _deferred)
                    action();
                _deferred.Clear();
            }
        }

        void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            TabButton("Mon vaisseau (I)", Page.Ship, true);
            TabButton("Magasin (M)", Page.Shop, Run.ShopOpen);
            TabButton("Catalogue (C)", Page.Catalog, true);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Crédits : {ItemText.Credits(Run.Credits)}", MenuStyles.Price, GUILayout.Height(32));
            GUILayout.Space(12);
            if (GUILayout.Button("Fermer (Échap)", MenuStyles.Tab))
                Close();
            GUILayout.EndHorizontal();
        }

        void TabButton(string label, Page page, bool enabled)
        {
            var previous = GUI.enabled;
            GUI.enabled = enabled;
            if (GUILayout.Button(label, _page == page ? MenuStyles.TabActive : MenuStyles.Tab) && _page != page)
            {
                _page = page;
                _scroll = Vector2.zero;
                _detailsScroll = Vector2.zero;
            }
            GUI.enabled = previous;
        }

        // ------------------------------------------------------------------ shared item details

        /// <summary>Large icon, name, rarity, description and numbers of an item; returns after the stats so callers add actions.</summary>
        void DrawItemDetails(ItemDefinition item, ItemDefinition compareWith = null)
        {
            if (item == null)
            {
                GUILayout.Label("Sélectionne un objet pour voir ses caractéristiques.", MenuStyles.Muted);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(ItemIcons.Get(item), GUILayout.Width(96), GUILayout.Height(96));
            GUILayout.BeginVertical();
            GUILayout.Label(item.Name, MenuStyles.Heading);
            var previous = GUI.contentColor;
            GUI.contentColor = ItemIcons.TierColor(item.Tier);
            GUILayout.Label($"{ItemText.Category(item.Category)} · {ItemText.Tier(item.Tier)}", MenuStyles.Small);
            GUI.contentColor = previous;
            GUILayout.Label(item.Starter ? "Équipement d'origine" : $"Prix : {ItemText.Credits(item.Price)}", MenuStyles.Small);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label(item.Description, MenuStyles.Text);
            GUILayout.Space(4);
            foreach (var line in ItemText.Stats(item))
                GUILayout.Label("• " + line, MenuStyles.Small);

            var comparison = compareWith != null && compareWith != item ? ItemText.Compare(item, compareWith) : null;
            if (comparison != null)
            {
                GUILayout.Space(4);
                GUILayout.Label(comparison, MenuStyles.Muted);
            }
        }

        /// <summary>The item fitted in the slot this item would go to (for comparisons).</summary>
        ItemDefinition FittedCounterpart(ItemDefinition item)
        {
            if (item == null)
                return null;
            var loadout = Run.Loadout;
            var kind = ItemCategories.Slot(item.Category);
            if (kind != SlotKind.Weapon && kind != SlotKind.Drone)
                return loadout.Resolve(loadout.Get(kind));
            // Weapons and drones: compare with a fitted item of the same category, if any.
            for (var i = 0; i < ShipLoadout.SlotCount(kind); i++)
            {
                var fitted = loadout.Resolve(loadout.Get(kind, i));
                if (fitted != null && fitted.Category == item.Category)
                    return fitted;
            }
            return null;
        }

        static bool ActionButton(string label, bool enabled, float width = 0f)
        {
            var previous = GUI.enabled;
            GUI.enabled = enabled;
            var clicked = width > 0f
                ? GUILayout.Button(label, MenuStyles.Button, GUILayout.Width(width))
                : GUILayout.Button(label, MenuStyles.Button);
            GUI.enabled = previous;
            return clicked && enabled;
        }

        /// <summary>Live state of the flagship in combat (hull, shield, ammunition), if a level is running.</summary>
        bool TryGetLiveFlagship(out EntityManager em, out Entity ship)
        {
            em = default;
            ship = Entity.Null;
            if (Runner == null || !Runner.IsLevelRunning || !Runner.TryGetFlagship(out ship))
                return false;
            em = World.DefaultGameObjectInjectionWorld.EntityManager;
            return em.Exists(ship) && em.HasComponent<Hull>(ship);
        }
    }
}
