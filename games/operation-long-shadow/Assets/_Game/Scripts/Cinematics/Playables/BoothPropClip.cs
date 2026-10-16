using LongShadow.Cinematics.Booth;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// Booth set-dressing actions at the clip start (bound to "@Booth"): reel deck, pen taps (four, or one per digit),
    /// phone lift, scrambler toggle (G1 payoff), lighter, clock ticking, smoke, clock flashes on a CRT (intro shot 5:
    /// "one stopped-clock frame flashes on a random monitor 3 times"), Calder's fists (outro shots 2 and 5, through
    /// IFirstPersonBody), the silent lips keyed to the digits (MID-B shot 6, WP05 SetJawOpen), cuff strain.
    /// </summary>
    [System.Serializable]
    public sealed class BoothPropClip : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>Booth actions.</summary>
        public enum Action
        {
            ReelPlay,
            ReelStop,
            PenTapGroup,
            PenTapOnce,
            PhoneLift,
            PhoneHang,
            ScramblerOff,
            ScramblerOn,
            LighterStrike,
            LighterClose,
            ClockTicking,
            ClockSilent,
            SmokeOn,
            SmokeOff,
            CrtFlashClock,
            FistsClench,
            FistsRelax,
            JawDigits,
            CuffStrain,
            InterrogatorVisible,
            InterrogatorHidden,
            FilmZoomIn,
            FilmZoomReset,
        }

        [SerializeField] private Action _action;
        [SerializeField, Tooltip("CRT number for CrtFlashClock; target vertical FOV for FilmZoomIn.")] private int _crt = 1;
        [SerializeField, Tooltip("Seconds for CrtFlashClock.")] private float _seconds = 0.034f;
        [SerializeField, Tooltip("Digit times (s from the clip start) for JawDigits.")] private float[] _times = new float[0];

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Action.</summary>
        public Action Kind { get => _action; set => _action = value; }
        /// <summary>CRT number (1..6).</summary>
        public int Crt { get => _crt; set => _crt = value; }
        /// <summary>Seconds argument.</summary>
        public float Seconds { get => _seconds; set => _seconds = value; }
        /// <summary>Digit times for the lips.</summary>
        public float[] Times { get => _times; set => _times = value ?? new float[0]; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<BoothPropBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        /// <summary>Runs the action on the booth.</summary>
        public void Run(BoothRoot booth)
        {
            if (booth == null) return;
            switch (_action)
            {
                case Action.ReelPlay: booth.Reel?.SetPlaying(true); break;
                case Action.ReelStop: booth.Reel?.SetPlaying(false); break;
                case Action.PenTapGroup: booth.Pen?.TapGroup(); break;
                case Action.PenTapOnce: booth.Pen?.TapOnce(); break;
                case Action.PhoneLift: booth.Phone?.Lift(BoothState.FindHand(booth.InterrogatorBody)); break;
                case Action.PhoneHang: booth.Phone?.Hang(); break;
                case Action.ScramblerOff: booth.Scrambler?.SetPowered(false); break;
                case Action.ScramblerOn: booth.Scrambler?.SetPowered(true); break;
                case Action.LighterStrike: booth.Lighter?.Strike(); break;
                case Action.LighterClose: booth.Lighter?.Close(); break;
                case Action.ClockTicking: booth.Clock?.SetTwitch(true, true); break;
                case Action.ClockSilent: booth.Clock?.SetTwitch(true, false); break;
                case Action.SmokeOn:
                    foreach (var s in booth.Smoke) s?.SetEmitting(true);
                    break;
                case Action.SmokeOff:
                    foreach (var s in booth.Smoke) s?.SetEmitting(false);
                    break;
                case Action.CrtFlashClock: booth.Crt(_crt)?.FlashFeed(BindingIds.FeedClock, Mathf.Max(0.034f, _seconds)); break;
                case Action.FistsClench: Fists(booth, true); break;
                case Action.FistsRelax: Fists(booth, false); break;
                case Action.CuffStrain:
                    CinematicAudio.Play(CinematicCues.LeatherCreak, booth.ToWorld(new Vector3(0f, 0.65f, 0.15f)));
                    CinematicRuntime.Cameras?.Impulse(0.35f);
                    break;
                case Action.InterrogatorVisible:
                    if (booth.InterrogatorBody != null) booth.InterrogatorBody.SetActive(true);
                    break;
                case Action.InterrogatorHidden:
                    if (booth.InterrogatorBody != null) booth.InterrogatorBody.SetActive(false);
                    break;
                case Action.FilmZoomIn:
                    FeedCameras.Get(BindingIds.FeedFilm)?.ZoomTo(_crt > 0 ? _crt : 16f, Mathf.Max(0.2f, _seconds), true);
                    break;
                case Action.FilmZoomReset:
                    FeedCameras.Get(BindingIds.FeedFilm)?.ZoomTo(-1f, 0.01f, false);
                    break;
            }
        }

        private static void Fists(BoothRoot booth, bool clenched)
        {
            var fp = GameServices.Get<IFirstPersonBody>();
            if (fp != null) fp.SetFists(clenched);
            if (clenched) CinematicAudio.Play(CinematicCues.LeatherCreak, booth.ToWorld(new Vector3(0f, 0.65f, 0.15f)));
        }

        /// <summary>Jaw opening 0..1 at clip time <paramref name="t"/> for <see cref="Action.JawDigits"/>.</summary>
        public float JawAt(float t)
        {
            var open = 0f;
            foreach (var d in _times)
            {
                var x = t - d;
                if (x < 0f || x > 0.32f) continue;
                open = Mathf.Max(open, Mathf.Sin(x / 0.32f * Mathf.PI) * 0.55f);
            }
            return open;
        }
    }

    /// <summary>Runs a booth action on entry; animates the lips for JawDigits.</summary>
    internal sealed class BoothPropBehaviour : PlayableBehaviour
    {
        public BoothPropClip Clip;
        private float _lastJaw = -1f;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Clip == null || !PlayableUtil.IsLive(info)) return;
            Clip.Run(BoothRoot.Current);
            _lastJaw = -1f;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (Clip == null || Clip.Kind != BoothPropClip.Action.JawDigits || !Application.isPlaying) return;
            var calder = BoothRoot.Current != null ? BoothRoot.Current.CalderBody : null;
            if (calder == null) return;
            var jaw = Clip.JawAt((float)playable.GetTime());
            if (Mathf.Abs(jaw - _lastJaw) < 0.02f) return;
            _lastJaw = jaw;
            calder.SendMessage("SetJawOpen", jaw, SendMessageOptions.DontRequireReceiver);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (Clip == null || Clip.Kind != BoothPropClip.Action.JawDigits || _lastJaw < 0f) return;
            var calder = BoothRoot.Current != null ? BoothRoot.Current.CalderBody : null;
            if (calder != null) calder.SendMessage("SetJawOpen", -1f, SendMessageOptions.DontRequireReceiver);
            _lastJaw = -1f;
        }
    }
}
