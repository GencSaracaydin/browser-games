namespace LongShadow.Core
{
    /// <summary>Receives suppression (halved rate and accuracy for the duration, GDD §14.3). Enemy brains and the player.</summary>
    public interface ISuppressible
    {
        /// <summary>Suppresses for <paramref name="seconds"/> by a team.</summary>
        void Suppress(float seconds, Team by);
    }
}
