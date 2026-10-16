namespace LongShadow.Mission.Data
{
    /// <summary>One intel item (GDD §1 "Intel", §7.7-7.9, §11 intel card).</summary>
    public sealed class IntelDef
    {
        /// <summary>Intel id ("Intel.Ledgers"); also the LIB_Mission entry id of its prop and photo.</summary>
        public readonly string Id;
        /// <summary>Objective it completes ("B7.Ledgers").</summary>
        public readonly string ObjectiveId;
        /// <summary>Marker id where the pickup spawns (null for the codebook, which Sparrow takes automatically).</summary>
        public readonly string MarkerId;
        /// <summary>Intel card title.</summary>
        public readonly string Title;
        /// <summary>Hold prompt ("HOLD [E] Take the ledgers").</summary>
        public readonly string Prompt;
        /// <summary>Optional intel (O1/O2: counts OPTIONAL n/2).</summary>
        public readonly bool Optional;
        /// <summary>Order among the main items (1-3) or the optional ones (1-2).</summary>
        public readonly int Order;
        /// <summary>Manifest id of the pickup prop model (null = placeholder only).</summary>
        public readonly string PropManifestId;
        /// <summary>Manifest id of the intel card photo.</summary>
        public readonly string PhotoManifestId;
        /// <summary>File-name fragment of the photo as WP09 ships it ("T_IntelLedgers_BC", DECISIONS [WP09]).</summary>
        public readonly string PhotoName;
        /// <summary>Beat at whose start an uncollected pickup and its objective are dropped (its section unloads).</summary>
        public readonly int ExpiresAtBeat;
        /// <summary>Beat the item belongs to.</summary>
        public readonly int Beat;

        /// <summary>Creates a definition.</summary>
        public IntelDef(string id, int beat, string objectiveId, string markerId, string title, string prompt, bool optional, int order,
                        string propManifestId, string photoManifestId, string photoName, int expiresAtBeat)
        {
            Beat = beat;
            PhotoName = photoName;
            ExpiresAtBeat = expiresAtBeat;
            Id = id;
            ObjectiveId = objectiveId;
            MarkerId = markerId;
            Title = title;
            Prompt = prompt;
            Optional = optional;
            Order = order;
            PropManifestId = propManifestId;
            PhotoManifestId = photoManifestId;
        }

        /// <summary>Hold time for every intel pickup (GDD §7.7: hold E for 1.2 s).</summary>
        public const float HoldSeconds = 1.2f;

        /// <summary>Intel card counter text ("INTEL 1/3" or "OPTIONAL 1/2").</summary>
        public string Counter(int count) =>
            Optional ? "OPTIONAL " + count + "/" + IntelTable.OptionalTotal : "INTEL " + count + "/" + IntelTable.MainTotal;
    }
}
