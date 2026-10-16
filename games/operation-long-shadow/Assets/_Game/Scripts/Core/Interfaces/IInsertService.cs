using System;

namespace LongShadow.Core
{
    /// <summary>Glitch inserts I1-I4 and the death insert (WP09 InsertService, GDD §5.5, §8.4). Time scale 0 while playing.</summary>
    public interface IInsertService
    {
        /// <summary>Plays an insert, optionally with a line.</summary>
        void Play(string insertId, string lineId = null);
        /// <summary>Death insert (booth 2.0 s, random DI_01..06); <paramref name="onDone"/> after it.</summary>
        void PlayDeath(Action onDone);
        /// <summary>True while an insert plays.</summary>
        bool IsPlaying { get; }
    }
}
