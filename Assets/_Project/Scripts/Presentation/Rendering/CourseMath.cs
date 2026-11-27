using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Space.Presentation.Rendering
{
    /// <summary>One control point of a <see cref="CoursePath"/>: a point of the path and its roll there.</summary>
    [System.Serializable]
    public struct CourseKnot
    {
        /// <summary>Position in the path's frame: X right, Y up, Z along the course (metres).</summary>
        public float3 Position;

        /// <summary>Roll in degrees: positive banks to the right (the up axis leans right).</summary>
        public float Roll;
    }

    /// <summary>Shape of a generated course: swings left and right, up and down, roll into the turns and of its own.</summary>
    [System.Serializable]
    public struct CourseShape
    {
        [UnityEngine.Tooltip("Length of the course, metres (past it, it goes straight on).")]
        public float Length;
        [UnityEngine.Tooltip("Distance between two control points, metres (short enough for the quickest roll).")]
        public float Spacing;
        [UnityEngine.Tooltip("Swing left and right, metres.")]
        public float Lateral;
        [UnityEngine.Tooltip("Length of one swing left and right, metres (the cruise covers 15 m/s).")]
        public float LateralWavelength;
        [UnityEngine.Tooltip("Swing up and down, metres.")]
        public float Vertical;
        public float VerticalWavelength;
        [UnityEngine.Tooltip("Distance over which the roll comes in (the intro starts level), metres.")]
        public float Ramp;
        [UnityEngine.Tooltip("Roll into the turns: degrees per unit of curvature (degrees × metres).")]
        public float Bank;
        [UnityEngine.Tooltip("Largest roll into the turns, degrees.")]
        public float MaxRoll;
        [UnityEngine.Tooltip("Roll of its own, independent of the turns, degrees.")]
        public float Wobble;
        [UnityEngine.Tooltip("Length of one roll of its own, metres (500 m ≈ 33 s at cruise speed).")]
        public float WobbleWavelength;
        [UnityEngine.Tooltip("How much the tactical camera follows the course's roll and pitch (0 = level, 1 = fully). The camera's roll is what turns the stars.")]
        [UnityEngine.Range(0f, 1f)] public float CameraFollow;

        /// <summary>Calm: slow, wide swings and a light roll.</summary>
        public static CourseShape Default => new()
        {
            Length = 32000f, Spacing = 75f,
            Lateral = 400f, LateralWavelength = 7000f,
            Vertical = 80f, VerticalWavelength = 6500f,
            Ramp = 900f, Bank = 35000f, MaxRoll = 10f,
            Wobble = 2f, WobbleWavelength = 3100f,
            CameraFollow = 0.4f,
        };

        /// <summary>Intense: tighter swings, a strong and quicker roll that the camera mostly follows.</summary>
        public static CourseShape Intense => new()
        {
            Length = 32000f, Spacing = 75f,
            Lateral = 400f, LateralWavelength = 4500f,
            Vertical = 160f, VerticalWavelength = 3600f,
            Ramp = 400f, Bank = 43000f, MaxRoll = 30f,
            Wobble = 15f, WobbleWavelength = 500f,
            CameraFollow = 0.75f,
        };

        /// <summary>Between two shapes, every setting (0 = <paramref name="a"/>, 1 = <paramref name="b"/>).</summary>
        public static CourseShape Lerp(in CourseShape a, in CourseShape b, float t) => new()
        {
            Length = math.lerp(a.Length, b.Length, t),
            Spacing = math.lerp(a.Spacing, b.Spacing, t),
            Lateral = math.lerp(a.Lateral, b.Lateral, t),
            LateralWavelength = math.lerp(a.LateralWavelength, b.LateralWavelength, t),
            Vertical = math.lerp(a.Vertical, b.Vertical, t),
            VerticalWavelength = math.lerp(a.VerticalWavelength, b.VerticalWavelength, t),
            Ramp = math.lerp(a.Ramp, b.Ramp, t),
            Bank = math.lerp(a.Bank, b.Bank, t),
            MaxRoll = math.lerp(a.MaxRoll, b.MaxRoll, t),
            Wobble = math.lerp(a.Wobble, b.Wobble, t),
            WobbleWavelength = math.lerp(a.WobbleWavelength, b.WobbleWavelength, t),
            CameraFollow = math.lerp(a.CameraFollow, b.CameraFollow, t),
        };
    }

    /// <summary>The course baked for rendering: a frame every <see cref="Step"/> metres of path.</summary>
    public struct CoursePathBlob
    {
        public float Step;
        public BlobArray<double3> Positions;
        public BlobArray<quaternion> Rotations;
    }

    /// <summary>
    /// Pure rules of the curved course (rendering only): generating the spline, baking it by arc
    /// length, reading a frame at a distance, and the idle sway of the enemies. The simulation keeps
    /// its straight lane; the render bends it onto this path. Unit tested.
    /// </summary>
    public static class CourseMath
    {
        // ------------------------------------------------------------------ generation

        /// <summary>
        /// Control points of a calm course: two slow swings left and right, one up and down, from a
        /// straight start, with the roll banking into the turns.
        /// </summary>
        public static CourseKnot[] Generate(uint seed, CourseShape shape)
        {
            var random = new Random(seed == 0 ? 1u : seed);
            // Swings of the form 1 - cos: no offset and no slope at the start, and a curvature never
            // above amplitude × wavenumber² anywhere. The seed picks the sides and the roll's wobble.
            var sideA = random.NextBool() ? 1f : -1f;
            var sideB = random.NextBool() ? 1f : -1f;
            var sideC = random.NextBool() ? 1f : -1f;
            var phaseD = random.NextFloat(0f, 2f * math.PI);
            var count = math.max(4, (int)math.ceil(shape.Length / math.max(1f, shape.Spacing)) + 1);
            var knots = new CourseKnot[count];
            var kLateral = 2f * math.PI / shape.LateralWavelength;
            var kVertical = 2f * math.PI / shape.VerticalWavelength;

            // The roll comes in over the ramp: the intro starts level.
            float Envelope(float z) => shape.Ramp > 0f ? math.smoothstep(0f, shape.Ramp, z) : 1f;
            float Lateral(float z) => shape.Lateral * (
                0.75f * sideA * (1f - math.cos(kLateral * z))
                + 0.25f * sideB * (1f - math.cos(kLateral / 1.7f * z)));
            float Vertical(float z) => shape.Vertical * sideC * (1f - math.cos(kVertical * z)) * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var z = i * shape.Spacing;
                // Curvature of the swing left and right: bank into it.
                const float h = 50f;
                var curvature = (Lateral(z + h) - 2f * Lateral(z) + Lateral(z - h)) / (h * h);
                var roll = Envelope(z) * (math.clamp(curvature * shape.Bank, -shape.MaxRoll, shape.MaxRoll)
                                          + shape.Wobble * math.sin(2f * math.PI * z / math.max(50f, shape.WobbleWavelength) + phaseD));
                knots[i] = new CourseKnot { Position = new float3(Lateral(z), Vertical(z), z), Roll = roll };
            }
            return knots;
        }

        // ------------------------------------------------------------------ baking

        public static float3 CatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            var t2 = t * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t2 * t);
        }

        public static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            var t2 = t * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t2 * t);
        }

        /// <summary>
        /// Orientation of the path where it heads along <paramref name="tangent"/> with a roll:
        /// forward along the path, up as close to the world's up as the heading allows, then banked.
        /// </summary>
        public static quaternion FrameRotation(float3 tangent, float rollDegrees)
        {
            var forward = math.normalizesafe(tangent, new float3(0f, 0f, 1f));
            var right = math.normalizesafe(math.cross(new float3(0f, 1f, 0f), forward), new float3(1f, 0f, 0f));
            var up = math.cross(forward, right);
            var roll = math.radians(rollDegrees);
            return quaternion.LookRotationSafe(forward, math.cos(roll) * up + math.sin(roll) * right);
        }

        /// <summary>
        /// Samples the spline through <paramref name="knots"/> every <paramref name="step"/> metres of
        /// its length, so a distance along the course reads directly as a frame.
        /// </summary>
        public static void Bake(IReadOnlyList<CourseKnot> knots, float step, List<double3> positions, List<quaternion> rotations)
        {
            positions.Clear();
            rotations.Clear();
            if (knots == null || knots.Count < 2 || step <= 0f)
                return;

            CourseKnot Knot(int i)
            {
                if (i < 0)
                    return new CourseKnot { Position = 2f * knots[0].Position - knots[1].Position, Roll = knots[0].Roll };
                if (i >= knots.Count)
                {
                    var last = knots.Count - 1;
                    return new CourseKnot { Position = 2f * knots[last].Position - knots[last - 1].Position, Roll = knots[last].Roll };
                }
                return knots[i];
            }

            const int subdivisions = 64;
            var rolls = new List<float>();
            var previous = knots[0].Position;
            var previousRoll = knots[0].Roll;
            var travelled = 0.0;
            var next = 0.0;
            positions.Add((double3)previous);
            rolls.Add(previousRoll);
            next += step;
            for (var segment = 0; segment < knots.Count - 1; segment++)
            {
                var k0 = Knot(segment - 1);
                var k1 = Knot(segment);
                var k2 = Knot(segment + 1);
                var k3 = Knot(segment + 2);
                for (var s = 1; s <= subdivisions; s++)
                {
                    var t = s / (float)subdivisions;
                    var point = CatmullRom(k0.Position, k1.Position, k2.Position, k3.Position, t);
                    var roll = CatmullRom(k0.Roll, k1.Roll, k2.Roll, k3.Roll, t);
                    var length = math.distance(previous, point);
                    while (length > 0f && travelled + length >= next)
                    {
                        var f = (float)((next - travelled) / length);
                        positions.Add((double3)math.lerp(previous, point, f));
                        rolls.Add(math.lerp(previousRoll, roll, f));
                        next += step;
                    }
                    travelled += length;
                    previous = point;
                    previousRoll = roll;
                }
            }

            for (var i = 0; i < positions.Count; i++)
            {
                var before = positions[math.max(0, i - 1)];
                var after = positions[math.min(positions.Count - 1, i + 1)];
                rotations.Add(FrameRotation((float3)(after - before), rolls[i]));
            }
        }

        /// <summary>Builds the blob read by the render systems from a baked course.</summary>
        public static BlobAssetReference<CoursePathBlob> BuildBlob(float step, IReadOnlyList<double3> positions, IReadOnlyList<quaternion> rotations)
        {
            using var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CoursePathBlob>();
            root.Step = step;
            var p = builder.Allocate(ref root.Positions, positions.Count);
            var r = builder.Allocate(ref root.Rotations, rotations.Count);
            for (var i = 0; i < positions.Count; i++)
                p[i] = positions[i];
            for (var i = 0; i < rotations.Count; i++)
                r[i] = rotations[i];
            return builder.CreateBlobAssetReference<CoursePathBlob>(Unity.Collections.Allocator.Persistent);
        }

        // ------------------------------------------------------------------ reading

        /// <summary>
        /// Frame of the course <paramref name="along"/> metres from its start. Before the start and
        /// past the end, the course goes straight on along its first and last headings.
        /// </summary>
        public static void Frame(ref CoursePathBlob path, double along, out double3 position, out quaternion rotation)
        {
            var count = path.Positions.Length;
            if (count == 0 || path.Step <= 0f)
            {
                position = new double3(0.0, 0.0, along);
                rotation = quaternion.identity;
                return;
            }
            var u = along / path.Step;
            if (u <= 0.0 || count == 1)
            {
                rotation = path.Rotations[0];
                position = path.Positions[0] + (double3)math.mul(rotation, new float3(0f, 0f, 1f)) * along;
                return;
            }
            var last = count - 1;
            if (u >= last)
            {
                rotation = path.Rotations[last];
                position = path.Positions[last] + (double3)math.mul(rotation, new float3(0f, 0f, 1f)) * (along - last * (double)path.Step);
                return;
            }
            var i = (int)math.floor(u);
            var f = u - i;
            position = math.lerp(path.Positions[i], path.Positions[i + 1], f);
            rotation = math.nlerp(path.Rotations[i], path.Rotations[i + 1], (float)f);
        }

        /// <summary>
        /// Bends a point of the straight lane onto the course: <paramref name="lane"/> holds (right,
        /// up, along) from the lane's origin; the result is in the same frame, on the course.
        /// </summary>
        public static double3 Bend(ref CoursePathBlob path, double3 lane, out quaternion frame)
        {
            Frame(ref path, lane.z, out var position, out frame);
            return position + (double3)math.mul(frame, new float3((float)lane.x, (float)lane.y, 0f));
        }

        /// <summary>
        /// Inverse of <see cref="Bend"/> near a known lane point (the course is gentle: a few steps
        /// along it settle the distance).
        /// </summary>
        public static double3 Unbend(ref CoursePathBlob path, double3 bent, double nearAlong)
        {
            var along = nearAlong;
            float3 local = default;
            for (var i = 0; i < 6; i++)
            {
                Frame(ref path, along, out var position, out var frame);
                local = math.mul(math.inverse(frame), (float3)(bent - position));
                along += local.z;
                if (math.abs(local.z) < 1e-3f)
                    break;
            }
            return new double3(local.x, local.y, along);
        }

        // ------------------------------------------------------------------ idle sway

        /// <summary>
        /// Gentle drift of an enemy around its station, in its lane frame (X right, Y up, Z ahead):
        /// a slow heave up and down, a slower surge back and forth, and a slight pitch and roll that
        /// follow the heave. Purely visual.
        /// </summary>
        public static void Sway(in IdleSway sway, double time, out float3 offset, out quaternion tilt)
        {
            var heave = 2.0 * math.PI * time / math.max(0.1f, sway.HeavePeriod) + sway.Phase;
            var surge = 2.0 * math.PI * time / math.max(0.1f, sway.SurgePeriod) + sway.Phase * 1.7;
            offset = new float3(0f, sway.Heave * (float)math.sin(heave), sway.Surge * (float)math.sin(surge));
            // Nose up while rising, a touch of roll out of phase.
            var pitch = -sway.Tilt * (float)math.cos(heave);
            var roll = sway.Tilt * 0.6f * (float)math.sin(heave * 0.5 + sway.Phase);
            tilt = quaternion.Euler(math.radians(pitch), 0f, math.radians(roll));
        }

        /// <summary>Sway of an enemy of <paramref name="radius"/>, varied by a seed so no two move alike.</summary>
        public static IdleSway SwayFor(float radius, uint seed)
        {
            var random = new Random(seed * 2654435761u + 1u);
            return new IdleSway
            {
                Heave = radius * random.NextFloat(0.10f, 0.16f),
                Surge = radius * random.NextFloat(0.25f, 0.4f),
                HeavePeriod = random.NextFloat(6.5f, 9.5f),
                SurgePeriod = random.NextFloat(13f, 19f),
                Phase = random.NextFloat(0f, 2f * math.PI),
                Tilt = random.NextFloat(2f, 3.5f),
            };
        }
    }

    /// <summary>Idle drift of an enemy around its station (visual only, see <see cref="CourseMath.Sway"/>).</summary>
    public struct IdleSway : IComponentData
    {
        /// <summary>Up and down amplitude, metres.</summary>
        public float Heave;
        /// <summary>Back and forth amplitude, metres.</summary>
        public float Surge;
        public float HeavePeriod;
        public float SurgePeriod;
        public float Phase;
        /// <summary>Largest pitch, degrees.</summary>
        public float Tilt;
    }
}
