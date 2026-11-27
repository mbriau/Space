using NUnit.Framework;
using Space.Core.Levels;

namespace Space.Core.Tests
{
    public class LevelHoldTests
    {
        [Test]
        public void AHeldClockPushesTheWholeLevelBack()
        {
            var level = new LevelState { StartTick = 100, EndTick = 6100 };
            for (var i = 0; i < 40; i++)
                LevelHoldMath.HoldOneTick(ref level);
            Assert.AreEqual(140u, level.StartTick, "les arrivées attendent");
            Assert.AreEqual(6140u, level.EndTick, "le temps de survie aussi");
            Assert.AreEqual(6000u, level.EndTick - level.StartTick, "la durée du niveau ne change pas");
        }
    }
}
