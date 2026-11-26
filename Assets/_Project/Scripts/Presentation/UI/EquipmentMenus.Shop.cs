using Space.Core.Equipment;
using UnityEngine;

namespace Space.Presentation.UI
{
    public partial class EquipmentMenus
    {
        string _shopSelection;

        void DrawShopPage()
        {
            GUILayout.BeginHorizontal();

            // Offers: a grid of cards, 4 per row.
            GUILayout.BeginVertical(GUILayout.Width(620));
            GUILayout.Label("Un marchand de passage propose quelques pièces. Ce qui est acheté va sur le vaisseau si un emplacement est libre, sinon dans la soute.", MenuStyles.Muted);
            GUILayout.Space(6);
            var offers = Run.ShopOffers;
            for (var row = 0; row * 4 < offers.Count; row++)
            {
                GUILayout.BeginHorizontal();
                for (var col = 0; col < 4; col++)
                {
                    var index = row * 4 + col;
                    if (index < offers.Count)
                        DrawOfferCard(ItemCatalog.Get(offers[index]));
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
            }
            GUILayout.EndVertical();

            // Details of the selected offer.
            GUILayout.BeginVertical(MenuStyles.Panel);
            _detailsScroll = GUILayout.BeginScrollView(_detailsScroll);
            var selected = ItemCatalog.Get(_shopSelection);
            DrawItemDetails(selected, FittedCounterpart(selected));
            GUILayout.EndScrollView();
            if (selected != null)
            {
                var sold = Run.ShopSold.Contains(selected.Id);
                var label = sold ? "Acheté" : Run.Credits < selected.Price
                    ? $"Il manque {ItemText.Credits(selected.Price - Run.Credits)}"
                    : $"Acheter ({ItemText.Credits(selected.Price)})";
                if (ActionButton(label, Run.CanBuy(selected)))
                    Run.Buy(selected);
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        void DrawOfferCard(ItemDefinition item)
        {
            if (item == null)
                return;
            var sold = Run.ShopSold.Contains(item.Id);
            var selected = _shopSelection == item.Id;

            var rect = GUILayoutUtility.GetRect(148, 200, GUILayout.Width(148), GUILayout.Height(200));
            MenuStyles.Fill(rect, selected ? new Color(0.16f, 0.22f, 0.34f, 1f) : MenuStyles.PanelColor);
            MenuStyles.Fill(new Rect(rect.x, rect.y, rect.width, 3f), ItemIcons.TierColor(item.Tier));

            var previous = GUI.color;
            if (sold)
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(rect.x + (rect.width - 80f) * 0.5f, rect.y + 10f, 80f, 80f), ItemIcons.Get(item));
            GUI.Label(new Rect(rect.x + 8, rect.y + 94, rect.width - 16, 40), item.Name, MenuStyles.Text);
            GUI.Label(new Rect(rect.x + 8, rect.y + 134, rect.width - 16, 36), ItemText.Category(item.Category), MenuStyles.Muted);
            GUI.color = previous;

            var priceStyle = Run.Credits >= item.Price ? MenuStyles.Price : MenuStyles.PriceTooHigh;
            GUI.Label(new Rect(rect.x + 8, rect.y + 172, rect.width - 16, 22), sold ? "Acheté" : ItemText.Credits(item.Price), priceStyle);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                _shopSelection = item.Id;
                _detailsScroll = Vector2.zero;
            }
            GUILayout.Space(6);
        }
    }
}
