using NUnit.Framework;
using Space.Core.Combat;
using Unity.Collections;

namespace Space.Core.Tests
{
    public class WeaponPowerTests
    {
        static Weapon Laser() => new Weapon
        {
            Type = DamageType.Laser, PowerDraw = 3, SuperchargeDraw = 2, SuperchargeDamageFactor = 1.6f,
        };

        static Weapon Cannon() => new Weapon { Type = DamageType.Kinetic, PowerDraw = 2, Ammo = 8 };

        /// <summary>The Cargo before its weapons: reactor 8, shield 3, engines 2.</summary>
        static PowerGrid Grid() => new PowerGrid
        {
            Reactor = 8, ShieldCapacity = 4, EnginesCapacity = 3, Shield = 3, Engines = 2,
        };

        [Test]
        public void APoweredWeaponDrawsItsWholeCost()
        {
            var grid = Grid();
            var laser = Laser();
            Assert.IsTrue(WeaponPower.TrySetPowered(ref grid, ref laser, true));
            Assert.IsTrue(laser.Powered);
            Assert.AreEqual(3, grid.Weapons);
            Assert.AreEqual(0, grid.Free);
            Assert.AreEqual(3, WeaponPower.Draw(laser));
        }

        [Test]
        public void PoweringUpIsRefusedWhenTheReactorIsFull()
        {
            var grid = Grid();
            var laser = Laser();
            var cannon = Cannon();
            WeaponPower.TrySetPowered(ref grid, ref laser, true);

            Assert.IsFalse(WeaponPower.TrySetPowered(ref grid, ref cannon, true), "2 units needed, 0 free");
            Assert.IsFalse(cannon.Powered);
            Assert.AreEqual(3, grid.Weapons);

            // Switching the laser off frees its units for the cannon.
            Assert.IsTrue(WeaponPower.TrySetPowered(ref grid, ref laser, false));
            Assert.IsTrue(WeaponPower.TrySetPowered(ref grid, ref cannon, true));
            Assert.AreEqual(2, grid.Weapons);
            Assert.AreEqual(1, grid.Free);
        }

        [Test]
        public void SuperchargeDrawsTheExtraUnitsAndPowerDownReleasesEverything()
        {
            var grid = Grid();
            grid.Shield = 1; // 8 - 1 - 2 = 5 free: laser 3 + supercharge 2
            var laser = Laser();
            Assert.IsFalse(WeaponPower.TrySetSupercharged(ref grid, ref laser, true), "an unpowered weapon cannot be supercharged");

            WeaponPower.TrySetPowered(ref grid, ref laser, true);
            Assert.IsTrue(WeaponPower.TrySetSupercharged(ref grid, ref laser, true));
            Assert.AreEqual(5, WeaponPower.Draw(laser));
            Assert.AreEqual(5, grid.Weapons);
            Assert.AreEqual(0, grid.Free);

            Assert.IsTrue(WeaponPower.TrySetPowered(ref grid, ref laser, false));
            Assert.IsFalse(laser.Supercharged);
            Assert.AreEqual(0, grid.Weapons);
        }

        [Test]
        public void SuperchargeIsRefusedWithoutRoomOrWithoutTheMode()
        {
            var grid = Grid();
            var laser = Laser();
            WeaponPower.TrySetPowered(ref grid, ref laser, true);
            Assert.IsFalse(WeaponPower.TrySetSupercharged(ref grid, ref laser, true), "0 free, 2 needed");

            grid.Shield = 0;
            grid.Engines = 0; // 8 - 3 for the laser = 5 free
            var cannon = Cannon();
            WeaponPower.TrySetPowered(ref grid, ref cannon, true);
            Assert.IsFalse(WeaponPower.TrySetSupercharged(ref grid, ref cannon, true), "the cannon has no supercharge mode");

            Assert.IsTrue(WeaponPower.TrySetSupercharged(ref grid, ref laser, true));
            Assert.IsTrue(WeaponPower.TrySetSupercharged(ref grid, ref laser, false));
            Assert.AreEqual(5, grid.Weapons, "laser 3 + cannon 2");
        }

        [Test]
        public void OpeningPowersTheWeaponsInOrderWhileTheyFit()
        {
            var grid = Grid();
            using var weapons = new NativeArray<Weapon>(new[] { Laser(), Cannon() }, Allocator.Temp);
            WeaponPower.PowerUpInOrder(ref grid, weapons);
            Assert.IsTrue(weapons[0].Powered, "laser: 3 units, fits");
            Assert.IsFalse(weapons[1].Powered, "cannon: 2 units, reactor full");

            WeaponPower.PowerDownAll(ref grid, weapons);
            WeaponPower.PowerUpInOrder(ref grid, weapons, energy: false, physical: true);
            Assert.IsFalse(weapons[0].Powered);
            Assert.IsTrue(weapons[1].Powered);
            Assert.AreEqual(2, grid.Weapons);
        }

        [Test]
        public void SuperchargedShotsHitHarder()
        {
            Assert.AreEqual(1.6f, WeaponMath.DamageFactor(supercharged: true, superchargeFactor: 1.6f), 1e-6f);
            Assert.AreEqual(1f, WeaponMath.DamageFactor(supercharged: false, superchargeFactor: 1.6f), 1e-6f);
            Assert.AreEqual(1f, WeaponMath.DamageFactor(supercharged: true, superchargeFactor: 0f), 1e-6f);
        }
    }
}
