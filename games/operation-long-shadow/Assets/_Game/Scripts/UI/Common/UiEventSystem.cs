using LongShadow.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Makes sure exactly one EventSystem with an <see cref="InputSystemUIInputModule"/> exists (S_Boot has none until the
    /// click-gate prefab brings one; WP01 request). The module is bound to the "UI" map of the project-wide actions
    /// (TECH_PLAN §7.2) when present, otherwise to the Input System defaults.
    /// </summary>
    public static class UiEventSystem
    {
        /// <summary>Returns the active EventSystem, creating one under <paramref name="parent"/> (or the scene root) if none exists.</summary>
        public static EventSystem Ensure(Transform parent = null)
        {
            if (EventSystem.current != null) return EventSystem.current;
            var existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing != null) return existing;

            var go = new GameObject("EventSystem");
            if (parent != null) go.transform.SetParent(parent, false);
            var es = go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            Bind(module);
            LSLog.Verbose(LogCategory.UI, "Created an EventSystem with InputSystemUIInputModule.");
            return es;
        }

        /// <summary>Binds a module to the project-wide "UI" map if it has the standard actions.</summary>
        public static bool Bind(InputSystemUIInputModule module)
        {
            if (module == null) return false;
            InputActionAsset asset;
            try
            {
                asset = InputSystem.actions;
            }
            catch (System.Exception e)
            {
                LSLog.Exception(LogCategory.UI, e);
                return false;
            }
            var map = asset != null ? asset.FindActionMap("UI") : null;
            if (map == null) return false;
            var point = map.FindAction("Point");
            var click = map.FindAction("Click");
            var navigate = map.FindAction("Navigate");
            var submit = map.FindAction("Submit");
            var cancel = map.FindAction("Cancel");
            if (point == null || click == null || navigate == null || submit == null || cancel == null) return false;
            module.actionsAsset = asset;
            module.point = InputActionReference.Create(point);
            module.leftClick = InputActionReference.Create(click);
            module.move = InputActionReference.Create(navigate);
            module.submit = InputActionReference.Create(submit);
            module.cancel = InputActionReference.Create(cancel);
            var scroll = map.FindAction("ScrollWheel");
            if (scroll != null) module.scrollWheel = InputActionReference.Create(scroll);
            var right = map.FindAction("RightClick");
            if (right != null) module.rightClick = InputActionReference.Create(right);
            var middle = map.FindAction("MiddleClick");
            if (middle != null) module.middleClick = InputActionReference.Create(middle);
            return true;
        }
    }
}
