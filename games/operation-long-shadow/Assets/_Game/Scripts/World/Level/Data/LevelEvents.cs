using LongShadow.Core;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// <see cref="LongShadow.Core.GameEvents.MissionEvent"/> ids the Level components listen to or raise. The values are the
    /// literals WP07 (<c>LongShadow.Mission.Data.MissionEvents</c>) and WP04 (<c>LongShadow.AI.Common.AIEvents</c>) use;
    /// they are requested as Core <c>SequenceIds</c> constants (Docs/CORE_CHANGES.md) and duplicated here until then,
    /// because Level may reference only Core.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.SequenceIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class LevelEvents
    {
        /// <summary>Beat 2: the Weighbridge chain is cut and the gate swings open (WP07).</summary>
        public const string WeighbridgeOpen = SequenceIds.WeighbridgeOpen;
        /// <summary>Beat 3: the first awning tore (raised by <see cref="AwningPanel"/> for D023; WP07 listens).</summary>
        public const string AwningTorn = SequenceIds.AwningTorn;
        /// <summary>Beat 4: E4-01 died 1.5 s ago and the fuel tank ignited (WP04): light the static fire prop.</summary>
        public const string TechnicalIgnited = SequenceIds.B4TechnicalIgnited;
        /// <summary>Beat 6: the breach detonated (WP07 raises it; WP04 listens): swap the wall to its blown state.</summary>
        public const string BreachBlast = SequenceIds.B6Blast;
        /// <summary>Beat 6: floods F1-F4 slam on 4 s after the blast (WP07).</summary>
        public const string FloodsOn = SequenceIds.FloodsOn;
        /// <summary>Beat 9: the fourth rack died; emergency lighting (WP07, G11).</summary>
        public const string TransmitterDead = SequenceIds.TransmitterDead;
        /// <summary>Beat 11: the exfil rail starts (WP07).</summary>
        public const string ExfilStart = SequenceIds.ExfilStart;
        /// <summary>Beat 11: the Clock Square trucks drive in (WP07, exfil t 16).</summary>
        public const string ExfilTrucks = SequenceIds.ExfilTrucks;
        /// <summary>MID-A starts (Core <c>SequenceIds.MidAStart</c>).</summary>
        public const string MidAStart = LongShadow.Core.SequenceIds.MidAStart;
    }
}
