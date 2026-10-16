using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Grenade indicator (GDD §11): a frag icon with an arrow on the damage ring while a live grenade lies within 8 m.
    /// Live grenades come from <see cref="GameEvents.GrenadeLanded"/> and end on <see cref="GameEvents.GrenadeExploded"/>
    /// (nearest within 1.5 m) or after 7 s.
    /// </summary>
    public sealed class GrenadeIndicator : HudView
    {
        /// <summary>Warning range (GDD §11).</summary>
        public const float Range = 8f;
        private const int Live = 8;
        private const int Pool = 4;
        private const float Timeout = 7f;

        private readonly Vector3[] _grenades = new Vector3[Live];
        private readonly float[] _ages = new float[Live];
        private readonly RectTransform[] _slots = new RectTransform[Pool];
        private readonly Image[] _icons = new Image[Pool];
        private readonly Image[] _arrows = new Image[Pool];
        private System.Action<Vector3, Team> _onLanded;
        private System.Action<Vector3, float> _onExploded;
        private System.Action<string> _onCheckpoint;

        /// <summary>Indicators shown last frame (tests).</summary>
        public int ShownCount { get; private set; }

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Rect("GrenadeRing", parent, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(10f, 10f));
            var frag = Ctx.Sprite(UISpriteId.IconFrag);
            var arrow = Ctx.Sprite(UISpriteId.GrenadeArrow);
            for (var i = 0; i < Pool; i++)
            {
                _slots[i] = UIFactory.Rect("Grenade" + i, Root, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(40f, 40f));
                _icons[i] = UIFactory.Image("Icon", _slots[i], frag, UIColors.Ink, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(34f, 34f));
                _icons[i].preserveAspect = true;
                _arrows[i] = UIFactory.Image("Arrow", _slots[i], arrow, UIColors.Red, UIFactory.Center, UIFactory.Center, new Vector2(0f, 32f), new Vector2(22f, 22f));
                _slots[i].gameObject.SetActive(false);
            }
            for (var i = 0; i < Live; i++) _ages[i] = 99f;
        }

        public override void Enable()
        {
            _onLanded ??= (p, t) => Add(p);
            _onExploded ??= (p, r) => Remove(p);
            _onCheckpoint ??= _ => ResetState();
            GameEvents.GrenadeLanded += _onLanded;
            GameEvents.GrenadeExploded += _onExploded;
            GameEvents.CheckpointLoaded += _onCheckpoint;
        }

        public override void Disable()
        {
            GameEvents.GrenadeLanded -= _onLanded;
            GameEvents.GrenadeExploded -= _onExploded;
            GameEvents.CheckpointLoaded -= _onCheckpoint;
        }

        public override void ResetState()
        {
            for (var i = 0; i < Live; i++) _ages[i] = 99f;
        }

        /// <summary>Registers a live grenade at a position.</summary>
        public void Add(Vector3 position)
        {
            var slot = 0;
            var oldest = -1f;
            for (var i = 0; i < Live; i++)
            {
                if (_ages[i] >= Timeout)
                {
                    slot = i;
                    break;
                }
                if (_ages[i] > oldest)
                {
                    oldest = _ages[i];
                    slot = i;
                }
            }
            _grenades[slot] = position;
            _ages[slot] = 0f;
        }

        /// <summary>Ends the live grenade nearest to a position (within 1.5 m).</summary>
        public void Remove(Vector3 position)
        {
            var best = -1;
            var bestD = 1.5f * 1.5f;
            for (var i = 0; i < Live; i++)
            {
                if (_ages[i] >= Timeout) continue;
                var d = (_grenades[i] - position).sqrMagnitude;
                if (d <= bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            if (best >= 0) _ages[best] = 99f;
        }

        public override void Tick(float dt)
        {
            var eye = Ctx.Eye;
            var rig = Ctx.Rig.Value;
            var feet = rig != null && rig.Root != null ? rig.Root.position : eye != null ? eye.position : Vector3.zero;
            var shown = 0;
            for (var i = 0; i < Live; i++)
            {
                if (_ages[i] >= Timeout) continue;
                _ages[i] += dt;
                if (eye == null || !Ctx.GameplayVisible || shown >= Pool) continue;
                var dist = Vector3.Distance(feet, _grenades[i]);
                if (dist > Range) continue;
                var slot = _slots[shown];
                if (!slot.gameObject.activeSelf) slot.gameObject.SetActive(true);
                var angle = HudMath.RingAngle(eye.forward, _grenades[i] - eye.position);
                var rad = angle * Mathf.Deg2Rad;
                var r = HudLayout.DamageRingRadius - 26f;
                slot.anchoredPosition = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * r;
                _arrows[shown].rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                _arrows[shown].rectTransform.anchoredPosition = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * 32f;
                var urgency = 1f - dist / Range;
                var blink = 0.65f + 0.35f * Mathf.Sin(Ctx.Time * Mathf.PI * 2f * (2f + 4f * urgency));
                var c = Color.Lerp(UIColors.Ink, UIColors.Red, urgency);
                c.a = blink;
                _icons[shown].color = c;
                shown++;
            }
            for (var i = shown; i < Pool; i++)
                if (_slots[i].gameObject.activeSelf) _slots[i].gameObject.SetActive(false);
            ShownCount = shown;
        }
    }
}
