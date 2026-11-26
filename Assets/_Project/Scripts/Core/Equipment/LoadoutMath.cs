using System.Collections.Generic;
using Space.Core.Combat;
using Unity.Mathematics;

namespace Space.Core.Equipment
{
    /// <summary>What a loadout makes of the ship: the numbers applied to the flagship entity.</summary>
    public struct LoadoutStats
    {
        public float Hull;
        public float ShieldMax;
        public float ShieldRechargePerPower;
        public float ShieldRebootSeconds;
        public PowerGrid Power;
        public float MaxSpeed;
        public float Acceleration;
        public float TurnRateDegrees;
        public float SpeedFactor;
        public List<WeaponStats> Weapons;
        public List<ItemDefinition> Drones;

        /// <summary>Power the subsystems could use at full capacity, compared with the reactor.</summary>
        public int PowerDemand => Power.ShieldCapacity + Power.EnergyWeaponsCapacity + Power.PhysicalWeaponsCapacity + Power.EnginesCapacity;
    }

    public static class LoadoutMath
    {
        public const float MinHull = 50f;

        /// <summary>Usual opening distribution: shield 3, energy weapons 3, engines 2 (as in the duel).</summary>
        public const int OpeningShield = 3, OpeningEnergy = 3, OpeningEngines = 2;

        public static LoadoutStats Compute(ShipLoadout loadout)
        {
            var stats = new LoadoutStats
            {
                Weapons = new List<WeaponStats>(),
                Drones = new List<ItemDefinition>(),
                SpeedFactor = 1f,
            };

            var hullBonus = 0f;
            foreach (var id in loadout.Fitted())
            {
                var item = ItemCatalog.Get(id);
                if (item == null)
                    continue;
                stats.SpeedFactor *= item.SpeedFactor;
                hullBonus += item.HullBonus;
            }

            var reactor = ItemCatalog.Get(loadout.Reactor);
            var shield = ItemCatalog.Get(loadout.Shield);
            var engine = ItemCatalog.Get(loadout.Engine);
            var armor = ItemCatalog.Get(loadout.Armor);

            stats.Hull = math.max(MinHull, (armor?.Hull ?? 0f) + hullBonus);

            if (shield != null)
            {
                stats.ShieldMax = shield.ShieldMax;
                stats.ShieldRechargePerPower = shield.ShieldRechargePerPower;
                stats.ShieldRebootSeconds = shield.ShieldRebootSeconds;
            }

            if (engine != null)
            {
                stats.MaxSpeed = engine.MaxSpeed * stats.SpeedFactor;
                stats.Acceleration = engine.Acceleration * stats.SpeedFactor;
                stats.TurnRateDegrees = engine.TurnRateDegrees * stats.SpeedFactor;
            }

            int energyCapacity = 0, physicalCapacity = 0;
            foreach (var id in loadout.Weapons)
            {
                var item = ItemCatalog.Get(id);
                if (item == null || !ItemCategories.IsWeapon(item.Category))
                    continue;
                stats.Weapons.Add(item.Weapon);
                if (item.Weapon.IsEnergy)
                    energyCapacity += item.Weapon.PowerDraw;
                else
                    physicalCapacity += item.Weapon.PowerDraw;
            }

            foreach (var id in loadout.Drones)
            {
                var item = ItemCatalog.Get(id);
                if (item != null && item.Category == ItemCategory.Drone)
                    stats.Drones.Add(item);
            }

            var grid = new PowerGrid
            {
                Reactor = (byte)math.clamp(reactor?.Power ?? 0, 0, 255),
                ShieldCapacity = (byte)(stats.ShieldMax > 0f ? math.clamp(shield.PowerCapacity, 0, 255) : 0),
                EnergyWeaponsCapacity = (byte)math.clamp(energyCapacity, 0, 255),
                PhysicalWeaponsCapacity = (byte)math.clamp(physicalCapacity, 0, 255),
                EnginesCapacity = (byte)math.clamp(engine?.PowerCapacity ?? 0, 0, 255),
            };
            // With no energy weapon, the opening power goes to the physical ones instead.
            var energy = grid.EnergyWeaponsCapacity > 0 ? OpeningEnergy : 0;
            var physical = grid.EnergyWeaponsCapacity > 0 ? 0 : OpeningEnergy;
            PowerMath.Allocate(ref grid, OpeningShield, energy, physical, OpeningEngines);
            stats.Power = grid;
            return stats;
        }

        /// <summary>The simulation weapon for a shop weapon (enabled, normal fire mode, loaded).</summary>
        public static Weapon ToWeapon(WeaponStats stats) => new()
        {
            Type = stats.Type,
            Range = stats.Range,
            Damage = stats.Damage,
            ShieldDamage = stats.ShieldDamage,
            Ammo = stats.Ammo,
            OrderedTargetOnly = stats.OrderedTargetOnly,
            Cooldown = math.max(0.05f, stats.Cooldown),
            ProjectileSpeed = math.max(1f, stats.ProjectileSpeed),
            Spread = math.radians(stats.SpreadDegrees),
            Mode = FireMode.Normal,
            Enabled = true,
        };

        /// <summary>Damage per second of a weapon at full power, against an unshielded hull.</summary>
        public static float HullDamagePerSecond(WeaponStats stats) =>
            stats.Cooldown > 0f ? stats.Damage / stats.Cooldown : 0f;

        /// <summary>Shield damage per second of a weapon at full power.</summary>
        public static float ShieldDamagePerSecond(WeaponStats stats) =>
            stats.Cooldown > 0f ? stats.ShieldDamage / stats.Cooldown : 0f;
    }
}
