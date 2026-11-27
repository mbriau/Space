using NUnit.Framework;
using Space.Core.Combat;
using Space.Core.Equipment;
using Unity.Collections;

namespace Space.Core.Tests
{
    public class ShipSettingsTests
    {
        static readonly string[] Keys = { "0:laser", "1:cannon" };

        static PowerGrid Grid() => new() { Reactor = 8, ShieldCapacity = 4, EnginesCapacity = 3 };

        static NativeArray<Weapon> Weapons() => new(new[]
        {
            new Weapon { Type = DamageType.Laser, PowerDraw = 3, SuperchargeDraw = 2, SuperchargeDamageFactor = 1.6f, Ammo = -1 },
            new Weapon { Type = DamageType.Kinetic, PowerDraw = 2, Ammo = 8 },
        }, Allocator.Temp);

        [Test]
        public void TheSettingsOfALevelComeBackInTheNext()
        {
            // During the level: shield 1, engines 0, laser supercharged (5), cannon on (2) and manual.
            var grid = Grid();
            var played = Weapons();
            PowerMath.Allocate(ref grid, shield: 1, engines: 0);
            var laser = played[0];
            WeaponPower.TrySetPowered(ref grid, ref laser, true);
            WeaponPower.TrySetSupercharged(ref grid, ref laser, true);
            played[0] = laser;
            var cannon = played[1];
            WeaponPower.TrySetPowered(ref grid, ref cannon, true);
            cannon.Control = WeaponControl.Manual;
            played[1] = cannon;
            var settings = ShipSettings.Capture(grid, played, Keys);

            // The next level's ship, fitted with the usual opening.
            var next = Grid();
            var fresh = Weapons();
            PowerMath.Allocate(ref next, 3, 2);
            WeaponPower.PowerUpInOrder(ref next, fresh);
            settings.Apply(ref next, fresh, Keys);

            Assert.AreEqual(1, next.Shield);
            Assert.AreEqual(0, next.Engines);
            Assert.IsTrue(fresh[0].Powered && fresh[0].Supercharged, "laser surchargé");
            Assert.IsTrue(fresh[1].Powered, "canon allumé");
            Assert.AreEqual(WeaponControl.Manual, fresh[1].Control);
            Assert.AreEqual(8, next.Allocated);
        }

        [Test]
        public void AWeaponChangedBetweenLevelsStartsAsUsual()
        {
            var grid = Grid();
            var played = Weapons();
            PowerMath.Allocate(ref grid, 3, 2);
            var laser = played[0];
            laser.Control = WeaponControl.Off;
            played[0] = laser;
            var settings = ShipSettings.Capture(grid, played, Keys);

            // A new weapon in slot 0: no setting for it.
            var next = Grid();
            var fresh = Weapons();
            settings.Apply(ref next, fresh, new[] { "0:ion", "1:cannon" });
            Assert.AreEqual(WeaponControl.Auto, fresh[0].Control, "la nouvelle arme est en automatique");
            Assert.IsTrue(fresh[0].Powered, "et allumée si elle tient dans le réacteur");
        }

        [Test]
        public void ASmallerReactorKeepsWhatFits()
        {
            var settings = new ShipSettings { Shield = 4, Engines = 3 };
            settings.Weapons.Add(new ShipSettings.WeaponSetting { Key = Keys[0], Powered = true, Control = WeaponControl.Auto });
            var grid = new PowerGrid { Reactor = 6, ShieldCapacity = 4, EnginesCapacity = 3 };
            var fresh = Weapons();
            settings.Apply(ref grid, fresh, Keys);
            Assert.LessOrEqual(grid.Allocated, 6, "jamais plus que le réacteur");
            Assert.AreEqual(4, grid.Shield);
        }
    }
}
