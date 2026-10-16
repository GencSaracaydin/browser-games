using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Ambience
{
    /// <summary>
    /// When and how loud one ambience bed plays (GDD §10.2 "Ambience beds change with the LightStage", §9.2a): a
    /// LightStage window with soft edges, a beat window, a gain per acoustic zone (outdoor beds are heard through walls
    /// at a low level, interior beds only inside), an optional proximity anchor (the station generator) and an optional
    /// world condition. <see cref="Weight"/> is pure, so the table is unit-tested; the <see cref="AmbienceDirector"/>
    /// smooths the result over <see cref="FadeSeconds"/>.
    /// </summary>
    public sealed class AmbienceBedRule
    {
        /// <summary>Number of <see cref="ZoneType"/> values (the length of <see cref="ZoneGains"/>).</summary>
        public const int ZoneCount = 7;

        /// <summary>Cue id in LIB_Audio (<c>AMB_*</c>, or a set-piece loop used as a bed).</summary>
        public readonly string CueId;
        /// <summary>Linear level at full weight (before the Ambience category gain).</summary>
        public readonly float Level;
        /// <summary>LightStage window start.</summary>
        public readonly float SFrom;
        /// <summary>LightStage window end.</summary>
        public readonly float STo;
        /// <summary>Width (in <c>s</c> units) of the linear ramp centred on each window edge; 0 = a step.</summary>
        public readonly float SRamp;
        /// <summary>First beat the bed plays in (negative = from the start).</summary>
        public readonly int BeatFrom;
        /// <summary>Last beat the bed plays in (negative = to the end).</summary>
        public readonly int BeatTo;
        /// <summary>Gain per <see cref="ZoneType"/> (index = enum value).</summary>
        public readonly float[] ZoneGains;
        /// <summary>Proximity anchor (world position).</summary>
        public readonly Vector3 Anchor;
        /// <summary>Full level within this horizontal distance of <see cref="Anchor"/>.</summary>
        public readonly float NearRadius;
        /// <summary>Silent beyond this distance; ≤ 0 disables proximity.</summary>
        public readonly float FarRadius;
        /// <summary>Extra world condition.</summary>
        public readonly AmbienceCondition Condition;
        /// <summary>Seconds the director takes to follow a change (~63 %).</summary>
        public readonly float FadeSeconds;
        /// <summary>Faster fade used when <see cref="Condition"/> turns false (a power cut stops the hum at once).</summary>
        public readonly float CutFadeSeconds;

        /// <summary>Creates a rule.</summary>
        public AmbienceBedRule(string cueId, float level, float sFrom, float sTo, float sRamp, int beatFrom, int beatTo,
                               float[] zoneGains, float fadeSeconds, AmbienceCondition condition = AmbienceCondition.None,
                               Vector3 anchor = default, float nearRadius = 0f, float farRadius = 0f, float cutFadeSeconds = 0.25f)
        {
            CueId = cueId;
            Level = Mathf.Max(0f, level);
            SFrom = sFrom;
            STo = sTo;
            SRamp = Mathf.Max(0f, sRamp);
            BeatFrom = beatFrom;
            BeatTo = beatTo;
            ZoneGains = zoneGains != null && zoneGains.Length == ZoneCount ? zoneGains : new float[ZoneCount];
            FadeSeconds = Mathf.Max(0.05f, fadeSeconds);
            Condition = condition;
            Anchor = anchor;
            NearRadius = Mathf.Max(0f, nearRadius);
            FarRadius = farRadius;
            CutFadeSeconds = Mathf.Max(0.02f, cutFadeSeconds);
        }

        /// <summary>Gain of the bed in a zone.</summary>
        public float ZoneGain(ZoneType zone)
        {
            var i = (int)zone;
            return i >= 0 && i < ZoneGains.Length ? ZoneGains[i] : 0f;
        }

        /// <summary>True when the world condition holds.</summary>
        public bool ConditionHolds(in AmbienceContext c) =>
            Condition != AmbienceCondition.TransmittersOn || c.TransmittersOn;

        /// <summary>True when <paramref name="beat"/> is inside the beat window (unknown beats always are).</summary>
        public bool InBeat(int beat)
        {
            if (beat < 0) return true;
            if (BeatFrom >= 0 && beat < BeatFrom) return false;
            if (BeatTo >= 0 && beat > BeatTo) return false;
            return true;
        }

        /// <summary>LightStage weight 0..1 of <paramref name="s"/> with the soft edges.</summary>
        public float StageWeight(float s)
        {
            if (float.IsNaN(s)) return 0f;
            return Edge(s, SFrom, SRamp) * (1f - Edge(s, STo, SRamp));
        }

        /// <summary>Target linear gain of the bed in <paramref name="c"/> (0 = silent).</summary>
        public float Weight(in AmbienceContext c)
        {
            if (!InBeat(c.Beat) || !ConditionHolds(c)) return 0f;
            var w = Level * StageWeight(c.S) * ZoneGain(c.Zone);
            if (w <= 0f) return 0f;
            if (FarRadius > 0f)
            {
                var d = new Vector2(c.Listener.x - Anchor.x, c.Listener.z - Anchor.z).magnitude;
                if (d >= FarRadius) return 0f;
                if (d > NearRadius) w *= 1f - (d - NearRadius) / Mathf.Max(0.01f, FarRadius - NearRadius);
            }
            return w;
        }

        // 0 below the edge, 1 above it, linear over the ramp centred on it (step when the ramp is 0: s ≥ edge is 1).
        private static float Edge(float s, float edge, float ramp)
        {
            if (ramp <= 0f) return s >= edge ? 1f : 0f;
            return Mathf.Clamp01((s - (edge - ramp * 0.5f)) / ramp);
        }
    }
}
