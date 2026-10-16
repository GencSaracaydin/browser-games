using LongShadow.Cinematics.FX;
using LongShadow.Cinematics.Inserts;
using LongShadow.Cinematics.Sets;
using LongShadow.Core.Editor;
using UnityEngine;
using UnityEngine.Playables;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Writes <c>Assets/_Game/Prefabs/Booth/PF_CinematicSystems.prefab</c>: the WP09 runtime for S_Persistent (overlay,
    /// cameras, screen FX, feeds, cinematic service with its PlayableDirector, inserts, insertion cast, MID-B projection).
    /// WP11 drops one instance into S_Persistent.
    /// </summary>
    public static class CinematicSystemsPrefabBuilder
    {
        /// <summary>Builds the prefab and returns its path.</summary>
        public static string Build()
        {
            var go = new GameObject("CinematicSystems");
            go.AddComponent<PlayableDirector>();
            go.AddComponent<CinematicOverlay>();
            go.AddComponent<CinematicCameras>();
            go.AddComponent<ScreenFXService>();
            go.AddComponent<FeedCameras>();
            go.AddComponent<CinematicService>();
            go.AddComponent<InsertService>();
            go.AddComponent<InsertionCast>();
            go.AddComponent<ProjectionRig>();
            go.AddComponent<CinematicSystems>();
            // Components built children in Awake while added in the editor; the prefab keeps only the components.
            for (var i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            BuilderUtil.SavePrefab(go, CinematicPaths.SystemsPrefab);
            return CinematicPaths.SystemsPrefab;
        }
    }
}
