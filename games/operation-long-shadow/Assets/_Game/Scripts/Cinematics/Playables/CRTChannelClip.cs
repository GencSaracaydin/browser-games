using LongShadow.Cinematics.Booth;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// What one CRT shows from the clip start (TECH_PLAN §7.12 CRTChannelClip: static / still / live RT / video / clock):
    /// content, the LIB_Cinematics image or feed id or video file, burn-in text, teletype scroll, zoom (UV window from
    /// → to over the clip), scanline thickening (intro shot 8) and an optional snap-on with degauss. The content stays
    /// after the clip ends until another clip changes it.
    /// </summary>
    [System.Serializable]
    public sealed class CRTChannelClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private CrtContent _content = CrtContent.Static;
        [SerializeField, Tooltip("Image id (LIB_Cinematics), feed id (Feed.*) or video file name.")] private string _assetId;
        [SerializeField] private string[] _slideIds = new string[0];
        [SerializeField] private float _slideSeconds = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _static = 0.06f;
        [SerializeField] private bool _snapOn;
        [SerializeField] private float _snapDelay;
        [SerializeField] private string _overlayText;
        [SerializeField] private Vector2 _scroll;
        [SerializeField] private Vector4 _uvFrom = new Vector4(0f, 0f, 1f, 1f);
        [SerializeField] private Vector4 _uvTo = new Vector4(0f, 0f, 1f, 1f);
        [SerializeField] private ShotEase _uvEase = ShotEase.Smooth;
        [SerializeField, Range(0f, 1f)] private float _scanFrom;
        [SerializeField, Range(0f, 1f)] private float _scanTo;
        [SerializeField, Tooltip("0 = default brightness.")] private float _brightness;
        [SerializeField] private bool _gunCam;
        [SerializeField] private bool _loopVideo = true;
        [SerializeField, Tooltip("Still shown until the video is prepared (and if it fails).")] private string _fallbackStillId;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        public CrtContent Content { get => _content; set => _content = value; }
        public string AssetId { get => _assetId; set => _assetId = value; }
        public string[] SlideIds { get => _slideIds; set => _slideIds = value ?? new string[0]; }
        public float SlideSeconds { get => _slideSeconds; set => _slideSeconds = value; }
        public float StaticAmount { get => _static; set => _static = value; }
        public bool SnapOn { get => _snapOn; set => _snapOn = value; }
        public float SnapDelay { get => _snapDelay; set => _snapDelay = value; }
        public string OverlayText { get => _overlayText; set => _overlayText = value; }
        public Vector2 Scroll { get => _scroll; set => _scroll = value; }
        public Vector4 UvFrom { get => _uvFrom; set => _uvFrom = value; }
        public Vector4 UvTo { get => _uvTo; set => _uvTo = value; }
        public ShotEase UvEase { get => _uvEase; set => _uvEase = value; }
        public float ScanFrom { get => _scanFrom; set => _scanFrom = value; }
        public float ScanTo { get => _scanTo; set => _scanTo = value; }
        public float Brightness { get => _brightness; set => _brightness = value; }
        public bool GunCam { get => _gunCam; set => _gunCam = value; }
        public bool LoopVideo { get => _loopVideo; set => _loopVideo = value; }
        public string FallbackStillId { get => _fallbackStillId; set => _fallbackStillId = value; }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<CRTChannelBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        /// <summary>Applies the content to a screen (clip start).</summary>
        public void Apply(CRTScreen screen)
        {
            if (screen == null) return;
            switch (_content)
            {
                case CrtContent.Off:
                    screen.Off();
                    break;
                case CrtContent.Static:
                    screen.ShowStatic(Mathf.Max(0.5f, _static));
                    break;
                case CrtContent.Still:
                    screen.ShowTexture(CinematicLibrary.Lookup<Texture>(_assetId), _static);
                    break;
                case CrtContent.Feed:
                    screen.ShowFeed(_assetId, _static);
                    break;
                case CrtContent.Video:
                    screen.ShowVideo(_assetId, _loopVideo, _gunCam, CinematicLibrary.Lookup<Texture>(_fallbackStillId));
                    break;
                case CrtContent.Slides:
                    var tex = new Texture[_slideIds.Length];
                    for (var i = 0; i < tex.Length; i++) tex[i] = CinematicLibrary.Lookup<Texture>(_slideIds[i]);
                    screen.ShowSlides(tex, _slideSeconds);
                    break;
            }
            if (_snapOn) screen.SnapOn(_snapDelay);
            screen.SetOverlayText(_overlayText);
            screen.SetScroll(_scroll);
            screen.SetBrightness(_brightness > 0f ? _brightness : 1.5f);
        }

        /// <summary>Continuous parameters at normalised time.</summary>
        public void Tick(CRTScreen screen, float t01)
        {
            if (screen == null) return;
            var k = Easing.Evaluate(_uvEase, t01);
            screen.SetUvRect(Vector4.Lerp(_uvFrom, _uvTo, k));
            screen.SetScanlineBoost(Mathf.Lerp(_scanFrom, _scanTo, k));
        }
    }

    /// <summary>Per-clip behaviour of a CRT channel.</summary>
    internal sealed class CRTChannelBehaviour : PlayableBehaviour
    {
        public CRTChannelClip Clip;
    }
}
