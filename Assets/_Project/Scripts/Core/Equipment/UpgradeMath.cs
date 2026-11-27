using System.Collections.Generic;
using Unity.Mathematics;

namespace Space.Core.Equipment
{
    /// <summary>Which item an upgrade fits, and what the item becomes once upgraded.</summary>
    public static class UpgradeMath
    {
        public static bool Accepts(UpgradeDefinition upgrade, ItemDefinition item)
        {
            if (upgrade == null || item == null)
                return false;
            if (System.Array.IndexOf(upgrade.Targets, item.Category) < 0)
                return false;
            if (upgrade.RequiresSupercharge && !item.Weapon.CanSupercharge)
                return false;
            if (upgrade.RequiresAmmo && item.Weapon.Ammo <= 0)
                return false;
            return true;
        }

        /// <summary>A copy of <paramref name="item"/> with the upgrades applied in order; the catalog item is left untouched.</summary>
        public static ItemDefinition Apply(ItemDefinition item, IEnumerable<UpgradeDefinition> upgrades)
        {
            if (item == null)
                return null;
            var copy = item.Clone();
            foreach (var upgrade in upgrades)
                if (upgrade != null)
                    ApplyTo(copy, upgrade);
            return copy;
        }

        static void ApplyTo(ItemDefinition item, UpgradeDefinition u)
        {
            item.SpeedFactor *= u.SpeedFactor;

            if (ItemCategories.IsWeapon(item.Category))
            {
                var w = item.Weapon;
                w.Cooldown *= u.CooldownFactor;
                w.Range *= u.RangeFactor;
                w.SpreadDegrees *= u.SpreadFactor;
                w.Damage *= u.DamageFactor;
                w.ShieldDamage *= u.ShieldDamageFactor;
                w.ProjectileSpeed *= u.ProjectileSpeedFactor;
                if (w.Ammo > 0)
                    w.Ammo = (int)math.ceil(w.Ammo * u.AmmoFactor - 1e-4f);
                w.PowerDraw = math.max(0, w.PowerDraw + u.PowerDrawDelta);
                if (w.CanSupercharge)
                {
                    w.SuperchargeDraw = math.max(1, w.SuperchargeDraw + u.SuperchargeDrawDelta);
                    w.SuperchargeDamageFactor += u.SuperchargeDamageBonus;
                }
                item.Weapon = w;
            }

            item.ShieldMax *= u.ShieldMaxFactor;
            item.ShieldRechargePerPower *= u.ShieldRechargeFactor;
            item.ShieldRebootSeconds *= u.ShieldRebootFactor;
            item.PowerCapacity = math.max(0, item.PowerCapacity + u.PowerCapacityDelta);

            item.MaxSpeed *= u.MaxSpeedFactor;
            item.Acceleration *= u.AccelerationFactor;
            item.TurnRateDegrees *= u.TurnRateFactor;

            item.Hull *= u.HullFactor;
            item.Power = math.max(0, item.Power + u.PowerDelta);
        }
    }
}
