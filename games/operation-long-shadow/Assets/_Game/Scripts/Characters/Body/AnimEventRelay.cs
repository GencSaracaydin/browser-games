using System;
using UnityEngine;

namespace LongShadow.Characters.Body
{
    /// <summary>
    /// Receiver for clip animation events. <c>ClipImporter</c> writes every event as function
    /// <see cref="EventFunction"/> with the id (Core <c>AnimEventIds</c>) in the string parameter. Sits on the GameObject
    /// of the Animator (Unity only calls receivers there) and forwards to <see cref="Raised"/>; every WP05 body has one,
    /// so shared clips never log "AnimationEvent has no receiver".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimEventRelay : MonoBehaviour
    {
        /// <summary>Function name used by every WP05 clip event.</summary>
        public const string EventFunction = "OnAnimEvent";

        /// <summary>Raised with the event id.</summary>
        public event Action<string> Raised;

        /// <summary>Called by the Animator.</summary>
        public void OnAnimEvent(string id)
        {
            if (!string.IsNullOrEmpty(id)) Raised?.Invoke(id);
        }

        /// <summary>Called by the Animator for events authored with an AnimationEvent object.</summary>
        public void OnAnimEventObject(AnimationEvent e)
        {
            if (e != null) OnAnimEvent(e.stringParameter);
        }
    }
}
