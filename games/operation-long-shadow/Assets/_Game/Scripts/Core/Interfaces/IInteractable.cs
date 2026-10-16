namespace LongShadow.Core
{
    /// <summary>Something the player can use with E (crates, pickups, intel, projector). Needs a collider on layer Interactable.</summary>
    public interface IInteractable
    {
        /// <summary>Prompt text including the key ("[E] Thread the projector").</summary>
        string PromptText { get; }
        /// <summary>Hold interaction (radial).</summary>
        bool Hold { get; }
        /// <summary>Hold duration in seconds.</summary>
        float HoldSeconds { get; }
        /// <summary>True if usable now.</summary>
        bool CanInteract(IPlayerRig player);
        /// <summary>Performs the interaction.</summary>
        void Interact(IPlayerRig player);
        /// <summary>Prompt anchor / LOS target.</summary>
        UnityEngine.Transform Anchor { get; }
    }
}
