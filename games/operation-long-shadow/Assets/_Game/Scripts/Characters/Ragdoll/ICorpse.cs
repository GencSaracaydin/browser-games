namespace LongShadow.Characters.Ragdoll
{
    /// <summary>A dead body tracked by <see cref="CorpseRegistry"/>.</summary>
    public interface ICorpse
    {
        /// <summary>Time (Time.time) of death.</summary>
        float DiedAt { get; }
        /// <summary>Starts removing the corpse (sink and despawn); called when the cap is exceeded.</summary>
        void Evict();
    }
}
