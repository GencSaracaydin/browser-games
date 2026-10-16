using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Hit markers (GDD §11): four diagonal ticks at the centre for 0.12 s on <see cref="GameEvents.HitConfirmed"/>; a
    /// kill turns them red and 20 % larger for 0.2 s; a headshot adds a distinct click. The HUD plays the ticks itself
    /// (WP03 plays none).
    /// </summary>
    public sealed class HitMarkers : HudView
    {
        /// <summary>Hit marker duration (GDD §11).</summary>
        public const float HitSeconds = 0.12f;
        /// <summary>Kill marker duration (GDD §11).</summary>
        public const float KillSeconds = 0.2f;

        private Image _marker;
        private float _age = 99f;
        private bool _kill;
        private System.Action<bool, bool> _onHit;

        /// <summary>True while a marker is visible (tests).</summary>
        public bool Showing => _age < (_kill ? KillSeconds : HitSeconds);

        /// <summary>True when the last marker was a kill (tests).</summary>
        public bool LastWasKill => _kill;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Rect("HitMarker", parent, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(42f, 42f));
            _marker = UIFactory.Image("Ticks", Root, Ctx.Sprite(UISpriteId.HitMarker), UIColors.Ink, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(42f, 42f));
            _marker.enabled = false;
        }

        public override void Enable()
        {
            _onHit ??= Show;
            GameEvents.HitConfirmed += _onHit;
        }

        public override void Disable() => GameEvents.HitConfirmed -= _onHit;

        public override void ResetState() => _age = 99f;

        /// <summary>Shows a marker (also used by the test bed).</summary>
        public void Show(bool kill, bool headshot)
        {
            _age = 0f;
            _kill = kill;
            Ctx.Audio.Play(UiCues.HitMarker, 0.9f);
            if (headshot) Ctx.Audio.Play(UiCues.Headshot, 1f);
            if (kill) Ctx.Audio.Play(UiCues.Kill, 0.9f);
        }

        public override void Tick(float dt)
        {
            if (_age > 10f) return;
            _age += dt;
            var duration = _kill ? KillSeconds : HitSeconds;
            var on = _age < duration && Ctx.GameplayVisible;
            if (_marker.enabled != on) _marker.enabled = on;
            if (!on) return;
            var t = _age / duration;
            var color = _kill ? UIColors.Red : UIColors.Ink;
            color.a = 1f - t * t;
            _marker.color = color;
            var scale = _kill ? Mathf.Lerp(1.35f, 1.2f, Mathf.Clamp01(t * 3f)) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01(t * 4f));
            Root.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
