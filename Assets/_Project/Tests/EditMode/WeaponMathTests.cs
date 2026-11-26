using NUnit.Framework;
using Space.Core.Combat;
using Unity.Mathematics;

namespace Space.Core.Tests
{
    public class WeaponMathTests
    {
        static float Probability(float distance, float radius, float3 targetVelocity, float spreadDegrees = 0.8f)
        {
            var toTarget = new float3(0f, 0f, distance);
            return WeaponMath.HitProbability(distance, radius, toTarget, targetVelocity, 1000f, math.radians(spreadDegrees));
        }

        [Test]
        public void PerfectWeaponAlwaysHitsAStillTarget()
        {
            Assert.AreEqual(1f, Probability(300f, 10f, float3.zero, spreadDegrees: 0f), 1e-6f);
        }

        [Test]
        public void SidewaysMotionMakesHitsHarder()
        {
            var still = Probability(300f, 7f, float3.zero);
            var crossing = Probability(300f, 7f, new float3(100f, 0f, 0f));
            Assert.Less(crossing, still);
        }

        [Test]
        public void MotionAlongTheLineOfFireDoesNotMatter()
        {
            var still = Probability(300f, 7f, float3.zero);
            var fleeing = Probability(300f, 7f, new float3(0f, 0f, 100f));
            Assert.AreEqual(still, fleeing, 1e-6f);
        }

        [Test]
        public void BigTargetsAreEasierToHit()
        {
            Assert.Greater(Probability(300f, 20f, new float3(60f, 0f, 0f)), Probability(300f, 7f, new float3(60f, 0f, 0f)));
        }

        [Test]
        public void FireModesOrderTheirThresholds()
        {
            Assert.Less(WeaponMath.FireThreshold(FireMode.TriggerHappy), WeaponMath.FireThreshold(FireMode.Normal));
            Assert.Less(WeaponMath.FireThreshold(FireMode.Normal), WeaponMath.FireThreshold(FireMode.Conservative));
            Assert.Less(WeaponMath.CooldownFactor(FireMode.TriggerHappy), 1f);
        }

        [Test]
        public void ADoubtfulShotIsTakenOnlyByTriggerHappyWeapons()
        {
            const float doubtful = 0.2f;
            Assert.IsTrue(WeaponMath.ShouldFire(FireMode.TriggerHappy, doubtful));
            Assert.IsFalse(WeaponMath.ShouldFire(FireMode.Normal, doubtful));
            Assert.IsFalse(WeaponMath.ShouldFire(FireMode.Conservative, doubtful));
        }

        [Test]
        public void AWeaponChargesAtFullSpeedOrNotAtAll()
        {
            Assert.AreEqual(1f, WeaponMath.ChargeRate(powered: true));
            Assert.AreEqual(0f, WeaponMath.ChargeRate(powered: false));
        }

        [Test]
        public void OnlyPoweredArmedWeaponsLookForTargets()
        {
            Assert.IsTrue(WeaponMath.CanEngage(WeaponControl.Auto, ammo: -1, powered: true));
            Assert.IsTrue(WeaponMath.CanEngage(WeaponControl.Manual, ammo: 3, powered: true));
            Assert.IsFalse(WeaponMath.CanEngage(WeaponControl.Off, ammo: -1, powered: true));
            Assert.IsFalse(WeaponMath.CanEngage(WeaponControl.Auto, ammo: 0, powered: true));
            Assert.IsFalse(WeaponMath.CanEngage(WeaponControl.Auto, ammo: -1, powered: false));
        }

        [Test]
        public void ManualWeaponsFireOnOrderWhateverTheOdds()
        {
            const float doubtful = 0.05f;
            Assert.IsTrue(WeaponMath.WantsToFire(WeaponControl.Manual, fireRequested: true, FireMode.Normal, doubtful));
            Assert.IsFalse(WeaponMath.WantsToFire(WeaponControl.Manual, fireRequested: false, FireMode.Normal, 1f));
        }

        [Test]
        public void AutomaticWeaponsFollowTheirFireModeAndHeldWeaponsNeverFire()
        {
            Assert.IsTrue(WeaponMath.WantsToFire(WeaponControl.Auto, fireRequested: false, FireMode.Normal, 0.9f));
            Assert.IsFalse(WeaponMath.WantsToFire(WeaponControl.Auto, fireRequested: true, FireMode.Normal, 0.2f));
            Assert.IsFalse(WeaponMath.WantsToFire(WeaponControl.Off, fireRequested: true, FireMode.Normal, 1f));
        }

        [Test]
        public void ChargeFractionGoesFromJustFiredToReady()
        {
            Assert.AreEqual(0f, WeaponMath.ChargeFraction(cooldownLeft: 4f, cooldown: 4f, FireMode.Normal), 1e-6f);
            Assert.AreEqual(0.75f, WeaponMath.ChargeFraction(cooldownLeft: 1f, cooldown: 4f, FireMode.Normal), 1e-6f);
            Assert.AreEqual(1f, WeaponMath.ChargeFraction(cooldownLeft: 0f, cooldown: 4f, FireMode.Normal), 1e-6f);
            // Just after a trigger-happy shot, the shorter reload starts empty too.
            var full = WeaponMath.ReloadSeconds(4f, FireMode.TriggerHappy);
            Assert.AreEqual(0f, WeaponMath.ChargeFraction(full, 4f, FireMode.TriggerHappy), 1e-6f);
        }

        [Test]
        public void ShotsAreReproducible()
        {
            var a = WeaponMath.ShotRandom(42, 100, 7, 0).NextFloat();
            var b = WeaponMath.ShotRandom(42, 100, 7, 0).NextFloat();
            var other = WeaponMath.ShotRandom(42, 101, 7, 0).NextFloat();
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, other);
        }
    }
}
