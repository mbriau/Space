using System;
using System.Collections.Generic;

namespace Space.Core.Equipment
{
    /// <summary>
    /// The equipment of the player's ship, the hold (items owned but not fitted) and the upgrades
    /// fitted onto each item. Plain data (ids) so the run can be saved. Equipment can only be
    /// changed between levels. The player owns at most one of each item, so an item id names one
    /// piece of equipment and its upgrades follow it between its slot and the hold.
    /// </summary>
    [Serializable]
    public sealed class ShipLoadout
    {
        public const int WeaponSlots = 3;
        public const int DroneSlots = 2;

        /// <summary>Upgrades one item can carry.</summary>
        public const int UpgradeSlots = 2;

        public string Reactor;
        public string Shield;
        public string Engine;
        public string Armor;
        public string[] Weapons = new string[WeaponSlots];
        public string[] Drones = new string[DroneSlots];

        /// <summary>Items bought or removed from the ship, waiting in the hold.</summary>
        public List<string> Hold = new();

        /// <summary>Upgrades fitted onto owned items (only items with at least one upgrade are listed).</summary>
        public List<ItemUpgrades> Upgrades = new();

        /// <summary>The upgrades of one item, by slot (null = free).</summary>
        [Serializable]
        public sealed class ItemUpgrades
        {
            public string Item;
            public string[] Upgrades = new string[UpgradeSlots];
        }

        /// <summary>Upgraded copies of items, rebuilt when upgrades change (not saved).</summary>
        [NonSerialized] Dictionary<string, ItemDefinition> _resolved;

        /// <summary>The Cargo as it starts every run.</summary>
        public static ShipLoadout Starter() => new()
        {
            Reactor = ItemCatalog.StarterReactor,
            Shield = ItemCatalog.StarterShield,
            Engine = ItemCatalog.StarterEngine,
            Armor = ItemCatalog.StarterArmor,
            Weapons = new[] { ItemCatalog.StarterLaser, ItemCatalog.StarterCannon, null },
            Drones = new string[DroneSlots],
        };

        public static int SlotCount(SlotKind kind) => kind switch
        {
            SlotKind.Weapon => WeaponSlots,
            SlotKind.Drone => DroneSlots,
            _ => 1,
        };

        public string Get(SlotKind kind, int index = 0) => kind switch
        {
            SlotKind.Reactor => Reactor,
            SlotKind.Shield => Shield,
            SlotKind.Engine => Engine,
            SlotKind.Armor => Armor,
            SlotKind.Weapon => Weapons[index],
            _ => Drones[index],
        };

        void Set(SlotKind kind, int index, string id)
        {
            switch (kind)
            {
                case SlotKind.Reactor: Reactor = id; break;
                case SlotKind.Shield: Shield = id; break;
                case SlotKind.Engine: Engine = id; break;
                case SlotKind.Armor: Armor = id; break;
                case SlotKind.Weapon: Weapons[index] = id; break;
                default: Drones[index] = id; break;
            }
        }

        /// <summary>Every fitted item id (empty slots skipped).</summary>
        public IEnumerable<string> Fitted()
        {
            foreach (var id in new[] { Reactor, Shield, Engine, Armor })
                if (!string.IsNullOrEmpty(id))
                    yield return id;
            foreach (var id in Weapons)
                if (!string.IsNullOrEmpty(id))
                    yield return id;
            foreach (var id in Drones)
                if (!string.IsNullOrEmpty(id))
                    yield return id;
        }

        /// <summary>
        /// Fits an item from the hold. With <paramref name="slotIndex"/> &lt; 0 it goes to the first
        /// empty slot of its kind, or replaces the first one. The replaced item goes to the hold.
        /// </summary>
        public bool EquipFromHold(string id, int slotIndex = -1)
        {
            var item = ItemCatalog.Get(id);
            if (item == null || !Hold.Contains(id))
                return false;

            var kind = ItemCategories.Slot(item.Category);
            var count = SlotCount(kind);
            if (slotIndex >= count)
                return false;
            if (slotIndex < 0)
            {
                slotIndex = 0;
                for (var i = 0; i < count; i++)
                {
                    if (string.IsNullOrEmpty(Get(kind, i)))
                    {
                        slotIndex = i;
                        break;
                    }
                }
            }

            Hold.Remove(id);
            var previous = Get(kind, slotIndex);
            if (!string.IsNullOrEmpty(previous))
                Hold.Add(previous);
            Set(kind, slotIndex, id);
            return true;
        }

        /// <summary>
        /// Removes an item from its slot to the hold. Weapons and drones only: the ship always keeps
        /// a reactor, a shield, engines and a hull (replace them instead).
        /// </summary>
        public bool Unequip(SlotKind kind, int index)
        {
            if (kind != SlotKind.Weapon && kind != SlotKind.Drone)
                return false;
            var id = Get(kind, index);
            if (string.IsNullOrEmpty(id))
                return false;
            Set(kind, index, null);
            Hold.Add(id);
            return true;
        }

        /// <summary>True if the item would go to an empty slot (bought items are fitted right away then).</summary>
        public bool HasFreeSlotFor(ItemDefinition item)
        {
            var kind = ItemCategories.Slot(item.Category);
            if (kind != SlotKind.Weapon && kind != SlotKind.Drone)
                return false;
            for (var i = 0; i < SlotCount(kind); i++)
                if (string.IsNullOrEmpty(Get(kind, i)))
                    return true;
            return false;
        }
    
        // ------------------------------------------------------------------ ownership and upgrades

        /// <summary>True if the item is fitted or in the hold.</summary>
        public bool Owns(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (Hold.Contains(id))
                return true;
            foreach (var fitted in Fitted())
                if (fitted == id)
                    return true;
            return false;
        }

        /// <summary>Fitted items, then the hold.</summary>
        public IEnumerable<string> Owned()
        {
            foreach (var id in Fitted())
                yield return id;
            foreach (var id in Hold)
                yield return id;
        }

        /// <summary>The upgrade ids of an item, by slot (null = free slot); null if it has none.</summary>
        public string[] UpgradesOf(string itemId)
        {
            foreach (var entry in Upgrades)
                if (entry.Item == itemId)
                    return entry.Upgrades;
            return null;
        }

        public int UpgradeCount(string itemId)
        {
            var count = 0;
            var slots = UpgradesOf(itemId);
            if (slots != null)
                foreach (var id in slots)
                    if (!string.IsNullOrEmpty(id))
                        count++;
            return count;
        }

        /// <summary>
        /// True if the upgrade can go onto this owned item: it fits the item's kind and the item
        /// does not carry it already. When both slots are taken it replaces one (see <see cref="InstallUpgrade"/>).
        /// </summary>
        public bool CanInstall(string itemId, string upgradeId)
        {
            if (!Owns(itemId) || !UpgradeMath.Accepts(UpgradeCatalog.Get(upgradeId), ItemCatalog.Get(itemId)))
                return false;
            var slots = UpgradesOf(itemId);
            return slots == null || Array.IndexOf(slots, upgradeId) < 0;
        }

        /// <summary>True if at least one owned item could take the upgrade.</summary>
        public bool CanInstallAnywhere(string upgradeId)
        {
            foreach (var id in Owned())
                if (CanInstall(id, upgradeId))
                    return true;
            return false;
        }

        /// <summary>
        /// Fits an upgrade onto an owned item. With <paramref name="slot"/> &lt; 0 it takes the first
        /// free slot and fails if both are taken; with a slot index it replaces what is there.
        /// </summary>
        public bool InstallUpgrade(string itemId, string upgradeId, int slot = -1)
        {
            if (!CanInstall(itemId, upgradeId) || slot >= UpgradeSlots)
                return false;
            var slots = UpgradesOf(itemId);
            if (slots == null)
            {
                slots = new string[UpgradeSlots];
                Upgrades.Add(new ItemUpgrades { Item = itemId, Upgrades = slots });
            }
            if (slot < 0)
            {
                slot = Array.FindIndex(slots, string.IsNullOrEmpty);
                if (slot < 0)
                    return false;
            }
            slots[slot] = upgradeId;
            _resolved = null;
            return true;
        }

        /// <summary>Drops the upgrades of an item that leaves the ship for good (sold).</summary>
        public void ForgetUpgrades(string itemId)
        {
            Upgrades.RemoveAll(entry => entry.Item == itemId);
            _resolved = null;
        }

        /// <summary>The item as the player has it: the catalog item with its upgrades applied.</summary>
        public ItemDefinition Resolve(string id)
        {
            var item = ItemCatalog.Get(id);
            var slots = item != null ? UpgradesOf(id) : null;
            if (slots == null)
                return item;
            _resolved ??= new Dictionary<string, ItemDefinition>();
            if (_resolved.TryGetValue(id, out var upgraded))
                return upgraded;
            var upgrades = new List<UpgradeDefinition>(UpgradeSlots);
            foreach (var upgradeId in slots)
                if (UpgradeCatalog.Get(upgradeId) is { } upgrade)
                    upgrades.Add(upgrade);
            upgraded = UpgradeMath.Apply(item, upgrades);
            _resolved[id] = upgraded;
            return upgraded;
        }
    }
}
