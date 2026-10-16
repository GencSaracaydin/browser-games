using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Dialogue playback (WP06 VOManager, TECH_PLAN §7.10): priority queue, dry/radio crossfade, subtitles,
    /// beat-window preloading and the played-line set (ISaveable).
    /// </summary>
    public interface IVOService
    {
        /// <summary>Plays or queues a DIALOGUE.json line; returns a handle (0 if the line is unknown).</summary>
        int Play(string lineId, VOOptions o = default);
        /// <summary>True while the line is queued or playing.</summary>
        bool IsPlaying(string lineId);
        /// <summary>True while any scripted/objective line is queued or playing on the net channel.</summary>
        bool IsNetBusy { get; }
        /// <summary>Stops a handle.</summary>
        void Stop(int handle);
        /// <summary>Stops everything (checkpoint reloads, inserts) with a fade in seconds.</summary>
        void StopAll(float fade);
        /// <summary>Enemy bark at a speaker (barks may overlap; cooldowns are the barker's job).</summary>
        void PlayBark(string lineId, Transform speaker, Team team);
        /// <summary>Greycoat squelch clicks (2 before moving, 3 on spotting).</summary>
        void PlayClicks(int count, Transform speaker);
        /// <summary>Envelope amplitude 0..1 of the line the speaker is saying now (30 Hz data, TECH_PLAN D10).</summary>
        float Amplitude(string speakerId);
        /// <summary>Loads audio data for lines of beats [from, to] and unloads passed beats.</summary>
        void PrepareWindow(int fromBeat, int toBeat);
        /// <summary>True if the line has played (and was not reset by a checkpoint reload).</summary>
        bool HasPlayed(string lineId);
        /// <summary>True if the line exists in LIB_Dialogue.</summary>
        bool HasLine(string lineId);
        /// <summary>Line duration in seconds (0 if unknown).</summary>
        float Duration(string lineId);
    }
}
