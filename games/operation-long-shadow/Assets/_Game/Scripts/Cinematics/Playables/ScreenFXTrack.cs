using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.FX;
using LongShadow.Cinematics.Sets;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>A screen FX channel (no binding; drives ScreenFXService's cinematic channels).</summary>
    [TrackColor(0.8f, 0.4f, 0.9f)]
    [TrackClipType(typeof(ScreenFXClip))]
    public sealed class ScreenFXTrack : TrackAsset
    {
        [SerializeField] private FxChannel _channel;

        /// <summary>The channel this track drives.</summary>
        public FxChannel Channel { get => _channel; set => _channel = value; }

        /// <inheritdoc/>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var p = ScriptPlayable<ScreenFXMixer>.Create(graph, inputCount);
            p.GetBehaviour().Channel = _channel;
            return p;
        }
    }

    /// <summary>Evaluates the active ramp of one channel.</summary>
    internal sealed class ScreenFXMixer : PlayableBehaviour
    {
        public FxChannel Channel;
        private int _last = -1;
        private string _feedHeld;

        public override void OnGraphStart(Playable playable) => _last = -1;

        public override void OnGraphStop(Playable playable)
        {
            ReleaseFeed();
            _last = -1;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying) return;
            var fx = CinematicRuntime.ScreenFx;
            if (fx == null) return;
            var i = PlayableUtil.ActiveInput(playable);
            if (i < 0)
            {
                if (_last >= 0) Write(fx, null, 0f, false);
                _last = -1;
                return;
            }
            var input = (ScriptPlayable<ScreenFXBehaviour>)playable.GetInput(i);
            var clip = input.GetBehaviour()?.Clip;
            if (clip == null) return;
            var entered = i != _last;
            _last = i;
            Write(fx, clip, clip.ValueAt(PlayableUtil.Normalized(input)), entered);
        }

        private void Write(ScreenFXService fx, ScreenFXClip clip, float v, bool entered)
        {
            switch (Channel)
            {
                case FxChannel.Fade:
                    fx.CinematicFade(clip != null ? clip.Color : Color.black, v);
                    break;
                case FxChannel.Exposure:
                    fx.CinematicExposure(v);
                    break;
                case FxChannel.Desaturate:
                    fx.CinematicDesaturate(v);
                    break;
                case FxChannel.Glitch:
                    fx.CinematicGlitch(v, entered && clip != null && clip.WhiteFrame ? 1f : 0f);
                    break;
                case FxChannel.Burn:
                    fx.CinematicBurn(v);
                    ProjectionRig.Instance?.SetBurn(v);
                    break;
                case FxChannel.Vignette:
                    fx.CinematicVignette(v);
                    break;
                case FxChannel.BoothSaturation:
                    var look = BoothRoot.Current != null ? BoothRoot.Current.Look : null;
                    look?.SetSaturationOffset(v);
                    break;
                case FxChannel.Feed:
                    if (clip == null)
                    {
                        fx.CinematicFeed(null, 0f);
                        ReleaseFeed();
                        break;
                    }
                    if (entered)
                    {
                        ReleaseFeed();
                        _feedHeld = clip.FeedId;
                    }
                    var tex = !string.IsNullOrEmpty(_feedHeld) ? (entered ? FeedCameras.Acquire(_feedHeld) : CurrentFeed()) : null;
                    if (entered && tex == null) _feedHeld = null;
                    fx.CinematicFeed(tex, v);
                    break;
            }
        }

        private Texture CurrentFeed()
        {
            var f = FeedCameras.Get(_feedHeld);
            return f != null && f.Camera != null ? f.Camera.targetTexture : null;
        }

        private void ReleaseFeed()
        {
            if (string.IsNullOrEmpty(_feedHeld)) return;
            FeedCameras.Release(_feedHeld);
            _feedHeld = null;
        }
    }
}
