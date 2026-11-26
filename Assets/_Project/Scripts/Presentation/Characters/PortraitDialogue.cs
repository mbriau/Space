using System.Collections.Generic;
using UnityEngine;

namespace Space.Presentation.Characters
{
    /// <summary>
    /// Plays the lines of several talking portraits one after the other (each rig says its own line),
    /// dims whoever is listening, then starts over after a pause.
    /// </summary>
    public class PortraitDialogue : MonoBehaviour
    {
        public List<PortraitRig> Speakers = new();
        public float FirstDelay = 1f;
        public float GapBetweenLines = 0.6f;
        public float PauseBeforeRepeat = 4f;
        [Range(0f, 1f)] public float ListenerBrightness = 0.55f;

        int _current = -1;
        float _nextStart;
        bool _started;

        void Start()
        {
            foreach (var speaker in Speakers)
                if (speaker != null)
                    speaker.AutoPlay = false;
            _nextStart = Time.time + FirstDelay;
        }

        void Update()
        {
            var speaking = _current >= 0 && _current < Speakers.Count && Speakers[_current] != null && Speakers[_current].IsSpeaking;
            if (!speaking)
            {
                if (_started)
                {
                    // The line just ended: the next one after a short gap, the first one after a pause.
                    _started = false;
                    _nextStart = Time.time + (_current + 1 < Speakers.Count ? GapBetweenLines : PauseBeforeRepeat);
                }
                else if (Time.time >= _nextStart && Speakers.Count > 0)
                {
                    _current = (_current + 1) % Speakers.Count;
                    Speakers[_current]?.Speak();
                    _started = true;
                }
            }

            for (var i = 0; i < Speakers.Count; i++)
            {
                if (Speakers[i] == null)
                    continue;
                var target = i == _current ? 1f : ListenerBrightness;
                Speakers[i].Brightness = Mathf.MoveTowards(Speakers[i].Brightness, target, Time.deltaTime * 2f);
            }
        }
    }
}
