using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Ambience
{
    /// <summary>
    /// G5 "civilians heard, never seen" (GDD §0.3, §7.3; TECH_PLAN §7.9 CivilianEmitter): a muted TV, a radio, a kettle,
    /// a shutter or a pigeon loft behind a wall of the market and square houses. It plays while its beat window is open
    /// and the listener is near, and <b>latches</b> when shooting starts within 40 m (a shot, an explosion within 80 m):
    /// after a human reaction delay the radio is turned down, the TV switched off, the kettle taken off, the shutter
    /// pulled shut, the pigeons flush. The town stays quiet afterwards; a checkpoint reload resets it.
    /// Place it on a wall or window (WP10 level builders, or <b>LongShadow/Audio/Add Civilian Emitter</b>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CivilianEmitter : MonoBehaviour
    {
        [SerializeField] private CivilianSound _kind = CivilianSound.MutedTv;
        [SerializeField, Tooltip("Loop while alive (Custom only; the other kinds use their defaults).")] private string _loopCue;
        [SerializeField, Tooltip("One-shot when it latches (Custom only).")] private string _latchCue;
        [SerializeField, Tooltip("Occasional one-shot while alive (Custom only).")] private string _idleCue;
        [SerializeField, Tooltip("Seconds between idle one-shots (min, max).")] private Vector2 _idleInterval = new Vector2(45f, 110f);
        [SerializeField, Tooltip("GDD §0.3 G5: Beats 1–4.")] private int _beatFrom = 1;
        [SerializeField] private int _beatTo = 4;
        [SerializeField, Tooltip("Shots closer than this latch it (GDD G5, TECH_PLAN §7.9: 40 m).")]
        private float _latchDistance = AudioConstants.CivilianLatchDistance;
        [SerializeField, Tooltip("The loop starts when the listener is this close (and stops beyond 1.2×).")]
        private float _audibleDistance = 30f;
        [SerializeField, Tooltip("Human reaction before the latch sound (min, max seconds).")]
        private Vector2 _reaction = new Vector2(0.4f, 1.6f);
        [SerializeField] private float _loopFadeOut = 1.2f;

        private ServiceRef<IAudioService> _audio;
        private ServiceRef<IMissionDirector> _mission;
        private int _loop;
        private int _beat = -1;
        private bool _latched;
        private float _latchAt = -1f;
        private float _nextIdle = -1f;
        private float _nextCheck;

        /// <summary>True after shooting started nearby.</summary>
        public bool Latched => _latched;
        /// <summary>The kind of emitter.</summary>
        public CivilianSound Kind => _kind;

        /// <summary>Configures the emitter (builders, menu).</summary>
        public void Configure(CivilianSound kind, int beatFrom, int beatTo)
        {
            _kind = kind;
            _beatFrom = beatFrom;
            _beatTo = beatTo;
        }

        /// <summary>Default cues of a kind: loop, latch one-shot, idle one-shot (null where none).</summary>
        public static void CuesOf(CivilianSound kind, out string loop, out string latch, out string idle)
        {
            loop = latch = idle = null;
            switch (kind)
            {
                case CivilianSound.MutedTv: loop = "SFX_TV_MUFFLED"; break;
                case CivilianSound.Radio: loop = "AMB_CASSETTE_TUNE"; latch = "SFX_RADIO_TURNED_DOWN"; break;
                case CivilianSound.Kettle: idle = "SFX_KETTLE"; break;
                case CivilianSound.Shutter: latch = "SFX_SHUTTER_LATCH"; break;
                case CivilianSound.Pigeons: latch = "SFX_PIGEONS"; break;
            }
        }

        private void OnEnable()
        {
            GameEvents.ShotFired += OnShot;
            GameEvents.Explosion += OnExplosion;
            GameEvents.BeatStarted += OnBeat;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.ShotFired -= OnShot;
            GameEvents.Explosion -= OnExplosion;
            GameEvents.BeatStarted -= OnBeat;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            StopLoop(0.1f);
        }

        private void Update()
        {
            var now = AudioRuntime.Now;
            if (_latchAt >= 0f && now >= _latchAt)
            {
                _latchAt = -1f;
                DoLatch();
            }
            if (now < _nextCheck) return;
            _nextCheck = now + 0.25f;
            Cues(out var loopCue, out _, out var idleCue);
            var audio = _audio.Value;
            if (audio == null) return;
            var dist = Vector3.Distance(transform.position, AudioRuntime.ListenerPosition);
            var live = !_latched && InBeat();
            if (!string.IsNullOrEmpty(loopCue))
            {
                if (live && _loop == 0 && dist < _audibleDistance) _loop = audio.PlayLoop(loopCue, transform);
                else if (_loop != 0 && (!live || dist > _audibleDistance * 1.2f)) StopLoop(_latched ? _loopFadeOut : 0.5f);
            }
            if (!string.IsNullOrEmpty(idleCue) && live && dist < _audibleDistance)
            {
                if (_nextIdle < 0f) _nextIdle = now + Random.Range(_idleInterval.x * 0.3f, _idleInterval.y * 0.5f);
                else if (now >= _nextIdle)
                {
                    audio.Play(idleCue, transform.position);
                    _nextIdle = now + Random.Range(_idleInterval.x, _idleInterval.y);
                }
            }
        }

        /// <summary>Latches as if shooting started nearby (scripts, tests).</summary>
        public void Latch()
        {
            if (_latched || _latchAt >= 0f) return;
            _latchAt = AudioRuntime.Now + Random.Range(_reaction.x, _reaction.y);
        }

        /// <summary>Back to the unlatched state (checkpoint reloads).</summary>
        public void ResetState()
        {
            _latched = false;
            _latchAt = -1f;
            _nextIdle = -1f;
        }

        private void DoLatch()
        {
            if (_latched) return;
            _latched = true;
            Cues(out _, out var latchCue, out _);
            var audio = _audio.Value;
            if (audio != null && !string.IsNullOrEmpty(latchCue) && InBeat()) audio.Play(latchCue, transform.position);
            StopLoop(_loopFadeOut);
        }

        private void OnShot(ShotEvent s)
        {
            if (_latched || !InBeat()) return;
            if ((s.Origin - transform.position).sqrMagnitude <= _latchDistance * _latchDistance) Latch();
        }

        private void OnExplosion(Vector3 position, float radius, Team team)
        {
            if (_latched || !InBeat()) return;
            var r = _latchDistance * 2f;
            if ((position - transform.position).sqrMagnitude <= r * r) Latch();
        }

        private void OnBeat(int beat) => _beat = beat;

        private void OnCheckpointLoaded(string cpId)
        {
            ResetState();
            StopLoop(0f);
        }

        private bool InBeat()
        {
            var beat = _beat;
            var m = _mission.Value;
            if (m != null && m.IsRunning) beat = m.Beat;
            if (beat < 0) return true;
            return (_beatFrom < 0 || beat >= _beatFrom) && (_beatTo < 0 || beat <= _beatTo);
        }

        private void Cues(out string loop, out string latch, out string idle)
        {
            if (_kind == CivilianSound.Custom)
            {
                loop = _loopCue;
                latch = _latchCue;
                idle = _idleCue;
                return;
            }
            CuesOf(_kind, out loop, out latch, out idle);
        }

        private void StopLoop(float fade)
        {
            if (_loop == 0) return;
            _audio.Value?.StopLoop(_loop, fade);
            _loop = 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _audibleDistance);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _latchDistance);
        }
#endif
    }
}
