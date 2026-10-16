namespace LongShadow.Cinematics.Inserts
{
    /// <summary>A glitch insert (GDD §5.5 table): its booth shots, default line and how it ends.</summary>
    public sealed class InsertDef
    {
        /// <summary>Insert id (SequenceIds.InsertI1 ...).</summary>
        public string Id { get; }
        /// <summary>Booth shots in order (I4 cascades four).</summary>
        public InsertShot[] Shots { get; }
        /// <summary>Line played when the caller passes none (I1 D017, I3 D054); null for none.</summary>
        public string DefaultLine { get; }
        /// <summary>The line continues over gameplay after the insert (I1: "VO continues over gameplay").</summary>
        public bool LineOutlivesInsert { get; }
        /// <summary>End on a held white screen instead of returning to the game (I4: "cut to white before any shot").</summary>
        public bool EndWhite { get; }
        /// <summary>Stinger cue at the cut in.</summary>
        public string Stinger { get; }

        /// <summary>Creates a row.</summary>
        public InsertDef(string id, InsertShot[] shots, string defaultLine, bool lineOutlivesInsert, bool endWhite, string stinger)
        {
            Id = id;
            Shots = shots;
            DefaultLine = defaultLine;
            LineOutlivesInsert = lineOutlivesInsert;
            EndWhite = endWhite;
            Stinger = stinger;
        }

        /// <summary>Total booth time in seconds (without the glitch in/out).</summary>
        public float BoothSeconds
        {
            get
            {
                var s = 0f;
                foreach (var shot in Shots) s += shot.Seconds;
                return s;
            }
        }
    }
}
