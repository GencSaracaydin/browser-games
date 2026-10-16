using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// The interrogator's pen on the steel table, the four-tap emitter (GDD §5.1, §5.4 "pen taps in fours, one tap per
    /// digit"): taps are a 7° lift about the pen's back end and a slam (PROC_BOOTH_SMALLS: the pen lies along local X).
    /// <see cref="TapGroup"/> plays the four-tap clip (SFX_PEN_TAP holds four taps, intro shot 6); <see cref="TapOnce"/>
    /// plays one tap per digit (outro shot 5) using the single-tap cue when WP06 provides it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PenTapper : MonoBehaviour
    {
        /// <summary>Single-tap cue (requested from WP06; see Docs/REQUESTS.md).</summary>
        public const string SingleTapCue = "SFX_PEN_TAP_1";
        /// <summary>Spacing of the four taps in SFX_PEN_TAP (seconds).</summary>
        public const float GroupSpacing = 0.42f;
        private const float TapSeconds = 0.13f;
        private const float LiftDeg = 7f;
        private const float HalfLength = 0.0675f;

        private Vector3 _restPos;
        private Quaternion _restRot;
        private readonly float[] _queue = new float[8];
        private int _queued;
        private float _clock;
        private float _tapStart = -1f;
        private ServiceRef<IAudioService> _audio;

        private void Awake()
        {
            _restPos = transform.localPosition;
            _restRot = transform.localRotation;
        }

        /// <summary>Four taps with the group clip (intro shot 6: four taps at 1:04-1:06).</summary>
        public void TapGroup()
        {
            CinematicAudio.Play(CinematicCues.PenTap, transform.position);
            for (var i = 0; i < 4; i++) Queue(i * GroupSpacing, false);
        }

        /// <summary>One tap (outro: one tap per digit of D087).</summary>
        public void TapOnce()
        {
            var a = _audio.Value;
            if (a != null && a.HasCue(SingleTapCue)) a.Play(SingleTapCue, transform.position);
            else LSLog.WarnOnce("pen-single", LogCategory.Cinematics, "No '" + SingleTapCue + "' cue: single pen taps are silent (see REQUESTS.md).", this);
            Queue(0f, true);
        }

        private void Queue(float delay, bool now)
        {
            if (now)
            {
                _tapStart = 0f;
                return;
            }
            if (_queued < _queue.Length) _queue[_queued++] = _clock + delay;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _clock += dt;
            for (var i = 0; i < _queued; i++)
            {
                if (_queue[i] > _clock) continue;
                _tapStart = 0f;
                _queue[i] = _queue[--_queued];
                i--;
            }
            if (_tapStart < 0f) return;
            _tapStart += dt;
            var k = _tapStart / TapSeconds;
            var angle = k < 0.55f ? Mathf.Lerp(0f, LiftDeg, k / 0.55f) : Mathf.Lerp(LiftDeg, 0f, (k - 0.55f) / 0.45f);
            if (k >= 1f)
            {
                angle = 0f;
                _tapStart = -1f;
            }
            ApplyAngle(angle);
        }

        private void ApplyAngle(float angle)
        {
            transform.localPosition = _restPos;
            transform.localRotation = _restRot;
            if (angle <= 0f) return;
            // Lift the tip (+X end) about the back end (−X end), around the pen's horizontal axis.
            var pivot = transform.TransformPoint(new Vector3(-HalfLength, 0f, 0f));
            transform.RotateAround(pivot, transform.forward, angle);
        }
    }
}
