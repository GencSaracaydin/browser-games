using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Compass strip (GDD §11): top centre, 640 × 28 px, the 180 degrees in front of the player, ticks every 5 degrees,
    /// labels every 45 degrees (N, NE, E ...). Objective diamonds and squad dots (green) are projected by bearing; markers
    /// outside the field sit at the strip edge, dimmed. Optional intel never appears here (GDD §7.12). A three-digit
    /// heading readout sits under the centre notch. Pooled images, no allocation per frame.
    /// </summary>
    public sealed class CompassStrip : HudView
    {
        private const int TickPool = 38;
        private const int LabelPool = 6;
        private const int MarkerPool = 10;
        private const float TickStep = 5f;

        private static readonly string[] s_cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        private static readonly string[] s_squad = { SquadIds.Hollis, SquadIds.Sparrow, SquadIds.Tinker };

        private readonly Image[] _ticks = new Image[TickPool];
        private readonly int[] _tickKind = new int[TickPool];
        private readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[LabelPool];
        private readonly int[] _labelIndex = new int[LabelPool];
        private readonly Image[] _markers = new Image[MarkerPool];
        private readonly Image[] _squadDots = new Image[3];
        private TextMeshProUGUI _heading;
        private readonly TextBuffer _headingText = new TextBuffer(8);
        private int _lastHeading = -1;
        private CanvasGroup _group;
        private float _ppd;

        /// <summary>Last heading drawn (degrees), for tests.</summary>
        public float Heading { get; private set; }

        protected override void OnBuild(RectTransform parent)
        {
            var size = new Vector2(HudLayout.CompassStrip.x, HudLayout.CompassBlockHeight);
            Root = UIFactory.Rect("Compass", parent, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -HudLayout.CompassTop), size);
            _group = UIFactory.Group(Root.gameObject);
            UIFactory.SubCanvas(Root.gameObject, false);
            _ppd = HudLayout.CompassStrip.x / (HudLayout.CompassHalfField * 2f);

            var ink = UIColors.WithAlpha(UIColors.Ink, 0.85f);
            var line = Ctx.Sprite(UISpriteId.Line);
            var strip = UIFactory.Rect("Strip", Root, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -10f), HudLayout.CompassStrip);
            // Faint backing and top rule.
            UIFactory.StretchImage("Backing", strip, Ctx.Sprite(UISpriteId.SoftPanel), new Color(0f, 0f, 0f, 0.22f), -6f);
            var rule = UIFactory.Image("Rule", strip, line, UIColors.WithAlpha(UIColors.Ink, 0.35f), UIFactory.TopCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(HudLayout.CompassStrip.x, 2f));
            rule.type = Image.Type.Sliced;

            for (var i = 0; i < TickPool; i++)
            {
                var t = UIFactory.Image("Tick" + i, strip, line, ink, UIFactory.TopCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(2f, 7f));
                t.type = Image.Type.Sliced;
                t.enabled = false;
                _ticks[i] = t;
                _tickKind[i] = -1;
            }
            for (var i = 0; i < LabelPool; i++)
            {
                var l = UIFactory.Text("Label" + i, strip, Ctx.Fonts, FontRole.Condensed, 19f, ink, TextAlignmentOptions.Top,
                    UIFactory.TopCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(48f, 24f));
                l.enabled = false;
                _labels[i] = l;
                _labelIndex[i] = -1;
            }
            var diamond = Ctx.Sprite(UISpriteId.Diamond);
            for (var i = 0; i < MarkerPool; i++)
            {
                var m = UIFactory.Image("Marker" + i, strip, diamond, UIColors.MarkerAmber, UIFactory.TopCenter, UIFactory.Center, new Vector2(0f, -14f), new Vector2(15f, 15f));
                m.enabled = false;
                _markers[i] = m;
            }
            var dot = Ctx.Sprite(UISpriteId.SoftDisc);
            for (var i = 0; i < 3; i++)
            {
                var d = UIFactory.Image("Squad" + i, strip, dot, UIColors.SquadGreen, UIFactory.TopCenter, UIFactory.Center, new Vector2(0f, -22f), new Vector2(14f, 14f));
                d.enabled = false;
                _squadDots[i] = d;
            }
            var notch = UIFactory.Image("Notch", Root, Ctx.Sprite(UISpriteId.Arrow), UIColors.KeyAmber, UIFactory.TopCenter, UIFactory.Center, new Vector2(0f, -4f), new Vector2(12f, 12f));
            notch.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
            _heading = UIFactory.Text("Heading", Root, Ctx.Fonts, FontRole.Condensed, 18f, UIColors.WithAlpha(UIColors.Ink, 0.7f), TextAlignmentOptions.Top,
                UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -44f), new Vector2(80f, 22f));
        }

        public override void Tick(float dt)
        {
            var eye = Ctx.Eye;
            var visible = eye != null && Ctx.Overlay != HudOverlay.DoorGun;
            _group.alpha = visible ? 1f : 0f;
            if (!visible) return;

            var heading = MathX.BearingOf(eye.forward);
            Heading = heading;
            DrawTicks(heading);
            DrawMarkers(eye.position, heading);
            DrawSquad(eye.position, heading);

            var h = Mathf.RoundToInt(heading) % 360;
            if (h != _lastHeading)
            {
                _lastHeading = h;
                _headingText.Clear().AppendPadded(h, 3).ApplyTo(_heading);
            }
        }

        private void DrawTicks(float heading)
        {
            var half = HudLayout.CompassHalfField;
            var hw = HudLayout.CompassStrip.x * 0.5f;
            var first = Mathf.Ceil((heading - half) / TickStep) * TickStep;
            var labelSlot = 0;
            for (var i = 0; i < TickPool; i++)
            {
                var b = first + i * TickStep;
                var tick = _ticks[i];
                if (b > heading + half + 0.001f)
                {
                    if (tick.enabled) tick.enabled = false;
                    continue;
                }
                var x = (b - heading) * _ppd;
                var wrapped = Mathf.RoundToInt(MathX.WrapAngle360(b));
                var kind = wrapped % 45 == 0 ? 2 : wrapped % 15 == 0 ? 1 : 0;
                if (_tickKind[i] != kind)
                {
                    _tickKind[i] = kind;
                    tick.rectTransform.sizeDelta = new Vector2(2f, kind == 2 ? 15f : kind == 1 ? 10f : 6f);
                }
                if (!tick.enabled) tick.enabled = true;
                tick.rectTransform.anchoredPosition = new Vector2(x, 0f);
                var edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, Mathf.Abs(x) / hw));
                UIFactory.SetAlpha(tick, (kind == 0 ? 0.55f : 0.85f) * edge);

                if (kind == 2 && labelSlot < LabelPool)
                {
                    var label = _labels[labelSlot];
                    var idx = (wrapped / 45) % 8;
                    if (_labelIndex[labelSlot] != idx)
                    {
                        _labelIndex[labelSlot] = idx;
                        label.SetText(s_cardinals[idx]);
                        label.color = idx == 0 ? UIColors.KeyAmber : UIColors.Ink;
                    }
                    if (!label.enabled) label.enabled = true;
                    label.rectTransform.anchoredPosition = new Vector2(x, -17f);
                    label.alpha = edge;
                    labelSlot++;
                }
            }
            for (var i = labelSlot; i < LabelPool; i++)
                if (_labels[i].enabled) _labels[i].enabled = false;
        }

        private void DrawMarkers(Vector3 eye, float heading)
        {
            var used = 0;
            var objectives = Ctx.Objectives.Value;
            if (objectives != null)
            {
                IReadOnlyList<MarkerDef> markers = objectives.Markers;
                var count = markers != null ? markers.Count : 0;
                for (var i = 0; i < count && used < MarkerPool; i++)
                {
                    var m = markers[i];
                    if (m.Kind == MarkerKind.Intel) continue; // optional intel: never on the compass (GDD §7.12)
                    var pos = m.WorldPosition;
                    var to = pos - eye;
                    to.y = 0f;
                    if (to.sqrMagnitude < 0.01f) continue;
                    var inside = HudMath.CompassOffset(MathX.BearingOf(to), heading, HudLayout.CompassHalfField, HudLayout.CompassStrip.x * 0.5f, out var x);
                    var img = _markers[used++];
                    if (!img.enabled) img.enabled = true;
                    img.rectTransform.anchoredPosition = new Vector2(x, -14f);
                    var color = m.Kind == MarkerKind.HighPriority ? UIColors.Red : Ctx.MarkerColor;
                    color.a = inside ? 1f : 0.45f;
                    UIFactory.SetColor(img, color);
                    var sprite = m.Kind == MarkerKind.DiamondHollow ? Ctx.Sprite(UISpriteId.DiamondHollow) :
                                 m.Kind == MarkerKind.Follow ? Ctx.Sprite(UISpriteId.Chevron) : Ctx.Sprite(UISpriteId.Diamond);
                    if (img.sprite != sprite) img.sprite = sprite;
                }
            }
            for (var i = used; i < MarkerPool; i++)
                if (_markers[i].enabled) _markers[i].enabled = false;
        }

        private void DrawSquad(Vector3 eye, float heading)
        {
            var squad = Ctx.Squad.Value;
            for (var i = 0; i < 3; i++)
            {
                var dot = _squadDots[i];
                var t = squad != null ? squad.Member(s_squad[i]) : null;
                if (t == null)
                {
                    if (dot.enabled) dot.enabled = false;
                    continue;
                }
                var to = t.position - eye;
                to.y = 0f;
                var inside = HudMath.CompassOffset(MathX.BearingOf(to), heading, HudLayout.CompassHalfField, HudLayout.CompassStrip.x * 0.5f, out var x);
                if (!inside)
                {
                    if (dot.enabled) dot.enabled = false;
                    continue;
                }
                if (!dot.enabled) dot.enabled = true;
                dot.rectTransform.anchoredPosition = new Vector2(x, -22f);
            }
        }

        public override void CollectLayout(List<HudLayoutRegion> regions)
        {
            regions.Add(HudLayoutRegion.Of("Compass", Root, Ctx.CanvasRect));
        }
    }
}
