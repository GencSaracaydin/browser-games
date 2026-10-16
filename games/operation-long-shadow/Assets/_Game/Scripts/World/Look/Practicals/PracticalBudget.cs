using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// Grants realtime lights to the nearest practicals (GDD §9.5, G4): at 4 Hz it counts the fixed lights that are on
    /// (non-budgeted practicals: floods, flares, fixed fills, fire), keeps <see cref="PracticalBudgetMath.MuzzleReserve"/>
    /// slots for WP03's pooled muzzle flashes, and lets the best-scored budgeted practicals within their realtime
    /// distance switch their light on (they fade over 0.25 s); everything else stays emissive-only. Practicals register
    /// themselves; one instance lives on <c>PF_LookRig</c>. Without an instance no budgeted practical gets a light.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.LightStage + 3)]
    [DisallowMultipleComponent]
    public sealed class PracticalBudget : MonoBehaviour
    {
        private static readonly List<PracticalLight> s_all = new List<PracticalLight>(128);
        private static readonly List<Light> s_fixed = new List<Light>(16);
        private static PracticalBudget s_active;

        [SerializeField, Tooltip("Decision interval in seconds.")]
        private float _interval = 0.25f;
        [SerializeField, Range(0, 12)] private int _maxPracticals = PracticalBudgetMath.MaxPracticals;
        [SerializeField, Range(0, 8)] private int _muzzleReserve = PracticalBudgetMath.MuzzleReserve;
        [SerializeField, Range(0, 24)] private int _maxAdditionalLights = PracticalBudgetMath.MaxAdditionalLights;

        private readonly List<PracticalBudgetMath.Candidate> _candidates = new List<PracticalBudgetMath.Candidate>(64);
        private readonly List<PracticalLight> _candidateLights = new List<PracticalLight>(64);
        private ServiceRef<IPlayerRig> _rig;
        private float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
            s_fixed.Clear();
            s_active = null;
        }

        /// <summary>Registered practicals.</summary>
        public static IReadOnlyList<PracticalLight> All => s_all;

        /// <summary>Practicals granted a realtime light at the last decision.</summary>
        public static int GrantedCount { get; private set; }

        /// <summary>Fixed lights on at the last decision.</summary>
        public static int FixedCount { get; private set; }

        /// <summary>Registers a practical (idempotent).</summary>
        public static void Register(PracticalLight p)
        {
            if (p != null && !s_all.Contains(p)) s_all.Add(p);
        }

        /// <summary>Unregisters a practical.</summary>
        public static void Unregister(PracticalLight p) => s_all.Remove(p);

        /// <summary>Registers a fixed realtime light outside the practical system (floods, flares): counted while enabled.</summary>
        public static void RegisterFixed(Light light)
        {
            if (light != null && !s_fixed.Contains(light)) s_fixed.Add(light);
        }

        /// <summary>Unregisters a fixed light.</summary>
        public static void UnregisterFixed(Light light) => s_fixed.Remove(light);

        private void OnEnable() => s_active = this;

        private void OnDisable()
        {
            if (s_active == this) s_active = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _interval;
            Decide();
        }

        /// <summary>Runs one decision now.</summary>
        public void Decide()
        {
            var eye = EyePose(out var forward);
            var fixedOn = 0;
            for (var i = s_fixed.Count - 1; i >= 0; i--)
            {
                var l = s_fixed[i];
                if (l == null) s_fixed.RemoveAt(i);
                else if (l.isActiveAndEnabled && l.intensity > 0f) fixedOn++;
            }
            _candidates.Clear();
            _candidateLights.Clear();
            for (var i = s_all.Count - 1; i >= 0; i--)
            {
                var p = s_all[i];
                if (p == null)
                {
                    s_all.RemoveAt(i);
                    continue;
                }
                if (!p.Budgeted)
                {
                    if (p.RealtimeActive) fixedOn++;
                    continue;
                }
                if (!p.WantsRealtime)
                {
                    p.SetBudgetAllowed(false);
                    continue;
                }
                var to = p.transform.position - eye;
                var d = to.magnitude;
                if (d > p.MaxRealtimeDistance)
                {
                    p.SetBudgetAllowed(false);
                    continue;
                }
                var inFront = Vector3.Dot(to, forward) > -2f;
                _candidates.Add(new PracticalBudgetMath.Candidate
                {
                    Index = _candidateLights.Count,
                    Score = PracticalBudgetMath.Score(d, p.Priority, inFront, p.RealtimeActive),
                });
                _candidateLights.Add(p);
            }

            var slots = PracticalBudgetMath.Slots(fixedOn, _muzzleReserve, _maxPracticals, _maxAdditionalLights);
            var win = PracticalBudgetMath.SelectBest(_candidates, slots);
            for (var k = 0; k < _candidates.Count; k++) _candidateLights[_candidates[k].Index].SetBudgetAllowed(k < win);
            GrantedCount = win;
            FixedCount = fixedOn;
        }

        private Vector3 EyePose(out Vector3 forward)
        {
            var rig = _rig.Value;
            Transform t = null;
            if (rig != null && rig.WorldCamera != null && rig.WorldCamera.isActiveAndEnabled) t = rig.WorldCamera.transform;
            if (t == null && Camera.main != null) t = Camera.main.transform;
            if (t == null)
            {
                forward = Vector3.forward;
                return transform.position;
            }
            forward = t.forward;
            return t.position;
        }
    }
}
