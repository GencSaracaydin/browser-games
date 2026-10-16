using System;
using System.Collections.Generic;
using LongShadow.AI.Enemy;

namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// Lists every Appendix A id the AI depends on (encounter table markers, closets, perches, routes, vehicles,
    /// suppress marks, trigger conditions, and the ids the special behaviours look up) and checks them against a
    /// predicate (the markup CSV in tests and in LongShadow/AI/Validate Encounter Ids; the marker registry at runtime).
    /// </summary>
    public static class EncounterValidation
    {
        /// <summary>Ids used by behaviours and squad scripts outside the table (with the user).</summary>
        public static readonly string[,] BehaviourIds =
        {
            { "B4.NodeSet.Q1", "TechnicalGunnerBehaviour arch fire" },
            { "B3.ArchTarget", "TechnicalGunnerBehaviour arch fallback" },
            { "B4.FireProp", "TechnicalGunnerBehaviour fuel-tank fireball" },
            { "B10.NodeSet.P1", "RpgBehaviour bleacher target" },
            { "B10.HeliApproach", "RpgBehaviour Magpie target" },
            { "B10.Pad", "RpgBehaviour fallback" },
            { ExfilSchedule.ExfilVehicleId, "RooftopTargetBehaviour exfil clock" },
            { "B2.AlertCover", "E2 gate alert cover group / sentry run" },
        };

        /// <summary>Every (id, used-by) pair the encounter table references.</summary>
        public static List<KeyValuePair<string, string>> ReferencedIds()
        {
            var list = new List<KeyValuePair<string, string>>();
            var all = EncounterTable.All;
            for (var e = 0; e < all.Count; e++)
            {
                var enc = all[e];
                for (var i = 0; i < enc.Enemies.Count; i++)
                {
                    var d = enc.Enemies[i];
                    if ((d.Kind == SpawnKind.Placed || d.Kind == SpawnKind.Perch) && !string.IsNullOrEmpty(d.MarkerId) && d.PerchId == null)
                        list.Add(Pair(d.MarkerId, d.Id + " marker"));
                    if (d.ClosetId != null) list.Add(Pair(d.ClosetId, d.Id + " closet"));
                    if (d.PerchId != null) list.Add(Pair(d.PerchId, d.Id + " perch"));
                    if (d.FlankRouteId != null) list.Add(Pair(d.FlankRouteId, d.Id + " flank route"));
                    if (d.VehicleId != null) list.Add(Pair(d.VehicleId, d.Id + " vehicle"));
                    if (d.SuppressMarkerId != null) list.Add(Pair(d.SuppressMarkerId, d.Id + " suppress mark"));
                    if (d.HoldUntil != null)
                        foreach (var h in d.HoldUntil)
                            if (LooksLikeMarker(h)) list.Add(Pair(h, d.Id + " hold-until"));
                    if (LooksLikeMarker(d.PreferSquadUntil)) list.Add(Pair(d.PreferSquadUntil, d.Id + " squad-first-until"));
                }
                for (var w = 0; w < enc.Waves.Count; w++)
                {
                    var wave = enc.Waves[w];
                    AddTrigger(list, wave.Trigger, enc.Id + "." + wave.Id + " trigger");
                    AddTrigger(list, wave.AlertOn, enc.Id + "." + wave.Id + " alert");
                }
            }
            for (var i = 0; i < BehaviourIds.GetLength(0); i++) list.Add(Pair(BehaviourIds[i, 0], BehaviourIds[i, 1]));
            return list;
        }

        /// <summary>Problems ("id (used by)") for ids the predicate does not know.</summary>
        public static List<string> Check(Func<string, bool> exists)
        {
            var problems = new List<string>();
            foreach (var kv in ReferencedIds())
                if (!exists(kv.Key)) problems.Add(kv.Key + " (" + kv.Value + ")");
            return problems;
        }

        private static void AddTrigger(List<KeyValuePair<string, string>> list, WaveTrigger t, string user)
        {
            if (t == null) return;
            foreach (var group in t.AnyOf)
                foreach (var c in group)
                {
                    if (c.Kind == ConditionKind.PlayerInside) list.Add(Pair(c.Id, user));
                    else if ((c.Kind == ConditionKind.Event || c.Kind == ConditionKind.SecondsAfterEvent) && LooksLikeMarker(c.Id)) list.Add(Pair(c.Id, user));
                }
        }

        /// <summary>
        /// True for ids that name Appendix A markup (trigger volumes "Bn.T.*", destructibles and interaction points such as
        /// "B6.BreachWall" / "B4.SideDoor"); false for pure mission events ("B6.Blast", "MIDA.Start", "VO:D028").
        /// </summary>
        public static bool LooksLikeMarker(string id)
        {
            if (string.IsNullOrEmpty(id) || id.StartsWith(AI.Common.AIEvents.VoPrefix, StringComparison.Ordinal)) return false;
            if (id.IndexOf(".T.", StringComparison.Ordinal) > 0) return true;
            return id == "B6.BreachWall" || id == "B4.SideDoor";
        }

        private static KeyValuePair<string, string> Pair(string id, string user) => new KeyValuePair<string, string>(id, user);
    }
}
