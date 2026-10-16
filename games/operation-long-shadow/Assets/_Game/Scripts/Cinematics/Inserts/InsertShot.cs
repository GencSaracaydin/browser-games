using LongShadow.Cinematics.Booth;

namespace LongShadow.Cinematics.Inserts
{
    /// <summary>One booth shot of an insert: a camera preset held for a number of 60-fps frames (or seconds).</summary>
    public readonly struct InsertShot
    {
        /// <summary>Booth camera set-up.</summary>
        public readonly BoothCameraPreset Preset;
        /// <summary>Unscaled seconds (frame counts converted at 60 fps, GDD §8.4).</summary>
        public readonly float Seconds;
        /// <summary>Show CRT static full-frame instead of the booth picture (I4 "CRT static").</summary>
        public readonly bool StaticFrame;

        /// <summary>Creates a shot.</summary>
        public InsertShot(BoothCameraPreset preset, float seconds, bool staticFrame = false)
        {
            Preset = preset;
            Seconds = seconds;
            StaticFrame = staticFrame;
        }

        /// <summary>A shot of <paramref name="frames"/> at the 60 fps reference.</summary>
        public static InsertShot Frames(BoothCameraPreset preset, int frames, bool staticFrame = false) =>
            new InsertShot(preset, InsertTiming.FramesToSeconds(frames), staticFrame);
    }
}
