using UnityEngine;

namespace Space.Presentation.Game
{
    /// <summary>
    /// The shots and timings of a level intro: the flyover that settles above our ship, the camera
    /// looking around for contacts, and the comms frames sliding in. Positions are in the ship's
    /// frame, in ship radii (X right, Y up, Z forward). Pure functions, unit tested.
    /// </summary>
    public static class IntroMath
    {
        /// <summary>Smooth start and stop, 0 to 1.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Fast start, soft landing, 0 to 1 (frames dropping into place).</summary>
        public static float EaseOut(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        /// <summary>Where the flyover starts: low behind the ship, a little to the side.</summary>
        public static readonly Vector3 FlyoverStart = new(0.9f, 0.35f, -6f);

        /// <summary>
        /// Where it ends: close above the stern, looking far ahead along the course, our ship in the
        /// bottom of the picture as if we stood on it.
        /// </summary>
        public static readonly Vector3 FlyoverEnd = new(0f, 1.4f, -1.9f);

        /// <summary>The curve bends over the ship rather than through it.</summary>
        static readonly Vector3 FlyoverBend = new(0.5f, 1.5f, -3.4f);

        /// <summary>Camera position of the flyover at <paramref name="t"/> (0 to 1), in ship radii.</summary>
        public static Vector3 FlyoverPosition(float t)
        {
            var s = Ease(t);
            var a = Vector3.Lerp(FlyoverStart, FlyoverBend, s);
            var b = Vector3.Lerp(FlyoverBend, FlyoverEnd, s);
            return Vector3.Lerp(a, b, s);
        }

        /// <summary>Point the flyover looks at: the ship at first, then far down its course.</summary>
        public static Vector3 FlyoverLook(float t) => Vector3.Lerp(new Vector3(0f, 0f, 1.5f), new Vector3(0f, -1.5f, 14f), Ease(t));

        /// <summary>
        /// Heading of the camera looking for contacts, in degrees from the ship's course (+ = right):
        /// a glance to the right, then a longer look to the left, where it stays.
        /// </summary>
        public static float SearchYaw(float seconds, float right = 35f, float left = -55f)
        {
            if (seconds < 1.4f)
                return right * Ease(seconds / 1.4f);
            if (seconds < 1.8f)
                return right;
            return Mathf.Lerp(right, left, Ease((seconds - 1.8f) / 1.8f));
        }

        /// <summary>
        /// A comms frame: how far it has dropped into place (0 hidden above the screen, 1 in place)
        /// and how visible its picture is, <paramref name="seconds"/> after it was called.
        /// The grey frame drops first, the picture fades in once it has landed.
        /// </summary>
        public static (float Drop, float Picture) FrameIn(float seconds, float drop = 0.45f, float fade = 0.35f)
        {
            var d = EaseOut(seconds / drop);
            var p = Mathf.Clamp01((seconds - drop) / fade);
            return (d, p);
        }

        /// <summary>
        /// The part of a long line to show as a subtitle at <paramref name="progress"/> (0 to 1 of the
        /// audio): its sentences in turn, each for a share of the time matching its length.
        /// </summary>
        public static string SubtitleAt(string text, float progress)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            var sentences = Sentences(text);
            var total = 0;
            foreach (var s in sentences)
                total += s.Length;
            var reached = Mathf.Clamp01(progress) * total;
            var sum = 0;
            foreach (var s in sentences)
            {
                sum += s.Length;
                if (reached < sum)
                    return s;
            }
            return sentences[sentences.Count - 1];
        }

        /// <summary>Splits after . ? ! (an ellipsis stays inside its sentence when the next word is lower case).</summary>
        static System.Collections.Generic.List<string> Sentences(string text)
        {
            var result = new System.Collections.Generic.List<string>();
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '.' && text[i] != '?' && text[i] != '!')
                    continue;
                var end = i;
                while (end + 1 < text.Length && text[end + 1] == '.')
                    end++;
                var next = end + 1;
                while (next < text.Length && text[next] == ' ')
                    next++;
                if (next < text.Length && !char.IsUpper(text[next]))
                {
                    i = end;
                    continue;
                }
                result.Add(text.Substring(start, end + 1 - start).Trim());
                start = next;
                i = next - 1;
            }
            if (start < text.Length && text.Substring(start).Trim().Length > 0)
                result.Add(text.Substring(start).Trim());
            if (result.Count == 0)
                result.Add(text.Trim());
            return result;
        }

        /// <summary>A frame leaving: the picture fades out first, then the frame rises away.</summary>
        public static (float Drop, float Picture) FrameOut(float seconds, float fade = 0.2f, float rise = 0.35f)
        {
            var p = 1f - Mathf.Clamp01(seconds / fade);
            var d = 1f - Ease((seconds - fade) / rise);
            return (d, p);
        }
    }
}
