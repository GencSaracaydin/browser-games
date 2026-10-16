namespace LongShadow.Mission.Data
{
    /// <summary>Mission flag ids (<c>IMissionDirector.Flag/SetFlag</c>; saved with the checkpoint snapshot).</summary>
    public static class MissionFlags
    {
        /// <summary>Beat 2: the sync shot was clean (GDD §7.2 "Clean").</summary>
        public const string B2Clean = "B2.Clean";
        /// <summary>Beat 2: the failure branch ran (D016 / alert).</summary>
        public const string B2Alerted = "B2.Alerted";
        /// <summary>Beat 2: the Weighbridge gate is open.</summary>
        public const string B2GateOpen = "B2.GateOpen";
        /// <summary>Beat 4: the cinema sky swap ran.</summary>
        public const string B4SkySwap = "B4.SkySwap";
        /// <summary>Beat 5: MID-A finished.</summary>
        public const string B5MidADone = "B5.MidADone";
        /// <summary>Beat 6: the wall is blown.</summary>
        public const string B6Blown = "B6.Blown";
        /// <summary>Beat 8: MID-B finished.</summary>
        public const string B8MidBDone = "B8.MidBDone";
        /// <summary>Beat 9: the drill reached 100 %.</summary>
        public const string B9Drilled = "B9.Drilled";
        /// <summary>Beat 10: Magpie waved off once.</summary>
        public const string B10WaveOff = "B10.WaveOff";
        /// <summary>Prefix of tutorial-shown flags ("Tut.slide").</summary>
        public const string TutorialPrefix = "Tut.";
    }
}
