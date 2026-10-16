namespace LongShadow.Audio.Editor
{
    /// <summary>One clip file under <c>Assets/_Game/Audio/{SFX,Music,Ambience}</c> as <see cref="CueNaming.TryParse"/> reads it.</summary>
    public struct CueFile
    {
        /// <summary>Project path of the clip.</summary>
        public string Path;
        /// <summary>Cue id (file name without the variant number and the <c>_FAR</c> marker).</summary>
        public string CueId;
        /// <summary>Baked distance variant (<c>_FAR</c> in the name or the <c>SFX/&lt;Group&gt;/Far</c> folder).</summary>
        public bool Far;
        /// <summary>Variant number (<c>_01</c> = 1), 0 when the file has none.</summary>
        public int Variant;
        /// <summary>Folder below <c>Audio/</c>: <c>SFX/Weapons</c>, <c>Music</c>, <c>Ambience</c>…</summary>
        public string Group;
        /// <summary>Top folder: <c>SFX</c>, <c>Music</c> or <c>Ambience</c>.</summary>
        public string Root;
    }
}
