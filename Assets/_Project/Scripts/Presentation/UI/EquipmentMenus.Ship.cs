using Space.Core.Combat;
using Space.Core.Equipment;
using UnityEngine;

namespace Space.Presentation.UI
{
    public partial class EquipmentMenus
    {
        SlotKind _slotKind = SlotKind.Weapon;
        int _slotIndex;
        string _holdSelection;

        static readonly SlotKind[] SlotOrder =
            { SlotKind.Reactor, SlotKind.Shield, SlotKind.Engine, SlotKind.Armor, SlotKind.Weapon, SlotKind.Drone };

        void DrawShipPage()
        {
            var loadout = Run.Loadout;
            var stats = LoadoutMath.Compute(loadout);
            var canEdit = Run.BetweenLevels;

            GUILayout.BeginHorizontal();

            // Column 1: what the equipment makes of the ship.
            GUILayout.BeginVertical(MenuStyles.Panel, GUILayout.Width(290));
            DrawShipSummary(stats);
            GUILayout.EndVertical();

            // Column 2: slots and hold.
            GUILayout.BeginVertical(GUILayout.Width(340));
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var kind in SlotOrder)
                for (var i = 0; i < ShipLoadout.SlotCount(kind); i++)
                    DrawSlot(loadout, kind, i);

            GUILayout.Space(10);
            GUILayout.Label($"Soute ({loadout.Hold.Count})", MenuStyles.Heading);
            if (loadout.Hold.Count == 0)
                GUILayout.Label("Vide. Les objets achetés ou retirés du vaisseau arrivent ici.", MenuStyles.Muted);
            for (var i = 0; i < loadout.Hold.Count; i++)
            {
                var id = loadout.Hold[i];
                var item = ItemCatalog.Get(id);
                if (item == null)
                    continue;
                var selected = _holdSelection == id;
                var content = new GUIContent($"{item.Name}\n{ItemText.Category(item.Category)} · {ItemText.Summary(item)}", ItemIcons.Get(item));
                if (GUILayout.Button(content, selected ? MenuStyles.SlotSelected : MenuStyles.Slot, GUILayout.Height(52)))
                    _holdSelection = id;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Column 3: details and actions.
            GUILayout.BeginVertical(MenuStyles.Panel);
            _detailsScroll = GUILayout.BeginScrollView(_detailsScroll);
            if (_holdSelection != null && loadout.Hold.Contains(_holdSelection))
                DrawHoldDetails(loadout, ItemCatalog.Get(_holdSelection), canEdit);
            else
                DrawSlotDetails(loadout, canEdit);
            GUILayout.EndScrollView();
            if (!canEdit)
                GUILayout.Label("L'équipement se change entre les niveaux.", MenuStyles.Muted);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        void DrawShipSummary(LoadoutStats stats)
        {
            GUILayout.Label("Cargo", MenuStyles.Title);
            var grid = stats.Power;

            var live = TryGetLiveFlagship(out var em, out var ship);
            if (live)
            {
                var hull = em.GetComponentData<Hull>(ship);
                var shield = em.GetComponentData<Shield>(ship);
                GUILayout.Label($"En combat : coque {hull.Current:0} / {hull.Max:0}, bouclier {shield.Current:0} / {shield.Max:0}", MenuStyles.Small);
                GUILayout.Space(4);
            }

            GUILayout.Label($"Coque : {stats.Hull:0}", MenuStyles.Text);
            GUILayout.Label(stats.ShieldMax > 0f
                ? $"Bouclier : {stats.ShieldMax:0} pts, recharge {stats.ShieldRechargePerPower * grid.ShieldCapacity:0.#} pts/s, redémarrage {stats.ShieldRebootSeconds:0} s"
                : "Bouclier : aucun", MenuStyles.Text);
            GUILayout.Label($"Vitesse : {stats.MaxSpeed:0} m/s · rotation {stats.TurnRateDegrees:0}°/s", MenuStyles.Text);

            GUILayout.Space(8);
            GUILayout.Label("Puissance", MenuStyles.Heading);
            GUILayout.Label($"Produite : {grid.Reactor} unités", MenuStyles.Text);
            var demand = stats.PowerDemand;
            var previous = GUI.contentColor;
            GUI.contentColor = demand > grid.Reactor ? new Color(1f, 0.8f, 0.4f) : Color.white;
            GUILayout.Label($"Demandée à plein régime : {demand} unités", MenuStyles.Text);
            GUI.contentColor = previous;
            PowerBar("Bouclier", grid.ShieldCapacity, grid.Reactor);
            PowerBar("Armes à énergie", grid.EnergyWeaponsCapacity, grid.Reactor);
            PowerBar("Armes physiques", grid.PhysicalWeaponsCapacity, grid.Reactor);
            PowerBar("Moteurs", grid.EnginesCapacity, grid.Reactor);
            if (demand > grid.Reactor)
                GUILayout.Label("Le réacteur ne peut pas tout alimenter à la fois : c'est à toi de répartir la puissance en combat (Q W E R).", MenuStyles.Muted);

            GUILayout.Space(8);
            GUILayout.Label("Armement", MenuStyles.Heading);
            if (stats.Weapons.Count == 0)
                GUILayout.Label("Aucune arme.", MenuStyles.Muted);
            var hasLive = live && em.HasBuffer<Weapon>(ship);
            for (var i = 0; i < stats.Weapons.Count; i++)
            {
                var w = stats.Weapons[i];
                var ammo = w.Ammo < 0 ? "∞" : w.Ammo.ToString();
                if (hasLive)
                {
                    var buffer = em.GetBuffer<Weapon>(ship);
                    if (i < buffer.Length && w.Ammo >= 0)
                        ammo = $"{buffer[i].Ammo}/{w.Ammo}";
                }
                var dps = w.Type == DamageType.Ion ? $"{LoadoutMath.ShieldDamagePerSecond(w):0.#} bouclier/s"
                    : $"{LoadoutMath.ShieldDamagePerSecond(w):0.#} bouclier/s, {LoadoutMath.HullDamagePerSecond(w):0.#} coque/s";
                GUILayout.Label($"{WeaponKind(w.Type)} · {w.Range:0} m · {dps} · munitions {ammo}", MenuStyles.Small);
            }
            if (stats.Drones.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label($"Drones : {stats.Drones.Count}", MenuStyles.Text);
            }
        }

        static string WeaponKind(DamageType type) => type switch
        {
            DamageType.Ion => "Ions",
            DamageType.Laser => "Laser",
            DamageType.Kinetic => "Canon",
            _ => "Missiles",
        };

        static void PowerBar(string label, int capacity, int reactor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, MenuStyles.Small, GUILayout.Width(110));
            var rect = GUILayoutUtility.GetRect(140, 14, GUILayout.ExpandWidth(true));
            rect.y += 3;
            var cell = rect.width / Mathf.Max(1, Mathf.Max(reactor, 12));
            for (var i = 0; i < capacity; i++)
                MenuStyles.Fill(new Rect(rect.x + i * cell, rect.y, cell - 2f, 10f), new Color(1f, 0.75f, 0.25f));
            GUILayout.Label(capacity.ToString(), MenuStyles.Small, GUILayout.Width(22));
            GUILayout.EndHorizontal();
        }

        void DrawSlot(ShipLoadout loadout, SlotKind kind, int index)
        {
            var item = ItemCatalog.Get(loadout.Get(kind, index));
            var selected = _holdSelection == null && _slotKind == kind && _slotIndex == index;
            var label = item != null
                ? $"{ItemText.Slot(kind, index)} : {item.Name}\n{ItemText.Summary(item)}"
                : $"{ItemText.Slot(kind, index)} : vide";
            var icon = item != null ? ItemIcons.Get(item) : null;
            if (GUILayout.Button(new GUIContent(label, icon), selected ? MenuStyles.SlotSelected : MenuStyles.Slot, GUILayout.Height(52)))
            {
                _slotKind = kind;
                _slotIndex = index;
                _holdSelection = null;
            }
        }

        void DrawSlotDetails(ShipLoadout loadout, bool canEdit)
        {
            var item = ItemCatalog.Get(loadout.Get(_slotKind, _slotIndex));
            GUILayout.Label(ItemText.Slot(_slotKind, _slotIndex), MenuStyles.Muted);
            if (item == null)
            {
                GUILayout.Label(_slotKind == SlotKind.Drone
                    ? "Emplacement libre : un drone acheté au magasin viendra s'y placer et t'escortera en formation."
                    : "Emplacement libre : équipe une arme depuis la soute, ou achètes-en une au magasin.", MenuStyles.Text);
            }
            else
            {
                DrawItemDetails(item);
                GUILayout.Space(8);
                if ((_slotKind == SlotKind.Weapon || _slotKind == SlotKind.Drone) &&
                    ActionButton("Retirer vers la soute", canEdit, 220))
                    loadout.Unequip(_slotKind, _slotIndex);
            }

            // Items of the hold that fit this slot.
            GUILayout.Space(10);
            var any = false;
            foreach (var id in loadout.Hold)
            {
                var candidate = ItemCatalog.Get(id);
                if (candidate == null || ItemCategories.Slot(candidate.Category) != _slotKind)
                    continue;
                if (!any)
                {
                    GUILayout.Label("Dans la soute, pour cet emplacement :", MenuStyles.Heading);
                    any = true;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent($" {candidate.Name} · {ItemText.Summary(candidate)}", ItemIcons.Get(candidate)), MenuStyles.Small, GUILayout.Height(40));
                if (ActionButton("Équiper ici", canEdit, 110))
                {
                    loadout.EquipFromHold(id, _slotIndex);
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }

        void DrawHoldDetails(ShipLoadout loadout, ItemDefinition item, bool canEdit)
        {
            DrawItemDetails(item, FittedCounterpart(item));
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (ActionButton("Équiper", canEdit, 120))
            {
                var kind = ItemCategories.Slot(item.Category);
                var slot = kind == _slotKind ? _slotIndex : -1;
                if (loadout.EquipFromHold(item.Id, slot))
                {
                    _holdSelection = null;
                    _slotKind = kind;
                    _slotIndex = slot < 0 ? FindSlot(loadout, kind, item.Id) : slot;
                }
            }
            if (ActionButton($"Vendre ({ItemText.Credits(ShopGenerator.SellPrice(item))})", canEdit, 170))
            {
                Run.Sell(item.Id);
                _holdSelection = null;
            }
            GUILayout.EndHorizontal();
        }

        static int FindSlot(ShipLoadout loadout, SlotKind kind, string id)
        {
            for (var i = 0; i < ShipLoadout.SlotCount(kind); i++)
                if (loadout.Get(kind, i) == id)
                    return i;
            return 0;
        }
    }
}
