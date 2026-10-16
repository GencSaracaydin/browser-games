using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Detection meters over the Beat 2 sentries (GDD §14.2 "a detection meter on a sentry's HUD icon"; WP04 request):
    /// a small hollow diamond over the sentry's head that fills from amber to red with its awareness. Awareness comes
    /// from <see cref="IEncounterService.Awareness01"/>, positions from <see cref="IEncounterService.Actor"/>. Hidden at 0, when dead, beyond 70 m and off-screen.
    /// </summary>
    public sealed class DetectionMeters : HudView
    {
        /// <summary>Enemies that get a meter (GDD §7.2 sentries).</summary>
        public static readonly string[] Watched = { "E2-01", "E2-02" };
        private const float MaxRange = 70f;
        private const float HeadLift = 2.25f;

        private readonly RectTransform[] _meters = new RectTransform[2];
        private readonly Image[] _fills = new Image[2];
        private readonly Image[] _frames = new Image[2];
        private readonly float[] _shown = new float[2];

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("DetectionMeters", parent);
            for (var i = 0; i < Watched.Length; i++)
            {
                _meters[i] = UIFactory.Rect(Watched[i], Root, UIFactory.BottomLeft, UIFactory.Center, Vector2.zero, new Vector2(30f, 30f));
                _frames[i] = UIFactory.Image("Frame", _meters[i], Ctx.Sprite(UISpriteId.DiamondHollow), UIColors.Ink, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(30f, 30f));
                _fills[i] = UIFactory.Image("Fill", _meters[i], Ctx.Sprite(UISpriteId.Diamond), UIColors.MarkerAmber, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(22f, 22f));
                _fills[i].type = Image.Type.Filled;
                _fills[i].fillMethod = Image.FillMethod.Vertical;
                _fills[i].fillOrigin = (int)Image.OriginVertical.Bottom;
                _meters[i].gameObject.SetActive(false);
            }
        }

        public override void Tick(float dt)
        {
            var encounters = Ctx.Encounters.Value;
            var cam = Ctx.Camera;
            var eye = Ctx.Eye;
            for (var i = 0; i < Watched.Length; i++)
            {
                var show = false;
                if (encounters != null && cam != null && eye != null && Ctx.GameplayVisible)
                {
                    var actor = encounters.Actor(Watched[i]);
                    if (actor != null && actor.IsAlive && actor.Root != null)
                    {
                        var aware = Mathf.Clamp01(encounters.Awareness01(Watched[i]));
                        _shown[i] = UIAnim.Damp(_shown[i], aware, 10f, dt);
                        var head = actor.Root.position + Vector3.up * HeadLift;
                        if (_shown[i] > 0.01f && Vector3.Distance(eye.position, head) <= MaxRange)
                        {
                            var p = HudMath.WorldToCanvas(cam, head, Ctx.ScaleFactor, out var behind);
                            if (!behind && p.x > 0f && p.y > 0f && p.x < Ctx.CanvasSize.x && p.y < Ctx.CanvasSize.y)
                            {
                                show = true;
                                _meters[i].anchoredPosition = p;
                                _fills[i].fillAmount = _shown[i];
                                _fills[i].color = Color.Lerp(UIColors.MarkerAmber, UIColors.Red, Mathf.SmoothStep(0.4f, 1f, _shown[i]));
                                var pulse = _shown[i] > 0.85f ? 0.6f + 0.4f * Mathf.Sin(Ctx.Time * 18f) : 1f;
                                UIFactory.SetAlpha(_frames[i], pulse);
                            }
                        }
                    }
                }
                if (_meters[i].gameObject.activeSelf != show) _meters[i].gameObject.SetActive(show);
            }
        }
    }
}
