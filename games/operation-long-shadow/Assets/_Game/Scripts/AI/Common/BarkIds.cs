namespace LongShadow.AI.Common
{
    /// <summary>
    /// Enemy bark line ids (Docs/DIALOGUE.json, GDD §14.4). Played through <c>IVOService.PlayBark</c> by
    /// <see cref="LongShadow.AI.Enemy.EnemyBarker"/> with per-id and global cooldowns. A test checks every id exists.
    /// </summary>
    public static class BarkIds
    {
        // ---- Legion cant (BK_LEG_01-15)
        /// <summary>"Tovek!" alert / first contact.</summary>
        public const string LegContact = "BK_LEG_01";
        /// <summary>"Druna, druna!" wave advances (push).</summary>
        public const string LegPush = "BK_LEG_02";
        /// <summary>"Vossek!" flank order.</summary>
        public const string LegFlank = "BK_LEG_03";
        /// <summary>"Pirrha!" grenade thrown, or one lands within 6 m.</summary>
        public const string LegGrenade = "BK_LEG_04";
        /// <summary>"Ollo, ollo!" reloading.</summary>
        public const string LegReload = "BK_LEG_05";
        /// <summary>"Tevo vrask!" cover me (moving between cover).</summary>
        public const string LegCoverMe = "BK_LEG_06";
        /// <summary>"Mennik!" man down (ally killed within 15 m).</summary>
        public const string LegManDown = "BK_LEG_07";
        /// <summary>"Fesko!" lights (Beat 6 floods; played by the mission script).</summary>
        public const string LegLights = "BK_LEG_08";
        /// <summary>"Dovvo!" fall back (morale break at 60 % of a wave dead).</summary>
        public const string LegFallBack = "BK_LEG_09";
        /// <summary>"Zaroud-ka!" Beat 10 trucks unload.</summary>
        public const string LegForZaroud = "BK_LEG_10";
        /// <summary>"Grakko!" first sight of the squad (Beats 2-4).</summary>
        public const string LegAmericans = "BK_LEG_11";
        /// <summary>"Brekka!" reinforcements running in.</summary>
        public const string LegHurry = "BK_LEG_12";
        /// <summary>"Nehh!" pain, non-lethal hit.</summary>
        public const string LegPain = "BK_LEG_13";
        /// <summary>"Ruvo tesk, ollan?" Beat 2 sentry chatter.</summary>
        public const string LegSentryAsk = "BK_LEG_14";
        /// <summary>"Ollan, ollan." Beat 2 sentry reply.</summary>
        public const string LegSentryReply = "BK_LEG_15";

        // ---- Greyspeak (BK_GRY_01-09), always over their radios
        /// <summary>"Hukt!" first contact (after 3 clicks).</summary>
        public const string GryContact = "BK_GRY_01";
        /// <summary>"Trekt, isk!" pair flank start (after 2 clicks).</summary>
        public const string GrySuppressMove = "BK_GRY_02";
        /// <summary>"Drezo! Ulsk-tavv, drezo!" Beat 6 gatehouse team holds.</summary>
        public const string GryHold = "BK_GRY_03";
        /// <summary>"Kisk!" Greycoat throws a frag.</summary>
        public const string GryGrenade = "BK_GRY_04";
        /// <summary>"Ottrek, ottrek." Beat 5 marksman settles.</summary>
        public const string GryMarksman = "BK_GRY_05";
        /// <summary>"Selt." area check after the player breaks contact.</summary>
        public const string GryClear = "BK_GRY_06";
        /// <summary>"Orvak." target re-acquired (with 3 clicks).</summary>
        public const string GryEyesOn = "BK_GRY_07";
        /// <summary>"Vaun dret!" pair retreat when one member dies.</summary>
        public const string GryFallBack = "BK_GRY_08";
        /// <summary>"Nolvek." Beat 10 NVG Greycoat, once, at the first flare.</summary>
        public const string GryItIsTime = "BK_GRY_09";

        /// <summary>Every bark id the AI can play (DIALOGUE.json check).</summary>
        public static readonly string[] All =
        {
            LegContact, LegPush, LegFlank, LegGrenade, LegReload, LegCoverMe, LegManDown, LegLights, LegFallBack,
            LegForZaroud, LegAmericans, LegHurry, LegPain, LegSentryAsk, LegSentryReply,
            GryContact, GrySuppressMove, GryHold, GryGrenade, GryMarksman, GryClear, GryEyesOn, GryFallBack, GryItIsTime,
        };
    }
}
