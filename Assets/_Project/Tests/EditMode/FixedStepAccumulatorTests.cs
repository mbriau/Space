using NUnit.Framework;
using Space.Core.Time;

namespace Space.Core.Tests
{
    public class FixedStepAccumulatorTests
    {
        const float Step = 0.0625f; // 16 Hz: exact in binary, keeps the tests free of rounding noise

        [Test]
        public void NoTimeProducesNoTick()
        {
            var acc = new FixedStepAccumulator(Step, 8);
            Assert.AreEqual(0, acc.Advance(0));
        }

        [Test]
        public void PausedOrNegativeTimeProducesNoTick()
        {
            var acc = new FixedStepAccumulator(Step, 8);
            Assert.AreEqual(0, acc.Advance(-1));
        }

        [Test]
        public void PartialStepsAccumulateAcrossFrames()
        {
            var acc = new FixedStepAccumulator(Step, 8);
            Assert.AreEqual(0, acc.Advance(0.04));
            Assert.AreEqual(1, acc.Advance(0.04));
            Assert.AreEqual(0.28f, acc.Alpha, 1e-4f);
        }

        [Test]
        public void DoubleSpeedRunsTwiceAsManyTicks()
        {
            var normal = new FixedStepAccumulator(Step, 8);
            var fast = new FixedStepAccumulator(Step, 8);
            int normalTicks = 0, fastTicks = 0;
            for (var i = 0; i < 64; i++)
            {
                normalTicks += normal.Advance(1.0 / 64.0);
                fastTicks += fast.Advance(2.0 / 64.0);
            }
            Assert.AreEqual(16, normalTicks);
            Assert.AreEqual(32, fastTicks);
        }

        [Test]
        public void LongFrameIsCappedAndBacklogDropped()
        {
            var acc = new FixedStepAccumulator(Step, 8);
            Assert.AreEqual(8, acc.Advance(5.0));
            Assert.AreEqual(0f, acc.Alpha);
            Assert.AreEqual(1, acc.Advance(Step));
        }
    }
}
