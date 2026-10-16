using LongShadow.Core;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// Binds timeline tracks at play time by id: a track named "Label @Id" is bound to
    /// <c>CinematicBindings.ResolveAs(Id, bindingType)</c>. Timelines live in LIB_Cinematics (S_Persistent) while the
    /// objects they drive live in S0_Booth, S1_Approach or S6_StationInt, so scene references cannot be serialized.
    /// Tracks without "@" keep whatever binding they have (usually none).
    /// </summary>
    public static class TrackBindingResolver
    {
        /// <summary>Separator between label and binding id in a track name.</summary>
        public const char Separator = '@';

        /// <summary>Binding id in a track name, or null.</summary>
        public static string BindingIdOf(string trackName)
        {
            if (string.IsNullOrEmpty(trackName)) return null;
            var at = trackName.LastIndexOf(Separator);
            if (at < 0 || at == trackName.Length - 1) return null;
            return trackName.Substring(at + 1).Trim();
        }

        /// <summary>Track name for a label and binding id.</summary>
        public static string TrackName(string label, string bindingId) =>
            string.IsNullOrEmpty(bindingId) ? label : label + " " + Separator + bindingId;

        /// <summary>Resolves every "@Id" track of the timeline on the director. Returns the number of unresolved ids.</summary>
        public static int Bind(PlayableDirector director, TimelineAsset timeline)
        {
            if (director == null || timeline == null) return 0;
            var missing = 0;
            foreach (var track in timeline.GetOutputTracks())
            {
                var id = BindingIdOf(track.name);
                if (id == null) continue;
                System.Type type = null;
                foreach (var output in track.outputs)
                {
                    type = output.outputTargetType;
                    if (type != null) break;
                }
                if (type == null) continue;
                var obj = CinematicBindings.ResolveAs(id, type);
                director.SetGenericBinding(track, obj);
                if (obj == null)
                {
                    missing++;
                    LSLog.WarnOnce("cine-bind:" + timeline.name + ":" + id, LogCategory.Cinematics,
                        timeline.name + ": track '" + track.name + "' has no '" + id + "' (" + type.Name + "); it will do nothing.");
                }
            }
            return missing;
        }
    }
}
