using NUnit.Framework;
using Space.Presentation.Rendering;
using UnityEngine;

namespace Space.Presentation.Tests
{
    public class BackdropMathTests
    {
        [Test]
        public void NearLayersSlideFasterThanFarOnes()
        {
            var travel = new Vector3(300f, 0f, 0f);
            var near = BackdropMath.TravelScroll(travel, Vector3.right, Vector3.up, metresPerTile: 1200f);
            var far = BackdropMath.TravelScroll(travel, Vector3.right, Vector3.up, metresPerTile: 6000f);
            Assert.AreEqual(0.25f, near.x, 1e-5f);
            Assert.Greater(near.x, far.x);
        }

        [Test]
        public void MovingAlongTheViewOrOnAFixedLayerDoesNotScroll()
        {
            Assert.AreEqual(Vector2.zero, BackdropMath.TravelScroll(new Vector3(0f, 0f, 500f), Vector3.right, Vector3.up, 1000f));
            Assert.AreEqual(Vector2.zero, BackdropMath.TravelScroll(new Vector3(500f, 0f, 0f), Vector3.right, Vector3.up, 0f));
        }

        [Test]
        public void TurningByAFieldOfViewMovesTheTilesInView()
        {
            var scroll = BackdropMath.TurnScroll(yawDegrees: 90f, pitchDegrees: -30f, horizontalFov: 90f, verticalFov: 60f, tilesInView: new Vector2(1.6f, 1f));
            Assert.AreEqual(1.6f, scroll.x, 1e-5f);
            Assert.AreEqual(0.5f, scroll.y, 1e-5f, "regarder plus haut fait descendre le ciel");
        }

        [Test]
        public void OffsetsStayBetweenZeroAndOne()
        {
            var wrapped = BackdropMath.Wrap(new Vector2(12.25f, -3.75f));
            Assert.AreEqual(0.25f, wrapped.x, 1e-5f);
            Assert.AreEqual(0.25f, wrapped.y, 1e-5f);
        }

        [Test]
        public void HorizontalFieldOfViewFollowsTheAspect()
        {
            Assert.AreEqual(60f, BackdropMath.HorizontalFov(60f, 1f), 1e-3f);
            Assert.Greater(BackdropMath.HorizontalFov(60f, 16f / 9f), 90f);
        }

        [Test]
        public void ALayerRolledAgainstTheCameraScrollsAlongItsOwnAxes()
        {
            // A layer turned a quarter turn: a scroll to the right of the screen runs down its own Y.
            var scroll = BackdropMath.ToLayer(new Vector2(1f, 0f), 90f);
            Assert.AreEqual(0f, scroll.x, 1e-5f);
            Assert.AreEqual(-1f, scroll.y, 1e-5f);
            Assert.AreEqual(new Vector2(0.3f, -0.2f), BackdropMath.ToLayer(new Vector2(0.3f, -0.2f), 0f));
        }

        [Test]
        public void ARolledLayerStillCoversTheView()
        {
            var side = BackdropMath.RollCover(16f, 9f);
            // The view's corners stay inside the square layer whatever the roll.
            for (var roll = 0f; roll < 360f; roll += 15f)
            {
                var a = roll * Mathf.Deg2Rad;
                foreach (var corner in new[] { new Vector2(8f, 4.5f), new Vector2(-8f, 4.5f) })
                {
                    var x = corner.x * Mathf.Cos(a) + corner.y * Mathf.Sin(a);
                    var y = -corner.x * Mathf.Sin(a) + corner.y * Mathf.Cos(a);
                    Assert.LessOrEqual(Mathf.Abs(x), side * 0.5f + 1e-4f);
                    Assert.LessOrEqual(Mathf.Abs(y), side * 0.5f + 1e-4f);
                }
            }
        }
    }
}
