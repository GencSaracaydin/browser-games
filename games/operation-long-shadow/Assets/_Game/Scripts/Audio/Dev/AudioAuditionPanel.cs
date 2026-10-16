using System.Collections.Generic;
using LongShadow.Audio.Ambience;
using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Dev
{
    /// <summary>
    /// Dev-only audition panel of <c>Dev_AudioRange</c> (built by <b>LongShadow/Audio/Build Audio Test Scene</b>; never
    /// in a release build, D1): AI bursts at 10–600 m (near → far crossfade, speed-of-sound delay, zone tails), player
    /// bursts, explosions, a near-miss crack, tinnitus, any VO line by id (dry/radio by the stand-in speaker's distance,
    /// D039's positional switch), the music cues with their layers and stingers, the ambience at any LightStage
    /// <c>s</c>/zone/beat, and a jump between the scene's acoustic zones. IMGUI; allocates, dev only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioAuditionPanel : MonoBehaviour
    {
        private struct Pending
        {
            public float At;
            public string Weapon;
            public Vector3 Position;
            public bool Player;
        }

        private static readonly string[] s_weapons = { WeaponIds.AK, WeaponIds.RPK, WeaponIds.AKMS, WeaponIds.SVD, WeaponIds.DShK, WeaponIds.Commando, WeaponIds.HushPuppy };
        private static readonly float[] s_rpm = { 600f, 650f, 600f, 40f, 575f, 750f, 60f };
        private static readonly float[] s_distances = { 10f, 40f, 80f, 150f, 300f, 600f };
        private static readonly string[] s_zones = { "OpenDesert", "Street", "Square", "Interior", "Basement", "Roof", "Cabin" };

        [SerializeField, Tooltip("The listener stand-in moved between zones (the main camera).")] private Transform _listener;
        [SerializeField, Tooltip("A squad stand-in: VO lines are played from it (dry/radio by distance).")] private Transform _speaker;
        [SerializeField, Tooltip("Listener positions inside the scene's ZoneTags, in ZoneType order.")] private Transform[] _zonePoints = new Transform[0];

        private readonly List<Pending> _pending = new List<Pending>();
        private ServiceRef<IAudioService> _audio;
        private ServiceRef<IVOService> _vo;
        private ServiceRef<IMusicService> _music;
        private AmbienceDirector _ambience;
        private int _weapon;
        private int _rounds = 6;
        private string _line = "D021";
        private float _speakerDistance = 4f;
        private float _s = 0.5f;
        private int _zone = 1;
        private int _beat = 3;
        private bool _forceAmbience = true;
        private Vector2 _scroll;

        /// <summary>Wires the panel (builder).</summary>
        public void Configure(Transform listener, Transform speaker, Transform[] zonePoints)
        {
            _listener = listener;
            _speaker = speaker;
            _zonePoints = zonePoints ?? new Transform[0];
        }

        private void Update()
        {
            var now = Time.time;
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (now < _pending[i].At) continue;
                _audio.Value?.PlayWeaponShot(_pending[i].Weapon, _pending[i].Position, _pending[i].Player);
                _pending.RemoveAt(i);
            }
            if (_speaker != null && _listener != null)
            {
                var flat = _listener.forward;
                flat.y = 0f;
                if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
                _speaker.position = _listener.position + Quaternion.Euler(0f, 30f, 0f) * flat.normalized * _speakerDistance + Vector3.down * 0.2f;
            }
            if (_ambience == null) _ambience = FindAnyObjectByType<AmbienceDirector>();
            if (_ambience != null)
            {
                if (_forceAmbience)
                    _ambience.ForceContext(new AmbienceContext { S = _s, Zone = (ZoneType)_zone, Beat = _beat, Listener = _listener != null ? _listener.position : Vector3.zero, TransmittersOn = true });
                else _ambience.ClearForcedContext();
            }
        }

        private void Burst(float distance, bool player)
        {
            var origin = _listener != null ? _listener.position : Vector3.zero;
            var fwd = _listener != null ? _listener.forward : Vector3.forward;
            fwd.y = 0f;
            var pos = player ? origin : origin + Quaternion.Euler(0f, -35f, 0f) * fwd.normalized * distance;
            var id = s_weapons[_weapon];
            var interval = 60f / s_rpm[_weapon];
            var t = Time.time;
            for (var i = 0; i < _rounds; i++)
                _pending.Add(new Pending { At = t + i * interval, Weapon = id, Position = pos, Player = player });
        }

        private void At(string cue, float distance)
        {
            var origin = _listener != null ? _listener.position : Vector3.zero;
            var fwd = _listener != null ? _listener.forward : Vector3.forward;
            fwd.y = 0f;
            _audio.Value?.Play(cue, origin + Quaternion.Euler(0f, 40f, 0f) * fwd.normalized * distance);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 380f, Screen.height - 20f), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("<b>WEAPONS</b> (AI at an angle; player 2D)");
            GUILayout.BeginHorizontal();
            for (var i = 0; i < s_weapons.Length; i++)
                if (GUILayout.Toggle(_weapon == i, s_weapons[i], GUI.skin.button)) _weapon = i;
            GUILayout.EndHorizontal();
            GUILayout.Label("Rounds per burst: " + _rounds);
            _rounds = Mathf.RoundToInt(GUILayout.HorizontalSlider(_rounds, 1f, 30f));
            GUILayout.BeginHorizontal();
            foreach (var d in s_distances)
                if (GUILayout.Button(d.ToString("0") + " m")) Burst(d, false);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Player burst")) Burst(0f, true);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Frag 25 m")) At("SFX_EXP_FRAG", 25f);
            if (GUILayout.Button("Frag 200 m")) At("SFX_EXP_FRAG", 200f);
            if (GUILayout.Button("Crack 2 m")) At(AudioCues.BulletCrack, 2f);
            if (GUILayout.Button("Tinnitus")) _audio.Value?.Tinnitus(2.5f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("<b>VO</b> (speaker stand-in at " + _speakerDistance.ToString("0.0") + " m: dry ≤ 8 m, radio ≥ 15 m)");
            _speakerDistance = GUILayout.HorizontalSlider(_speakerDistance, 1f, 30f);
            GUILayout.BeginHorizontal();
            _line = GUILayout.TextField(_line, GUILayout.Width(110f));
            if (GUILayout.Button("Play")) _vo.Value?.Play(_line, new VOOptions { Speaker = _speaker });
            if (GUILayout.Button("Radio")) _vo.Value?.Play(_line, new VOOptions { ForceRadio = true });
            if (GUILayout.Button("D039 G9")) _vo.Value?.Play("D039", new VOOptions { Speaker = _speaker, ForceDry = true });
            if (GUILayout.Button("Stop")) _vo.Value?.StopAll(0.1f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("<b>MUSIC</b>");
            var m = _music.Value;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("01")) m?.PlayCue("MUS_01_SESSION", 0, 1f);
            if (GUILayout.Button("02 LO")) m?.PlayCue("MUS_02_MARKET", 0, 1f);
            if (GUILayout.Button("02 HI")) m?.SetLayer(1, 1f);
            if (GUILayout.Button("03")) m?.PlayCue("MUS_03_LASTLIGHT", 0, 1f);
            if (GUILayout.Button("04")) m?.PlayCue("MUS_04_STATION", 0, 1f);
            if (GUILayout.Button("04 FULL")) m?.SetLayer(1, 1f);
            if (GUILayout.Button("05")) m?.PlayCue("MUS_05_EXFIL", 0, 1f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Greycoat")) m?.Stinger("STG_GREYCOAT");
            if (GUILayout.Button("Stop")) m?.Stop(2f);
            if (GUILayout.Button("Hard cut")) m?.HardCut();
            GUILayout.Label(m != null ? (m.CurrentCue ?? "-") + " L" + m.CurrentLayer : "no music");
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("<b>AMBIENCE</b> s " + _s.ToString("0.00") + "  beat " + _beat);
            _forceAmbience = GUILayout.Toggle(_forceAmbience, "Force context (else ZoneTags + LightStage)");
            _s = GUILayout.HorizontalSlider(_s, 0f, 4f);
            _beat = Mathf.RoundToInt(GUILayout.HorizontalSlider(_beat, 0f, 12f));
            GUILayout.BeginHorizontal();
            for (var i = 0; i < s_zones.Length; i++)
            {
                if (!GUILayout.Toggle(_zone == i, s_zones[i].Substring(0, 4), GUI.skin.button)) continue;
                if (_zone != i && _listener != null && i < _zonePoints.Length && _zonePoints[i] != null) _listener.position = _zonePoints[i].position;
                _zone = i;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Zone (AudioService): " + AudioRuntime.Zone);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
