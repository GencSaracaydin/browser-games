using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// World objective markers (GDD §11): a 28 px diamond with a short label and the distance in metres (1 m steps below
    /// 100 m, 5 m beyond), clamped to the screen edge with an arrow when off-screen, 30 % while ADS, hidden within 4 m.
    /// Colour from <see cref="ILightStage.HudColor"/> (amber before s 2.0, pale green after); red high-priority markers
    /// show through walls, other markers dim when a wall is in the way (one occlusion ray per frame, round robin).
    /// FOLLOW chevrons ride over squadmates; optional intel shows only within 6 m and never on the edge (GDD §7.12).
    /// </summary>
    public sealed class ObjectiveMarkers : HudView
    {
        /// <summary>Markers drawn at once.</summary>
        public const int Pool = 12;
        /// <summary>Markers closer than this are hidden (GDD §11).</summary>
        public const float HideWithin = 4f;
        /// <summary>Optional intel shows only within this range (GDD §7.12).</summary>
        public const float OptionalIntelRange = 6f;

        private const int OcclusionMask = 1 << LayerIds.Default;
        private const float FollowLift = 2.15f;
        private const float ActorLift = 1.3f;

        private readonly MarkerWidget[] _widgets = new MarkerWidget[Pool];
        private int _occlusionCursor;

        /// <summary>Markers drawn last frame (tests).</summary>
        public int VisibleCount { get; private set; }

        /// <summary>Widget state for tests: canvas position, whether clamped to the edge, alpha.</summary>
        internal bool TryGetWidget(int index, out Vector2 position, out bool onEdge, out float alpha)
        {
            position = default;
            onEdge = false;
            alpha = 0f;
            if (index < 0 || index >= Pool || !_widgets[index].Active) return false;
            position = _widgets[index].Root.anchoredPosition;
            onEdge = _widgets[index].Arrow.enabled;
            alpha = _widgets[index].Alpha;
            return true;
        }

        /// <summary>Icon colour of a widget (tests).</summary>
        internal Color WidgetColor(int index) => index >= 0 && index < Pool ? _widgets[index].Icon.color : default;

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("Markers", parent);
            UIFactory.SubCanvas(Root.gameObject, false);
            for (var i = 0; i < Pool; i++) _widgets[i] = MarkerWidget.Create(Ctx, Root, i);
        }

        public override void Tick(float dt)
        {
            var objectives = Ctx.Objectives.Value;
            var cam = Ctx.Camera;
            var eye = Ctx.Eye;
            var used = 0;
            var show = objectives != null && cam != null && eye != null && Ctx.Overlay != HudOverlay.DoorGun;
            if (show)
            {
                IReadOnlyList<MarkerDef> markers = objectives.Markers;
                var count = markers != null ? markers.Count : 0;
                var canvas = Ctx.CanvasSize;
                var center = canvas * 0.5f;
                var half = new Vector2(Mathf.Max(10f, center.x - HudLayout.MarkerEdgeInset), Mathf.Max(10f, center.y - HudLayout.MarkerEdgeInset));
                var eyePos = eye.position;
                for (var i = 0; i < count && used < Pool; i++)
                {
                    var m = markers[i];
                    var world = WorldPosition(m);
                    var dist = Vector3.Distance(eyePos, world);
                    if (dist < HideWithin) continue;
                    var optionalIntel = IsOptionalIntel(objectives, m);
                    if (optionalIntel && dist > OptionalIntelRange) continue;

                    var p = HudMath.WorldToCanvas(cam, world, Ctx.ScaleFactor, out var behind);
                    var clamped = HudMath.ClampToRect(ref p, center, half, out var angle) || behind;
                    if (clamped && optionalIntel) continue; // never on the edge

                    var w = _widgets[used++];
                    Draw(w, m, p, clamped, angle, dist);
                }
                UpdateOcclusion(eyePos, markers, count);
            }
            for (var i = used; i < Pool; i++) _widgets[i].SetActive(false);
            VisibleCount = used;
        }

        private void Draw(MarkerWidget w, in MarkerDef m, Vector2 p, bool clamped, float angle, float dist)
        {
            if (!w.Active) w.SetActive(true);
            if (!string.Equals(w.MarkerId, m.Id))
            {
                w.MarkerId = m.Id;
                w.Occluded = false;
                w.Icon.sprite = SpriteFor(m.Kind);
                var size = m.Kind == MarkerKind.Follow ? 24f : m.Kind == MarkerKind.Intel ? 26f : 28f;
                w.Icon.rectTransform.sizeDelta = new Vector2(size, size);
            }
            w.Root.anchoredPosition = p;

            var color = m.Kind == MarkerKind.HighPriority || (m.Kind == MarkerKind.TargetX && m.ThroughWalls) ? UIColors.Red : Ctx.MarkerColor;
            w.SetAlpha(HudMath.MarkerAlpha(Ctx.AdsBlend, w.Occluded, m.ThroughWalls || m.Kind == MarkerKind.HighPriority));
            w.SetColor(color);

            // Edge arrow pointing out of the screen towards the marker.
            if (w.Arrow.enabled != clamped) w.Arrow.enabled = clamped;
            if (clamped)
            {
                var rad = angle * Mathf.Deg2Rad;
                w.Arrow.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 26f;
                w.Arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            // Label: on-screen only (FOLLOW chevrons always say FOLLOW).
            var labelText = m.Kind == MarkerKind.Follow && string.IsNullOrEmpty(m.Label) ? "FOLLOW" : m.Label;
            var showLabel = !clamped && !string.IsNullOrEmpty(labelText);
            if (w.LabelVisible != showLabel)
            {
                w.LabelVisible = showLabel;
                w.Label.enabled = showLabel;
            }
            if (showLabel && !ReferenceEquals(w.LabelShown, labelText))
            {
                w.LabelShown = labelText;
                w.Label.SetText(labelText);
            }

            // Distance in metres (GDD §11).
            var showDistance = m.ShowDistance || m.Kind != MarkerKind.Follow;
            if (w.Distance.enabled != showDistance) w.Distance.enabled = showDistance;
            if (showDistance)
            {
                var d = HudMath.RoundDistance(dist);
                if (d != w.DistanceShown)
                {
                    w.DistanceShown = d;
                    w.DistanceText.Clear().Append(d).Append(" m").ApplyTo(w.Distance);
                }
            }
        }

        private void UpdateOcclusion(Vector3 eyePos, IReadOnlyList<MarkerDef> markers, int count)
        {
            // One ray per frame across the active widgets (TECH_PLAN R2: no allocation, bounded cost).
            if (VisibleCount == 0 && count == 0) return;
            for (var tries = 0; tries < Pool; tries++)
            {
                _occlusionCursor = (_occlusionCursor + 1) % Pool;
                var w = _widgets[_occlusionCursor];
                if (!w.Active || w.MarkerId == null) continue;
                for (var i = 0; i < count; i++)
                {
                    var m = markers[i];
                    if (!string.Equals(m.Id, w.MarkerId)) continue;
                    var world = WorldPosition(m);
                    var dir = world - eyePos;
                    var len = dir.magnitude;
                    if (len < 1f)
                    {
                        w.Occluded = false;
                        return;
                    }
                    var end = eyePos + dir * ((len - 0.6f) / len);
                    w.Occluded = Physics.Linecast(eyePos, end, OcclusionMask, QueryTriggerInteraction.Ignore);
                    return;
                }
                return;
            }
        }

        private UnityEngine.Sprite SpriteFor(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.DiamondHollow: return Ctx.Sprite(UISpriteId.DiamondHollow);
                case MarkerKind.HighPriority: return Ctx.Sprite(UISpriteId.HighPriority);
                case MarkerKind.TargetX: return Ctx.Sprite(UISpriteId.TargetX);
                case MarkerKind.Follow: return Ctx.Sprite(UISpriteId.Chevron);
                case MarkerKind.Intel: return Ctx.Sprite(UISpriteId.IconIntel);
                case MarkerKind.Ring: return Ctx.Sprite(UISpriteId.Ring);
                default: return Ctx.Sprite(UISpriteId.Diamond);
            }
        }

        /// <summary>World position of a marker with the default lift for markers that follow an actor.</summary>
        internal static Vector3 WorldPosition(in MarkerDef m)
        {
            if (m.Target != null && m.Offset == Vector3.zero)
                return m.Target.position + Vector3.up * (m.Kind == MarkerKind.Follow ? FollowLift : ActorLift);
            return m.WorldPosition;
        }

        /// <summary>
        /// True for optional intel (GDD §7.12: its icon shows only within 6 m): an Intel marker whose objective is
        /// optional, or whose id is an optional-intel id ("B7.O1", "B8.O2").
        /// </summary>
        internal static bool IsOptionalIntel(IObjectiveService objectives, in MarkerDef m)
        {
            if (m.Kind != MarkerKind.Intel) return false;
            var id = m.Id;
            if (string.IsNullOrEmpty(id)) return false;
            var active = objectives != null ? objectives.Active : null;
            var n = active != null ? active.Count : 0;
            for (var i = 0; i < n; i++)
            {
                var o = active[i];
                if (string.Equals(o.Id, id)) return o.Optional;
            }
            // "<beat>.O<n>" pattern.
            var dot = id.LastIndexOf('.');
            return dot >= 0 && dot + 2 < id.Length && id[dot + 1] == 'O' && char.IsDigit(id[dot + 2]);
        }
    }
}
