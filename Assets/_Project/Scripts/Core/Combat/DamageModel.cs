using Unity.Mathematics;

namespace Space.Core.Combat
{
    /// <summary>
    /// How each damage type interacts with shields and hulls, and how the reactor powers the shield.
    /// Pure functions, unit tested.
    /// </summary>
    public static class DamageModel
    {
        /// <summary>Applies one hit of <paramref name="weapon"/> to a target.</summary>
        public static void ApplyHit(in Weapon weapon, ref Shield shield, ref Hull hull)
        {
            var shieldUp = shield.IsUp;
            switch (weapon.Type)
            {
                case DamageType.Ion:
                    if (shieldUp)
                        shield.Current = math.max(0f, shield.Current - weapon.ShieldDamage);
                    break;

                case DamageType.Laser:
                case DamageType.Kinetic:
                    if (!shieldUp)
                        hull.Current = math.max(0f, hull.Current - weapon.Damage);
                    break;

                case DamageType.Missile:
                    // Disrupts the shield first; the warhead reaches the hull once the shield is down.
                    if (shieldUp)
                        shield.Current = math.max(0f, shield.Current - weapon.ShieldDamage);
                    if (!shield.IsUp)
                        hull.Current = math.max(0f, hull.Current - weapon.Damage);
                    break;
            }
        }

        /// <summary>Whether a weapon can do anything against a target in its current state.</summary>
        public static bool IsUseful(DamageType type, bool targetShieldUp) => type switch
        {
            DamageType.Ion => targetShieldUp,
            DamageType.Laser => !targetShieldUp,
            DamageType.Kinetic => !targetShieldUp,
            _ => true,
        };

        /// <summary>
        /// One tick of reactor and shield: the reactor regenerates, then pays the shield upkeep (the
        /// shield drops if it cannot), then recharges the shield with what is left.
        /// </summary>
        public static void UpdateEnergy(ref Energy energy, ref Shield shield, float dt)
        {
            energy.Current = math.min(energy.Max, energy.Current + energy.RegenPerSecond * dt);

            if (!shield.Online || shield.Max <= 0f)
            {
                shield.Active = false;
                shield.Current = 0f;
                return;
            }

            var upkeep = shield.UpkeepPerSecond * dt;
            if (energy.Current < upkeep)
            {
                // Not enough power: the shield collapses and must recharge from zero.
                shield.Active = false;
                shield.Current = 0f;
                return;
            }

            energy.Current -= upkeep;
            shield.Active = true;

            var missing = shield.Max - shield.Current;
            if (missing <= 0f)
                return;

            var recharge = math.min(shield.RechargePerSecond * dt, missing);
            if (shield.RechargeCostPerPoint > 0f)
                recharge = math.min(recharge, energy.Current / shield.RechargeCostPerPoint);
            shield.Current += recharge;
            energy.Current -= recharge * shield.RechargeCostPerPoint;
        }
    }
}
