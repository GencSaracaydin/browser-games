using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Holds the <c>LIB_*</c> assets in S_Persistent and registers them with <see cref="Libraries"/> before any system
    /// reads them (execution order <see cref="ExecOrder.Libraries"/>). WP11 owns the instance.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Libraries)]
    [DisallowMultipleComponent]
    public sealed class LibraryRegistry : MonoBehaviour
    {
        [SerializeField] private ScriptableObject[] _libraries = new ScriptableObject[0];

        /// <summary>Registered library assets.</summary>
        public ScriptableObject[] LibraryAssets => _libraries;

        private void Awake()
        {
            foreach (var l in _libraries)
            {
                if (l == null)
                {
                    LSLog.Warn(LogCategory.Assets, "LibraryRegistry has an empty slot", this);
                    continue;
                }
                Libraries.Register(l);
            }
        }

        private void OnDestroy()
        {
            foreach (var l in _libraries)
                if (l != null) Libraries.Unregister(l);
        }

#if UNITY_EDITOR
        /// <summary>Editor builders: sets the library list.</summary>
        public void EditorSetLibraries(ScriptableObject[] libraries)
        {
            _libraries = libraries ?? new ScriptableObject[0];
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
