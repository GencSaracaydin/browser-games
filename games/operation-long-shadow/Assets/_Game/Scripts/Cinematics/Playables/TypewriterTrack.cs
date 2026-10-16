using System.Text;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Typed text on the cinematic overlay (no binding).</summary>
    [TrackColor(0.95f, 0.75f, 0.35f)]
    [TrackClipType(typeof(TypewriterClip))]
    public sealed class TypewriterTrack : TrackAsset
    {
        /// <inheritdoc/>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            ScriptPlayable<TypewriterMixer>.Create(graph, inputCount);
    }

    /// <summary>Reveals the active clip's text and clicks keys.</summary>
    internal sealed class TypewriterMixer : PlayableBehaviour
    {
        private readonly StringBuilder _sb = new StringBuilder(256);
        private int _last = -1;
        private int _typed;
        private TypewriterClip.Slot _slot;

        public override void OnGraphStart(Playable playable) => _last = -1;

        public override void OnGraphStop(Playable playable)
        {
            if (_last >= 0) Clear();
            _last = -1;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying) return;
            var overlay = CinematicRuntime.Overlay;
            if (overlay == null) return;
            var i = PlayableUtil.ActiveInput(playable);
            if (i < 0)
            {
                if (_last >= 0) Clear();
                _last = -1;
                return;
            }
            var input = (ScriptPlayable<TypewriterBehaviour>)playable.GetInput(i);
            var b = input.GetBehaviour();
            var clip = b?.Clip;
            if (clip == null) return;
            if (clip.OnlyWithoutHud && CinematicRuntime.Service != null && CinematicRuntime.Service.HasHud) return;
            if (i != _last)
            {
                if (_last >= 0) Clear();
                _last = i;
                _typed = 0;
                _slot = clip.Position;
                if (b.Model == null) b.Model = clip.CreateModel();
            }
            var t = (float)input.GetTime();
            b.Model.Build(_sb, t);
            switch (_slot)
            {
                case TypewriterClip.Slot.Lower: overlay.SetLowerText(_sb, clip.Color); break;
                case TypewriterClip.Slot.Title: overlay.SetTitleText(_sb, clip.Color); break;
                default: overlay.SetCenterText(_sb, clip.Color); break;
            }
            var n = b.Model.TypedCount(t);
            if (clip.ClickEvery > 0 && n >= _typed + clip.ClickEvery)
            {
                _typed = n;
                CinematicAudio.Play2D(clip.ClickCue, 0.55f);
            }
        }

        private void Clear()
        {
            var overlay = CinematicRuntime.Overlay;
            if (overlay == null) return;
            switch (_slot)
            {
                case TypewriterClip.Slot.Lower: overlay.SetLowerText(null, Color.white); break;
                case TypewriterClip.Slot.Title: overlay.SetTitleText(null, Color.white); break;
                default: overlay.SetCenterText(null, Color.white); break;
            }
        }
    }
}
