using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Damage direction (GDD §11): red arcs on a ring around the centre (radius <see cref="HudLayout.DamageRingRadius"/>)
    /// pointing at the source bearing, fading over 1.5 s. Arcs track the source while the player turns; a new hit within
    /// 25 degrees of a live arc refreshes it instead of stacking.
    /// </summary>
    public sealed class DamageIndicator : HudView
    {
        /// <summary>Fade time (GDD §11).</summary>
        public const float FadeSeconds = 1.5f;
        private const int Pool = 6;
        private const float MergeDegrees = 25f;

        private readonly Image[] _arcs = new Image[Pool];
        private readonly Vector3[] _sources = new Vector3[Pool];
        private readonly float[] _ages = new float[Pool];
        private System.Action<DamageInfo> _onDamaged;

        /// <summary>Arcs currently visible (tests).</summary>
        public int ActiveCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Pool; i++) if (_ages[i] < FadeSeconds) n++;
                return n;
            }
        }

        /// <summary>Z rotation of an arc (tests).</summary>
        internal float ArcAngle(int i) => _arcs[i].rectTransform.localEulerAngles.z;

        protected override void OnBuild(RectTransform parent)
        {
            var size = 2f * HudLayout.DamageRingRadius / (2f * UISprites.DamageArcRadius01);
            Root = UIFactory.Rect("DamageRing", parent, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(size, size));
            var sprite = Ctx.Sprite(UISpriteId.DamageArc);
            for (var i = 0; i < Pool; i++)
            {
                _arcs[i] = UIFactory.Image("Arc" + i, Root, sprite, UIColors.DamageArc, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(size, size));
                _arcs[i].enabled = false;
                _ages[i] = 99f;
            }
        }

        public override void Enable()
        {
            _onDamaged ??= OnDamaged;
            GameEvents.PlayerDamaged += _onDamaged;
        }

        public override void Disable() => GameEvents.PlayerDamaged -= _onDamaged;

        public override void ResetState()
        {
            for (var i = 0; i < Pool; i++)
            {
                _ages[i] = 99f;
                _arcs[i].enabled = false;
            }
        }

        private void OnDamaged(DamageInfo d)
        {
            Vector3 source;
            if (d.Instigator != null) source = d.Instigator.transform.position;
            else if (d.Direction.sqrMagnitude > 1e-4f) source = d.Point - d.Direction.normalized * 10f;
            else return; // falls and scripted damage have no direction
            Show(source);
        }

        /// <summary>Shows (or refreshes) an arc towards a world position.</summary>
        public void Show(Vector3 source)
        {
            var eye = Ctx.Eye;
            var slot = -1;
            if (eye != null)
            {
                var bearing = MathX.Bearing(eye.position, source);
                for (var i = 0; i < Pool; i++)
                {
                    if (_ages[i] >= FadeSeconds) continue;
                    var other = MathX.Bearing(eye.position, _sources[i]);
                    if (Mathf.Abs(MathX.WrapAngle180(other - bearing)) < MergeDegrees)
                    {
                        slot = i;
                        break;
                    }
                }
            }
            if (slot < 0)
            {
                var oldest = 0f;
                for (var i = 0; i < Pool; i++)
                {
                    if (_ages[i] >= oldest)
                    {
                        oldest = _ages[i];
                        slot = i;
                    }
                }
            }
            if (slot < 0) slot = 0;
            _sources[slot] = source;
            _ages[slot] = 0f;
        }

        public override void Tick(float dt)
        {
            var eye = Ctx.Eye;
            for (var i = 0; i < Pool; i++)
            {
                var arc = _arcs[i];
                if (_ages[i] >= FadeSeconds)
                {
                    if (arc.enabled) arc.enabled = false;
                    continue;
                }
                _ages[i] += dt;
                if (eye == null || !Ctx.GameplayVisible)
                {
                    if (arc.enabled) arc.enabled = false;
                    continue;
                }
                if (!arc.enabled) arc.enabled = true;
                var t = Mathf.Clamp01(_ages[i] / FadeSeconds);
                var c = UIColors.DamageArc;
                c.a *= 1f - t * t;
                arc.color = c;
                var angle = HudMath.RingAngle(eye.forward, _sources[i] - eye.position);
                arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                var s = 1f + 0.06f * (1f - Mathf.Clamp01(_ages[i] * 6f));
                arc.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
