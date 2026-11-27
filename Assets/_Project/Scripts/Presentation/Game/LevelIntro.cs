using System;
using System.Collections;
using System.Collections.Generic;
using Space.Core.Levels;
using Space.Presentation.Controls;
using Space.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space.Presentation.Game
{
    /// <summary>
    /// Scripted opening of a level. With the level clock held (no enemy, no shot, timer stopped):
    /// the camera flies over our ship and settles above it; Kaïa tells a story on the comms; a
    /// little later she picks up contacts, the clock runs, the camera looks right then left and the
    /// enemies drop out of hyperspace; she calls them, their captain answers in the other corner;
    /// then the frames leave and the fight begins. Escape skips it.
    /// </summary>
    public class LevelIntro : MonoBehaviour
    {
        [Serializable]
        public class Line
        {
            [Tooltip("0 = left speaker (our pilot), 1 = right speaker (the enemy).")]
            public int Speaker;
            public AudioClip Audio;
            public TextAsset LipSync;
            [TextArea] public string Subtitle;
        }

        [Tooltip("The level this intro opens.")]
        public LevelDefinition Level;

        [Header("Interlocuteurs")]
        public GameObject LeftPortrait;
        public string LeftName = "KAÏA MOREAU";
        public string LeftRole = "Remorqueur";
        public Color LeftAccent = new(0.35f, 0.85f, 1f);
        public GameObject RightPortrait;
        public string RightName = "SILAS BRACK";
        public string RightRole = "Écorcheurs";
        public Color RightAccent = new(1f, 0.45f, 0.25f);

        [Header("Répliques")]
        public Line Story;
        public Line Detection;
        public List<Line> Dialogue = new();

        [Header("Rythme")]
        public float FlyoverSeconds = 7f;
        public float SilenceAfterStory = 3.5f;
        public float AfterArrivals = 1.5f;

        readonly CommsChannel[] _channels = new CommsChannel[2];
        LevelRunner _runner;
        ShipCameraRig _cameraRig;
        PlayerControls _controls;
        Coroutine _routine;
        Line _subtitle;
        GUIStyle _name, _role, _subtitleStyle, _hint;

        public bool Playing => _routine != null;

        /// <summary>Whether this intro opens <paramref name="level"/>.</summary>
        public bool Plays(LevelDefinition level) => level != null && level == Level && LeftPortrait != null;

        /// <summary>Starts the intro (the runner has just started the level, held).</summary>
        public void Play(LevelRunner runner)
        {
            Stop();
            _runner = runner;
            _cameraRig = FindAnyObjectByType<ShipCameraRig>();
            _controls = FindAnyObjectByType<PlayerControls>();
            // The voices need ears: the camera carries the scene's listener.
            if (FindAnyObjectByType<AudioListener>() == null && _cameraRig != null)
                _cameraRig.gameObject.AddComponent<AudioListener>();
            if (_channels[0] == null && LeftPortrait != null)
                _channels[0] = new CommsChannel(LeftPortrait, CommsChannel.Side.Left, 0f, LeftName, LeftRole, LeftAccent);
            if (_channels[1] == null && RightPortrait != null)
                _channels[1] = new CommsChannel(RightPortrait, CommsChannel.Side.Right, 1f, RightName, RightRole, RightAccent);
            _routine = StartCoroutine(Run());
        }

        void Stop()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
        }

        IEnumerator Run()
        {
            UiFocus.Cinematic = true;
            _cameraRig?.TakeScripted();

            // Wait for our ship to be in space.
            while (!_controls.TryGetMainShipFrame(out _, out _, out _))
                yield return null;

            // 1. The camera comes from behind and settles above our ship.
            for (var t = 0f; t < FlyoverSeconds; t += Time.deltaTime)
            {
                Shot(IntroMath.FlyoverPosition(t / FlyoverSeconds), IntroMath.FlyoverLook(t / FlyoverSeconds), 0f);
                yield return null;
            }

            // 2. Kaïa tells her story, the camera drifting slowly above the ship.
            var drift = 0f;
            yield return Say(Story, () => HoldAbove(drift += Time.deltaTime));
            Channel(0)?.Hide();
            for (var t = 0f; t < SilenceAfterStory; t += Time.deltaTime)
            {
                HoldAbove(drift += Time.deltaTime);
                yield return null;
            }

            // 3. Contacts: the clock runs, the enemies drop out of hyperspace while the camera looks around.
            _runner.SetHold(clock: false, ceaseFire: true);
            var search = 0f;
            var wanted = Mathf.Max(1, Level != null ? Level.Enemies.Length : 1);
            var lineDone = false;
            StartCoroutine(SayThen(Detection, () => lineDone = true));
            while (!lineDone || _runner.ArrivedEnemies() < wanted)
            {
                search += Time.deltaTime;
                Shot(IntroMath.FlyoverEnd, Search(search), 0f);
                yield return null;
            }
            for (var t = 0f; t < AfterArrivals; t += Time.deltaTime)
            {
                search += Time.deltaTime;
                Shot(IntroMath.FlyoverEnd, Search(search), 0f);
                yield return null;
            }

            // 4. The dialogue, the battle camera framing everyone, the clock held again.
            _runner.SetHold(clock: true, ceaseFire: true);
            _cameraRig?.ReleaseScripted();
            foreach (var line in Dialogue)
                yield return Say(line, null);
            yield return new WaitForSeconds(0.8f);

            // 5. The fight.
            Finish();
        }

        /// <summary>Ends the intro: frames away, hands back, clock and weapons free.</summary>
        void Finish()
        {
            foreach (var channel in _channels)
            {
                channel?.Rig.StopSpeaking();
                channel?.Hide();
            }
            _subtitle = null;
            if (_runner != null)
                _runner.SetHold(clock: false, ceaseFire: false);
            if (_cameraRig != null && _cameraRig.Scripted)
                _cameraRig.ReleaseScripted();
            UiFocus.Cinematic = false;
            _routine = null;
        }

        Vector3 Search(float seconds)
        {
            var yaw = IntroMath.SearchYaw(seconds) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yaw) * 10f, -0.6f, Mathf.Cos(yaw) * 10f);
        }

        void HoldAbove(float seconds)
        {
            var sway = new Vector3(Mathf.Sin(seconds * 0.21f) * 0.35f, Mathf.Sin(seconds * 0.13f) * 0.15f, Mathf.Sin(seconds * 0.17f) * 0.25f);
            Shot(IntroMath.FlyoverEnd + sway, IntroMath.FlyoverLook(1f), 0f);
        }

        /// <summary>Places the camera in the ship's frame (ship radii), looking at a point of that frame.</summary>
        void Shot(Vector3 cameraLocal, Vector3 lookLocal, float roll)
        {
            if (_controls == null || !_controls.TryGetMainShipFrame(out var position, out var rotation, out var radius))
                return;
            var from = position + rotation * (cameraLocal * radius);
            var to = position + rotation * (lookLocal * radius);
            var camera = _cameraRig != null ? _cameraRig.transform : Camera.main.transform;
            camera.SetPositionAndRotation(from, Quaternion.LookRotation(to - from, rotation * Vector3.up) * Quaternion.Euler(0f, 0f, roll));
        }

        IEnumerator SayThen(Line line, Action done)
        {
            yield return Say(line, null);
            done();
        }

        /// <summary>Shows the speaker's frame, says the line with its subtitle, calls <paramref name="each"/> every frame.</summary>
        IEnumerator Say(Line line, Action each)
        {
            var channel = line != null ? Channel(line.Speaker) : null;
            if (channel == null || line.Audio == null)
                yield break;
            if (!channel.Visible)
            {
                channel.Show();
                for (var t = 0f; t < 0.8f; t += Time.deltaTime)
                {
                    each?.Invoke();
                    yield return null;
                }
            }
            channel.Rig.Speak(line.Audio, line.LipSync);
            _subtitle = line;
            yield return null;
            while (channel.Rig.IsSpeaking)
            {
                each?.Invoke();
                yield return null;
            }
            if (_subtitle == line)
                _subtitle = null;
            for (var t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                each?.Invoke();
                yield return null;
            }
        }

        CommsChannel Channel(int speaker) => speaker >= 0 && speaker < _channels.Length ? _channels[speaker] : null;

        void Update()
        {
            foreach (var channel in _channels)
                channel?.Tick(Time.deltaTime);

            if (Playing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Skip();
        }

        /// <summary>Ends the intro at once and starts the fight (Escape).</summary>
        public void Skip()
        {
            if (!Playing)
                return;
            Stop();
            Finish();
        }

        void OnDestroy()
        {
            UiFocus.Cinematic = false;
            foreach (var channel in _channels)
                channel?.Destroy();
        }

        // ------------------------------------------------------------------ frames and subtitles

        void OnGUI()
        {
            EnsureStyles();
            foreach (var channel in _channels)
                if (channel != null)
                    DrawFrame(channel);

            if (_subtitle != null && !string.IsNullOrEmpty(_subtitle.Subtitle))
            {
                var speaker = Channel(_subtitle.Speaker);
                var progress = speaker != null && _subtitle.Audio != null ? speaker.Rig.SpokenSeconds / _subtitle.Audio.length : 0f;
                var sentence = IntroMath.SubtitleAt(_subtitle.Subtitle, progress);
                var text = speaker != null ? $"<b>{Capitalized(speaker.Name)}</b> : {sentence}" : sentence;
                var width = Mathf.Min(900f, Screen.width - 80f);
                var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 130f, width, 90f);
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), StripTags(text), _subtitleStyle);
                GUI.color = Color.white;
                GUI.Label(rect, text, _subtitleStyle);
            }

            if (Playing)
                GUI.Label(new Rect(Screen.width - 220, Screen.height - 34, 200, 24), "Échap : passer", _hint);
        }

        /// <summary>
        /// A frame a quarter of the screen wide in its top corner: a grey plate that drops in, the
        /// portrait fading in on it, a name strip below, and an accent border while the speaker talks.
        /// </summary>
        void DrawFrame(CommsChannel channel)
        {
            var (drop, picture) = channel.State;
            if (drop <= 0.001f)
                return;

            const float margin = 14f, strip = 34f, inset = 6f;
            var width = Screen.width * 0.25f;
            var height = width + strip;
            var x = channel.Corner == CommsChannel.Side.Left ? margin : Screen.width - margin - width;
            var y = Mathf.Lerp(-height - 10f, margin, drop);
            var frame = new Rect(x, y, width, height);

            var speaking = channel.Rig.IsSpeaking;
            Fill(frame, speaking ? channel.Accent : new Color(0.5f, 0.52f, 0.56f, 0.95f));
            Fill(new Rect(frame.x + 2, frame.y + 2, frame.width - 4, frame.height - 4), new Color(0.2f, 0.21f, 0.24f, 0.97f));

            var image = new Rect(frame.x + inset, frame.y + inset, width - 2 * inset, width - 2 * inset);
            Fill(image, new Color(0.08f, 0.09f, 0.11f, 1f));
            if (picture > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, picture);
                GUI.DrawTexture(image, channel.Texture, ScaleMode.ScaleAndCrop, false);
                GUI.color = Color.white;
            }

            var nameRect = new Rect(frame.x + 10, image.yMax + 4, width - 20, strip - 8);
            GUI.Label(nameRect, channel.Name, _name);
            GUI.Label(nameRect, channel.Role, _role);
        }

        static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static string Capitalized(string name) =>
            string.IsNullOrEmpty(name) ? name : name.Substring(0, 1) + name.Substring(1).ToLowerInvariant().Split(' ')[0];

        static string StripTags(string text) => text.Replace("<b>", "").Replace("</b>", "");

        void EnsureStyles()
        {
            if (_name != null)
                return;
            _name = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            _role = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.75f, 0.78f, 0.84f) } };
            _subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.UpperCenter, wordWrap = true, richText = true, normal = { textColor = Color.white } };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1f, 1f, 1f, 0.6f) } };
        }
    }
}
