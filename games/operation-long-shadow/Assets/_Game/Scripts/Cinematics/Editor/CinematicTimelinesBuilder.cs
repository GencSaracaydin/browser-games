using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Writes the five TL_* timelines (Assets/_Game/Timelines) from the shot tables in code (IntroTimeline,
    /// TitleCardTimeline, MidBTimeline, ClimaxTimeline, OutroTimeline). Timings and line durations come from the tables
    /// and the VO envelopes; re-run after VO lands so clip lengths match the real takes.
    /// </summary>
    public static class CinematicTimelinesBuilder
    {
        /// <summary>Builds every timeline; returns report lines (shot lists).</summary>
        public static List<string> Build()
        {
            var log = new List<string>();
            var dialogue = new DialogueData();
            log.Add("DIALOGUE.json lines: " + dialogue.Count);
            One(log, CinematicAssetIds.TimelineIntro, IntroTimeline.Length, dialogue, IntroTimeline.Build);
            One(log, CinematicAssetIds.TimelineTitleCard, TitleCardTimeline.Length, dialogue, TitleCardTimeline.Build);
            One(log, CinematicAssetIds.TimelineMidB, MidBTimeline.Length, dialogue, MidBTimeline.Build);
            One(log, CinematicAssetIds.TimelineClimax, ClimaxTimeline.Length, dialogue, ClimaxTimeline.Build);
            One(log, CinematicAssetIds.TimelineOutro, OutroTimeline.Length, dialogue, OutroTimeline.Build);
            return log;
        }

        private static void One(List<string> log, string id, double seconds, DialogueData dialogue, System.Action<TimelineBuilder> build)
        {
            var path = CinematicPaths.Timelines + "/" + id + ".playable";
            var b = TimelineBuilder.Begin(path, seconds, dialogue);
            build(b);
            var tl = b.Save();
            var tracks = 0;
            var clips = 0;
            foreach (var t in tl.GetOutputTracks())
            {
                tracks++;
                foreach (var _ in t.GetClips()) clips++;
            }
            log.Add(id + ": " + seconds.ToString("0.0") + " s, " + tracks + " tracks, " + clips + " clips");
            foreach (var shot in b.ShotLog) log.Add("    " + shot);
            LSLog.Info(LogCategory.Cinematics, "Timeline " + path + " written (" + tracks + " tracks, " + clips + " clips).");
        }
    }
}
