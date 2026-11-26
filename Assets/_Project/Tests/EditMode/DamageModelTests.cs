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
        public void AFullShieldStopsMostPhysicalDamage(DamageType type)
        {
            var shield = RaisedShield(100f);
            var hull = FullHull();
            DamageModel.ApplyHit(Make(type, damage: 100f), ref shield, ref hull);
            Assert.AreEqual(100f, shield.Current, "physical damage does not drain the shield");
            Assert.AreEqual(90f, hull.Current, 1e-4f, "10 % gets through a full shield");
        }

        [TestCase(DamageType.Laser)]
        [TestCase(DamageType.Kinetic)]
        public void AWeakerShieldLetsMoreThrough(DamageType type)
        {
            var shield = RaisedShield(50f);
            var hull = FullHull();
            DamageModel.ApplyHit(Make(type, damage: 100f), ref shield, ref hull);
            Assert.AreEqual(45f, hull.Current, 1e-4f, "55 % gets through a half shield");
        }

        [TestCase(DamageType.Laser)]
        [TestCase(DamageType.Kinetic)]
        public void PhysicalDamageHitsTheHullFullyOnceTheShieldIsDown(DamageType type)
        {
            var shield = RaisedShield(0f);
            var hull = FullHull();
            DamageModel.ApplyHit(Make(type), ref shield, ref hull);
            Assert.AreEqual(90f, hull.Current);
        }

        [Test]
        public void MissileBreaksAWeakShieldThenHitsTheHullFully()
        {
            var shield = RaisedShield(30f);
            var hull = FullHull();
            DamageModel.ApplyHit(Make(DamageType.Missile, damage: 60f, shieldDamage: 40f), ref shield, ref hull);
            Assert.AreEqual(0f, shield.Current);
            Assert.AreEqual(40f, hull.Current, 1e-4f);
        }

        [Test]
        public void MissileHitsTheShieldFirstThenThePhysicalDamageIsMitigated()
        {
            var shield = RaisedShield(80f);
            var hull = FullHull();
            DamageModel.ApplyHit(Make(DamageType.Missile, damage: 60f, shieldDamage: 40f), ref shield, ref hull);
            Assert.AreEqual(40f, shield.Current);
            // Shield left at 40 % stops 36 % of the warhead: 60 × 0.64 = 38.4.
            Assert.AreEqual(100f - 38.4f, hull.Current, 1e-3f);
        }

        [Test]
        public void WeaponsPreferWhatTheyHurtMost()
        {
            var raised = RaisedShield(100f);
            var down = default(Shield);
            Assert.AreEqual(1f, DamageModel.Effectiveness(DamageType.Ion, raised));
            Assert.AreEqual(0f, DamageModel.Effectiveness(DamageType.Ion, down));
            Assert.Less(DamageModel.Effectiveness(DamageType.Laser, raised), DamageModel.Effectiveness(DamageType.Laser, down));
            Assert.Greater(DamageModel.Effectiveness(DamageType.Kinetic, raised), 0f, "physical weapons still hurt shielded ships a little");
            Assert.AreEqual(1f, DamageModel.Effectiveness(DamageType.Missile, raised));
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
