using Unity.Mathematics;

namespace Space.Core.Combat
{
    /// <summary>
    /// How each damage type interacts with shields and hulls, and how the reactor powers the shield.
    /// Pure functions, unit tested.
    /// </summary>
    public static class DamageModel
    {
        /// <summary>Share of physical damage a full shield stops. The rest always reaches the hull.</summary>
        public const float MaxShieldMitigation = 0.9f;

        /// <summary>
        /// Share of physical damage that reaches the hull: the stronger the shield (charge relative to
        /// its capacity), the less gets through. A downed shield lets everything through.
        /// </summary>
        public static float HullDamageFactor(in Shield shield)
        {
            if (!shield.IsUp || shield.Max <= 0f)
                return 1f;
            return 1f - MaxShieldMitigation * math.saturate(shield.Current / shield.Max);
        }

        /// <summary>Applies one hit of <paramref name="weapon"/> to a target.</summary>
        public static void ApplyHit(in Weapon weapon, ref Shield shield, ref Hull hull)
        {
            switch (weapon.Type)
            {
                case DamageType.Ion:
                    if (shield.IsUp)
                        shield.Current = math.max(0f, shield.Current - weapon.ShieldDamage);
                    break;

                case DamageType.Laser:
                case DamageType.Kinetic:
                    hull.Current = math.max(0f, hull.Current - weapon.Damage * HullDamageFactor(shield));
                    break;

                case DamageType.Missile:
                    // Shield first, then the physical warhead, mitigated by what is left of the shield.
                    if (shield.IsUp)
                        shield.Current = math.max(0f, shield.Current - weapon.ShieldDamage);
                    hull.Current = math.max(0f, hull.Current - weapon.Damage * HullDamageFactor(shield));
                    break;
            }
        }

        /// <summary>
        /// How much of its damage a weapon would deal to a target right now, from 0 (useless) to 1.
        /// Weapons prefer targets where they are most effective.
        /// </summary>
        public static float Effectiveness(DamageType type, in Shield targetShield) => type switch
        {
            DamageType.Ion => targetShield.IsUp ? 1f : 0f,
            DamageType.Missile => 1f,
            _ => HullDamageFactor(targetShield),
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
