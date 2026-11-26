using Space.Core.Equipment;
using UnityEngine;

namespace Space.Presentation.UI
{
    public partial class EquipmentMenus
    {
        ItemCategory _catalogCategory = ItemCategory.Reactor;
        string _catalogSelection;

        /// <summary>
        /// Development view: every item is visible. Later, a new player only sees what the roguelite
        /// progression has unlocked; the others show as unknown.
        /// </summary>
        public bool ShowAllItems = true;

        bool IsKnown(ItemDefinition item) => ShowAllItems || item.Starter || item.Tier == 1;

        void DrawCatalogPage()
        {
            // Category tabs with their icon.
            GUILayout.BeginHorizontal();
            for (var c = 0; c < ItemCategories.Count; c++)
            {
                var category = (ItemCategory)c;
                var content = new GUIContent(" " + ItemText.CategoryPlural(category), ItemIcons.Get(category));
                if (GUILayout.Button(content, _catalogCategory == category ? MenuStyles.SlotSelected : MenuStyles.Slot,
                        GUILayout.Height(40), GUILayout.MinWidth(110)))
                {
                    _catalogCategory = category;
                    _catalogSelection = null;
                    _scroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(470));
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var item in ItemCatalog.InCategory(_catalogCategory))
            {
                var known = IsKnown(item);
                var label = known
                    ? $"{item.Name}  ·  {ItemText.Tier(item.Tier)}{(item.Starter ? " · d'origine" : $" · {ItemText.Credits(item.Price)}")}\n{ItemText.Summary(item)}"
                    : "???\nPas encore découvert";
                var content = new GUIContent(label, known ? ItemIcons.Get(item) : null);
                if (GUILayout.Button(content, _catalogSelection == item.Id ? MenuStyles.SlotSelected : MenuStyles.Slot, GUILayout.Height(60)) && known)
                {
                    _catalogSelection = item.Id;
                    _detailsScroll = Vector2.zero;
                }
            }
            GUILayout.EndScrollView();
            ShowAllItems = GUILayout.Toggle(ShowAllItems, " Vue de développement : tout afficher");
            GUILayout.EndVertical();

            GUILayout.BeginVertical(MenuStyles.Panel);
            _detailsScroll = GUILayout.BeginScrollView(_detailsScroll);
            var selected = ItemCatalog.Get(_catalogSelection);
            DrawItemDetails(selected, FittedCounterpart(selected));
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}
