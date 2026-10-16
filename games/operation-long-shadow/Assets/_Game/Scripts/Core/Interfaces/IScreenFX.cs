namespace LongShadow.Core
{
    /// <summary>Full-screen effects (WP09 ScreenFXService, TECH_PLAN §3.7 FX volumes).</summary>
    public interface IScreenFX
    {
        /// <summary>Low-health desaturation/vignette amount 0..1 (below 50 % HP).</summary>
        void SetLowHealth(float amount01);
        /// <summary>Suppression vignette pulse.</summary>
        void PulseSuppression(float amount01);
        /// <summary>Death desaturation progress 0..1 (0.6 s).</summary>
        void Death(float t01);
        /// <summary>Exposure flash (+EV) for seconds.</summary>
        void Flash(float ev, float seconds);
        /// <summary>Glitch full-screen pass.</summary>
        void Glitch(float intensity, float seconds);
        /// <summary>Flood glare overlay 0..1 (Beat 6).</summary>
        void Glare(float amount01);
        /// <summary>Fades to black over seconds (unscaled).</summary>
        void FadeToBlack(float seconds);
        /// <summary>Fades in from black over seconds (unscaled).</summary>
        void FadeIn(float seconds);
        /// <summary>Fades to white over seconds (unscaled): the climax cut to white (GDD §8.7).</summary>
        void FadeToWhite(float seconds);
        /// <summary>True while fully black.</summary>
        bool IsBlack { get; }
    }
}
