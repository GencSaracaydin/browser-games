using LongShadow.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Health (GDD §11): no bar; a screen-edge dust-and-blood vignette that rises below 50 % HP (the desaturation is the
    /// player's <c>IScreenFX.SetLowHealth</c>, WP02/WP09), with a slow heartbeat pulse below 25 %. Reads
    /// <see cref="LongShadow.Core.IPlayerHealth"/> every frame.
    /// </summary>
    public sealed class HealthVignette : HudView
    {
        private Image _image;
        private float _alpha;

        /// <summary>Current vignette opacity (tests).</summary>
        public float Alpha => _alpha;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("HealthVignette", parent);
            _image = UIFactory.StretchImage("Vignette", Root, Ctx.Sprite(UISpriteId.DamageVignette), new Color(1f, 1f, 1f, 0f));
            _image.type = Image.Type.Simple;
            _image.preserveAspect = false;
            _image.enabled = false;
        }

        public override void Tick(float dt)
        {
            var health = Ctx.Health.Value;
            var ratio = health != null && health.Max > 0f ? health.Current / health.Max : 1f;
            var target = HudMath.VignetteAlpha(ratio);
            _alpha = UIAnim.Damp(_alpha, target, target > _alpha ? 14f : 2.2f, dt);
            if (_alpha < 0.003f)
            {
                if (_image.enabled) _image.enabled = false;
                return;
            }
            if (!_image.enabled) _image.enabled = true;
            var a = _alpha * 0.95f;
            if (ratio < 0.25f)
            {
                // Heartbeat: a double pulse about 72 bpm.
                var phase = Mathf.Repeat(Ctx.Time * 1.2f, 1f);
                var beat = Mathf.Exp(-phase * 14f) + 0.6f * Mathf.Exp(-Mathf.Abs(phase - 0.22f) * 16f);
                a *= 0.86f + 0.14f * Mathf.Clamp01(beat);
            }
            UIFactory.SetAlpha(_image, a);
        }
    }
}
