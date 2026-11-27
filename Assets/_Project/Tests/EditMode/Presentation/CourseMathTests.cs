using System.Collections.Generic;
using NUnit.Framework;
using Space.Presentation.Rendering;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Presentation.Tests
{
    public class CourseMathTests
    {
        const float Step = 4f;

        static BlobAssetReference<CoursePathBlob> Baked(out List<double3> positions, out List<quaternion> rotations)
        {
            var knots = CourseMath.Generate(7, CourseShape.Default);
            positions = new List<double3>();
            rotations = new List<quaternion>();
            CourseMath.Bake(knots, Step, positions, rotations);
            return CourseMath.BuildBlob(Step, positions, rotations);
        }

        [Test]
        public void TheCourseStartsStraightAndLevel()
        {
            var knots = CourseMath.Generate(7, CourseShape.Default);
            Assert.AreEqual(0f, math.length(knots[0].Position), 1e-3f);
            Assert.AreEqual(0f, knots[0].Roll, 1e-3f);
        }

        [Test]
        public void TheCourseTurnsGentlyAndBanksALittle()
        {
            var shape = CourseShape.Default;
            using var path = Baked(out var positions, out var rotations);
            var tightest = float.MaxValue;
            var maxRoll = 0f;
            // Curvature over 40 m, up to the last segment (past it the course goes straight on).
            const int span = 10;
            var lastKnot = (int)((shape.Length - shape.Spacing) / Step);
            for (var i = span; i < math.min(positions.Count - span, lastKnot); i++)
            {
                var a = math.normalize((float3)(positions[i] - positions[i - span]));
                var b = math.normalize((float3)(positions[i + span] - positions[i]));
                var angle = math.acos(math.clamp(math.dot(a, b), -1f, 1f));
                if (angle > 1e-6f)
                    tightest = math.min(tightest, span * Step / angle);
                var up = math.mul(rotations[i], new float3(0f, 1f, 0f));
                var forward = math.mul(rotations[i], new float3(0f, 0f, 1f));
                var right = math.normalize(math.cross(new float3(0f, 1f, 0f), forward));
                maxRoll = math.max(maxRoll, math.abs(math.degrees(math.asin(math.dot(up, right)))));
            }
            Assert.Greater(tightest, 2000f, "aucun virage plus serré qu'un rayon de 2 km");
            Assert.LessOrEqual(maxRoll, shape.MaxRoll + shape.Wobble + 0.5f, "roulis léger");
            Assert.Greater(maxRoll, 3f, "mais visible");
        }

        /// <summary>Largest roll, and how often the roll turns back, over the first 3 km (about three minutes).</summary>
        static float MaxRoll(in CourseShape shape, out int rollSwings)
        {
            var knots = CourseMath.Generate(7, shape);
            var max = 0f;
            rollSwings = 0;
            var lastDirection = 0f;
            for (var i = 1; i < knots.Length && knots[i].Position.z <= 3000f; i++)
            {
                max = math.max(max, math.abs(knots[i].Roll));
                var direction = math.sign(knots[i].Roll - knots[i - 1].Roll);
                if (direction != 0f && direction != lastDirection)
                {
                    rollSwings++;
                    lastDirection = direction;
                }
            }
            return max;
        }

        [Test]
        public void TheIntenseCourseRollsMuchMoreAndMoreOften()
        {
            var calm = MaxRoll(CourseShape.Default, out var calmSwings);
            var intense = MaxRoll(CourseShape.Intense, out var intenseSwings);
            Assert.Greater(intense, 25f, "roulis franc");
            Assert.Greater(intense, calm * 2.5f);
            Assert.GreaterOrEqual(intenseSwings, 6, "le roulis change de sens plusieurs fois en trois minutes");
            Assert.Greater(intenseSwings, calmSwings);
            Assert.Greater(CourseShape.Intense.CameraFollow, CourseShape.Default.CameraFollow, "la caméra suit plus, le fond tourne plus");
        }

        [Test]
        public void TheIntensityBlendsEverySetting()
        {
            var calm = CourseShape.Default;
            var intense = CourseShape.Intense;
            Assert.AreEqual(calm.Wobble, CourseShape.Lerp(calm, intense, 0f).Wobble);
            Assert.AreEqual(intense.MaxRoll, CourseShape.Lerp(calm, intense, 1f).MaxRoll);
            var half = CourseShape.Lerp(calm, intense, 0.5f);
            Assert.AreEqual((calm.CameraFollow + intense.CameraFollow) * 0.5f, half.CameraFollow, 1e-5f);
            Assert.AreEqual((calm.WobbleWavelength + intense.WobbleWavelength) * 0.5f, half.WobbleWavelength, 1e-3f);
        }

        [Test]
        public void TheIntenseCourseStillTurnsWide()
        {
            var knots = CourseMath.Generate(7, CourseShape.Intense);
            var positions = new List<double3>();
            var rotations = new List<quaternion>();
            CourseMath.Bake(knots, Step, positions, rotations);
            var tightest = float.MaxValue;
            const int span = 10;
            for (var i = span; i < positions.Count - 200; i++)
            {
                var a = math.normalize((float3)(positions[i] - positions[i - span]));
                var b = math.normalize((float3)(positions[i + span] - positions[i]));
                var angle = math.acos(math.clamp(math.dot(a, b), -1f, 1f));
                if (angle > 1e-6f)
                    tightest = math.min(tightest, span * Step / angle);
            }
            Assert.Greater(tightest, 1000f, "même intense, pas de virage plus serré qu'un rayon de 1 km");
        }

        [Test]
        public void TheBakedFramesAreEvenlySpacedAlongThePath()
        {
            using var path = Baked(out var positions, out _);
            Assert.Greater(positions.Count * Step, 25000f, "assez long pour un niveau");
            for (var i = 1; i < positions.Count; i++)
                Assert.AreEqual(Step, math.distance(positions[i - 1], positions[i]), Step * 0.01, $"pas {i}");
        }

        [Test]
        public void ShipsKeepTheirPlaceRelativeToEachOther()
        {
            using var path = Baked(out _, out _);
            // Flagship 2 km along, an enemy 300 m to its right, another 110 m ahead of that one.
            var flagship = CourseMath.Bend(ref path.Value, new double3(0, 0, 2000), out var frame);
            var enemy = CourseMath.Bend(ref path.Value, new double3(300, 0, 2000), out _);
            var ahead = CourseMath.Bend(ref path.Value, new double3(300, 0, 2110), out _);
            Assert.AreEqual(300.0, math.distance(flagship, enemy), 1e-3, "même distance par le travers");
            Assert.AreEqual(300f, math.dot((float3)(enemy - flagship), math.mul(frame, new float3(1f, 0f, 0f))), 1e-2f, "toujours à tribord");
            Assert.AreEqual(110.0, math.distance(enemy, ahead), 110 * 0.12, "l'espacement varie à peine dans les virages");
        }

        [Test]
        public void UnbendFindsTheLanePointBack()
        {
            using var path = Baked(out _, out _);
            foreach (var lane in new[] { new double3(0, 0, 50), new double3(-320, 12, 3000), new double3(280, -8, 9500), new double3(150, 0, -400) })
            {
                var bent = CourseMath.Bend(ref path.Value, lane, out _);
                var back = CourseMath.Unbend(ref path.Value, bent, lane.z - 80);
                Assert.AreEqual(0.0, math.distance(lane, back), 0.02, $"{lane}");
            }
        }

        [Test]
        public void PastTheEndTheCourseGoesStraightOn()
        {
            using var path = Baked(out var positions, out _);
            var end = (positions.Count - 1) * (double)Step;
            CourseMath.Frame(ref path.Value, end + 100, out var p1, out var r1);
            CourseMath.Frame(ref path.Value, end + 600, out var p2, out var r2);
            Assert.AreEqual(500.0, math.distance(p1, p2), 1e-3);
            Assert.AreEqual(1f, math.abs(math.dot(r1.value, r2.value)), 1e-5f);
        }

        [Test]
        public void RenderSpaceMapsTheOriginToZeroAndBack()
        {
            using var path = Baked(out _, out _);
            var space = RenderSpace.Flat(new double3(100, 0, 2500));
            space.Curved = true;
            space.Path = path;
            space.LaneOrigin = new double3(100, 0, 0);
            space.RenderOrigin = space.Bent(space.Origin);
            Assert.AreEqual(0f, math.length(space.ToRender(space.Origin)), 1e-3f);

            var enemy = new double3(400, 0, 2600);
            var render = space.ToRender(enemy);
            Assert.AreEqual(0.0, math.distance(enemy, space.ToSim(render, space.Origin)), 0.02);
        }

        [Test]
        public void EnemiesSwayALittleAndEachTheirOwnWay()
        {
            var a = CourseMath.SwayFor(20f, 1);
            var b = CourseMath.SwayFor(20f, 2);
            Assert.AreNotEqual(a.Phase, b.Phase);
            var highest = 0f;
            var furthest = 0f;
            for (var t = 0.0; t < 60.0; t += 0.25)
            {
                CourseMath.Sway(a, t, out var offset, out _);
                highest = math.max(highest, math.abs(offset.y));
                furthest = math.max(furthest, math.abs(offset.z));
                Assert.AreEqual(0f, offset.x, "jamais de côté : la distance de combat ne change pas");
            }
            Assert.Greater(highest, 1f);
            Assert.LessOrEqual(highest, 20f * 0.16f + 1e-3f);
            Assert.LessOrEqual(furthest, 20f * 0.4f + 1e-3f);
        }
    }
}
