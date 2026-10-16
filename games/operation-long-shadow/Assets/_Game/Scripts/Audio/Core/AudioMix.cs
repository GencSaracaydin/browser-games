using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// The whole mix without an AudioMixer (TECH_PLAN D9): per-category gain = master × user option × script gain ×
    /// duck, applied by every owner of an AudioSource to <c>AudioSource.volume</c>. Ducks come from timed requests
    /// (<see cref="Duck"/>, IAudioService.Duck) and sustained holds keyed by an owner (VO lines, combat state, the
    /// numbers broadcast); overlapping ducks on one category do not add up, the deepest wins. The applied duck moves
    /// towards its target with a fast attack and a slower release so nothing pumps. Pure C# (no Unity objects), so the
    /// rules are unit-tested; time is passed in.
    /// </summary>
    public sealed class AudioMix
    {
        private const int MaxTimed = 24;
        private const int MaxHolds = 48;

        private struct Timed
        {
            public MixCategory Cat;
            public float Db;
            public float Until;
        }

        private struct Hold
        {
            public int Key;
            public MixCategory Cat;
            public float Db;
        }

        private readonly float[] _script = new float[MixCategories.Count];
        private readonly float[] _duck = new float[MixCategories.Count];
        private readonly float[] _target = new float[MixCategories.Count];
        private readonly Timed[] _timed = new Timed[MaxTimed];
        private readonly Hold[] _holds = new Hold[MaxHolds];
        private int _timedCount;
        private int _holdCount;
        private float _masterScript = 1f;
        private float _masterUser = 1f;
        private float _musicUser = 1f;
        private float _effectsUser = 1f;
        private float _voiceUser = 1f;

        /// <summary>Seconds for a duck to reach ~63 % of a deeper target.</summary>
        public float AttackSeconds { get; set; } = 0.06f;
        /// <summary>Seconds for a duck to recover ~63 % towards a shallower target.</summary>
        public float ReleaseSeconds { get; set; } = 0.45f;

        /// <summary>Creates a neutral mix (all gains 1, no ducks).</summary>
        public AudioMix()
        {
            Reset();
        }

        /// <summary>Back to neutral.</summary>
        public void Reset()
        {
            for (var i = 0; i < MixCategories.Count; i++)
            {
                _script[i] = 1f;
                _duck[i] = 0f;
                _target[i] = 0f;
            }
            _timedCount = 0;
            _holdCount = 0;
            _masterScript = 1f;
            _masterUser = _musicUser = _effectsUser = _voiceUser = 1f;
        }

        /// <summary>
        /// Perceptual taper of an options slider (0..1, GDD §11 Options): gain = v², so 50 % is −12 dB, which reads as
        /// "half as loud" (−10 dB) better than a linear −6 dB.
        /// </summary>
        public static float Taper(float slider)
        {
            if (float.IsNaN(slider)) return 1f;
            var v = Mathf.Clamp01(slider);
            return v * v;
        }

        /// <summary>Applies the four option sliders (linear 0..1 as stored in <see cref="GameSettings"/>).</summary>
        public void SetUserVolumes(float master, float music, float effects, float voice)
        {
            _masterUser = Taper(master);
            _musicUser = Taper(music);
            _effectsUser = Taper(effects);
            _voiceUser = Taper(voice);
        }

        /// <summary>Applies the options of a settings object (null leaves everything at 1).</summary>
        public void SetUserVolumes(GameSettings s)
        {
            if (s == null)
            {
                SetUserVolumes(1f, 1f, 1f, 1f);
                return;
            }
            SetUserVolumes(s.MasterVolume, s.MusicVolume, s.EffectsVolume, s.VoiceVolume);
        }

        /// <summary>Script gain of a category name (IAudioService.SetCategoryGain); "Master" scales everything.</summary>
        public void SetCategoryGain(string category, float linear)
        {
            var v = float.IsNaN(linear) ? 1f : Mathf.Max(0f, linear);
            if (category == AudioCategories.Master)
            {
                _masterScript = v;
                return;
            }
            _script[(int)MixCategories.Parse(category)] = v;
        }

        /// <summary>Script gain of a category.</summary>
        public void SetScriptGain(MixCategory c, float linear) => _script[(int)c] = float.IsNaN(linear) ? 1f : Mathf.Max(0f, linear);

        /// <summary>Script gain of a category.</summary>
        public float ScriptGain(MixCategory c) => _script[(int)c];

        /// <summary>
        /// Ducks a category by <paramref name="db"/> (a positive value is taken as a cut) for <paramref name="seconds"/>
        /// from <paramref name="now"/>, then releases. A new request for the same category replaces a shorter or
        /// shallower one only by being the deeper/longer of the two (the deepest active duck wins).
        /// </summary>
        public void Duck(MixCategory c, float db, float seconds, float now)
        {
            if (float.IsNaN(db) || float.IsNaN(seconds) || seconds <= 0f) return;
            var cut = -Mathf.Abs(db);
            if (cut >= -0.01f) return;
            var until = now + seconds;
            // Merge into an existing request of the same category and depth.
            for (var i = 0; i < _timedCount; i++)
            {
                if (_timed[i].Cat == c && Mathf.Abs(_timed[i].Db - cut) < 0.05f)
                {
                    if (until > _timed[i].Until) _timed[i].Until = until;
                    return;
                }
            }
            if (_timedCount >= MaxTimed)
            {
                // Replace the one that ends first.
                var idx = 0;
                for (var i = 1; i < _timedCount; i++)
                    if (_timed[i].Until < _timed[idx].Until) idx = i;
                _timed[idx] = new Timed { Cat = c, Db = cut, Until = until };
                return;
            }
            _timed[_timedCount++] = new Timed { Cat = c, Db = cut, Until = until };
        }

        /// <summary>Starts or changes a sustained duck owned by <paramref name="key"/> on one category.</summary>
        public void SetHold(int key, MixCategory c, float db)
        {
            var cut = -Mathf.Abs(float.IsNaN(db) ? 0f : db);
            for (var i = 0; i < _holdCount; i++)
            {
                if (_holds[i].Key == key && _holds[i].Cat == c)
                {
                    _holds[i].Db = cut;
                    return;
                }
            }
            if (_holdCount >= MaxHolds)
            {
                LSLog.WarnOnce("audio-holds-full", LogCategory.Audio, "AudioMix: too many duck holds; ignoring one.");
                return;
            }
            _holds[_holdCount++] = new Hold { Key = key, Cat = c, Db = cut };
        }

        /// <summary>Ends the sustained duck of <paramref name="key"/> on one category.</summary>
        public void ClearHold(int key, MixCategory c)
        {
            for (var i = _holdCount - 1; i >= 0; i--)
            {
                if (_holds[i].Key != key || _holds[i].Cat != c) continue;
                _holds[i] = _holds[--_holdCount];
            }
        }

        /// <summary>Ends every sustained duck of <paramref name="key"/>.</summary>
        public void ClearHolds(int key)
        {
            for (var i = _holdCount - 1; i >= 0; i--)
                if (_holds[i].Key == key) _holds[i] = _holds[--_holdCount];
        }

        /// <summary>Number of active holds (tests, debug).</summary>
        public int HoldCount => _holdCount;

        /// <summary>
        /// Advances the duck smoothing to <paramref name="now"/> (seconds, any monotonic clock) by <paramref name="dt"/>.
        /// Expired timed ducks are dropped.
        /// </summary>
        public void Tick(float now, float dt)
        {
            for (var i = 0; i < MixCategories.Count; i++) _target[i] = 0f;
            for (var i = _timedCount - 1; i >= 0; i--)
            {
                if (now >= _timed[i].Until)
                {
                    _timed[i] = _timed[--_timedCount];
                    continue;
                }
                var c = (int)_timed[i].Cat;
                if (_timed[i].Db < _target[c]) _target[c] = _timed[i].Db;
            }
            for (var i = 0; i < _holdCount; i++)
            {
                var c = (int)_holds[i].Cat;
                if (_holds[i].Db < _target[c]) _target[c] = _holds[i].Db;
            }
            if (dt <= 0f) return;
            for (var i = 0; i < MixCategories.Count; i++)
            {
                var cur = _duck[i];
                var tgt = _target[i];
                if (Mathf.Abs(cur - tgt) < 0.01f)
                {
                    _duck[i] = tgt;
                    continue;
                }
                var tau = tgt < cur ? AttackSeconds : ReleaseSeconds;
                _duck[i] = tau <= 0f ? tgt : Mathf.Lerp(cur, tgt, 1f - Mathf.Exp(-dt / tau));
            }
        }

        /// <summary>Current applied duck of a category in dB (≤ 0).</summary>
        public float DuckDb(MixCategory c) => _duck[(int)c];

        /// <summary>Target duck of a category in dB after the last <see cref="Tick"/> (≤ 0).</summary>
        public float TargetDb(MixCategory c) => _target[(int)c];

        /// <summary>User option gain of a category (already tapered).</summary>
        public float UserGain(MixCategory c)
        {
            switch (c)
            {
                case MixCategory.Music: return _musicUser;
                case MixCategory.Voice: return _voiceUser;
                case MixCategory.Insert: return 1f;
                default: return _effectsUser; // Effects, Ambience, UI, Tinnitus
            }
        }

        /// <summary>Final linear gain of a category: master × user × script × duck.</summary>
        public float Gain(MixCategory c)
        {
            var i = (int)c;
            return _masterUser * _masterScript * UserGain(c) * _script[i] * MathX.DbToLinear(_duck[i]);
        }

        /// <summary>
        /// Gain of a source that plays during a glitch insert: its own category × the Insert category's script gain and
        /// duck (WP09 fades the insert bus with SetCategoryGain("Insert")).
        /// </summary>
        public float GainDuringInsert(MixCategory c)
        {
            if (c == MixCategory.Insert) return Gain(c);
            var ins = (int)MixCategory.Insert;
            return Gain(c) * _script[ins] * MathX.DbToLinear(_duck[ins]);
        }
    }
}
