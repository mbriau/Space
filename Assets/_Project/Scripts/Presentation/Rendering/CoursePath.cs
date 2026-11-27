using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// The course the fleet appears to fly: a spline through control points with a roll at each,
    /// generated (menu Space > Generate Course Path) and shared by every level for now. Rendering
    /// only: the simulation keeps its straight lane.
    /// </summary>
    [CreateAssetMenu(menuName = "Space/Course Path", fileName = "CoursePath")]
    public class CoursePath : ScriptableObject
    {
        public const string ResourceName = "CoursePath";

        [Tooltip("Seed and shape it was generated with (for regenerating it).")]
        public uint Seed = 7;
        public CourseShape Shape = CourseShape.Default;

        [Tooltip("Distance between two baked frames, metres.")]
        [Min(0.5f)] public float BakeStep = 4f;

        public CourseKnot[] Knots = new CourseKnot[0];

        /// <summary>Rebuilds the knots from <see cref="Seed"/> and <see cref="Shape"/>.</summary>
        public void Regenerate() => Knots = CourseMath.Generate(Seed, Shape);
    }
}
