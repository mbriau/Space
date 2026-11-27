using Unity.Mathematics;

namespace Space.Core.Combat
{
    /// <summary>
    /// Hit estimation and fire decisions for automatic weapons. Shots are resolved instantly with a
    /// hit probability rather than simulated projectiles: cheap, deterministic, and enough for
    /// combat by orders. Pure functions, unit tested.
    /// </summary>
    public static class WeaponMath
    {
        /// <summary>Share of its lateral speed a target is assumed to turn into dodging during the projectile flight.</summary>
        public const float EvasionFactor = 0.25f;

        /// <summary>
        /// Probability that a shot hits: the target radius compared with the expected aiming error
        /// (weapon spread at that distance, plus the target's lateral motion during the flight).
        /// </summary>
        public static float HitProbability(float distance, float targetRadius, float3 toTarget, float3 targetVelocity,
                                           float projectileSpeed, float spread)
        {
            if (distance <= 1e-3f)
                return 1f;

            var direction = toTarget / distance;
            var lateralVelocity = targetVelocity - direction * math.dot(targetVelocity, direction);
            var flightTime = projectileSpeed > 0f ? distance / projectileSpeed : 0f;

            var error = spread * distance + EvasionFactor * math.length(lateralVelocity) * flightTime;
            return math.saturate(targetRadius / (targetRadius + error));
        }

        /// <summary>Lowest hit probability at which the weapon fires in a given mode.</summary>
        public static float FireThreshold(FireMode mode) => mode switch
        {
            FireMode.TriggerHappy => 0.1f,
            FireMode.Conservative => 0.7f,
            _ => 0.35f,
        };

        /// <summary>Cooldown multiplier: trigger-happy weapons fire faster, but hastier shots are less precise.</summary>
        public static float CooldownFactor(FireMode mode) => mode == FireMode.TriggerHappy ? 0.6f : 1f;

        /// <summary>Hit probability multiplier for hasty shots.</summary>
        public static float AccuracyFactor(FireMode mode) => mode == FireMode.TriggerHappy ? 0.85f : 1f;

        public static bool ShouldFire(FireMode mode, float hitProbability) => hitProbability >= FireThreshold(mode);

        /// <summary>
        /// Where a missed shot ends, relative to the target's centre, from three random numbers in
        /// [0, 1): just beside the hull (1.05 to 1.5 radii to the side) and around it along the line of
        /// fire (half a radius short to one radius long). A near miss, not a wild one.
        /// </summary>
        public static float2 MissOffset(float radius, float side, float spread, float depth) =>
            new((side < 0.5f ? -1f : 1f) * radius * (1.05f + spread * 0.45f), radius * (depth * 1.5f - 0.5f));

        /// <summary>Charging speed of a weapon: full speed when powered, nothing otherwise (no fractions).</summary>
        public static float ChargeRate(bool powered) => powered ? 1f : 0f;

        /// <summary>The weapon looks for a target: it is powered, has ammunition and is not holding fire.</summary>
        public static bool CanEngage(WeaponControl control, int ammo, bool powered) =>
            powered && control != WeaponControl.Off && ammo != 0;

        /// <summary>Damage multiplier of a shot: the supercharge factor when supercharged.</summary>
        public static float DamageFactor(bool supercharged, float superchargeFactor) =>
            supercharged && superchargeFactor > 0f ? superchargeFactor : 1f;

        /// <summary>
        /// Decision to fire a charged weapon at a target in range: automatic weapons follow their fire
        /// mode, manual weapons fire on order whatever the odds, held weapons never fire.
        /// </summary>
        public static bool WantsToFire(WeaponControl control, bool fireRequested, FireMode mode, float hitProbability) => control switch
        {
            WeaponControl.Auto => ShouldFire(mode, hitProbability),
            WeaponControl.Manual => fireRequested,
            _ => false,
        };

        /// <summary>Full reload time after a shot, in seconds at full power.</summary>
        public static float ReloadSeconds(float cooldown, FireMode mode) => cooldown * CooldownFactor(mode);

        /// <summary>How far the reload has gone, from 0 (just fired) to 1 (ready to fire).</summary>
        public static float ChargeFraction(float cooldownLeft, float cooldown, FireMode mode)
        {
            var full = ReloadSeconds(cooldown, mode);
            return full > 0f ? math.saturate(1f - cooldownLeft / full) : 1f;
        }

        /// <summary>
        /// Random generator for one weapon at one tick: same inputs, same shots, so a run can be
        /// replayed and synchronised over the network.
        /// </summary>
        public static Random ShotRandom(uint runSeed, uint tick, int entityIndex, int weaponIndex)
        {
            var hash = math.hash(new uint4(runSeed, tick, (uint)entityIndex, (uint)weaponIndex));
            return Random.CreateFromIndex(hash);
        }
    }
}
