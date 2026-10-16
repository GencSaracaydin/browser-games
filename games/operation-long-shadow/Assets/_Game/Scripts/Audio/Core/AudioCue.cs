using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// One entry of <c>LIB_Audio</c> (TECH_PLAN §7.9): a cue id (the ASSET_MANIFEST id, or the sliced sub-id such as
    /// <c>SFX_WPN_AK_RELOAD_MAGOUT</c>) with its variant clips, baked distance variants (<c>_FAR</c>), volume and pitch
    /// ranges, 3D settings, priority, category and loop data. Built by <c>AudioLibraryBuilder</c> from the file names
    /// under <c>Assets/_Game/Audio/</c> (TECH_PLAN §4.2) and <c>Audio/Music/LOOP_POINTS.json</c>; never hand-edited.
    /// </summary>
    [Serializable]
    public sealed class AudioCue : ILibraryEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private string _category = AudioCategories.Effects;
        [SerializeField] private AudioClip[] _clips = new AudioClip[0];
        [SerializeField, Tooltip("Loop region per clip in seconds (x start, y end); y <= 0 = whole clip.")]
        private Vector2[] _loopRegions = new Vector2[0];
        [SerializeField, Tooltip("Baked distance variants (air absorption + outdoor echo, D9).")]
        private AudioClip[] _farClips = new AudioClip[0];
        [SerializeField] private float _volume = 1f;
        [SerializeField, Tooltip("± random volume in dB per play.")] private float _volumeJitterDb;
        [SerializeField, Tooltip("± random pitch as a fraction (0.03 = ±3 %).")] private float _pitchJitter;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f;
        [SerializeField] private float _minDistance = 1f;
        [SerializeField] private float _maxDistance = 50f;
        [SerializeField] private AudioRolloffMode _rolloff = AudioRolloffMode.Logarithmic;
        [SerializeField, Range(0f, 360f)] private float _spread;
        [SerializeField, Range(0, 100), Tooltip("Importance for voice stealing (100 = never stolen by lower).")]
        private int _priority = 50;
        [SerializeField] private bool _loop;
        [SerializeField, Tooltip("Near layer fades out / far variant fades in between these distances (m).")]
        private Vector2 _farCrossfade = new Vector2(AudioConstants.WeaponNearDistance, AudioConstants.WeaponFarDistance);
        [SerializeField] private float _farMinDistance = 30f;
        [SerializeField] private float _farMaxDistance = 1500f;
        [SerializeField, Tooltip("Delay 3D plays by distance / speed of sound.")] private bool _speedOfSound;
        [SerializeField, Tooltip("Max simultaneous instances (0 = unlimited).")] private int _maxInstances;
        [SerializeField, Tooltip("Minimum seconds between two starts of this cue (0 = none).")] private float _minInterval;
        [SerializeField, Tooltip("Music grid: beats per minute (0 = none).")] private float _bpm;
        [SerializeField, Tooltip("Music grid: seconds per bar (0 = none).")] private float _barSeconds;
        [SerializeField] private int _loopBars;
        [SerializeField, Tooltip("Music: the clip has an intro before its loop region.")] private bool _hasIntro;

        /// <inheritdoc />
        public string Id => _id;
        /// <summary>Mix category (<see cref="AudioCategories"/>).</summary>
        public string Category => _category;
        /// <summary>Variant clips (one is picked per play, never the last two in a row when there are ≥ 4).</summary>
        public AudioClip[] Clips => _clips;
        /// <summary>Distance variants.</summary>
        public AudioClip[] FarClips => _farClips;
        /// <summary>Linear volume.</summary>
        public float Volume => _volume;
        /// <summary>± dB per play.</summary>
        public float VolumeJitterDb => _volumeJitterDb;
        /// <summary>± pitch fraction per play.</summary>
        public float PitchJitter => _pitchJitter;
        /// <summary>0 = 2D, 1 = 3D.</summary>
        public float SpatialBlend => _spatialBlend;
        /// <summary>Rolloff min distance.</summary>
        public float MinDistance => _minDistance;
        /// <summary>Rolloff max distance.</summary>
        public float MaxDistance => _maxDistance;
        /// <summary>Rolloff mode.</summary>
        public AudioRolloffMode Rolloff => _rolloff;
        /// <summary>3D spread in degrees.</summary>
        public float Spread => _spread;
        /// <summary>Stealing importance 0..100.</summary>
        public int Priority => _priority;
        /// <summary>Loops (beds, set-piece loops).</summary>
        public bool Loop => _loop;
        /// <summary>Near/far crossfade start and end distances.</summary>
        public Vector2 FarCrossfade => _farCrossfade;
        /// <summary>Rolloff min distance of the far variant.</summary>
        public float FarMinDistance => _farMinDistance;
        /// <summary>Rolloff max distance of the far variant.</summary>
        public float FarMaxDistance => _farMaxDistance;
        /// <summary>Apply the speed-of-sound delay to 3D plays.</summary>
        public bool SpeedOfSound => _speedOfSound;
        /// <summary>Max simultaneous instances (0 = unlimited).</summary>
        public int MaxInstances => _maxInstances;
        /// <summary>Minimum seconds between starts.</summary>
        public float MinInterval => _minInterval;
        /// <summary>Music grid BPM (0 = none).</summary>
        public float Bpm => _bpm;
        /// <summary>Music grid bar length in seconds (0 = none).</summary>
        public float BarSeconds => _barSeconds;
        /// <summary>Bars in the loop region (music).</summary>
        public int LoopBars => _loopBars;
        /// <summary>True when the clip plays an intro before looping (MUS_01_SESSION).</summary>
        public bool HasIntro => _hasIntro;
        /// <summary>True if at least one playable clip exists.</summary>
        public bool HasClips
        {
            get
            {
                if (_clips == null) return false;
                for (var i = 0; i < _clips.Length; i++)
                    if (_clips[i] != null) return true;
                return false;
            }
        }
        /// <summary>True if baked distance variants exist.</summary>
        public bool HasFar
        {
            get
            {
                if (_farClips == null) return false;
                for (var i = 0; i < _farClips.Length; i++)
                    if (_farClips[i] != null) return true;
                return false;
            }
        }

        /// <summary>Loop region of clip <paramref name="index"/> in seconds (y &lt;= 0 means the whole clip).</summary>
        public Vector2 LoopRegion(int index) =>
            _loopRegions != null && index >= 0 && index < _loopRegions.Length ? _loopRegions[index] : Vector2.zero;

        /// <summary>Serialization constructor.</summary>
        public AudioCue()
        {
        }

        /// <summary>Creates a cue (builders and tests).</summary>
        public AudioCue(string id, string category, AudioClip[] clips)
        {
            _id = id;
            _category = string.IsNullOrEmpty(category) ? AudioCategories.Effects : category;
            _clips = clips ?? new AudioClip[0];
        }

        /// <summary>Sets the 3D settings (builders and tests). Returns this.</summary>
        public AudioCue With3D(float spatialBlend, float minDistance, float maxDistance, AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic, float spread = 0f)
        {
            _spatialBlend = Mathf.Clamp01(spatialBlend);
            _minDistance = Mathf.Max(0.01f, minDistance);
            _maxDistance = Mathf.Max(_minDistance + 0.01f, maxDistance);
            _rolloff = rolloff;
            _spread = Mathf.Clamp(spread, 0f, 360f);
            return this;
        }

        /// <summary>Sets volume and randomisation (builders and tests). Returns this.</summary>
        public AudioCue WithLevels(float volume, float volumeJitterDb, float pitchJitter, int priority)
        {
            _volume = Mathf.Max(0f, volume);
            _volumeJitterDb = Mathf.Max(0f, volumeJitterDb);
            _pitchJitter = Mathf.Clamp(pitchJitter, 0f, 0.5f);
            _priority = Mathf.Clamp(priority, 0, 100);
            return this;
        }

        /// <summary>Sets the far variants (builders and tests). Returns this.</summary>
        public AudioCue WithFar(AudioClip[] farClips, Vector2 crossfade, float farMin, float farMax)
        {
            _farClips = farClips ?? new AudioClip[0];
            _farCrossfade = crossfade;
            _farMinDistance = Mathf.Max(0.01f, farMin);
            _farMaxDistance = Mathf.Max(_farMinDistance + 0.01f, farMax);
            return this;
        }

        /// <summary>Sets loop data (builders and tests). Returns this.</summary>
        public AudioCue WithLoop(bool loop, Vector2[] regions)
        {
            _loop = loop;
            _loopRegions = regions ?? new Vector2[0];
            return this;
        }

        /// <summary>Sets playback limits (builders and tests). Returns this.</summary>
        public AudioCue WithLimits(bool speedOfSound, int maxInstances, float minInterval)
        {
            _speedOfSound = speedOfSound;
            _maxInstances = Mathf.Max(0, maxInstances);
            _minInterval = Mathf.Max(0f, minInterval);
            return this;
        }

        /// <summary>Sets the music grid (builders and tests). Returns this.</summary>
        public AudioCue WithMusicGrid(float bpm, float barSeconds, int loopBars, bool hasIntro)
        {
            _bpm = Mathf.Max(0f, bpm);
            _barSeconds = Mathf.Max(0f, barSeconds);
            _loopBars = Mathf.Max(0, loopBars);
            _hasIntro = hasIntro;
            return this;
        }
    }
}
