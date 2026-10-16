using System.Collections.Generic;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// The five intel items (GDD §1: Ledgers, S-3 Reels, Codebook 3/3; optional Broadcast schedule, Film-can label 0/2),
    /// their objectives (§7.12), pickup markers (Appendix A) and card assets (ASSET_MANIFEST IMG_INTEL_*).
    /// </summary>
    public static class IntelTable
    {
        public const string Ledgers = "Intel.Ledgers";
        public const string Reels = "Intel.Reels";
        public const string Codebook = "Intel.Codebook";
        public const string Schedule = "Intel.O1";
        public const string FilmLabel = "Intel.O2";

        /// <summary>Main intel items (INTEL n/3).</summary>
        public const int MainTotal = 3;
        /// <summary>Optional intel items (OPTIONAL n/2).</summary>
        public const int OptionalTotal = 2;

        private static readonly IntelDef[] s_all =
        {
            // GDD §7.7: hold E 1.2 s on Harrou's desk, INTEL 1/3.
            new IntelDef(Ledgers, 7, ObjectiveTable.B7Ledgers, MissionMarkers.B7Ledgers, "SHIPPING LEDGERS", "HOLD [E] Take the ledgers",
                false, 1, "FAL_LEDGER_STACK", "IMG_INTEL_LEDGERS", "IntelLedgers", 8),
            // GDD §7.8: hold E at the can rack, INTEL 2/3.
            new IntelDef(Reels, 8, ObjectiveTable.B8Film, MissionMarkers.B8CanRack, "S-3 REELS", "HOLD [E] Take the reels",
                false, 2, "FAL_FILM_CAN", "IMG_INTEL_REELS", "IntelReels", 9),
            // GDD §7.9: Sparrow takes it automatically when the drill reaches 100 %, INTEL 3/3.
            new IntelDef(Codebook, 9, ObjectiveTable.B9Codebook, null, "CODEBOOK", null,
                false, 3, "FAL_CODEBOOK", "IMG_INTEL_CODEBOOK", "IntelCodebook", 10),
            // GDD §7.7 optional O1: the teletype strip ("Tallow net" on the broadcast schedule, D052).
            new IntelDef(Schedule, 7, ObjectiveTable.B7O1, MissionMarkers.B7O1, "BROADCAST SCHEDULE", "HOLD [E] Take the schedule",
                true, 1, null, "IMG_INTEL_SCHEDULE", "IntelSchedule", 10),
            // GDD §7.8 optional O2: a film-can label on the rack (D058).
            new IntelDef(FilmLabel, 8, ObjectiveTable.B8O2, MissionMarkers.B8O2, "FILM-CAN LABEL", "HOLD [E] Take the label",
                true, 2, "FAL_FILM_CAN", "IMG_INTEL_FILMLABEL", "IntelFilmLabel", 10),
        };

        /// <summary>Every intel item.</summary>
        public static IReadOnlyList<IntelDef> All => s_all;

        /// <summary>Finds an item by id (null if unknown).</summary>
        public static IntelDef Get(string id)
        {
            for (var i = 0; i < s_all.Length; i++)
                if (s_all[i].Id == id) return s_all[i];
            return null;
        }

        /// <summary>Line played when the item is picked up (GDD §7.7-7.9), or null.</summary>
        public static string LineFor(string id)
        {
            switch (id)
            {
                case Ledgers: return LineIds.D050;
                case Reels: return LineIds.D056;
                case Schedule: return LineIds.D052;
                case FilmLabel: return LineIds.D058;
                default: return null;
            }
        }

        /// <summary>Finds the item an objective collects (null if none).</summary>
        public static IntelDef ForObjective(string objectiveId)
        {
            for (var i = 0; i < s_all.Length; i++)
                if (s_all[i].ObjectiveId == objectiveId) return s_all[i];
            return null;
        }
    }
}
