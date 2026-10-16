namespace LongShadow.Mission.Data
{
    /// <summary>
    /// DIALOGUE.json line ids used by the mission scripts (GDD §7, §8, §19). Scripts never write line ids as literals;
    /// the EditMode tests check every constant against Docs/DIALOGUE.json. Intro (D001-D006, D200-D207), climax (D082) and
    /// outro (D083-D092) lines are played by WP09's Timelines; the Mission fallback flows play them only when no
    /// ICinematicService is registered.
    /// </summary>
    public static class LineIds
    {
        /// <summary>INTERROGATOR: "Sergeant Calder. Eyes on the screens, not on me. October twenty-fir..."</summary>
        public const string D001 = "D001";
        /// <summary>TALLOW: "Sundial, Tallow. Three items: ledgers, film, codebook. The book is ..."</summary>
        public const string D002 = "D002";
        /// <summary>TALLOW: "Zaroud's station goes on the air at eighteen-forty. Sunset. You'll ..."</summary>
        public const string D003 = "D003";
        /// <summary>CALDER: "I told you. We got the book. It was on the helicopter."</summary>
        public const string D004 = "D004";
        /// <summary>INTERROGATOR: "Then why am I still asking?"</summary>
        public const string D005 = "D005";
        /// <summary>INTERROGATOR: "Start with the light, Nathan. You always remember the light."</summary>
        public const string D006 = "D006";
        /// <summary>HOLLIS: "Sun's at our backs. Only gift we get today. Glass the town."</summary>
        public const string D007 = "D007";
        /// <summary>SPARROW: "Orange armbands are Harrou's Legion. Grey ones at the station gate ..."</summary>
        public const string D008 = "D008";
        /// <summary>TINKER: "Clock tower's stuck at six-forty. Been stuck for years, they say."</summary>
        public const string D009 = "D009";
        /// <summary>SPARROW: "Weighbridge Gate. One truck, one gun on the bed. That's our door."</summary>
        public const string D010 = "D010";
        /// <summary>HOLLIS: "Grove, weighbridge, market, station. We're gone before last light. ..."</summary>
        public const string D011 = "D011";
        /// <summary>HARROU: "Ilo Zaroudan, veskri kael. Morrhan uvan talvi-sa."</summary>
        public const string D012 = "D012";
        /// <summary>TINKER: "Curfew siren's been howlin' twenty minutes. Anybody out on those st..."</summary>
        public const string D013 = "D013";
        /// <summary>HOLLIS: "Two at the pump house. You take left, I take right. Wait for my mark."</summary>
        public const string D014 = "D014";
        /// <summary>HOLLIS: "Three. Two. Mark."</summary>
        public const string D015 = "D015";
        /// <summary>HOLLIS: "He's running! Drop him!"</summary>
        public const string D016 = "D016";
        /// <summary>INTERROGATOR: "Clean. Quiet. You were always good at quiet."</summary>
        public const string D017 = "D017";
        /// <summary>HOLLIS: "Gunner on the truck! Calder, put him down!"</summary>
        public const string D018 = "D018";
        /// <summary>SPARROW: "Weighbridge clear. Gate's chained. Tinker?"</summary>
        public const string D019 = "D019";
        /// <summary>TINKER: "Chains are just a suggestion, cher."</summary>
        public const string D020 = "D020";
        /// <summary>HOLLIS: "Off the middle of the street. Stalls and doorways. Leapfrog."</summary>
        public const string D021 = "D021";
        /// <summary>SPARROW: "Balcony, right side, second floor. Machine gun!"</summary>
        public const string D022 = "D022";
        /// <summary>TINKER: "He's cuttin' the awnings to ribbons! Stay in the shade!"</summary>
        public const string D023 = "D023";
        /// <summary>HOLLIS: "Smoke out! Cross on the smoke!"</summary>
        public const string D024 = "D024";
        /// <summary>HARROU: "Americans. The market closes at five. You are trespassing in my town."</summary>
        public const string D025 = "D025";
        /// <summary>SPARROW: "Tinsmiths' Lane! They're coming around behind us, left side!"</summary>
        public const string D026 = "D026";
        /// <summary>TINKER: "Oranges and cordite. That's oranges ruined for me."</summary>
        public const string D027 = "D027";
        /// <summary>SPARROW: "Clock's stopped at six-forty. Same as the broadcast schedule."</summary>
        public const string D028 = "D028";
        /// <summary>HOLLIS: "Technical! Get off the arch! ...Calder, the cinema. Side door. Hit ..."</summary>
        public const string D029 = "D029";
        /// <summary>HOLLIS: "Cal, we can't hold this arch all day. Marquee. Now."</summary>
        public const string D030 = "D030";
        /// <summary>TINKER: "Lobby's dark, Cal. Check your corners."</summary>
        public const string D031 = "D031";
        /// <summary>HOLLIS: "Gunner's down! Good kill. On me."</summary>
        public const string D032 = "D032";
        /// <summary>SPARROW: "Greycoats, east street. Two. These ones know what they're doing."</summary>
        public const string D033 = "D033";
        /// <summary>HOLLIS: "Up top. Roof to roof to the station wall."</summary>
        public const string D034 = "D034";
        /// <summary>SPARROW: "Glint on the mast! Sniper!"</summary>
        public const string D035 = "D035";
        /// <summary>HOLLIS: "Water tanks. Move after he fires."</summary>
        public const string D036 = "D036";
        /// <summary>HOLLIS: "Sun's down. Now it's our time."</summary>
        public const string D037 = "D037";
        /// <summary>TINKER: "Hold up. My set's pickin' somethin' up... that ain't us."</summary>
        public const string D038 = "D038";
        /// <summary>CLOCKMAKER: "Attention. Attention. Seven-four-four-one-zero. Seven-four-four-one..."</summary>
        public const string D039 = "D039";
        /// <summary>TINKER: "Why the hell is the Clockmaker on our net?"</summary>
        public const string D040 = "D040";
        /// <summary>HOLLIS: "Kill it. Go to alternate. ...Calder? Cal, you with me?"</summary>
        public const string D041 = "D041";
        /// <summary>INTERROGATOR: "There. What did you feel when you heard the numbers?"</summary>
        public const string D042 = "D042";
        /// <summary>HOLLIS: "Green on your shoulders. Anything without green is fair game."</summary>
        public const string D043 = "D043";
        /// <summary>TINKER: "Charge is on the wall. Everybody get small. Fire in the hole!"</summary>
        public const string D044 = "D044";
        /// <summary>HARROU: "You are in my house now, Americans. Mind the furniture."</summary>
        public const string D045 = "D045";
        /// <summary>HOLLIS: "They're blinding us. Shoot out the floods!"</summary>
        public const string D046 = "D046";
        /// <summary>SPARROW: "Yard's clear. Offices are ground floor, east end."</summary>
        public const string D047 = "D047";
        /// <summary>HOLLIS: "Stack on the door. Calder, you've got the kick."</summary>
        public const string D048 = "D048";
        /// <summary>TINKER: "Movement upstairs!"</summary>
        public const string D049 = "D049";
        /// <summary>SPARROW: "Shipping ledgers. End-user certificates, all countersigned with a '..."</summary>
        public const string D050 = "D050";
        /// <summary>HOLLIS: "...I know that signature. Bag it. We talk on the bird."</summary>
        public const string D051 = "D051";
        /// <summary>SPARROW: "Broadcast schedule. Eighteen-forty, every night. Relayed through......"</summary>
        public const string D052 = "D052";
        /// <summary>SPARROW: "Every can's marked S-3. Thread one, Cal. I want to know what we're ..."</summary>
        public const string D053 = "D053";
        /// <summary>INTERROGATOR: "You weren't supposed to watch that, Nathan."</summary>
        public const string D054 = "D054";
        /// <summary>HOLLIS: "Calder. Calder! Grab the reels. We're moving."</summary>
        public const string D055 = "D055";
        /// <summary>SPARROW: "That was you in that chair, Cal."</summary>
        public const string D056 = "D056";
        /// <summary>TINKER: "We didn't see nothin'. Basement. Go."</summary>
        public const string D057 = "D057";
        /// <summary>TINKER: "'Operator: P.' Same 'P' as the ledgers."</summary>
        public const string D058 = "D058";
        /// <summary>SPARROW: "Safe's old. Give me ninety seconds with the drill."</summary>
        public const string D059 = "D059";
        /// <summary>HOLLIS: "Both stairwells! Nobody touches her!"</summary>
        public const string D060 = "D060";
        /// <summary>TINKER: "That's him. That's the Clockmaker. A tape loop and a transmitter th..."</summary>
        public const string D061 = "D061";
        /// <summary>TINKER: "East stairs! They're comin' down the east stairs!"</summary>
        public const string D062 = "D062";
        /// <summary>HOLLIS: "Hatch! South wall!"</summary>
        public const string D063 = "D063";
        /// <summary>SPARROW: "Got it! Codebook's in hand."</summary>
        public const string D064 = "D064";
        /// <summary>HOLLIS: "Calder. Kill that transmitter."</summary>
        public const string D065 = "D065";
        /// <summary>CLOCKMAKER: "Zero. Six. Fo..."</summary>
        public const string D066 = "D066";
        /// <summary>TINKER: "Cable tunnel runs north to the postern. Pitch is right outside."</summary>
        public const string D067 = "D067";
        /// <summary>HOLLIS: "Magpie, Sundial. Package secure. Moving to the pitch, popping green."</summary>
        public const string D068 = "D068";
        /// <summary>MAGPIE: "Sundial, Magpie. Ninety seconds out. How hot?"</summary>
        public const string D069 = "D069";
        /// <summary>HOLLIS: "Hot enough. Bring the door guns."</summary>
        public const string D070 = "D070";
        /// <summary>SPARROW: "Truck, east road! They're unloading!"</summary>
        public const string D071 = "D071";
        /// <summary>TINKER: "Flare! They're lightin' us up!"</summary>
        public const string D072 = "D072";
        /// <summary>SPARROW: "RPG, by the trucks!"</summary>
        public const string D073 = "D073";
        /// <summary>MAGPIE: "I have green smoke. Coming in. I don't stay long."</summary>
        public const string D074 = "D074";
        /// <summary>MAGPIE: "RPG in the air! Waving off, coming around. Twenty seconds."</summary>
        public const string D075 = "D075";
        /// <summary>HOLLIS: "Bird's down! Sparrow, Tinker, go! Calder, last man. Door gun!"</summary>
        public const string D076 = "D076";
        /// <summary>MAGPIE: "Hang on back there. Going out low over the town."</summary>
        public const string D077 = "D077";
        /// <summary>TINKER: "Rooftops, left side! Light 'em up, Cal!"</summary>
        public const string D078 = "D078";
        /// <summary>TINKER: "Transmitter's dead... so who's reading numbers on our freq?"</summary>
        public const string D079 = "D079";
        /// <summary>CLOCKMAKER: "Attention, Sundial-Three. Zero. Six. Four. Zero."</summary>
        public const string D080 = "D080";
        /// <summary>MAGPIE: "RPG, left side! Hang on!"</summary>
        public const string D081 = "D081";
        /// <summary>HOLLIS: "Cal? Cal, what are you..."</summary>
        public const string D082 = "D082";
        /// <summary>INTERROGATOR: "Frank Hollister didn't come home, Nathan."</summary>
        public const string D083 = "D083";
        /// <summary>CALDER: "RPG. It was the RPG."</summary>
        public const string D084 = "D084";
        /// <summary>INTERROGATOR: "The crew says the RPG missed."</summary>
        public const string D085 = "D085";
        /// <summary>INTERROGATOR: "The last page of the book is a list. Names that answer to numbers. ..."</summary>
        public const string D086 = "D086";
        /// <summary>INTERROGATOR: "Zero. Six. Four. Zero."</summary>
        public const string D087 = "D087";
        /// <summary>INTERROGATOR: "Good. Now. Who else saw the book?"</summary>
        public const string D088 = "D088";
        /// <summary>CALDER: "Castellanos. She photographed every page."</summary>
        public const string D089 = "D089";
        /// <summary>TALLOW: "Thank you, Nathan. That will be all."</summary>
        public const string D090 = "D090";
        /// <summary>TALLOW: "Magpie, Tallow. Prep the aircraft. One more pickup."</summary>
        public const string D091 = "D091";
        /// <summary>CLOCKMAKER: "Attention, Sundial-Two."</summary>
        public const string D092 = "D092";
        /// <summary>HOLLIS: "Calder! Where the hell are you going? Back on me!"</summary>
        public const string D093 = "D093";
        /// <summary>MAGPIE: "Sundial, Magpie. One minute. Sun's right where you ordered it."</summary>
        public const string D200 = "D200";
        /// <summary>HOLLIS: "One minute!"</summary>
        public const string D201 = "D201";
        /// <summary>TINKER: "Magpie, Sundial-Four. Comms check."</summary>
        public const string D202 = "D202";
        /// <summary>MAGPIE: "Lima Charlie, Four."</summary>
        public const string D203 = "D203";
        /// <summary>HOLLIS: "Go, go!"</summary>
        public const string D204 = "D204";
        /// <summary>MAGPIE: "Wheels up. See you at last light, Sundial."</summary>
        public const string D205 = "D205";
        /// <summary>CALDER: "Zaroud. Every shadow in that town pointed at the mast."</summary>
        public const string D206 = "D206";
        /// <summary>INTERROGATOR: "Go on."</summary>
        public const string D207 = "D207";
        /// <summary>LEGION: "Fesko! Fesko!"</summary>
        public const string BarkLeg08 = "BK_LEG_08";
        /// <summary>GREYCOAT: "Nolvek."</summary>
        public const string BarkGry09 = "BK_GRY_09";

        /// <summary>Every constant above (tests check the list is complete and each id exists in DIALOGUE.json).</summary>
        public static readonly string[] All =
        {
            "D001", "D002", "D003", "D004", "D005", "D006", "D007", "D008", "D009", "D010",
            "D011", "D012", "D013", "D014", "D015", "D016", "D017", "D018", "D019", "D020",
            "D021", "D022", "D023", "D024", "D025", "D026", "D027", "D028", "D029", "D030",
            "D031", "D032", "D033", "D034", "D035", "D036", "D037", "D038", "D039", "D040",
            "D041", "D042", "D043", "D044", "D045", "D046", "D047", "D048", "D049", "D050",
            "D051", "D052", "D053", "D054", "D055", "D056", "D057", "D058", "D059", "D060",
            "D061", "D062", "D063", "D064", "D065", "D066", "D067", "D068", "D069", "D070",
            "D071", "D072", "D073", "D074", "D075", "D076", "D077", "D078", "D079", "D080",
            "D081", "D082", "D083", "D084", "D085", "D086", "D087", "D088", "D089", "D090",
            "D091", "D092", "D093", "D200", "D201", "D202", "D203", "D204", "D205", "D206",
            "D207", "BK_LEG_08", "BK_GRY_09",
        };
    }
}
