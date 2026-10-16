using UnityEngine;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// LightStage progress (GDD §9.1, TECH_PLAN §7.11): <c>progress = max(previous, 0.5 × objective completion + 0.5 ×
    /// critical-path progress)</c>, mapped into the beat's driver range and sent to <c>ILightStage.SetTarget</c> with 2 s
    /// smoothing, never decreasing. Scripted sequences (cinema sky swap, MID-A) hold <c>ILightStage.Override</c>; while
    /// overridden the driver only tracks the floor. Beat 4 and Beat 5 narrow the range (the sky must not change outside the
    /// cinema; the sun is held at 1.5° until MID-A): <see cref="SetRange"/>. Pure (unit-tested).
    /// </summary>
    public sealed class LightProgressDriver
    {
        /// <summary>Smoothing time constant passed to SetTarget (GDD §9.1 "A jump is smoothed over 2 s").</summary>
        public const float Smoothing = 2f;
        /// <summary>Smallest target change that is sent again.</summary>
        public const float SendThreshold = 0.002f;

        private float _sentTarget = -1f;

        /// <summary>Beat being driven.</summary>
        public int Beat { get; private set; }
        /// <summary>Progress 0..1 (monotonic within the beat).</summary>
        public float Progress { get; private set; }
        /// <summary>Lower end of the driver range.</summary>
        public float RangeMin { get; private set; }
        /// <summary>Upper end of the driver range.</summary>
        public float RangeMax { get; private set; }
        /// <summary>Lowest target allowed (the highest s reached so far in the beat).</summary>
        public float Floor { get; private set; }
        /// <summary>Last computed target.</summary>
        public float Target { get; private set; }
        /// <summary>When false the driver computes nothing (intro, outro, scripted beat 11 timing).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Starts a beat: range = [sStart, sEnd], progress 0, floor = max(sStart, current s).</summary>
        public void BeginBeat(int beat, float sStart, float sEnd, float currentS)
        {
            Beat = beat;
            Progress = 0f;
            RangeMin = sStart;
            RangeMax = sEnd;
            Floor = Mathf.Max(sStart, Mathf.Min(currentS, sEnd));
            Target = Floor;
            _sentTarget = -1f;
        }

        /// <summary>Narrows the range (Beat 4 before/after the sky swap, Beat 5 before MID-A). Progress restarts in the new range.</summary>
        public void SetRange(float min, float max)
        {
            RangeMin = min;
            RangeMax = Mathf.Max(min, max);
            Progress = 0f;
            Floor = Mathf.Clamp(Floor, RangeMin, RangeMax);
            if (Floor < RangeMin) Floor = RangeMin;
            Target = Mathf.Max(Target, Floor);
        }

        /// <summary>Raises the floor (after an override moved s up, e.g. the sky swap to 1.35).</summary>
        public void RaiseFloor(float s)
        {
            if (s > Floor) Floor = Mathf.Min(s, Mathf.Max(RangeMax, s));
            if (Target < Floor) Target = Floor;
        }

        /// <summary>
        /// Computes the target for the current objective completion and critical-path progress (both 0..1). Returns true
        /// when the target moved by at least <see cref="SendThreshold"/> since it was last sent (call SetTarget then).
        /// </summary>
        public bool Update(float objectiveCompletion01, float pathProgress01, bool overridden, float currentS)
        {
            if (!Enabled) return false;
            if (overridden)
            {
                RaiseFloor(currentS);
                _sentTarget = -1f;
                return false;
            }
            var p = 0.5f * Mathf.Clamp01(objectiveCompletion01) + 0.5f * Mathf.Clamp01(pathProgress01);
            if (p > Progress) Progress = p;
            var s = Mathf.Lerp(RangeMin, RangeMax, Progress);
            Target = Mathf.Max(Floor, s);
            Floor = Target;
            if (_sentTarget >= 0f && Mathf.Abs(Target - _sentTarget) < SendThreshold) return false;
            _sentTarget = Target;
            return true;
        }
    }
}
