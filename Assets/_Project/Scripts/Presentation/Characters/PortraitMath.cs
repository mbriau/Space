using System;
using UnityEngine;

namespace Space.Presentation.Characters
{
    /// <summary>One mouth shape of a Rhubarb Lip Sync track: from <see cref="start"/> to <see cref="end"/> seconds.</summary>
    [Serializable]
    public struct MouthCue
    {
        public float start;
        public float end;
        public string value;
    }

    /// <summary>A Rhubarb Lip Sync JSON export (rhubarb -f json).</summary>
    [Serializable]
    public class LipSyncTrack
    {
        public MouthCue[] mouthCues = Array.Empty<MouthCue>();

        public static LipSyncTrack Parse(string json) =>
            string.IsNullOrEmpty(json) ? new LipSyncTrack() : JsonUtility.FromJson<LipSyncTrack>(json) ?? new LipSyncTrack();
    }

    /// <summary>Where the parts of a portrait are, in pixels of the source image (origin top left).</summary>
    [Serializable]
    public struct PortraitLandmarks
    {
        public float ImageSize;
        public Vector2 HeadCenter;
        /// <summary>Half-size of the ellipse that moves with the head (it also takes the hand on the headset).</summary>
        public Vector2 HeadRadii;
        /// <summary>The head turns around this point.</summary>
        public Vector2 Neck;
        /// <summary>Horizontal band of the torso that breathes.</summary>
        public float TorsoLeft, TorsoRight;
        /// <summary>Width of the border kept still, so the frame never shows a gap.</summary>
        public float BorderPx;
    }

    /// <summary>The pose of a portrait at one moment.</summary>
    public struct PortraitPose
    {
        /// <summary>Head roll around the neck, in degrees.</summary>
        public float HeadAngle;
        /// <summary>Head shift, in pixels of the source image.</summary>
        public Vector2 HeadOffset;
        /// <summary>Breathing lift of the torso, in pixels (positive = up).</summary>
        public float Breath;
    }

    /// <summary>
    /// The puppet maths of a talking portrait: how each point of the image moves for a pose, which
    /// mouth to show at a time of the voice line, and how open it is. Pure functions, unit tested.
    /// </summary>
    public static class PortraitMath
    {
        /// <summary>
        /// Where a point of the source image (pixels, origin top left) is drawn for a pose. The head
        /// ellipse turns around the neck and shifts; the torso lifts with the breath; the border of the
        /// image never moves.
        /// </summary>
        public static Vector2 Displace(Vector2 p, in PortraitLandmarks l, in PortraitPose pose)
        {
            var size = l.ImageSize;
            var border = Mathf.Max(1f, l.BorderPx);
            var still = Smooth(0f, border, p.x) * Smooth(size, size - border, p.x)
                        * Smooth(0f, border, p.y) * Smooth(size, size - border, p.y);
            if (still <= 0f)
                return p;

            // Head: inside the ellipse, faded out below the neck.
            var e = new Vector2((p.x - l.HeadCenter.x) / Mathf.Max(1f, l.HeadRadii.x), (p.y - l.HeadCenter.y) / Mathf.Max(1f, l.HeadRadii.y)).magnitude;
            var head = 1f - Smooth(0.55f, 1f, e);
            head *= 1f - Smooth(l.Neck.y, l.Neck.y + 90f, p.y);
            var turned = Rotate(p - l.Neck, pose.HeadAngle) + l.Neck + pose.HeadOffset;
            var d = (turned - p) * head;

            // Breath: the torso and everything above it lift a little, the bottom of the frame stays.
            var band = Smooth(l.TorsoLeft - 120f, l.TorsoLeft + 60f, p.x) * Smooth(l.TorsoRight + 120f, l.TorsoRight - 60f, p.x);
            var lift = Smooth(size, l.Neck.y + 60f, p.y);
            d.y -= pose.Breath * band * lift;

            return p + d * still;
        }

        /// <summary>The mouth cue playing at <paramref name="time"/> seconds, or rest ("X") outside the track.</summary>
        public static string CueAt(MouthCue[] cues, float time)
        {
            if (cues == null || cues.Length == 0 || time < 0f)
                return "X";
            // Binary search: cues are sorted and contiguous.
            int lo = 0, hi = cues.Length - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                if (time < cues[mid].start)
                    hi = mid - 1;
                else if (time >= cues[mid].end)
                    lo = mid + 1;
                else
                    return string.IsNullOrEmpty(cues[mid].value) ? "X" : cues[mid].value;
            }
            return "X";
        }

        /// <summary>
        /// The drawn mouth for a Rhubarb shape. Extended shapes reuse the basic ones (G → B, H → C),
        /// and the wide-open D uses C: our D drawing is too strong for normal speech.
        /// </summary>
        public static string PatchFor(string shape) => shape switch
        {
            "A" or "B" or "C" or "E" or "F" => shape,
            "D" or "H" => "C",
            "G" => "B",
            _ => "X",
        };

        /// <summary>How open a mouth shape is, 0 (closed) to 1 (wide open): drives the small talking nods.</summary>
        public static float Openness(string shape) => shape switch
        {
            "D" => 1f,
            "C" or "H" => 0.75f,
            "B" or "E" or "G" => 0.5f,
            "F" => 0.3f,
            "A" => 0.1f,
            _ => 0f,
        };

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            var c = Mathf.Cos(r);
            var s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Smoothstep from <paramref name="from"/> (0) to <paramref name="to"/> (1); works both ways.</summary>
        static float Smooth(float from, float to, float x)
        {
            var t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }
    }
}
