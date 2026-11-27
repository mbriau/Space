using NUnit.Framework;
using Space.Core.Ships;

namespace Space.Core.Tests
{
    public class EnemyStationTests
    {
        [Test]
        public void AnAuthoredPositionAlongTheCourseReplacesTheSpread()
        {
            var ahead = Engagement.EnemyStation(0, 190f, -35f, along: 130f);
            var astern = Engagement.EnemyStation(1, 260f, -35f, along: -110f);
            Assert.AreEqual(-190f, ahead.x, 1e-4f, "à bâbord");
            Assert.AreEqual(130f, ahead.z, 1e-4f, "devant");
            Assert.AreEqual(-260f, astern.x, 1e-4f, "plus loin");
            Assert.AreEqual(-110f, astern.z, 1e-4f, "derrière");
        }

        [Test]
        public void WithoutAnAuthoredPositionTheEnemiesSpreadAsBefore()
        {
            Assert.AreEqual(Engagement.EnemyStation(1, 200f, 35f), Engagement.EnemyStation(1, 200f, 35f, along: 0f));
        }
    }
}
