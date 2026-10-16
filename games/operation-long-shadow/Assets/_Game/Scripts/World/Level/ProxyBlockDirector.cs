using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// Decides, for every far-proxy block, whether the real section geometry or the baked proxy shows (TECH_PLAN D7, GDD §6.7):
    /// the proxy hides only while its section is loaded and the viewer is within the swap distance (90 m), with a 10 m
    /// hysteresis band standing in for the cross-fade. Runs at 5 Hz from <c>S9_TownFar</c>; the viewer is the player eye
    /// (<see cref="IPlayerRig"/>) or the main camera during cinematics.
    /// </summary>
    public sealed class ProxyBlockDirector : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds between visibility passes.")]
        private float _interval = 0.2f;
        [SerializeField, Tooltip("Width of the hand-over band (TECH_PLAN D7: 10 m).")]
        private float _band = Data.WorldConst.FarProxyFadeBand;

        private ServiceRef<IPlayerRig> _rig;
        private float _next;

        private void OnEnable()
        {
            GameEvents.SectionLoaded += OnSectionChanged;
            GameEvents.SectionUnloaded += OnSectionChanged;
        }

        private void OnDisable()
        {
            GameEvents.SectionLoaded -= OnSectionChanged;
            GameEvents.SectionUnloaded -= OnSectionChanged;
        }

        private void OnSectionChanged(SectionId id) => _next = 0f;

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _interval;
            Refresh(ViewerPosition(out var known), known);
        }

        /// <summary>Applies the visibility rule for a viewer position (tests and the dev console call it directly).</summary>
        public void Refresh(Vector3 viewer, bool viewerKnown)
        {
            var pairs = ProxyBlockRegistry.Pairs;
            for (var i = 0; i < pairs.Count; i++)
            {
                var p = pairs[i];
                if (p.Real == null)
                {
                    if (p.Proxy != null) p.Proxy.SetVisible(true);
                    p.ShowingReal = false;
                    p.Decided = true;
                    continue;
                }
                if (p.Proxy == null)
                {
                    p.Real.SetVisible(true);
                    p.ShowingReal = true;
                    p.Decided = true;
                    continue;
                }
                bool showReal;
                if (!viewerKnown) showReal = true;
                else
                {
                    var d = p.Real.Rect.DistanceTo(viewer);
                    var swap = p.Real.SwapDistance;
                    if (!p.Decided) showReal = d < swap;
                    else if (p.ShowingReal) showReal = d < swap + _band * 0.5f;
                    else showReal = d < swap - _band * 0.5f;
                }
                p.Real.SetVisible(showReal);
                p.Proxy.SetVisible(!showReal);
                p.ShowingReal = showReal;
                p.Decided = true;
            }
        }

        private Vector3 ViewerPosition(out bool known)
        {
            var rig = _rig.Value;
            if (rig != null && rig.Eye != null && rig.WorldCamera != null && rig.WorldCamera.isActiveAndEnabled)
            {
                known = true;
                return rig.Eye.position;
            }
            var cam = Camera.main;
            if (cam != null)
            {
                known = true;
                return cam.transform.position;
            }
            known = rig != null && rig.Eye != null;
            return known ? rig.Eye.position : Vector3.zero;
        }
    }
}
