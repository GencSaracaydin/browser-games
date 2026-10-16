using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Squad nameplates (GDD §11): the green callsign appears over a squadmate's head when looked at within 25 m
    /// (aim within about 8 degrees, or the head inside 110 px of the screen centre), with the short name under it.
    /// Hidden behind walls (one ray per member every 0.2 s).
    /// </summary>
    public sealed class SquadNameplates : HudView
    {
        /// <summary>Range (GDD §11).</summary>
        public const float Range = 25f;
        private const float LookDegrees = 8f;
        private const float CenterRadius = 110f;
        private const float HeadLift = 2.05f;
        private const int OcclusionMask = 1 << LayerIds.Default;

        private static readonly string[] s_members = { SquadIds.Hollis, SquadIds.Sparrow, SquadIds.Tinker };
        private readonly RectTransform[] _plates = new RectTransform[3];
        private readonly CanvasGroup[] _groups = new CanvasGroup[3];
        private readonly float[] _alpha = new float[3];
        private readonly bool[] _occluded = new bool[3];
        private float _rayTimer;
        private int _rayCursor;

        /// <summary>Opacity of a member's plate (tests).</summary>
        internal float AlphaOf(int index) => _alpha[index];

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("Nameplates", parent);
            for (var i = 0; i < 3; i++)
            {
                var plate = UIFactory.Rect(s_members[i], Root, UIFactory.BottomLeft, UIFactory.BottomCenter, Vector2.zero, new Vector2(240f, 46f));
                _groups[i] = UIFactory.Group(plate.gameObject, 0f);
                var callsign = UIFactory.Text("Callsign", plate, Ctx.Fonts, FontRole.Condensed, 21f, UIColors.SquadGreen, TextAlignmentOptions.Bottom,
                    UIFactory.TopCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(240f, 24f));
                callsign.characterSpacing = 5f;
                callsign.SetText(SquadCallsigns.CallsignOf(s_members[i]) ?? s_members[i]);
                var name = UIFactory.Text("Name", plate, Ctx.Fonts, FontRole.CondensedMedium, 15f, UIColors.SquadGreenDim, TextAlignmentOptions.Top,
                    UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -24f), new Vector2(240f, 20f));
                name.characterSpacing = 6f;
                name.SetText(SquadCallsigns.NameOf(s_members[i]) ?? string.Empty);
                _plates[i] = plate;
            }
        }

        public override void Tick(float dt)
        {
            var squad = Ctx.Squad.Value;
            var cam = Ctx.Camera;
            var eye = Ctx.Eye;
            var canvasCenter = Ctx.CanvasSize * 0.5f;
            _rayTimer -= dt;
            var castRay = _rayTimer <= 0f;
            if (castRay) _rayTimer = 0.2f / 3f;

            for (var i = 0; i < 3; i++)
            {
                var target = 0f;
                var t = squad != null ? squad.Member(s_members[i]) : null;
                if (t != null && cam != null && eye != null && Ctx.GameplayVisible && Ctx.Overlay == HudOverlay.None)
                {
                    var head = t.position + Vector3.up * HeadLift;
                    var to = head - eye.position;
                    var dist = to.magnitude;
                    if (dist <= Range && dist > 0.5f)
                    {
                        var p = HudMath.WorldToCanvas(cam, head, Ctx.ScaleFactor, out var behind);
                        if (!behind)
                        {
                            var looked = Vector3.Angle(eye.forward, to) <= LookDegrees || (p - canvasCenter).sqrMagnitude <= CenterRadius * CenterRadius;
                            if (castRay && _rayCursor == i)
                                _occluded[i] = Physics.Linecast(eye.position, head - to.normalized * 0.4f, OcclusionMask, QueryTriggerInteraction.Ignore);
                            if (looked && !_occluded[i]) target = 1f - Mathf.Clamp01((dist - Range * 0.8f) / (Range * 0.2f)) * 0.6f;
                            _plates[i].anchoredPosition = p + new Vector2(0f, 6f);
                        }
                    }
                }
                _alpha[i] = UIAnim.Approach(_alpha[i], target, target > _alpha[i] ? 7f : 3.5f, dt);
                _groups[i].alpha = _alpha[i];
            }
            if (castRay) _rayCursor = (_rayCursor + 1) % 3;
        }
    }
}
