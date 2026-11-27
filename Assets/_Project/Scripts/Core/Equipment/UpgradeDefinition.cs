namespace Space.Core.Equipment
{
    /// <summary>
    /// A generic improvement fitted onto one piece of equipment the player owns (a weapon, a
    /// shield...). It stays attached to that item, in a slot or in the hold, and changes its numbers
    /// (<see cref="UpgradeMath.Apply"/>). Each item takes up to <see cref="ShipLoadout.UpgradeSlots"/>.
    /// Factors multiply the item's numbers (1 = unchanged); deltas are added.
    /// </summary>
    public sealed class UpgradeDefinition
    {
        public string Id;
        public string Name;
        public string Description;

        /// <summary>Rarity, 1 to 3, as for items: rarer upgrades show up later in a run.</summary>
        public int Tier = 1;

        /// <summary>Kinds of equipment it can be fitted onto.</summary>
        public ItemCategory[] Targets = System.Array.Empty<ItemCategory>();

        /// <summary>Only for weapons with a supercharge mode.</summary>
        public bool RequiresSupercharge;

        /// <summary>Only for weapons with limited ammunition.</summary>
        public bool RequiresAmmo;

        // Any item.
        public float SpeedFactor = 1f;

        // Weapons.
        public float CooldownFactor = 1f;
        public float RangeFactor = 1f;
        public float SpreadFactor = 1f;
        public float DamageFactor = 1f;
        public float ShieldDamageFactor = 1f;
        public float ProjectileSpeedFactor = 1f;
        public float AmmoFactor = 1f;
        public int PowerDrawDelta;
        public int SuperchargeDrawDelta;
        public float SuperchargeDamageBonus;

        // Shield.
        public float ShieldMaxFactor = 1f;
        public float ShieldRechargeFactor = 1f;
        public float ShieldRebootFactor = 1f;

        // Shield or engine.
        public int PowerCapacityDelta;

        // Engine.
        public float MaxSpeedFactor = 1f;
        public float AccelerationFactor = 1f;
        public float TurnRateFactor = 1f;

        // Armor.
        public float HullFactor = 1f;

        // Reactor.
        public int PowerDelta;
    }
}
