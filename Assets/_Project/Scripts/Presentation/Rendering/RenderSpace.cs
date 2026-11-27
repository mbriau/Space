using Unity.Entities;
using Unity.Mathematics;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Singleton: how a simulation position becomes a render position. The simulation flies a
    /// straight lane; the render bends that lane onto a gentle course (a <see cref="CoursePath"/>),
    /// every ship keeping its place relative to the lane, then subtracts the floating origin.
    /// Updated each frame by <see cref="CourseWarpSystem"/>; without a course it is the plain
    /// floating-origin offset.
    /// </summary>
    public struct RenderSpace : IComponentData
    {
        /// <summary>The floating origin, in simulation space.</summary>
        public double3 Origin;

        /// <summary>Where the floating origin lands once bent: render (0,0,0) in the bent world.</summary>
        public double3 RenderOrigin;

        /// <summary>The lane is bent onto <see cref="Path"/>.</summary>
        public bool Curved;

        /// <summary>The flagship's lane: a point of it and its axes (simulation space).</summary>
        public double3 LaneOrigin;
        public double3 Right, Up, Forward;

        public BlobAssetReference<CoursePathBlob> Path;

        public static RenderSpace Flat(double3 origin) => new()
        {
            Origin = origin, RenderOrigin = origin,
            Right = new double3(1, 0, 0), Up = new double3(0, 1, 0), Forward = new double3(0, 0, 1),
        };

        /// <summary>Lane coordinates (right, up, along) of a simulation point.</summary>
        public double3 ToLane(double3 sim)
        {
            var d = sim - LaneOrigin;
            return new double3(math.dot(d, Right), math.dot(d, Up), math.dot(d, Forward));
        }

        double3 FromLane(double3 lane) => LaneOrigin + Right * lane.x + Up * lane.y + Forward * lane.z;

        quaternion LaneRotation => quaternion.LookRotationSafe((float3)Forward, (float3)Up);

        /// <summary>The bent-world position of a simulation point (double precision).</summary>
        public double3 Bent(double3 sim) => Bent(sim, out _);

        /// <summary>The bent-world position of a simulation point and the course's rotation there.</summary>
        public double3 Bent(double3 sim, out quaternion frame)
        {
            if (!Curved || !Path.IsCreated)
            {
                frame = quaternion.identity;
                return sim;
            }
            var bent = CourseMath.Bend(ref Path.Value, ToLane(sim), out var local);
            var lane = LaneRotation;
            frame = math.mul(lane, math.mul(local, math.inverse(lane)));
            return FromLane(bent);
        }

        /// <summary>Render position of a simulation point.</summary>
        public float3 ToRender(double3 sim) => (float3)(Bent(sim) - RenderOrigin);

        /// <summary>Render position and rotation of a ship at a simulation point.</summary>
        public void ToRender(double3 sim, quaternion simRotation, out float3 position, out quaternion rotation)
        {
            position = (float3)(Bent(sim, out var frame) - RenderOrigin);
            rotation = math.mul(frame, simRotation);
        }

        /// <summary>Simulation point under a render point, searched near a known simulation point.</summary>
        public double3 ToSim(float3 render, double3 near)
        {
            var bent = RenderOrigin + (double3)render;
            if (!Curved || !Path.IsCreated)
                return bent;
            var lane = CourseMath.Unbend(ref Path.Value, ToLane(bent), ToLane(near).z);
            return FromLane(lane);
        }

        /// <summary>A small offset in the lane's frame (X right, Y up, Z ahead), in simulation space.</summary>
        public double3 LaneOffset(float3 local) => Right * local.x + Up * local.y + Forward * local.z;
    }
}
