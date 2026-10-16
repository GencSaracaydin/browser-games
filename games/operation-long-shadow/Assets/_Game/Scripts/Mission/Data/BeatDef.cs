using LongShadow.Core;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// One row of the GDD §7.0 pacing table plus the §6.7 streaming plan for that beat (TECH_PLAN §7.11 BeatTable):
    /// LightStage range, checkpoints, music, objectives, enter/exit triggers, section loads/unloads at the beat start and
    /// at named quiet points, backtrack blockers closed at the beat start and the resupply crates spawned in the beat.
    /// </summary>
    public sealed class BeatDef
    {
        /// <summary>Beat number (0 intro, 1..11, 12 outro and debrief).</summary>
        public readonly int Number;
        /// <summary>Beat name ("THE LONG MARKET").</summary>
        public readonly string Name;
        /// <summary>LightStage s at the beat start (GDD §7.0, §9.2a).</summary>
        public readonly float SStart;
        /// <summary>LightStage s at the beat end.</summary>
        public readonly float SEnd;
        /// <summary>Story clock at the beat start ("18:17").</summary>
        public readonly string StoryClock;
        /// <summary>Checkpoints in the beat (first one at the beat start).</summary>
        public readonly string[] Checkpoints;
        /// <summary>Music cue at the beat start: null keeps what plays, "" stops the music (with <see cref="MusicFade"/>).</summary>
        public readonly string MusicCue;
        /// <summary>Music layer at the beat start (<see cref="MissionCues.LayerLow"/> / <see cref="MissionCues.LayerHigh"/>).</summary>
        public readonly int MusicLayer;
        /// <summary>Fade in seconds for the beat-start cue change.</summary>
        public readonly float MusicFade;
        /// <summary>Objective ids of the beat in display order.</summary>
        public readonly string[] Objectives;
        /// <summary>What starts the beat (GDD "Enter trigger"), for docs and the smoke test report.</summary>
        public readonly string EnterTrigger;
        /// <summary>What ends the beat (GDD "Exit trigger"): a trigger id, an interaction id or a description.</summary>
        public readonly string ExitTrigger;
        /// <summary>The section the beat is played in (encounters wait for it, GDD §6.7 "holds the trigger").</summary>
        public readonly SectionId Section;
        /// <summary>True when <see cref="Section"/> is meaningful (false for the booth flows).</summary>
        public readonly bool HasSection;
        /// <summary>Sections requested when the beat starts (GDD §6.7 "Load request at").</summary>
        public readonly SectionId[] LoadOnEnter;
        /// <summary>Sections unloaded when the beat starts (GDD §6.7 "Unloaded at").</summary>
        public readonly SectionId[] UnloadOnEnter;
        /// <summary>Sections requested at a quiet point inside the beat (line or event id → section).</summary>
        public readonly QuietLoad[] QuietLoads;
        /// <summary>Backtrack blockers closed at the beat start (GDD §6.7).</summary>
        public readonly string[] CloseOnEnter;
        /// <summary>Resupply crate markers spawned during the beat (GDD §13.2).</summary>
        public readonly string[] Crates;

        /// <summary>Creates a row.</summary>
        public BeatDef(int number, string name, float sStart, float sEnd, string storyClock, string[] checkpoints, string musicCue,
                       int musicLayer, float musicFade, string[] objectives, string enterTrigger, string exitTrigger,
                       SectionId? section, SectionId[] loadOnEnter, SectionId[] unloadOnEnter, QuietLoad[] quietLoads,
                       string[] closeOnEnter, string[] crates)
        {
            Number = number;
            Name = name;
            SStart = sStart;
            SEnd = sEnd;
            StoryClock = storyClock;
            Checkpoints = checkpoints ?? new string[0];
            MusicCue = musicCue;
            MusicLayer = musicLayer;
            MusicFade = musicFade;
            Objectives = objectives ?? new string[0];
            EnterTrigger = enterTrigger;
            ExitTrigger = exitTrigger;
            HasSection = section.HasValue;
            Section = section ?? SectionId.Booth;
            LoadOnEnter = loadOnEnter ?? new SectionId[0];
            UnloadOnEnter = unloadOnEnter ?? new SectionId[0];
            QuietLoads = quietLoads ?? new QuietLoad[0];
            CloseOnEnter = closeOnEnter ?? new string[0];
            Crates = crates ?? new string[0];
        }

        /// <summary>True for the playable beats 1..11.</summary>
        public bool IsPlayable => Number >= 1 && Number <= BeatTable.PlayableBeats;

        /// <summary>LightStage s at a progress 0..1 through the beat.</summary>
        public float SAt(float progress01) => SStart + (SEnd - SStart) * UnityEngine.Mathf.Clamp01(progress01);

        /// <summary>The section requested at a quiet point (line or event id), or false.</summary>
        public bool TryQuietLoad(string at, out SectionId section)
        {
            for (var i = 0; i < QuietLoads.Length; i++)
            {
                if (QuietLoads[i].At != at) continue;
                section = QuietLoads[i].Section;
                return true;
            }
            section = SectionId.Booth;
            return false;
        }

        /// <inheritdoc/>
        public override string ToString() => "Beat " + Number + " " + Name;
    }
}
