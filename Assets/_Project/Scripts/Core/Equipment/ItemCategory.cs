namespace Space.Core.Equipment
{
    /// <summary>Kinds of equipment. Each ship slot accepts one or more categories.</summary>
    public enum ItemCategory : byte
    {
        Reactor = 0,
        Shield = 1,
        EnergyWeapon = 2,
        PhysicalWeapon = 3,
        Missile = 4,
        Engine = 5,
        Armor = 6,
        Drone = 7,
    }

    /// <summary>The slots of a ship. Weapon slots take energy weapons, physical weapons and missiles.</summary>
    public enum SlotKind : byte
    {
        Reactor = 0,
        Shield = 1,
        Engine = 2,
        Armor = 3,
        Weapon = 4,
        Drone = 5,
    }

    public static class ItemCategories
    {
        public const int Count = 8;

        public static SlotKind Slot(ItemCategory category) => category switch
        {
            ItemCategory.Reactor => SlotKind.Reactor,
            ItemCategory.Shield => SlotKind.Shield,
            ItemCategory.Engine => SlotKind.Engine,
            ItemCategory.Armor => SlotKind.Armor,
            ItemCategory.Drone => SlotKind.Drone,
            _ => SlotKind.Weapon,
        };

        public static bool IsWeapon(ItemCategory category) =>
            category == ItemCategory.EnergyWeapon || category == ItemCategory.PhysicalWeapon || category == ItemCategory.Missile;
    }
}
