namespace LongShadow.Core
{
    /// <summary>Where an actor stands; used by squad callouts ("rooftop!", "balcony!") and perch-only archetypes.</summary>
    public enum PerchTag
    {
        Ground = 0,
        Balcony = 1,
        Rooftop = 2,
        Window = 3,
        Vehicle = 4,
    }
}
