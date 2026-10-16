namespace LongShadow.Player.Dev
{
    /// <summary>One measured value of <see cref="MovementTelemetry"/> against its GDD target.</summary>
    public struct TelemetryResult
    {
        /// <summary>What was measured ("Speed.Sprint", "Jump.Apex", "Vault.Duration", ...).</summary>
        public string Metric;
        /// <summary>Measured value.</summary>
        public float Measured;
        /// <summary>GDD target.</summary>
        public float Target;
        /// <summary>Allowed deviation in percent.</summary>
        public float TolerancePercent;
        /// <summary>Signed deviation in percent.</summary>
        public float DeviationPercent => Target != 0f ? (Measured - Target) / Target * 100f : 0f;
        /// <summary>True when within tolerance.</summary>
        public bool Pass;

        /// <summary>"Speed.Sprint 6.87 (target 6.90, -0.4 %) PASS".</summary>
        public override string ToString() =>
            Metric + " " + Measured.ToString("0.000") + " (target " + Target.ToString("0.000") + ", " +
            DeviationPercent.ToString("+0.0;-0.0;0.0") + " %) " + (Pass ? "PASS" : "FAIL");
    }
}
