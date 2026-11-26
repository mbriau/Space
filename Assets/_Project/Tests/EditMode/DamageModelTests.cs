using NUnit.Framework;
using Space.Core.Combat;

namespace Space.Core.Tests
{
    public class DamageModelTests
    {
        const float TickRate = 20f;

        static Shield RaisedShield(float current = 50f) => new Shield
        {
            Current = current, Max = 100f, RechargePerPower = 2f, RebootSeconds = 6f, Active = true,
        };

        static Hull FullHull() => new Hull { Current = 100f, Max = 100f };

        static Weapon Make(DamageType type, float hullDamage, float shieldDamage) =>
            new Weapon { Type = type, Damage = hullDamage, ShieldDamage = shieldDamage };

        static void Hit(Weapon weapon, ref Shield shield, ref Hull hull, uint tick = 100) =>
            DamageModel.ApplyHit(weapon, ref shield, ref hull, tick, TickRate);

        [Test]
        public void IonDrainsTheShieldOnly()
        {
            var shield = RaisedShield();
            var hull = FullHull();
            Hit(Make(DamageType.Ion, 0f, 10f), ref shield, ref hull);
            Assert.AreEqual(40f, shield.Current);
            Assert.AreEqual(100f, hull.Current);
        }

        [Test]
        public void EnergyWeaponHitsTheShieldThenAMitigatedHull()
        {
            var shield = RaisedShield(100f);
            var hull = FullHull();
            Hit(Make(DamageType.Laser, hullDamage: 20f, shieldDamage: 50f), ref shield, ref hull);
            Assert.AreEqual(50f, shield.Current);
            // Half shield left stops 45 %: 20 × 0.55 = 11.
            Assert.AreEqual(89f, hull.Current, 1e-4f);
        }

        [Test]
        public void AFullShieldStopsMostPhysicalDamage()
        {
            var shield = RaisedShield(100f);
            var hull = FullHull();
            Hit(Make(DamageType.Kinetic, hullDamage: 100f, shieldDamage: 0f), ref shield, ref hull);
            Assert.AreEqual(100f, shield.Current, "physical damage does not drain the shield");
            Assert.AreEqual(90f, hull.Current, 1e-4f, "10 % gets through a full shield");
        }

        [Test]
        public void PhysicalDamageHitsTheHullFullyOnceTheShieldIsDown()
        {
            var shield = RaisedShield(0f);
            var hull = FullHull();
            Hit(Make(DamageType.Kinetic, hullDamage: 40f, shieldDamage: 0f), ref shield, ref hull);
            Assert.AreEqual(60f, hull.Current);
        }

        [Test]
        public void MissileBreaksAWeakShieldThenHitsTheHullFully()
        {
            var shield = RaisedShield(30f);
            var hull = FullHull();
            Hit(Make(DamageType.Missile, hullDamage: 60f, shieldDamage: 40f), ref shield, ref hull);
            Assert.AreEqual(0f, shield.Current);
            Assert.AreEqual(40f, hull.Current, 1e-4f);
        }

        [Test]
        public void ABrokenShieldStaysDownForItsRebootDelay()
        {
            var shield = RaisedShield(10f);
            var hull = FullHull();
            Hit(Make(DamageType.Laser, 0f, 20f), ref shield, ref hull, tick: 100);
            Assert.AreEqual(100u + 6u * 20u, shield.BrokenUntilTick);

            DamageModel.UpdateShield(ref shield, power: 4, tick: 150, dt: 1f);
            Assert.AreEqual(0f, shield.Current, "no recharge while broken");

            DamageModel.UpdateShield(ref shield, power: 4, tick: 220, dt: 1f);
            Assert.AreEqual(8f, shield.Current, "recharges after the reboot delay");
        }

        [Test]
        public void MorePowerRechargesFaster()
        {
            var low = RaisedShield(0f);
            var high = RaisedShield(0f);
            DamageModel.UpdateShield(ref low, power: 1, tick: 1, dt: 1f);
            DamageModel.UpdateShield(ref high, power: 4, tick: 1, dt: 1f);
            Assert.AreEqual(2f, low.Current);
            Assert.AreEqual(8f, high.Current);
        }

        [Test]
        public void AnUnpoweredShieldIsDownAndFades()
        {
            var shield = RaisedShield(80f);
            DamageModel.UpdateShield(ref shield, power: 0, tick: 1, dt: 1f);
            Assert.IsFalse(shield.IsUp);
            Assert.AreEqual(30f, shield.Current, 1e-4f);
        }

        [Test]
        public void WeaponsPreferWhatTheyHurtMost()
        {
            var raised = RaisedShield(100f);
            var down = default(Shield);
            var ion = Make(DamageType.Ion, 0f, 10f);
            var cannon = Make(DamageType.Kinetic, 40f, 0f);
            Assert.AreEqual(1f, DamageModel.Effectiveness(ion, raised));
            Assert.AreEqual(0f, DamageModel.Effectiveness(ion, down));
            Assert.Less(DamageModel.Effectiveness(cannon, raised), DamageModel.Effectiveness(cannon, down));
            Assert.Greater(DamageModel.Effectiveness(cannon, raised), 0f);
        }
    }

    public class PowerMathTests
    {
        static PowerGrid Grid() => new PowerGrid
        {
            Reactor = 8,
            ShieldCapacity = 4, EnergyWeaponsCapacity = 3, PhysicalWeaponsCapacity = 2, EnginesCapacity = 3,
            Shield = 3, EnergyWeapons = 3, Engines = 2,
        };

        [Test]
        public void CannotUseMoreThanTheReactorProduces()
        {
            var grid = Grid();
            Assert.AreEqual(0, grid.Free);
            Assert.IsFalse(PowerMath.TryAdd(ref grid, PowerSystem.PhysicalWeapons));
        }

        [Test]
        public void MovingPowerBetweenSubsystems()
        {
            var grid = Grid();
            Assert.IsTrue(PowerMath.TryRemove(ref grid, PowerSystem.EnergyWeapons));
            Assert.IsTrue(PowerMath.TryAdd(ref grid, PowerSystem.PhysicalWeapons));
            Assert.AreEqual(2, grid.EnergyWeapons);
            Assert.AreEqual(1, grid.PhysicalWeapons);
            Assert.AreEqual(0.5f, grid.Factor(PowerSystem.PhysicalWeapons));
        }

        [Test]
        public void ASubsystemCannotExceedItsCapacity()
        {
            var grid = Grid();
            PowerMath.TryRemove(ref grid, PowerSystem.Engines);
            Assert.IsFalse(PowerMath.TryAdd(ref grid, PowerSystem.EnergyWeapons), "energy weapons already at 3/3");
            Assert.IsTrue(PowerMath.TryAdd(ref grid, PowerSystem.Shield));
            Assert.AreEqual(4, grid.Shield);
        }

        [Test]
        public void AllocateServesInOrderAndGivesTheRestToTheEngines()
        {
            var grid = Grid();
            // "Shield broken: physical weapons on, everything else on the shield."
            PowerMath.Allocate(ref grid, shield: 4, energyWeapons: 0, physicalWeapons: 2, engines: 0);
            Assert.AreEqual(4, grid.Shield);
            Assert.AreEqual(0, grid.EnergyWeapons);
            Assert.AreEqual(2, grid.PhysicalWeapons);
            Assert.AreEqual(2, grid.Engines);
            Assert.AreEqual(8, grid.Allocated);
        }

        [Test]
        public void EnginesNeverStopTheShipCompletely()
        {
            var grid = Grid();
            grid.Engines = 0;
            Assert.Greater(DamageModel.EngineFactor(grid), 0f);
            grid.Engines = 3;
            Assert.AreEqual(1f, DamageModel.EngineFactor(grid), 1e-6f);
        }
    }
}
