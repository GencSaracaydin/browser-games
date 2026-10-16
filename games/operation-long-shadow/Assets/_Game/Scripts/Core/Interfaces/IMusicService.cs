namespace LongShadow.Core
{
    /// <summary>Music cues and layers (WP06 MusicDirector, GDD §10.3).</summary>
    public interface IMusicService
    {
        /// <summary>Starts a cue at a layer with a fade.</summary>
        void PlayCue(string cueId, int layer = 0, float fade = 1f);
        /// <summary>Crossfades to another layer of the current cue.</summary>
        void SetLayer(int layer, float fade);
        /// <summary>One-shot stinger.</summary>
        void Stinger(string id);
        /// <summary>Fades out.</summary>
        void Stop(float fade);
        /// <summary>Cuts everything instantly (climax, inserts).</summary>
        void HardCut();
        /// <summary>Current cue id (checkpoint snapshot), or null.</summary>
        string CurrentCue { get; }
        /// <summary>Current layer.</summary>
        int CurrentLayer { get; }
    }
}
