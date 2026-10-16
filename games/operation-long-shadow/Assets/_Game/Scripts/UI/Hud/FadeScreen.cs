using LongShadow.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// UI-level black fade used by the UI's own transitions (debrief in/out, quit to session). World fades during play
    /// belong to <c>IScreenFX.FadeToBlack</c> (WP09).
    /// </summary>
    public sealed class FadeScreen : HudView
    {
        private Image _image;
        private float _from, _to, _t = 1f, _duration;

        /// <summary>Current opacity.</summary>
        public float Alpha { get; private set; }

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("Fade", parent);
            _image = UIFactory.StretchImage("Black", Root, Ctx.Sprite(UISpriteId.Square), new Color(0f, 0f, 0f, 0f));
            _image.enabled = false;
        }

        /// <summary>Fades to an opacity over seconds.</summary>
        public void FadeTo(float alpha, float seconds)
        {
            _from = Alpha;
            _to = Mathf.Clamp01(alpha);
            _duration = Mathf.Max(0f, seconds);
            _t = 0f;
            if (_duration <= 0f) Apply(_to);
        }

        public override void ResetState() => FadeTo(0f, 0f);

        public override void Tick(float dt)
        {
            if (_t >= 1f) return;
            _t = _duration > 0f ? Mathf.Min(1f, _t + dt / _duration) : 1f;
            Apply(Mathf.Lerp(_from, _to, _t));
        }

        private void Apply(float a)
        {
            Alpha = a;
            var on = a > 0.002f;
            if (_image.enabled != on) _image.enabled = on;
            UIFactory.SetAlpha(_image, a);
        }
    }
}
