using System;
using System.Collections.Generic;

namespace Space.Core.Equipment
{
    /// <summary>
    /// The equipment of the player's ship and the hold (items owned but not fitted). Plain data
    /// (item ids) so the run can be saved. Equipment can only be changed between levels.
    /// </summary>
    [Serializable]
    public sealed class ShipLoadout
    {
        public const int WeaponSlots = 3;
        public const int DroneSlots = 2;

        public string Reactor;
        public string Shield;
        public string Engine;
        public string Armor;
        public string[] Weapons = new string[WeaponSlots];
        public string[] Drones = new string[DroneSlots];

        /// <summary>Items bought or removed from the ship, waiting in the hold.</summary>
        public List<string> Hold = new();

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
    }
}
