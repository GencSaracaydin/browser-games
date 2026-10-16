using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Base of the beat scripts (TECH_PLAN §7.11 "BeatScript base + Beat01Shoulder … Beat11Magpie, IntroFlow, OutroFlow"):
    /// coroutines on the <see cref="ScriptRunner"/> that implement GDD §7.1-7.11 and §8 with the
    /// <see cref="MissionContext"/> helpers. The director has already done the common beat entry (sections, blockers,
    /// music, VO window, LightStage range, crates, BeatStarted) when <see cref="Run"/> starts. <c>entryCheckpoint</c> is
    /// null on a normal entry and the checkpoint id on a resume (reload, ?cp=, SELECT BEAT): every checkpoint is a beat
    /// start except CP3b, which <see cref="Beat03LongMarket"/> handles. A beat ends when <see cref="Run"/> returns.
    /// <para>Rules every script follows: waits for intermediate triggers also accept a later trigger of the beat (a player
    /// who runs ahead never soft-locks it), the exit trigger is only checked after the beat's required objectives, and
    /// every line id, marker id and objective id comes from the Data tables (the tests check them).</para>
    /// </summary>
    public abstract class BeatScript
    {
        /// <summary>Beat number 0..12.</summary>
        public abstract int Number { get; }

        /// <summary>The beat's table row.</summary>
        public BeatDef Def => BeatTable.Get(Number);

        /// <summary>Runs the beat from its start or from <paramref name="entryCheckpoint"/>.</summary>
        public abstract IEnumerator Run(MissionContext ctx, string entryCheckpoint);

        /// <inheritdoc/>
        public override string ToString() => Def.ToString();

        /// <summary>Saves the beat-start checkpoint (also on a resume: it re-captures the restored state).</summary>
        protected void SaveStartCheckpoint(MissionContext ctx)
        {
            var cp = BeatTable.StartCheckpoint(Number);
            if (cp != null && Def.Checkpoints.Length > 0) ctx.Checkpoint(cp);
        }

        /// <summary>Holds until the beat's section is loaded (GDD §6.7: the squad waits at its node set, no visible stop).</summary>
        protected IEnumerator WaitForSection(MissionContext ctx)
        {
            if (Def.HasSection) yield return ctx.SectionReady(Def.Section);
        }

        /// <summary>Plays a fallback line schedule (no ICinematicService): absolute times from now, or chained after the previous line.</summary>
        protected static IEnumerator PlaySchedule(MissionContext ctx, TimedLine[] lines)
        {
            var t0 = ctx.Now;
            string previous = null;
            for (var i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                if (l.AfterPrevious && previous != null)
                {
                    yield return ctx.LineDone(previous);
                    if (l.Time > 0f) yield return ctx.Seconds(l.Time);
                }
                else
                {
                    yield return ctx.At(t0 + l.Time);
                }
                ctx.Line(l.LineId);
                previous = l.LineId;
            }
            if (previous != null) yield return ctx.LineDone(previous);
        }

        /// <summary>Plays the line of a 1-of-n tick ("OBSERVE ZAROUD n/3") and updates an objective counter text.</summary>
        protected static void Count(MissionContext ctx, string objectiveId, int count, int total)
        {
            var spec = ObjectiveTable.Get(objectiveId);
            if (spec == null) return;
            ctx.Objectives.SetText(objectiveId, Objectives.ObjectiveFormat.WithCounter(spec.Text, count, total));
        }

        /// <summary>Waits gameplay seconds then plays a line.</summary>
        protected static IEnumerator LineAfter(MissionContext ctx, float seconds, string lineId, VOOptions options = default)
        {
            if (seconds > 0f) yield return ctx.Seconds(seconds);
            ctx.Line(lineId, options);
        }
    }
}
