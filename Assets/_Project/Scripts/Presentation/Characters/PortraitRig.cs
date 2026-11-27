using System;
using System.Collections.Generic;
using UnityEngine;

namespace Space.Presentation.Characters
{
    /// <summary>
    /// Brings a painted portrait to life without re-drawing it: the body breathes, the head sways a
    /// little and nods while talking (the image bends around the neck, <see cref="PortraitMath"/>),
    /// the eyes blink (a closed-eyes patch) and the mouth follows the voice line shape by shape
    /// (mouth patches chosen from a Rhubarb Lip Sync track, timed on the AudioSource).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PortraitRig : MonoBehaviour
    {
        [Serializable]
        public struct MouthPatch
        {
            [Tooltip("Rhubarb shape: X (rest), A, B, C, D, E, F.")]
            public string Shape;
            public Texture Texture;
        }

        [Header("Image")]
        public PortraitLandmarks Landmarks = new()
        {
            ImageSize = 1024f,
            HeadCenter = new Vector2(680f, 400f),
            HeadRadii = new Vector2(260f, 250f),
            Neck = new Vector2(660f, 640f),
            TorsoLeft = 220f,
            TorsoRight = 900f,
            BorderPx = 40f,
        };

        [Header("Calques")]
        public PortraitLayer Base;
        public PortraitLayer Eyes;
        public PortraitLayer Mouth;
        public Texture EyesClosed;
        public List<MouthPatch> Mouths = new();

        [Header("Voix")]
        public AudioClip Line;
        public TextAsset LipSync;
        [Tooltip("Plays the line on its own (off when a PortraitDialogue drives the portrait).")]
        public bool AutoPlay = true;
        [Tooltip("Seconds between two replays of the line (0 = once).")]
        public float RepeatEvery = 9f;
        public float FirstDelay = 1f;

        [Header("Mouvements")]
        public float BreathPixels = 4f;
        public float BreathPeriod = 4.2f;
        public float SwayDegrees = 1.2f;
        public float SwayPixels = 3f;
        public float TalkNodDegrees = 1.6f;
        public Vector2 BlinkInterval = new(2.5f, 5.5f);
        public float BlinkDuration = 0.13f;

        AudioSource _audio;
        LipSyncTrack _track;
        readonly Dictionary<string, Texture> _mouths = new();
        float _nextPlay;
        float _nextBlink;
        float _blinkLeft;
        float _nod;

        /// <summary>Brightness of the whole portrait (a dialogue dims whoever is listening).</summary>
        [Range(0f, 1f)] public float Brightness = 1f;

        /// <summary>How far into the current line the voice is, in seconds.</summary>
        public float SpokenSeconds => _audio != null && _audio.isPlaying ? _audio.time : 0f;

        /// <summary>The voice line is playing.</summary>
        public bool IsSpeaking => _audio != null && _audio.isPlaying;

        /// <summary>The mouth shape shown this frame (for tests and debugging).</summary>
        public string CurrentShape { get; private set; } = "X";
        public PortraitPose Pose { get; private set; }

        void Awake()
        {
            _audio = GetComponent<AudioSource>();
            _audio.playOnAwake = false;
            _track = LipSyncTrack.Parse(LipSync != null ? LipSync.text : null);
            foreach (var mouth in Mouths)
                if (mouth.Texture != null && !string.IsNullOrEmpty(mouth.Shape))
                    _mouths[mouth.Shape] = mouth.Texture;
            _nextPlay = Time.time + FirstDelay;
            _nextBlink = Time.time + UnityEngine.Random.Range(BlinkInterval.x, BlinkInterval.y);
        }

        /// <summary>Says another line: its audio and its Rhubarb lip-sync track.</summary>
        public void Speak(AudioClip line, TextAsset lipSync)
        {
            Line = line;
            LipSync = lipSync;
            _track = LipSyncTrack.Parse(lipSync != null ? lipSync.text : null);
            Speak();
        }

        /// <summary>Stops talking at once (skipped scene).</summary>
        public void StopSpeaking()
        {
            if (_audio != null)
                _audio.Stop();
        }

        /// <summary>Plays the voice line now (the mouth follows it).</summary>
        public void Speak()
        {
            if (Line == null)
                return;
            _audio.clip = Line;
            _audio.Play();
        }

        void Update()
        {
            var t = Time.time;
            if (AutoPlay && Line != null && !_audio.isPlaying && t >= _nextPlay && _nextPlay > 0f)
            {
                Speak();
                _nextPlay = RepeatEvery > 0f ? t + Line.length + RepeatEvery : -1f;
            }

            // Mouth: the cue at the audio time, rest otherwise.
            var shape = _audio.isPlaying ? PortraitMath.CueAt(_track.mouthCues, _audio.time) : "X";
            CurrentShape = shape;
            _nod = Mathf.MoveTowards(_nod, PortraitMath.Openness(shape), Time.deltaTime * 6f);

            // Blink.
            if (t >= _nextBlink)
            {
                _blinkLeft = BlinkDuration;
                _nextBlink = t + UnityEngine.Random.Range(BlinkInterval.x, BlinkInterval.y);
            }
            _blinkLeft -= Time.deltaTime;

            // Pose: slow breathing, a drifting sway, small nods on the open vowels.
            var sway = Mathf.PerlinNoise(t * 0.25f, 0.3f) * 2f - 1f;
            var drift = new Vector2(Mathf.PerlinNoise(t * 0.2f, 5.1f) * 2f - 1f, Mathf.PerlinNoise(t * 0.2f, 9.7f) * 2f - 1f);
            Pose = new PortraitPose
            {
                HeadAngle = sway * SwayDegrees + _nod * TalkNodDegrees,
                HeadOffset = drift * SwayPixels + new Vector2(0f, _nod * 2f),
                Breath = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.5f, BreathPeriod)) * BreathPixels,
            };
        }

        void LateUpdate()
        {
            var pose = Pose;
            foreach (var layer in new[] { Base, Eyes, Mouth })
                if (layer != null)
                    layer.Brightness = Brightness;
            if (Base != null)
                Base.Apply(Landmarks, pose);

            if (Eyes != null)
            {
                Eyes.Texture = EyesClosed;
                Eyes.Alpha = _blinkLeft > 0f ? 1f : 0f;
                Eyes.Apply(Landmarks, pose);
            }

            if (Mouth != null)
            {
                _mouths.TryGetValue(PortraitMath.PatchFor(CurrentShape), out var texture);
                Mouth.Texture = texture;
                Mouth.Alpha = texture != null ? 1f : 0f;
                Mouth.Apply(Landmarks, pose);
            }
        }
    }
}
