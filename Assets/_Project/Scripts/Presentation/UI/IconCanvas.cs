using Unity.Mathematics;
using UnityEngine;

namespace Space.Presentation.UI
{
    /// <summary>
    /// Tiny anti-aliased vector painter on a CPU pixel buffer, used to generate the item icons at
    /// runtime (no texture assets to author). Coordinates are in pixels, origin bottom-left, y up.
    /// Each shape is a signed distance function; coverage is derived from the distance for smooth edges.
    /// </summary>
    public sealed class IconCanvas
    {
        public readonly int Size;
        public readonly Color[] Pixels;

        public IconCanvas(int size)
        {
            Size = size;
            Pixels = new Color[size * size];
        }

        public delegate float Sdf(float2 p);

        /// <summary>Fills the inside of a shape (distance &lt; 0) with a colour, blended over what is there.</summary>
        public void Fill(Sdf sdf, Color color)
        {
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var d = sdf(new float2(x + 0.5f, y + 0.5f));
                var coverage = math.saturate(0.5f - d);
                if (coverage > 0f)
                    Blend(x, y, color, coverage);
            }
        }

        /// <summary>Fills with a vertical gradient (bottom colour to top colour) over the shape's height range.</summary>
        public void FillGradient(Sdf sdf, Color bottom, Color top, float yBottom, float yTop)
        {
            for (var y = 0; y < Size; y++)
            {
                var t = math.saturate((y + 0.5f - yBottom) / math.max(1f, yTop - yBottom));
                var color = Color.Lerp(bottom, top, t);
                for (var x = 0; x < Size; x++)
                {
                    var d = sdf(new float2(x + 0.5f, y + 0.5f));
                    var coverage = math.saturate(0.5f - d);
                    if (coverage > 0f)
                        Blend(x, y, color, coverage);
                }
            }
        }

        /// <summary>Soft additive halo around a shape, fading over <paramref name="radius"/> pixels.</summary>
        public void Glow(Sdf sdf, Color color, float radius)
        {
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var d = math.max(0f, sdf(new float2(x + 0.5f, y + 0.5f)));
                var k = math.exp(-d / math.max(0.5f, radius)) * color.a;
                if (k < 0.004f)
                    continue;
                ref var px = ref Pixels[y * Size + x];
                px.r = math.min(1f, px.r + color.r * k);
                px.g = math.min(1f, px.g + color.g * k);
                px.b = math.min(1f, px.b + color.b * k);
                px.a = math.min(1f, px.a + k);
            }
        }

        void Blend(int x, int y, Color color, float coverage)
        {
            ref var px = ref Pixels[y * Size + x];
            var a = color.a * coverage;
            var outA = a + px.a * (1f - a);
            if (outA <= 0f)
                return;
            px.r = (color.r * a + px.r * px.a * (1f - a)) / outA;
            px.g = (color.g * a + px.g * px.a * (1f - a)) / outA;
            px.b = (color.b * a + px.b * px.a * (1f - a)) / outA;
            px.a = outA;
        }

        public Color32[] ToColor32()
        {
            var result = new Color32[Pixels.Length];
            for (var i = 0; i < Pixels.Length; i++)
                result[i] = Pixels[i];
            return result;
        }

        // ------------------------------------------------------------------ distance functions

        public static Sdf Circle(float2 center, float radius) => p => math.length(p - center) - radius;

        public static Sdf Ring(float2 center, float radius, float thickness) =>
            p => math.abs(math.length(p - center) - radius) - thickness * 0.5f;

        /// <summary>Ellipse ring (approximate distance), for orbits and perspective circles.</summary>
        public static Sdf EllipseRing(float2 center, float2 radii, float angleRadians, float thickness) => p =>
        {
            var q = Rotate(p - center, -angleRadians) / radii;
            var k = math.length(q);
            var gradient = math.length(q / radii);
            return math.abs(k - 1f) * k / math.max(1e-4f, gradient) - thickness * 0.5f;
        };

        /// <summary>Rectangle with rounded corners, rotated around its center.</summary>
        public static Sdf Box(float2 center, float2 halfSize, float angleRadians = 0f, float rounding = 0f) => p =>
        {
            var q = math.abs(Rotate(p - center, -angleRadians)) - (halfSize - rounding);
            return math.length(math.max(q, 0f)) + math.min(math.max(q.x, q.y), 0f) - rounding;
        };

        /// <summary>Thick line with round caps.</summary>
        public static Sdf Segment(float2 a, float2 b, float thickness) => p =>
        {
            var pa = p - a;
            var ba = b - a;
            var h = math.saturate(math.dot(pa, ba) / math.max(1e-6f, math.dot(ba, ba)));
            return math.length(pa - ba * h) - thickness * 0.5f;
        };

        /// <summary>Filled polygon (any simple polygon, either winding).</summary>
        public static Sdf Polygon(params float2[] v) => p =>
        {
            var d = math.dot(p - v[0], p - v[0]);
            var s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * math.saturate(math.dot(w, e) / math.dot(e, e));
                d = math.min(d, math.dot(b, b));
                var c1 = p.y >= v[i].y;
                var c2 = p.y < v[j].y;
                var c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                    s = -s;
            }
            return s * math.sqrt(d);
        };

        public static Sdf Outline(Sdf shape, float thickness) => p => math.abs(shape(p)) - thickness * 0.5f;

        public static Sdf Union(Sdf a, Sdf b) => p => math.min(a(p), b(p));

        public static Sdf Subtract(Sdf a, Sdf b) => p => math.max(a(p), -b(p));

        public static float2 Rotate(float2 v, float angle)
        {
            math.sincos(angle, out var s, out var c);
            return new float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
