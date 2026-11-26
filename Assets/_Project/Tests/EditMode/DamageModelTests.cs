using NUnit.Framework;
using Space.Core.Combat;

namespace Space.Core.Tests
{
    public class DamageModelTests
    {
        static Shield RaisedShield(float current = 50f) => new Shield
        {
            Current = current, Max = 100f, RechargePerSecond = 10f, UpkeepPerSecond = 2f,
            RechargeCostPerPoint = 1f, Online = true, Active = true,
        };

        static Hull FullHull() => new Hull { Current = 100f, Max = 100f };

        static Weapon Make(DamageType type, float damage = 10f, float shieldDamage = 10f) =>
            new Weapon { Type = type, Damage = damage, ShieldDamage = shieldDamage };

        [Test]
        public void IonDrainsTheShieldOnly()
        {
            var shield = RaisedShield();
            var hull = FullHull();
            DamageModel.ApplyHit(Make(DamageType.Ion), ref shield, ref hull);
            Assert.AreEqual(40f, shield.Current);
            Assert.AreEqual(100f, hull.Current);
        }

        [Test]
        public void IonDoesNothingToABareHull()
        {
            var shield = default(Shield);
            var hull = FullHull();
            DamageModel.ApplyHit(Make(DamageType.Ion), ref shield, ref hull);
            Assert.AreEqual(100f, hull.Current);
        }

        [TestCase(DamageType.Laser)]
        [TestCase(DamageType.Kinetic)]
        public void PhysicalDamageIsFullyBlockedByARaisedShield(DamageType type)
        {
            var shield = RaisedShield();
            var hull = FullHull();
            DamageModel.ApplyHit(Make(type), ref shield, ref hull);
            Assert.AreEqual(50f, shield.Current);
            Assert.AreEqual(100f, hull.Current);
        }

        [TestCase(DamageType.Laser)]
        [TestCase(DamageType.Kinetic)]
        public void PhysicalDamageHitsTheHullOnceTheShieldIsDown(DamageType type)
        {
            var shield = RaisedShield(0f);
            var hull = FullHull();
            DamageModel.ApplyHit(Make(type), ref shield, ref hull);
            Assert.AreEqual(90f, hull.Current);
        }

        [Test]
        public void MissileBreaksAWeakShieldAndHitsTheHull()
        {
            var shield = RaisedShield(30f);
            var hull = FullHull();
            DamageModel.ApplyHit(Make(DamageType.Missile, damage: 60f, shieldDamage: 40f), ref shield, ref hull);
            Assert.AreEqual(0f, shield.Current);
            Assert.AreEqual(40f, hull.Current);
        }

        [Test]
        public void MissileOnlyDisruptsAStrongShield()
        {
            var shield = RaisedShield(80f);
            var hull = FullHull();
            DamageModel.ApplyHit(Make(DamageType.Missile, damage: 60f, shieldDamage: 40f), ref shield, ref hull);
            Assert.AreEqual(40f, shield.Current);
            Assert.AreEqual(100f, hull.Current);
        }

        [Test]
        public void WeaponsOnlyAimAtWhatTheyCanDamage()
        {
            Assert.IsTrue(DamageModel.IsUseful(DamageType.Ion, targetShieldUp: true));
            Assert.IsFalse(DamageModel.IsUseful(DamageType.Ion, targetShieldUp: false));
            Assert.IsFalse(DamageModel.IsUseful(DamageType.Laser, targetShieldUp: true));
            Assert.IsTrue(DamageModel.IsUseful(DamageType.Kinetic, targetShieldUp: false));
            Assert.IsTrue(DamageModel.IsUseful(DamageType.Missile, targetShieldUp: true));
        }

        [Test]
        public void ShieldUpkeepAndRechargeUseEnergy()
        {
            var energy = new Energy { Current = 50f, Max = 100f, RegenPerSecond = 0f };
            var shield = RaisedShield(50f);
            DamageModel.UpdateEnergy(ref energy, ref shield, 1f);
            Assert.AreEqual(60f, shield.Current);   // +10 recharge
            Assert.AreEqual(38f, energy.Current);   // -2 upkeep, -10 recharge
            Assert.IsTrue(shield.Active);
        }

        [Test]
        public void ShieldCollapsesWithoutEnergy()
        {
            var energy = new Energy { Current = 0f, Max = 100f, RegenPerSecond = 0f };
            var shield = RaisedShield(50f);
            DamageModel.UpdateEnergy(ref energy, ref shield, 1f);
            Assert.IsFalse(shield.Active);
            Assert.AreEqual(0f, shield.Current);
        }

        [Test]
        public void SwitchingTheShieldOffFreesItsUpkeep()
        {
            var energy = new Energy { Current = 50f, Max = 100f, RegenPerSecond = 5f };
            var shield = RaisedShield(50f);
            shield.Online = false;
            DamageModel.UpdateEnergy(ref energy, ref shield, 1f);
            Assert.AreEqual(55f, energy.Current);
            Assert.IsFalse(shield.IsUp);
        }
    }
}
