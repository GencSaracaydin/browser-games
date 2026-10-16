namespace LongShadow.Core
{
    /// <summary>Interaction prompt ("[E] Thread the projector", "HOLD [E] Take the ledgers"), GDD §11.</summary>
    public struct PromptData
    {
        /// <summary>Prompt id; <see cref="IHud.HidePrompt"/> uses it.</summary>
        public string Id;
        /// <summary>Full prompt text including the key label.</summary>
        public string Text;
        /// <summary>Hold prompt (shows the radial fill).</summary>
        public bool Hold;
        /// <summary>Hold progress 0..1.</summary>
        public float Progress01;
    }
}
