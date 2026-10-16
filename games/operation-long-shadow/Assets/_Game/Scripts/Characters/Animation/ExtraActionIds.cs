using LongShadow.Core;

namespace LongShadow.Characters.Animation
{
    /// <summary>
    /// Action ids WP05 supports beyond Core <c>ActionIds</c> (requested as an additive Core change in
    /// Docs/CORE_CHANGES.md, 2026-10-16 "WP05 extra action ids"). Until Core carries them, callers in other packages pass
    /// the same string literals; the values must never change.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.ActionIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class ExtraActionIds
    {
        /// <summary>One shot fired: procedural upper-body recoil (no state; completes immediately).</summary>
        public const string Fire = ActionIds.Fire;
        /// <summary>Booth: seated nervous idle (Calder in the chair).</summary>
        public const string SitIdle = ActionIds.SitIdle;
        /// <summary>Booth: seated, answering (MID-B reel, intro).</summary>
        public const string SitTalk = ActionIds.SitTalk;
        /// <summary>Seated with the arms crossed (interrogator, waiting).</summary>
        public const string SitArmsCrossed = ActionIds.SitArmsCrossed;
        /// <summary>Booth/heli: phone or handset conversation pose (interrogator D091).</summary>
        public const string PhoneTalk = ActionIds.PhoneTalk;
        /// <summary>Booth outro: the interrogator reaches to the scrambler toggle (IK to BOOTH.Scrambler).</summary>
        public const string ReachToggle = ActionIds.ReachToggle;
        /// <summary>Booth reel (MID-B shot 5): the seated man lifts his head.</summary>
        public const string HeadLift = ActionIds.HeadLift;
        /// <summary>Booth reel (MID-B shot 6): the seated man goes limp.</summary>
        public const string GoLimp = ActionIds.GoLimp;
        /// <summary>Sentry looks around (B2 sentries, idle variety).</summary>
        public const string LookAround = ActionIds.LookAround;
        /// <summary>Kneel on one knee (holds; cover, cinematics).</summary>
        public const string Kneel = ActionIds.Kneel;
        /// <summary>A squadmate glances at his wristwatch (MID-A ambience, idle variety).</summary>
        public const string CheckWatch = ActionIds.CheckWatch;
        /// <summary>Seated: raises one finger, "One minute!" (INSERTION I-3, Hollis). Arms only: keeps the seat.</summary>
        public const string OneFinger = ActionIds.OneFinger;
        /// <summary>One-hand wave (arms only).</summary>
        public const string Wave = ActionIds.Wave;
        /// <summary>Standing conversation with gestures (briefings, cinematics).</summary>
        public const string Talk = ActionIds.Talk;
        /// <summary>Ends any looping/timed action and returns to locomotion (completes immediately).</summary>
        public const string Stop = ActionIds.Stop;
    }
}
