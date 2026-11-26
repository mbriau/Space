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
