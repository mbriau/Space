using UnityEngine;

namespace Space.Presentation.Rendering
{
    /// <summary>
    /// The course the fleet appears to fly: a spline through control points with a roll at each,
    /// shared by every level for now. Two shapes, calm and intense, and an intensity between them.
    /// Any change in the inspector regenerates it, and the game picks it up at once (Play mode
    /// included). Rendering only: the simulation keeps its straight lane.
    /// </summary>
    [CreateAssetMenu(menuName = "Space/Course Path", fileName = "CoursePath")]
    public class CoursePath : ScriptableObject
    {
        public const string ResourceName = "CoursePath";

        [Tooltip("0 = the calm shape, 1 = the intense shape; every setting in between.")]
        [Range(0f, 1f)] public float Intensity = 1f;

        [Tooltip("Picks the sides of the swings and the phase of the roll.")]
        public uint Seed = 7;

        public CourseShape Calm = CourseShape.Default;
        public CourseShape Intense = CourseShape.Intense;

        [Tooltip("Distance between two baked frames, metres.")]
        [Min(0.5f)] public float BakeStep = 4f;

        [HideInInspector] public CourseKnot[] Knots = new CourseKnot[0];

        /// <summary>Changes each time the knots are rebuilt (the render systems rebake on a change).</summary>
        [System.NonSerialized] public int Version;

        /// <summary>The shape in use: between <see cref="Calm"/> and <see cref="Intense"/>.</summary>
        public CourseShape Shape => CourseShape.Lerp(Calm, Intense, Intensity);

        /// <summary>Rebuilds the knots from <see cref="Seed"/> and <see cref="Shape"/>.</summary>
        public void Regenerate()
        {
            Knots = CourseMath.Generate(Seed, Shape);
            Version++;
        }

        void OnValidate() => Regenerate();
    }
}
