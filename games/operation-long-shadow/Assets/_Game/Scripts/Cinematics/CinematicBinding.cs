using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Registers this GameObject (or <see cref="Target"/>) in <see cref="CinematicBindings"/> under an id while enabled,
    /// so timeline tracks named "Label @Id" can drive it from another scene. Builders add it to cameras, cast members
    /// and set dressing that the timelines animate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CinematicBinding : MonoBehaviour
    {
        [SerializeField, Tooltip("Binding id (see BindingIds).")] private string _id;
        [SerializeField, Tooltip("Optional component to register instead of the GameObject.")] private Object _target;

        /// <summary>Binding id.</summary>
        public string Id => _id;

        /// <summary>Registered object (the target, or this GameObject).</summary>
        public Object Target => _target != null ? _target : gameObject;

        private void OnEnable() => CinematicBindings.Register(_id, Target);

        private void OnDisable() => CinematicBindings.Unregister(_id, Target);

        /// <summary>Sets the id and target (builders, runtime spawners). Re-registers when enabled.</summary>
        public void Setup(string id, Object target = null)
        {
            if (isActiveAndEnabled) CinematicBindings.Unregister(_id, Target);
            _id = id;
            _target = target;
            if (isActiveAndEnabled) CinematicBindings.Register(_id, Target);
        }
    }
}
