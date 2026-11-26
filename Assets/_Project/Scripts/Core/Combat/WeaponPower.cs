using Unity.Collections;

namespace Space.Core.Combat
{
    /// <summary>
    /// Switching weapons on and off the reactor. A weapon draws its whole cost or nothing; switching
    /// it on (or supercharging it) is refused when the reactor has too few free units. Keeps
    /// <see cref="PowerGrid.Weapons"/> equal to what the powered weapons draw. Pure functions, unit tested.
    /// </summary>
    public static class WeaponPower
    {
        /// <summary>Units the weapon draws right now.</summary>
        public static int Draw(in Weapon weapon) =>
            weapon.Powered ? weapon.PowerDraw + (weapon.Supercharged ? weapon.SuperchargeDraw : 0) : 0;

        /// <summary>Powers the weapon up (if the reactor has its cost free) or down. Returns whether it is now as asked.</summary>
        public static bool TrySetPowered(ref PowerGrid grid, ref Weapon weapon, bool powered)
        {
            if (weapon.Powered == powered)
                return true;

            if (powered)
            {
                if (grid.Free < weapon.PowerDraw)
                    return false;
                weapon.Powered = true;
                weapon.Supercharged = false;
                grid.Weapons = (byte)(grid.Weapons + weapon.PowerDraw);
                return true;
            }

            grid.Weapons = (byte)(grid.Weapons - Draw(weapon));
            weapon.Powered = false;
            weapon.Supercharged = false;
            return true;
        }

        /// <summary>
        /// Supercharges a powered weapon (if it can be and the reactor has the extra units free), or
        /// brings it back to normal. Returns whether it is now as asked.
        /// </summary>
        public static bool TrySetSupercharged(ref PowerGrid grid, ref Weapon weapon, bool supercharged)
        {
            if (weapon.Supercharged == supercharged)
                return true;

            if (supercharged)
            {
                if (!weapon.Powered || !weapon.CanSupercharge || grid.Free < weapon.SuperchargeDraw)
                    return false;
                weapon.Supercharged = true;
                grid.Weapons = (byte)(grid.Weapons + weapon.SuperchargeDraw);
                return true;
            }

            grid.Weapons = (byte)(grid.Weapons - weapon.SuperchargeDraw);
            weapon.Supercharged = false;
            return true;
        }

        /// <summary>Powers every weapon down.</summary>
        public static void PowerDownAll(ref PowerGrid grid, NativeArray<Weapon> weapons)
        {
            for (var i = 0; i < weapons.Length; i++)
            {
                var weapon = weapons[i];
                TrySetPowered(ref grid, ref weapon, false);
                weapons[i] = weapon;
            }
        }

        /// <summary>
        /// Powers up, in order, the weapons of the given kinds whose cost still fits in the reactor
        /// (opening distribution, AI). Weapons that do not fit stay off.
        /// </summary>
        public static void PowerUpInOrder(ref PowerGrid grid, NativeArray<Weapon> weapons, bool energy = true, bool physical = true)
        {
            for (var i = 0; i < weapons.Length; i++)
            {
                var weapon = weapons[i];
                if (weapon.IsEnergy ? !energy : !physical)
                    continue;
                TrySetPowered(ref grid, ref weapon, true);
                weapons[i] = weapon;
            }
        }
    }
}
