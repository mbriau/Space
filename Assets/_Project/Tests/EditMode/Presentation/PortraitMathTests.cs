using NUnit.Framework;
using Space.Presentation.Characters;
using UnityEngine;

namespace Space.Presentation.Tests
{
    public class PortraitMathTests
    {
        static readonly PortraitLandmarks Landmarks = new()
        {
            ImageSize = 1024f, HeadCenter = new Vector2(680f, 400f), HeadRadii = new Vector2(260f, 250f),
            Neck = new Vector2(660f, 640f), TorsoLeft = 220f, TorsoRight = 900f, BorderPx = 40f,
        };

        [Test]
        public void TheFrameBorderNeverMoves()
        {
            var pose = new PortraitPose { HeadAngle = 5f, HeadOffset = new Vector2(8f, 8f), Breath = 6f };
            foreach (var p in new[] { new Vector2(0f, 500f), new Vector2(1024f, 300f), new Vector2(600f, 1024f), new Vector2(500f, 0f) })
                Assert.AreEqual(p, PortraitMath.Displace(p, Landmarks, pose));
        }

        [Test]
        public void TheHeadTurnsAndTheBellyStays()
        {
            var pose = new PortraitPose { HeadAngle = 3f };
            var face = new Vector2(720f, 420f);
            Assert.Greater((PortraitMath.Displace(face, Landmarks, pose) - face).magnitude, 1f, "le visage bouge");
            var belly = new Vector2(600f, 900f);
            Assert.AreEqual(belly, PortraitMath.Displace(belly, Landmarks, pose), "le ventre ne tourne pas avec la tête");
        }

        [Test]
        public void BreathingLiftsTheShouldersMoreThanTheWaist()
        {
            var pose = new PortraitPose { Breath = 4f };
            var shoulder = new Vector2(600f, 700f);
            var waist = new Vector2(600f, 960f);
            var liftShoulder = shoulder.y - PortraitMath.Displace(shoulder, Landmarks, pose).y;
            var liftWaist = waist.y - PortraitMath.Displace(waist, Landmarks, pose).y;
            Assert.Greater(liftShoulder, 2f);
            Assert.Less(liftWaist, liftShoulder);
        }

        [Test]
        public void CuesAreFoundByTime()
        {
            var cues = new[]
            {
                new MouthCue { start = 0f, end = 0.2f, value = "X" },
                new MouthCue { start = 0.2f, end = 0.35f, value = "B" },
                new MouthCue { start = 0.35f, end = 0.6f, value = "D" },
            };
            Assert.AreEqual("B", PortraitMath.CueAt(cues, 0.25f));
            Assert.AreEqual("D", PortraitMath.CueAt(cues, 0.35f));
            Assert.AreEqual("X", PortraitMath.CueAt(cues, 1f), "après la réplique : bouche au repos");
            Assert.AreEqual("X", PortraitMath.CueAt(null, 0.1f));
        }

        [Test]
        public void ExtendedShapesUseTheBasicDrawings()
        {
            Assert.AreEqual("B", PortraitMath.PatchFor("G"));
            Assert.AreEqual("C", PortraitMath.PatchFor("H"));
            Assert.AreEqual("C", PortraitMath.PatchFor("D"));
            Assert.AreEqual("A", PortraitMath.PatchFor("A"));
            Assert.AreEqual("X", PortraitMath.PatchFor("?"));
            Assert.Greater(PortraitMath.Openness("D"), PortraitMath.Openness("A"));
        }

        [Test]
        public void RhubarbJsonIsRead()
        {
            var track = LipSyncTrack.Parse("{\"metadata\":{\"duration\":0.5},\"mouthCues\":[{\"start\":0.00,\"end\":0.25,\"value\":\"X\"},{\"start\":0.25,\"end\":0.50,\"value\":\"C\"}]}");
            Assert.AreEqual(2, track.mouthCues.Length);
            Assert.AreEqual("C", track.mouthCues[1].value);
        }
    }
}
