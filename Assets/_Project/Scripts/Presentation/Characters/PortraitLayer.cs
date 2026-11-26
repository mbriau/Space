using UnityEngine;

namespace Space.Presentation.Characters
{
    /// <summary>
    /// One layer of a talking portrait: a part of the source image (the whole picture, or a patch
    /// such as the mouth) drawn as a grid that bends with the <see cref="PortraitRig"/>'s pose.
    /// The portrait spans one unit, centred on the rig.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PortraitLayer : MonoBehaviour
    {
        [Tooltip("Region of the source image this layer covers, in pixels (origin top left).")]
        public Rect RegionPx = new(0, 0, 1024, 1024);

        public Texture Texture;
        [Range(2, 64)] public int Grid = 32;

        [Tooltip("Drawing order among the portrait's layers (higher in front).")]
        public int Order;

        [Range(0f, 1f)] public float Alpha = 1f;

        Mesh _mesh;
        Vector3[] _vertices;
        Material _material;
        MaterialPropertyBlock _block;
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void EnsureMesh()
        {
            if (_mesh != null)
                return;
            var n = Grid + 1;
            _vertices = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var triangles = new int[Grid * Grid * 6];
            for (int j = 0, k = 0, t = 0; j <= Grid; j++)
            for (var i = 0; i <= Grid; i++, k++)
            {
                uvs[k] = new Vector2(i / (float)Grid, j / (float)Grid);
                if (i == Grid || j == Grid)
                    continue;
                triangles[t++] = k; triangles[t++] = k + n; triangles[t++] = k + 1;
                triangles[t++] = k + 1; triangles[t++] = k + n; triangles[t++] = k + n + 1;
            }
            _mesh = new Mesh { name = $"Portrait {name}" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            var shader = Shader.Find("Space/PortraitLayer");
            _material = new Material(shader) { name = $"Portrait {name}", renderQueue = 3000 + Order };
            GetComponent<MeshRenderer>().sharedMaterial = _material;
            _block = new MaterialPropertyBlock();
        }

        /// <summary>Bends the grid for the current pose (called by the rig every frame).</summary>
        public void Apply(in PortraitLandmarks landmarks, in PortraitPose pose)
        {
            EnsureMesh();
            var size = landmarks.ImageSize;
            for (int j = 0, k = 0; j <= Grid; j++)
            for (var i = 0; i <= Grid; i++, k++)
            {
                // Texture v goes up, the image's y goes down.
                var source = new Vector2(RegionPx.xMin + RegionPx.width * i / Grid, RegionPx.yMax - RegionPx.height * j / Grid);
                var drawn = PortraitMath.Displace(source, landmarks, pose);
                _vertices[k] = new Vector3(drawn.x / size - 0.5f, 0.5f - drawn.y / size, -Order * 0.001f);
            }
            _mesh.vertices = _vertices;

            var renderer = GetComponent<MeshRenderer>();
            renderer.enabled = Texture != null && Alpha > 0.001f;
            renderer.GetPropertyBlock(_block);
            if (Texture != null)
                _block.SetTexture(MainTex, Texture);
            _block.SetColor(ColorId, new Color(1f, 1f, 1f, Alpha));
            renderer.SetPropertyBlock(_block);
        }

        void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
            if (_material != null)
                Destroy(_material);
        }
    }
}
