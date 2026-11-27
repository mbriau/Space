using System.Collections.Generic;
using Space.Core.Combat;
using Unity.Collections;

namespace Space.Core.Equipment
{
    /// <summary>
    /// How the player set the ship up during a level: the power of the shield and the engines, and
    /// for each weapon whether it is powered or supercharged, who fires it and how eagerly. Kept by
    /// the run and given back to the ship of the next level. Weapons are matched by slot and item, so
    /// a weapon changed between levels starts with the usual settings. Pure functions, unit tested.
    /// </summary>
    public sealed class ShipSettings
    {
        public struct WeaponSetting
        {
            public string Key;
            public bool Powered;
            public bool Supercharged;
            public WeaponControl Control;
            public FireMode Mode;
        }

        public int Shield;
        public int Engines;
        public readonly List<WeaponSetting> Weapons = new();

        /// <summary>
        /// Reads the settings of a ship: its power grid and its weapons, whose loadout keys are
        /// <paramref name="keys"/> (same order as the weapons).
        /// </summary>
        public static ShipSettings Capture(in PowerGrid grid, NativeArray<Weapon> weapons, IReadOnlyList<string> keys)
        {
            var settings = new ShipSettings { Shield = grid.Shield, Engines = grid.Engines };
            for (var i = 0; i < weapons.Length && i < keys.Count; i++)
            {
                var weapon = weapons[i];
                settings.Weapons.Add(new WeaponSetting
                {
                    Key = keys[i],
                    Powered = weapon.Powered,
                    Supercharged = weapon.Supercharged,
                    Control = weapon.Control,
                    Mode = weapon.Mode,
                });
            }
            return settings;
        }

        /// <summary>
        /// Gives the settings back to a freshly fitted ship: shield and engines first (within their
        /// capacity), then the weapons that were powered (and supercharged) as far as the reactor
        /// allows; weapons new to the ship are powered in slot order if they still fit. Controls and
        /// fire modes come back as they were.
        /// </summary>
        public void Apply(ref PowerGrid grid, NativeArray<Weapon> weapons, IReadOnlyList<string> keys)
        {
            WeaponPower.PowerDownAll(ref grid, weapons);
            PowerMath.Allocate(ref grid, Shield, Engines);

            var known = new bool[weapons.Length];
            for (var i = 0; i < weapons.Length && i < keys.Count; i++)
            {
                if (!TryFind(keys[i], out var setting))
                    continue;
                known[i] = true;
                var weapon = weapons[i];
                weapon.Control = setting.Control;
                weapon.Mode = setting.Mode;
                if (setting.Powered && WeaponPower.TrySetPowered(ref grid, ref weapon, true) && setting.Supercharged)
                    WeaponPower.TrySetSupercharged(ref grid, ref weapon, true);
                weapons[i] = weapon;
            }

            for (var i = 0; i < weapons.Length; i++)
            {
                if (known[i])
                    continue;
                var weapon = weapons[i];
                WeaponPower.TrySetPowered(ref grid, ref weapon, true);
                weapons[i] = weapon;
            }
        }

        bool TryFind(string key, out WeaponSetting setting)
        {
            foreach (var weapon in Weapons)
            {
                if (weapon.Key == key)
                {
                    setting = weapon;
                    return true;
                }
            }
            setting = default;
            return false;
        }
    }
}
