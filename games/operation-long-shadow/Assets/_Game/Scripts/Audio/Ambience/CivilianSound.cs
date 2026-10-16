namespace LongShadow.Audio.Ambience
{
    /// <summary>What a <see cref="CivilianEmitter"/> is (GDD §0.3 G5: civilians heard, never seen).</summary>
    public enum CivilianSound
    {
        /// <summary>A TV behind a wall (<c>SFX_TV_MUFFLED</c> loop); switched off when shooting starts.</summary>
        MutedTv = 0,
        /// <summary>A radio behind a shutter (<c>AMB_CASSETTE_TUNE</c> loop); turned down (<c>SFX_RADIO_TURNED_DOWN</c>).</summary>
        Radio = 1,
        /// <summary>A kettle that whistles now and then (<c>SFX_KETTLE</c>); taken off the stove when shooting starts.</summary>
        Kettle = 2,
        /// <summary>A shutter that is pulled shut and latched when shooting starts (<c>SFX_SHUTTER_LATCH</c>).</summary>
        Shutter = 3,
        /// <summary>A pigeon loft that flushes on the first nearby shot (<c>SFX_PIGEONS</c>).</summary>
        Pigeons = 4,
        /// <summary>Cue ids set by hand.</summary>
        Custom = 5,
    }
}
