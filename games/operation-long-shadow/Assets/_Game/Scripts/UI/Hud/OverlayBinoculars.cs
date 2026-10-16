using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Binocular overlay (GDD §11, Beat 1): double-eyepiece mask (black side bars beyond 16:9), mil reticle with range
    /// ticks, a rangefinder/azimuth readout, and a tick-off on each glassed POI (WP02 raises the mission event
    /// <c>Glassed.&lt;POI id&gt;</c>) with an <c>OBSERVED n/3</c> counter. Shown by <see cref="IHud.SetOverlay"/>.
    /// </summary>
    public sealed class OverlayBinoculars : HudView
    {
        /// <summary>Mission event prefix raised by WP02 Binoculars (requested as SequenceIds.GlassedPrefix).</summary>
        public const string GlassedPrefix = SequenceIds.GlassedPrefix;
        private const int RangeMask = (1 << LayerIds.Default) | (1 << LayerIds.Vehicle) | (1 << LayerIds.FarProxy);
        private const float TickSeconds = 1.4f;

        private CanvasGroup _group;
        private RectTransform _maskRect;
        private Image _leftBar, _rightBar;
        private TextMeshProUGUI _readout;
        private TextMeshProUGUI _observed;
        private RectTransform _tick;
        private TextMeshProUGUI _tickLabel;
        private readonly TextBuffer _buf = new TextBuffer(48);
        private readonly HashSet<string> _glassed = new HashSet<string>();
        private readonly List<GlassingPOI> _pois = new List<GlassingPOI>();
        private float _alpha;
        private float _rangeTimer;
        private int _range = -1, _azimuth = -1;
        private int _total;
        private int _observedShown = -1;
        private float _tickAge = 99f;
        private System.Action<string> _onMission;
        private System.Action<string> _onCheckpoint;

        /// <summary>POIs ticked off since the last checkpoint load (tests).</summary>
        public int ObservedCount => _glassed.Count;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("Binoculars", parent);
            _group = UIFactory.Group(Root.gameObject, 0f);
            _maskRect = UIFactory.Rect("Mask", Root, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(1920f, 1080f));
            var mask = UIFactory.StretchImage("Image", _maskRect, Ctx.Sprite(UISpriteId.BinocularMask), Color.black);
            mask.type = Image.Type.Simple;
            mask.color = Color.white; // the art is black with alpha; white keeps it as authored
            var square = Ctx.Sprite(UISpriteId.Square);
            _leftBar = UIFactory.Image("LeftBar", Root, square, Color.black, UIFactory.MiddleLeft, UIFactory.MiddleLeft, Vector2.zero, new Vector2(0f, 4000f));
            _rightBar = UIFactory.Image("RightBar", Root, square, Color.black, UIFactory.MiddleRight, UIFactory.MiddleRight, Vector2.zero, new Vector2(0f, 4000f));

            var reticle = UIFactory.Image("Reticle", Root, Ctx.Sprite(UISpriteId.BinocularReticle), new Color(0.063f, 0.063f, 0.063f, 0.85f),
                UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(432f, 432f));
            reticle.preserveAspect = true;

            _readout = UIFactory.Text("Readout", Root, Ctx.Fonts, FontRole.Mono, 20f, UIColors.WithAlpha(UIColors.Ink, 0.85f), TextAlignmentOptions.Center,
                UIFactory.Center, UIFactory.Center, new Vector2(0f, -250f), new Vector2(420f, 26f));
            _readout.characterSpacing = 4f;
            _observed = UIFactory.Text("Observed", Root, Ctx.Fonts, FontRole.Condensed, 22f, UIColors.KeyAmber, TextAlignmentOptions.Center,
                UIFactory.Center, UIFactory.Center, new Vector2(0f, -284f), new Vector2(420f, 28f));
            _observed.characterSpacing = 6f;

            _tick = UIFactory.Rect("Tick", Root, UIFactory.Center, UIFactory.Center, new Vector2(0f, 64f), new Vector2(60f, 60f));
            var line = Ctx.Sprite(UISpriteId.Line);
            var a = UIFactory.Image("Short", _tick, line, UIColors.KeyAmber, UIFactory.Center, UIFactory.Center, new Vector2(-9f, -4f), new Vector2(5f, 18f));
            a.type = Image.Type.Sliced;
            a.rectTransform.localEulerAngles = new Vector3(0f, 0f, 40f);
            var b = UIFactory.Image("Long", _tick, line, UIColors.KeyAmber, UIFactory.Center, UIFactory.Center, new Vector2(7f, 6f), new Vector2(5f, 36f));
            b.type = Image.Type.Sliced;
            b.rectTransform.localEulerAngles = new Vector3(0f, 0f, -35f);
            _tickLabel = UIFactory.Text("Label", _tick, Ctx.Fonts, FontRole.Condensed, 18f, UIColors.KeyAmber, TextAlignmentOptions.Top,
                UIFactory.Center, UIFactory.TopCenter, new Vector2(0f, -30f), new Vector2(160f, 24f));
            _tickLabel.characterSpacing = 6f;
            _tickLabel.SetText("MARKED");
            _tick.gameObject.SetActive(false);
            Root.gameObject.SetActive(false);
        }

        public override void Enable()
        {
            _onMission ??= OnMissionEvent;
            _onCheckpoint ??= _ => _glassed.Clear();
            GameEvents.MissionEvent += _onMission;
            GameEvents.CheckpointLoaded += _onCheckpoint;
        }

        public override void Disable()
        {
            GameEvents.MissionEvent -= _onMission;
            GameEvents.CheckpointLoaded -= _onCheckpoint;
        }

        public override void ResetState()
        {
            _glassed.Clear();
            _tickAge = 99f;
        }

        private void OnMissionEvent(string id)
        {
            if (id == null || !id.StartsWith(GlassedPrefix, System.StringComparison.Ordinal)) return;
            if (!_glassed.Add(id)) return;
            _tickAge = 0f;
            Ctx.Audio.Play(UiCues.Objective, 0.7f);
        }

        public override void Tick(float dt)
        {
            var on = Ctx.Overlay == HudOverlay.Binoculars;
            if (on && !Root.gameObject.activeSelf)
            {
                Root.gameObject.SetActive(true);
                CountPois();
                Relayout(Ctx.CanvasSize);
            }
            _alpha = UIAnim.Approach(_alpha, on ? 1f : 0f, 8f, dt);
            _group.alpha = _alpha;
            if (!on && _alpha <= 0f)
            {
                if (Root.gameObject.activeSelf) Root.gameObject.SetActive(false);
                return;
            }

            // Rangefinder and azimuth, 10 Hz.
            _rangeTimer -= dt;
            var eye = Ctx.Eye;
            if (_rangeTimer <= 0f && eye != null)
            {
                _rangeTimer = 0.1f;
                var range = Physics.Raycast(eye.position, eye.forward, out var hit, 3000f, RangeMask, QueryTriggerInteraction.Ignore)
                    ? Mathf.RoundToInt(hit.distance) : -1;
                var az = Mathf.RoundToInt(MathX.BearingOf(eye.forward)) % 360;
                if (range != _range || az != _azimuth)
                {
                    _range = range;
                    _azimuth = az;
                    _buf.Clear().Append("RNG ");
                    if (range < 0) _buf.Append("----");
                    else _buf.AppendPadded(range, 4);
                    _buf.Append(" M   AZ ").AppendPadded(az, 3);
                    _buf.ApplyTo(_readout);
                }
            }

            var observed = _glassed.Count;
            if (observed != _observedShown)
            {
                _observedShown = observed;
                if (_total > 0) _buf.Clear().Append("OBSERVED ").Append(Mathf.Min(observed, _total)).Append('/').Append(_total).ApplyTo(_observed);
                else _observed.SetText(string.Empty);
            }

            if (_tickAge < TickSeconds)
            {
                _tickAge += dt;
                var showTick = _tickAge < TickSeconds;
                if (_tick.gameObject.activeSelf != showTick) _tick.gameObject.SetActive(showTick);
                var s = Mathf.Lerp(1.5f, 1f, UIAnim.OutCubic(_tickAge * 6f));
                _tick.localScale = new Vector3(s, s, 1f);
            }
        }

        public override void Relayout(Vector2 canvasSize)
        {
            var maskWidth = canvasSize.y * 16f / 9f;
            _maskRect.sizeDelta = new Vector2(maskWidth, canvasSize.y);
            var bar = Mathf.Max(0f, (canvasSize.x - maskWidth) * 0.5f + 2f);
            _leftBar.rectTransform.sizeDelta = new Vector2(bar, canvasSize.y + 4f);
            _rightBar.rectTransform.sizeDelta = new Vector2(bar, canvasSize.y + 4f);
        }

        private void CountPois()
        {
            var registry = Ctx.MarkerRegistry.Value;
            _pois.Clear();
            if (registry != null) registry.GetAll(_pois);
            _total = _pois.Count > 0 ? _pois.Count : 3; // GDD §7.1: three POIs
            _observedShown = -1;
        }
    }
}
