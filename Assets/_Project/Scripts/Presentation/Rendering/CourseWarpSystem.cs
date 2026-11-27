using System.Collections.Generic;
using Space.Core.Combat;
using Space.Core.Ships;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// Keeps <see cref="RenderSpace"/> up to date: bakes the shared <see cref="CoursePath"/> once,
    /// follows the player's flagship lane (kept after the flagship is lost, so nothing snaps back),
    /// and bends the floating origin. Runs after the origin moves, before the render transforms.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FloatingOriginSystem))]
    [UpdateBefore(typeof(SimToRenderTransformSystem))]
    public partial class CourseWarpSystem : SystemBase
    {
        /// <summary>Bends the lane onto the course (off: the straight lane, as simulated).</summary>
        public static bool Bend = true;

        /// <summary>
        /// How much the tactical camera follows the course's roll and pitch, from the course in use;
        /// negative when there is none (the camera then uses its own setting).
        /// </summary>
        public static float CameraFollow = -1f;

        CoursePath _course;
        int _bakedVersion = -1;
        BlobAssetReference<CoursePathBlob> _path;
        EntityQuery _flagships;
        bool _hasLane;
        double3 _laneOrigin;
        float3 _heading;

        protected override void OnCreate()
        {
            EntityManager.CreateSingleton(RenderSpace.Flat(double3.zero), "RenderSpace");
            _flagships = GetEntityQuery(ComponentType.ReadOnly<PlayerFlagship>(), ComponentType.ReadOnly<CruiseCourse>());
            RequireForUpdate<FloatingOrigin>();
            _course = Resources.Load<CoursePath>(CoursePath.ResourceName);
        }

        protected override void OnDestroy()
        {
            CameraFollow = -1f;
            if (_path.IsCreated)
                _path.Dispose();
        }

        /// <summary>Bakes the course again when it changed (an edit in the inspector, in Play mode too).</summary>
        void BakeIfChanged()
        {
            if (_course == null || _course.Version == _bakedVersion)
                return;
            _bakedVersion = _course.Version;
            if (_course.Knots == null || _course.Knots.Length < 2)
                _course.Regenerate();
            _bakedVersion = _course.Version;

            var positions = new List<double3>();
            var rotations = new List<quaternion>();
            CourseMath.Bake(_course.Knots, _course.BakeStep, positions, rotations);
            // The render jobs of the last frame may still read the old course.
            EntityManager.CompleteAllTrackedJobs();
            if (_path.IsCreated)
                _path.Dispose();
            _path = CourseMath.BuildBlob(_course.BakeStep, positions, rotations);
        }

        protected override void OnUpdate()
        {
            BakeIfChanged();
            CameraFollow = Bend && _course != null ? _course.Shape.CameraFollow : -1f;

            if (!_flagships.IsEmpty)
            {
                var course = _flagships.GetSingleton<CruiseCourse>();
                _laneOrigin = course.LaneOrigin;
                _heading = math.normalizesafe(course.Heading, new float3(0f, 0f, 1f));
                _hasLane = true;
            }

            var origin = SystemAPI.GetSingleton<FloatingOrigin>().Origin;
            var space = RenderSpace.Flat(origin);
            if (Bend && _hasLane && _path.IsCreated)
            {
                var forward = (double3)_heading;
                var right = math.normalizesafe(math.cross(new double3(0, 1, 0), forward), new double3(1, 0, 0));
                space.Curved = true;
                space.Path = _path;
                space.LaneOrigin = _laneOrigin;
                space.Forward = forward;
                space.Right = right;
                space.Up = math.cross(forward, right);
                space.RenderOrigin = space.Bent(origin);
            }
            SystemAPI.SetSingleton(space);
        }
    }
}
