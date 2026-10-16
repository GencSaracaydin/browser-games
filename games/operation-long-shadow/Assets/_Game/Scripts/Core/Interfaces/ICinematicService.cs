using System;

namespace LongShadow.Core
{
    /// <summary>Timeline sequences and camera hand-off (WP09 CinematicService). Ids are in <see cref="SequenceIds"/> ("Intro" includes the §8.2a insertion cold open).</summary>
    public interface ICinematicService
    {
        /// <summary>Plays a sequence; <paramref name="onDone"/> fires at its end or on skip.</summary>
        void Play(string id, Action onDone);
        /// <summary>True while a sequence plays.</summary>
        bool IsPlaying { get; }
        /// <summary>Id of the playing sequence, or null.</summary>
        string Current { get; }
        /// <summary>Skips the current sequence (hold Space 1 s after the first viewing).</summary>
        void Skip();
    }
}
