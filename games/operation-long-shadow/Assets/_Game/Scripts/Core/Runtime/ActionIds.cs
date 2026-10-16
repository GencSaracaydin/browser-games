namespace LongShadow.Core
{
    /// <summary>
    /// Action ids for <see cref="ICharacterBody.PlayAction"/>, <see cref="ISquadService.PlayScripted"/> and the WP09
    /// CharacterActionClip (TECH_PLAN §7.7, §7.8; CINEMATIC_DIRECTION §2). WP05 maps each id to an animator action index
    /// (<c>Action</c> int + <c>ActionTrigger</c>) in AC_Soldier; an unknown id logs once and completes immediately.
    /// </summary>
    public static class ActionIds
    {
        // ---- squad scripted actions (TECH_PLAN §7.7)
        /// <summary>B2 Weighbridge gate chain, 3 s.</summary>
        public const string BoltCut = "BoltCut";
        /// <summary>B3 white smoke across the street (smoke lasts 20 s).</summary>
        public const string ThrowSmokeWhite = "ThrowSmokeWhite";
        /// <summary>B6 breach charge, 3 s.</summary>
        public const string PlantSatchel = "PlantSatchel";
        /// <summary>B5 hold signal (MID-A).</summary>
        public const string FistHold = "FistHold";
        /// <summary>B10 green smoke at the LZ.</summary>
        public const string PopSmokeGreen = "PopSmokeGreen";
        /// <summary>B1 and B8 Sparrow photographs.</summary>
        public const string Photograph = "Photograph";
        /// <summary>B9 safe drill loop (Tinker).</summary>
        public const string DrillLoop = "DrillLoop";
        /// <summary>B6 chemlights.</summary>
        public const string CrackChemlight = "CrackChemlight";
        /// <summary>Door kick with the stack behind (Hall door).</summary>
        public const string KickDoorStack = "KickDoorStack";
        /// <summary>B11 Hollis hauls Sparrow's bag into the cabin.</summary>
        public const string HaulBag = "HaulBag";

        // ---- common body actions (WP04 brains, WP05 bodies)
        /// <summary>Upper-body reload.</summary>
        public const string Reload = "Reload";
        /// <summary>Overhand grenade throw (raises AnimEvent ThrowRelease).</summary>
        public const string Throw = "Throw";
        /// <summary>Vault over a 0.6-1.4 m obstacle (NavMeshLink traversal).</summary>
        public const string Vault = "Vault";
        /// <summary>Climb up 1.4-2.1 m (NavMeshLink traversal).</summary>
        public const string Climb = "Climb";
        /// <summary>Drop down a ledge (R3/R4 drops).</summary>
        public const string Drop = "Drop";
        /// <summary>Scripted ladder climb (ladder L).</summary>
        public const string LadderClimb = "LadderClimb";
        /// <summary>Hand signal "move" / "follow".</summary>
        public const string HandSignal = "HandSignal";
        /// <summary>RPG raise telegraph (1.2 s, GDD §14.1).</summary>
        public const string RpgRaise = "RpgRaise";

        // ---- insertion cold open (CINEMATIC_DIRECTION §2, played through the WP09 CharacterActionClip)
        /// <summary>Seated in the helicopter cabin.</summary>
        public const string SitHeli = "SitHeli";
        /// <summary>Drop from the skid onto the sand (cut I-8 starts at the landing).</summary>
        public const string SkidDrop = "SkidDrop";
        /// <summary>Crouch-run with a rifle.</summary>
        public const string CrouchRun = "CrouchRun";
        /// <summary>Prone idle on the crest.</summary>
        public const string ProneIdle = "ProneIdle";
        /// <summary>Tinker keys the PRC-77 handset (IK pose).</summary>
        public const string HandsetToEar = "HandsetToEar";
        /// <summary>Sparrow raises the camera (IK pose).</summary>
        public const string RaiseCamera = "RaiseCamera";

        // ---- WP05 extras (booth, MID-B, outro, insertion, idle variety; LongShadow.Characters ActionTable)
        /// <summary>One shot fired: procedural upper-body recoil (no state; completes immediately).</summary>
        public const string Fire = "Fire";
        /// <summary>Booth: seated nervous idle (Calder in the chair).</summary>
        public const string SitIdle = "SitIdle";
        /// <summary>Booth: seated, answering (MID-B reel, intro).</summary>
        public const string SitTalk = "SitTalk";
        /// <summary>Seated with the arms crossed (interrogator, waiting).</summary>
        public const string SitArmsCrossed = "SitArmsCrossed";
        /// <summary>Handset/phone conversation pose, arms only.</summary>
        public const string PhoneTalk = "PhoneTalk";
        /// <summary>Outro row 7: the interrogator reaches to BOOTH.Scrambler.</summary>
        public const string ReachToggle = "ReachToggle";
        /// <summary>MID-B shot 5: the seated man lifts his head.</summary>
        public const string HeadLift = "HeadLift";
        /// <summary>MID-B shot 6: the seated man goes limp.</summary>
        public const string GoLimp = "GoLimp";
        /// <summary>Sentry looks around (idle variety).</summary>
        public const string LookAround = "LookAround";
        /// <summary>Kneel on one knee (holds).</summary>
        public const string Kneel = "Kneel";
        /// <summary>A squadmate glances at his wristwatch.</summary>
        public const string CheckWatch = "CheckWatch";
        /// <summary>INSERTION I-3 Hollis: seated, one finger raised ("One minute!"), arms only.</summary>
        public const string OneFinger = "OneFinger";
        /// <summary>One-hand wave (arms only).</summary>
        public const string Wave = "Wave";
        /// <summary>Standing conversation with gestures.</summary>
        public const string Talk = "Talk";
        /// <summary>Ends any looping/timed action and returns to locomotion (completes immediately).</summary>
        public const string Stop = "Stop";
        /// <summary>
        /// Beat 2 sentries: smoking idle loop (cigarette to the mouth every few seconds); ended by <see cref="Stop"/> or any
        /// other action (GDD §7.2).
        /// </summary>
        public const string SmokeIdle = "SmokeIdle";
    }
}
