using Unity.Mathematics;

namespace Space.Core.Combat
{
    /// <summary>
    /// How hits interact with shields and hulls, and how the shield runs on its power.
    /// Pure functions, unit tested.
    /// </summary>
    public static class DamageModel
    {
        /// <summary>Share of hull damage a full shield stops. The rest always reaches the hull.</summary>
        public const float MaxShieldMitigation = 0.9f;

        /// <summary>Share of its charge an unpowered shield loses per second.</summary>
        public const float UnpoweredDecayPerSecond = 0.5f;

        /// <summary>
        /// Share of hull damage that gets through: the stronger the shield (charge relative to its
        /// capacity), the less. A downed shield lets everything through.
        /// </summary>
        public static float HullDamageFactor(in Shield shield)
        {
            if (!shield.IsUp || shield.Max <= 0f)
                return 1f;
            return 1f - MaxShieldMitigation * math.saturate(shield.Current / shield.Max);
        }

        /// <summary>
        /// Applies one hit: shield damage first (if the shield is up), then hull damage mitigated by
        /// what is left of the shield. A shield brought to zero is broken until
        /// <paramref name="tick"/> + its reboot delay.
        /// </summary>
        public static void ApplyHit(in Weapon weapon, ref Shield shield, ref Hull hull, uint tick, float ticksPerSecond)
        {
            if (shield.IsUp && weapon.ShieldDamage > 0f)
            {
                shield.Current = math.max(0f, shield.Current - weapon.ShieldDamage);
                if (shield.Current <= 0f)
                    shield.BrokenUntilTick = tick + (uint)math.ceil(shield.RebootSeconds * ticksPerSecond);
            }

            if (weapon.Damage > 0f)
                hull.Current = math.max(0f, hull.Current - weapon.Damage * HullDamageFactor(shield));
        }

        /// <summary>
        /// Expected share of a weapon's damage that would count against a target right now, from 0
        /// (useless) to 1. Weapons prefer targets where they are most effective.
        /// </summary>
        public static float Effectiveness(in Weapon weapon, in Shield targetShield)
        {
            var total = weapon.ShieldDamage + weapon.Damage;
            if (total <= 0f)
                return 0f;
            var useful = (targetShield.IsUp ? weapon.ShieldDamage : 0f) + weapon.Damage * HullDamageFactor(targetShield);
            return useful / total;
        }

        /// <summary>
        /// One tick of the shield: with power it recharges (unless broken); without power it is down
        /// and its charge fades.
        /// </summary>
        public static void UpdateShield(ref Shield shield, int power, uint tick, float dt)
        {
            shield.Active = power > 0 && shield.Max > 0f;
            if (!shield.Active)
            {
                shield.Current = math.max(0f, shield.Current - shield.Max * UnpoweredDecayPerSecond * dt);
                return;
            }

            if (tick < shield.BrokenUntilTick)
                return;

            shield.Current = math.min(shield.Max, shield.Current + shield.RechargePerPower * power * dt);
        }

        /// <summary>Engines: speed and acceleration factor from their power (a ship never stops completely).</summary>
        public static float EngineFactor(in PowerGrid grid)
        {
            return grid.EnginesCapacity > 0 ? 0.35f + 0.65f * grid.Factor(PowerSystem.Engines) : 1f;
        }
    }

    /// <summary>Changing the power distribution. Pure functions, unit tested.</summary>
    public static class PowerMath
    {
        /// <summary>Moves one unit into a subsystem, if the reactor has a free unit and the subsystem room for it.</summary>
        public static bool TryAdd(ref PowerGrid grid, PowerSystem system)
        {
            if (grid.Free <= 0 || grid.Get(system) >= grid.Capacity(system))
                return false;
            grid.Set(system, grid.Get(system) + 1);
            return true;
        }

        /// <summary>Takes one unit out of a subsystem, back to the reactor's free units.</summary>
        public static bool TryRemove(ref PowerGrid grid, PowerSystem system)
        {
            if (grid.Get(system) <= 0)
                return false;
            grid.Set(system, grid.Get(system) - 1);
            return true;
        }

        /// <summary>
        /// Sets a whole distribution at once (AI, presets). Requests are served in the order
        /// shield, energy weapons, physical weapons, engines, each clamped to its capacity and to
        /// what the reactor has left; any unit left over goes to the engines.
        /// </summary>
        public static void Allocate(ref PowerGrid grid, int shield, int energyWeapons, int physicalWeapons, int engines)
        {
            grid.Shield = grid.EnergyWeapons = grid.PhysicalWeapons = grid.Engines = 0;
            var left = (int)grid.Reactor;
            left = Give(ref grid, PowerSystem.Shield, left, shield);
            left = Give(ref grid, PowerSystem.EnergyWeapons, left, energyWeapons);
            left = Give(ref grid, PowerSystem.PhysicalWeapons, left, physicalWeapons);
            Give(ref grid, PowerSystem.Engines, left, math.max(engines, left));
        }

        static int Give(ref PowerGrid grid, PowerSystem system, int available, int request)
        {
            var amount = math.max(0, math.min(math.min(available, grid.Capacity(system)), request));
            grid.Set(system, amount);
            return available - amount;
        }
    }
}
