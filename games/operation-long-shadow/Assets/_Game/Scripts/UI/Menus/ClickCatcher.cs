using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// Full-screen click target for "click anywhere" screens (click gate, click to resume). Raises <see cref="Down"/> on
    /// the pointer-down (inside the browser gesture, so the page can lock the pointer, TECH_PLAN §5.2) and
    /// <see cref="Click"/> on the click.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class ClickCatcher : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        /// <summary>Pointer pressed (any button).</summary>
        public event Action Down;

        /// <summary>Pointer clicked (left button).</summary>
        public event Action Click;

        /// <summary>Adds a transparent full-screen catcher under a parent.</summary>
        public static ClickCatcher Create(string name, Transform parent)
        {
            var rt = LongShadow.UI.Common.UIFactory.Stretch(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            return rt.gameObject.AddComponent<ClickCatcher>();
        }

        public void OnPointerDown(PointerEventData eventData) => Down?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Click?.Invoke();
        }
    }
}
