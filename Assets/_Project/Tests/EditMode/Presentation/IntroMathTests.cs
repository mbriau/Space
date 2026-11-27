using NUnit.Framework;
using Space.Presentation.Game;
using UnityEngine;

namespace Space.Presentation.Tests
{
    public class IntroMathTests
    {
        [Test]
        public void TheFlyoverGoesFromBehindToAboveTheShip()
        {
            Assert.AreEqual(IntroMath.FlyoverStart, IntroMath.FlyoverPosition(0f));
            Assert.AreEqual(IntroMath.FlyoverEnd, IntroMath.FlyoverPosition(1f));
            Assert.Less(IntroMath.FlyoverStart.z, -3f, "départ derrière");
            Assert.Greater(IntroMath.FlyoverEnd.y, 1f, "arrivée au-dessus");
            var look = IntroMath.FlyoverLook(1f) - IntroMath.FlyoverEnd;
            Assert.Less(Vector3.Angle(look, Vector3.forward), 15f, "on regarde au loin, devant le vaisseau");
            // Never through the hull: always above it once level with the ship.
            for (var t = 0f; t <= 1f; t += 0.05f)
            {
                var p = IntroMath.FlyoverPosition(t);
                if (p.z > -1.5f)
                    Assert.Greater(p.y, 1f, $"t={t}");
            }
        }

        [Test]
        public void TheSearchLooksRightThenSettlesOnTheLeft()
        {
            Assert.AreEqual(0f, IntroMath.SearchYaw(0f), 1e-4f);
            Assert.AreEqual(35f, IntroMath.SearchYaw(1.6f), 1e-4f, "à droite");
            Assert.AreEqual(-55f, IntroMath.SearchYaw(10f), 1e-4f, "puis à gauche, où elle reste");
        }

        [Test]
        public void LongLinesAreSubtitledSentenceBySentence()
        {
            const string text = "Tu sais ce qu'il m'a dit ? Des monstres. Et puis un jour... quelqu'un a eu une idée.";
            Assert.AreEqual("Tu sais ce qu'il m'a dit ?", IntroMath.SubtitleAt(text, 0f));
            Assert.AreEqual("Des monstres.", IntroMath.SubtitleAt(text, 0.4f));
            Assert.AreEqual("Et puis un jour... quelqu'un a eu une idée.", IntroMath.SubtitleAt(text, 0.99f), "les points de suspension restent dans la phrase");
            Assert.AreEqual("Court.", IntroMath.SubtitleAt("Court.", 0.5f));
        }

        [Test]
        public void AFrameDropsBeforeItsPictureAppears()
        {
            var (drop, picture) = IntroMath.FrameIn(0.2f);
            Assert.Greater(drop, 0.5f);
            Assert.AreEqual(0f, picture, "le fond gris d'abord");
            (drop, picture) = IntroMath.FrameIn(2f);
            Assert.AreEqual(1f, drop, 1e-4f);
            Assert.AreEqual(1f, picture, 1e-4f);

            (drop, picture) = IntroMath.FrameOut(0.1f);
            Assert.AreEqual(1f, drop, 1e-4f, "l'image s'efface avant que le cadre remonte");
            Assert.Less(picture, 1f);
            (drop, _) = IntroMath.FrameOut(2f);
            Assert.AreEqual(0f, drop, 1e-4f);
        }
    }
}
