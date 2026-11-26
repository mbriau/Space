using Unity.Mathematics;

namespace Space.Core.Ships
{
    /// <summary>
    /// Arcade steering for ships: turn toward the target at a limited rate, accelerate with
    /// limited thrust (weaker sideways than along the hull axis), and brake so the ship stops
    /// on the target. Pure functions so they can be unit tested without a World.
    /// </summary>
    public static class ShipSteering
    {
        /// <summary>The ship has arrived when closer than this and slower than <see cref="ArrivalSpeed"/>.</summary>
        public const float ArrivalDistance = 2f;
        public const float ArrivalSpeed = 0.5f;

        /// <summary>Share of the thrust available sideways (main engines and retro thrusters push along the hull).</summary>
        public const float LateralThrustFactor = 0.3f;

        static readonly float3 Forward = new float3(0f, 0f, 1f);

        /// <summary>Advances one tick toward <paramref name="target"/>. Returns true once arrived (velocity is then zero).</summary>
        public static bool MoveTowards(ref double3 position, ref quaternion rotation, ref float3 velocity,
                                       double3 target, in ShipMovementStats stats, float dt)
        {
            var offset = (float3)(target - position);
            var distance = math.length(offset);

            if (distance <= ArrivalDistance && math.length(velocity) <= ArrivalSpeed)
            {
                velocity = float3.zero;
                return true;
            }

            var direction = distance > 1e-4f ? offset / distance : float3.zero;

            // Highest speed from which the ship can still stop on the target: v² = 2·a·d.
            var brakingDistance = math.max(0f, distance - ArrivalDistance * 0.5f);
            var desiredSpeed = math.min(stats.MaxSpeed, math.sqrt(2f * stats.Acceleration * brakingDistance));

            Turn(ref rotation, direction, stats.TurnRateRadians * dt);
            Accelerate(ref velocity, direction * desiredSpeed, rotation, stats, dt);
            position += (double3)(velocity * dt);
            return false;
        }

        /// <summary>Advances one tick with no order: the ship brakes to a stop.</summary>
        public static void Brake(ref double3 position, quaternion rotation, ref float3 velocity,
                                 in ShipMovementStats stats, float dt)
        {
            Accelerate(ref velocity, float3.zero, rotation, stats, dt);
            position += (double3)(velocity * dt);
        }

        /// <summary>Rotates toward <paramref name="direction"/> by at most <paramref name="maxAngle"/> radians.</summary>
        public static void Turn(ref quaternion rotation, float3 direction, float maxAngle)
        {
            if (math.lengthsq(direction) < 1e-8f)
                return;

            var target = quaternion.LookRotationSafe(direction, math.up());
            var angle = Angle(rotation, target);
            rotation = angle <= maxAngle
                ? target
                : math.normalize(math.slerp(rotation, target, maxAngle / angle));
        }

        /// <summary>Angle between two orientations, in radians, in [0, π].</summary>
        public static float Angle(quaternion a, quaternion b)
        {
            var dot = math.min(math.abs(math.dot(a.value, b.value)), 1f);
            return 2f * math.acos(dot);
        }

        static void Accelerate(ref float3 velocity, float3 desiredVelocity, quaternion rotation,
                               in ShipMovementStats stats, float dt)
        {
            var delta = desiredVelocity - velocity;
            var length = math.length(delta);
            if (length < 1e-5f)
            {
                velocity = desiredVelocity;
                return;
            }

            var deltaDirection = delta / length;
            var forward = math.mul(rotation, Forward);
            var alignment = math.abs(math.dot(forward, deltaDirection));
            var step = stats.Acceleration * math.lerp(LateralThrustFactor, 1f, alignment) * dt;

            velocity = length <= step ? desiredVelocity : velocity + deltaDirection * step;

            var speed = math.length(velocity);
            if (speed > stats.MaxSpeed)
                velocity *= stats.MaxSpeed / speed;
        }
    }
}
