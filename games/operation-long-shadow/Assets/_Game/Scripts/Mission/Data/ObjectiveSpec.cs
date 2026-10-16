using LongShadow.Core;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// One row of the GDD §7.12 objective master list: id, HUD text, marker kind and targets, completion rule. The
    /// <see cref="ObjectiveTable"/> holds every row; the EditMode tests compare it with the GDD table cell by cell.
    /// </summary>
    public sealed class ObjectiveSpec
    {
        /// <summary>Objective id ("B4.Technical").</summary>
        public readonly string Id;
        /// <summary>Beat number (HUD counter "OBJ beat/11").</summary>
        public readonly int Beat;
        /// <summary>HUD text exactly as GDD §7.12 ("—" for optional intel).</summary>
        public readonly string Text;
        /// <summary>Marker style, or null for objectives without a marker.</summary>
        public readonly MarkerKind? Kind;
        /// <summary>Marker targets added when the objective is set (empty = none, or script-managed).</summary>
        public readonly ObjectiveTarget[] Targets;
        /// <summary>Short label under the marker.</summary>
        public readonly string Label;
        /// <summary>Show the distance in metres.</summary>
        public readonly bool ShowDistance;
        /// <summary>Visible through walls (red high-priority markers).</summary>
        public readonly bool ThroughWalls;
        /// <summary>Optional objective (optional intel O1/O2).</summary>
        public readonly bool Optional;
        /// <summary>
        /// When &gt; 0 the marker only exists while the player is within this many metres (optional intel icons show within
        /// 6 m, GDD §7.12).
        /// </summary>
        public readonly float ProximityOnly;
        /// <summary>
        /// False when the beat script adds the markers itself at a later moment (B6.Yard floods after D046, B11.Cover per
        /// target window); <see cref="Targets"/> still lists them for the tests.
        /// </summary>
        public readonly bool AutoMarkers;
        /// <summary>Completion rule, verbatim from GDD §7.12 ("Completes when").</summary>
        public readonly string CompletionRule;

        /// <summary>Creates a row.</summary>
        public ObjectiveSpec(string id, int beat, string text, MarkerKind? kind, ObjectiveTarget[] targets, string label,
                             bool showDistance, bool throughWalls, bool optional, float proximityOnly, bool autoMarkers,
                             string completionRule)
        {
            Id = id;
            Beat = beat;
            Text = text;
            Kind = kind;
            Targets = targets ?? new ObjectiveTarget[0];
            Label = label ?? string.Empty;
            ShowDistance = showDistance;
            ThroughWalls = throughWalls;
            Optional = optional;
            ProximityOnly = proximityOnly;
            AutoMarkers = autoMarkers;
            CompletionRule = completionRule;
        }

        /// <summary>Five-digit flavour group (FNV-1a 32-bit of the id mod 100000, zero-padded, GDD §7.12).</summary>
        public string FlavourGroup => MathX.FlavourGroup(Id).ToString("D5");

        /// <summary>The ObjectiveDef for <see cref="IObjectiveService.Set"/> (optionally with replaced text, e.g. counters).</summary>
        public ObjectiveDef ToDef(string textOverride = null) => new ObjectiveDef
        {
            Id = Id,
            Text = textOverride ?? Text,
            Index = Beat,
            Total = BeatTable.PlayableBeats,
            FlavourGroup = FlavourGroup,
            Optional = Optional,
        };

        /// <summary>Marker id of target <paramref name="index"/> (the objective id for single-target objectives).</summary>
        public string MarkerId(int index) => Targets.Length <= 1 ? Id : Id + "/" + Targets[index].Id;
    }
}
