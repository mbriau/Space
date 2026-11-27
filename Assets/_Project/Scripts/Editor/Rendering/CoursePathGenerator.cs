using Space.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Space.Editor.Rendering
{
    /// <summary>
    /// Generates the course the fleet appears to fly (shared by every level for now), in
    /// Resources so the render systems find it. Menu: Space &gt; Generate Course Path. Running it
    /// again keeps the asset's seed and shape, so tweaking them in the inspector then regenerating
    /// reshapes the course.
    /// </summary>
    public static class CoursePathGenerator
    {
        public const string AssetPath = "Assets/_Project/Resources/" + CoursePath.ResourceName + ".asset";

        [MenuItem("Space/Generate Course Path")]
        public static CoursePath Generate()
        {
            var course = AssetDatabase.LoadAssetAtPath<CoursePath>(AssetPath);
            if (course == null)
            {
                course = ScriptableObject.CreateInstance<CoursePath>();
                AssetDatabase.CreateAsset(course, AssetPath);
            }
            course.Regenerate();
            EditorUtility.SetDirty(course);
            AssetDatabase.SaveAssets();
            Debug.Log($"Course path: {course.Knots.Length} knots, {course.Knots[^1].Position.z / 1000f:0.#} km ({AssetPath}).");
            return course;
        }
    }
}
