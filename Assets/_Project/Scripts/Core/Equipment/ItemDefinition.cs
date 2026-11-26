using Space.Core.Combat;

namespace Space.Core.Equipment
{
    /// <summary>
    /// Numbers of a weapon, as sold in the shop. Converted to a <see cref="Weapon"/> buffer element
    /// when the ship is fitted (<see cref="LoadoutMath.ToWeapon"/>).
    /// </summary>
    public struct WeaponStats
    {
        public DamageType Type;
        public float Range;

        /// <summary>Damage to the hull, mitigated by the target's shield.</summary>
        public float Damage;

        /// <summary>Damage to the shield, applied first.</summary>
        public float ShieldDamage;

        /// <summary>Seconds between two shots at full power.</summary>
        public float Cooldown;

        /// <summary>Shots per level; negative means unlimited.</summary>
        public int Ammo;

        public float ProjectileSpeed;
        public float SpreadDegrees;
        public bool OrderedTargetOnly;

        /// <summary>Power units the weapon draws from the reactor while it is powered.</summary>
        public int PowerDraw;

        /// <summary>Extra units drawn when supercharged; 0 = no supercharge mode.</summary>
        public int SuperchargeDraw;

        /// <summary>Damage multiplier of a supercharged shot.</summary>
        public float SuperchargeDamageFactor;

        public bool CanSupercharge => SuperchargeDraw > 0;

        public bool IsEnergy => Type == DamageType.Ion || Type == DamageType.Laser;
    }

    /// <summary>
    /// One piece of equipment. Items are side-grades more than upgrades: each one trades something
    /// (range, reload time, power, speed, hull) for something else. Only the fields of its category
    /// are used; <see cref="SpeedFactor"/> and <see cref="HullBonus"/> apply to any item.
    /// </summary>
    public sealed class ItemDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public ItemCategory Category;

        /// <summary>Rarity, 1 to 3: rarer items show up later in a run and cost more.</summary>
        public int Tier = 1;
        public int Price;

        /// <summary>Fitted on the starting ship; never offered in the shop.</summary>
        public bool Starter;

        // Any category.
        /// <summary>Multiplies the ship's speed, acceleration and turn rate (heavy or light equipment).</summary>
        public float SpeedFactor = 1f;

        /// <summary>Added to the hull (negative for fragile equipment).</summary>
        public float HullBonus;

        // Reactor.
        public int Power;

        // Shield.
        public float ShieldMax;
        public float ShieldRechargePerPower;
        public float ShieldRebootSeconds;

        /// <summary>Shield or engine: power units it can use (more power = faster recharge or speed).</summary>
        public int PowerCapacity;

        // Weapons (energy, physical, missile) and drone armament.
        public WeaponStats Weapon;

        // Engine.
        public float MaxSpeed;
        public float Acceleration;
        public float TurnRateDegrees;

        // Armor.
        public float Hull;

        // Drone: the escort ship it adds and its numbers.
        public string DroneShip = "Drone";
        public float DroneHull;
        public float DroneSpeed;
    }
}
