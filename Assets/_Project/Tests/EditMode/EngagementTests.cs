using NUnit.Framework;
using Space.Core.Ships;
using Unity.Mathematics;

namespace Space.Core.Tests
{
    public class EngagementTests
    {
        [Test]
        public void LanePointBringsTheShipBackOntoItsLane()
        {
            var heading = new float3(0f, 0f, 1f);
            var point = Engagement.LanePoint(new double3(12.0, 0.0, 300.0), double3.zero, heading);
            Assert.AreEqual(0.0, point.x, 1e-9);
            Assert.AreEqual(300.0, point.z, 1e-9);
        }

        [Test]
        public void LanePointWorksFarAlongTheCourse()
        {
            var heading = math.normalize(new float3(1f, 0f, 1f));
            var origin = new double3(1000.0, 0.0, -500.0);
            var onLane = origin + (double3)heading * 2_000_000.0;
            var point = Engagement.LanePoint(onLane + new double3(0.0, 0.0, 0.0), origin, heading);
            Assert.AreEqual(0.0, math.distance(point, onLane), 1e-3);
        }

        [Test]
        public void TheFirstEnemySailsAbeamOnTheRight()
        {
            var station = Engagement.EnemyStation(0, 650f, 35f);
            Assert.AreEqual(650f, station.x, 1e-4f);
            Assert.AreEqual(0f, station.z, 1e-4f);
        }

        [Test]
        public void EnemiesKeepStationWithinReachOfTheirWeapons()
        {
            Assert.AreEqual(480f * Engagement.StationRangeShare, Engagement.StationDistance(650f, 480f), 1e-3f);
            Assert.AreEqual(300f, Engagement.StationDistance(300f, 480f), 1e-3f);
            Assert.AreEqual(650f, Engagement.StationDistance(650f, 0f), 1e-3f);
        }

        [Test]
        public void ANegativeAngleMeansThePortSide()
        {
            Assert.Less(Engagement.EnemyStation(0, 500f, -20f).x, 0f);
        }

        [Test]
        public void SeveralEnemiesSpreadAlongTheCourseThenInRanks()
        {
            var a = Engagement.EnemyStation(0, 600f, 10f);
            var b = Engagement.EnemyStation(1, 600f, 10f);
            var c = Engagement.EnemyStation(2, 600f, 10f);
            var d = Engagement.EnemyStation(3, 600f, 10f);
            Assert.AreEqual(a.x, b.x);
            Assert.AreNotEqual(a.z, b.z);
            Assert.AreNotEqual(b.z, c.z);
            Assert.Greater(d.x, a.x, "a fourth enemy sails in a further rank");
            Assert.GreaterOrEqual(math.distance(a, b), Engagement.StationSpacing - 1e-3f);
        }
    }
}
