namespace LongShadow.AI.Squad
{
    /// <summary>Callout direction relative to the player's view (GDD §15: front ±45°, left, right, rear).</summary>
    public enum RelativeDirection
    {
        /// <summary>Within ±45° of the view direction.</summary>
        Front,
        /// <summary>45°-135° to the left.</summary>
        Left,
        /// <summary>45°-135° to the right.</summary>
        Right,
        /// <summary>More than 135° from the view direction.</summary>
        Rear,
    }
}
