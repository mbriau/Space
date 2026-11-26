using NUnit.Framework;
using Space.Core.Ships;
using Unity.Mathematics;

namespace Space.Core.Tests
{
    public class ShipSteeringTests
    {
        const float Dt = 0.05f; // 20 Hz, the simulation tick

        static readonly ShipMovementStats Cargo = new ShipMovementStats
        {
            MaxSpeed = 60f,
            Acceleration = 6f,
            TurnRateRadians = math.radians(30f),
        };

        static int RunUntilArrived(ref double3 position, ref quaternion rotation, ref float3 velocity,
                                   double3 target, ShipMovementStats stats, int maxTicks, out float maxSpeed)
        {
            maxSpeed = 0f;
            for (var tick = 1; tick <= maxTicks; tick++)
            {
                if (ShipSteering.MoveTowards(ref position, ref rotation, ref velocity, target, stats, Dt))
                    return tick;
                maxSpeed = math.max(maxSpeed, math.length(velocity));
            }
            return -1;
        }

        [Test]
        public void ReachesTargetAheadAndStops()
        {
            var position = double3.zero;
            var rotation = quaternion.identity;
            var velocity = float3.zero;
            var target = new double3(0, 0, 800);

            var ticks = RunUntilArrived(ref position, ref rotation, ref velocity, target, Cargo, 20 * 120, out _);

            Assert.Greater(ticks, 0, "the ship never arrived");
            Assert.LessOrEqual(math.distance(position, target), ShipSteering.ArrivalDistance);
            Assert.AreEqual(float3.zero, velocity);
        }

        [Test]
        public void ReachesTargetBehindAfterTurningAround()
        {
            var position = double3.zero;
            var rotation = quaternion.identity; // facing +Z
            var velocity = float3.zero;
            var target = new double3(100, 0, -400);

            var ticks = RunUntilArrived(ref position, ref rotation, ref velocity, target, Cargo, 20 * 180, out _);

            Assert.Greater(ticks, 0, "the ship never arrived");
            Assert.LessOrEqual(math.distance(position, target), ShipSteering.ArrivalDistance);
        }

        [Test]
        public void NeverExceedsMaxSpeed()
        {
            var position = double3.zero;
            var rotation = quaternion.identity;
            var velocity = float3.zero;

            RunUntilArrived(ref position, ref rotation, ref velocity, new double3(0, 0, 5000), Cargo, 20 * 300, out var maxSpeed);

            Assert.LessOrEqual(maxSpeed, Cargo.MaxSpeed + 1e-3f);
            Assert.Greater(maxSpeed, Cargo.MaxSpeed * 0.95f, "a long trip should reach cruise speed");
        }

        [Test]
        public void TurnRateIsLimited()
        {
            var rotation = quaternion.identity;
            var maxAngle = Cargo.TurnRateRadians * Dt;

            ShipSteering.Turn(ref rotation, new float3(0, 0, -1), maxAngle);

            Assert.AreEqual(maxAngle, ShipSteering.Angle(quaternion.identity, rotation), 1e-4f);
        }

        [Test]
        public void BrakeStopsAMovingShip()
        {
            var position = double3.zero;
            var velocity = new float3(0, 0, 30);

            for (var i = 0; i < 20 * 30; i++)
                ShipSteering.Brake(ref position, quaternion.identity, ref velocity, Cargo, Dt);

            Assert.AreEqual(float3.zero, velocity);
        }
    }
}
