using UnityEngine;
using UnityEngine.EventSystems;

namespace LongShadow.UI.Menus
{
    /// <summary>Programmatic selection of a menu entry without the "move" tick (opening a menu should not click).</summary>
    public static class MenuSelection
    {
        /// <summary>True while a programmatic selection is in progress.</summary>
        public static bool Quiet { get; private set; }

        /// <summary>Selects an object through the current EventSystem quietly.</summary>
        public static void Select(GameObject go)
        {
            var es = EventSystem.current;
            if (es == null) return;
            Quiet = true;
            try
            {
                es.SetSelectedGameObject(null);
                es.SetSelectedGameObject(go);
            }
            finally
            {
                Quiet = false;
            }
        }

        /// <summary>Clears the selection (gameplay: no UI object may eat Space/Enter).</summary>
        public static void Clear()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
        }
    }
}
