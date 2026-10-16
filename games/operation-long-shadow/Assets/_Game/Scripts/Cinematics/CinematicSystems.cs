using LongShadow.Cinematics.FX;
using LongShadow.Cinematics.Inserts;
using LongShadow.Cinematics.Sets;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Composition root of the WP09 runtime, placed once in S_Persistent (prefab
    /// <c>Assets/_Game/Prefabs/Booth/PF_CinematicSystems.prefab</c>, built by <c>LongShadow/Cinematics/Build Systems Prefab</c>):
    /// adds whatever is missing of the overlay canvas, camera arbiter, screen FX (IScreenFX), feed cameras, cinematic
    /// service (ICinematicService + PlayableDirector), insert service (IInsertService), insertion cast and MID-B
    /// projection rig, in dependency order. Every part also works when added by hand.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Cinematics - 40)]
    [DisallowMultipleComponent]
    public sealed class CinematicSystems : MonoBehaviour
    {
        private void Awake()
        {
            Ensure<CinematicOverlay>();
            Ensure<CinematicCameras>();
            Ensure<ScreenFXService>();
            Ensure<FeedCameras>();
            Ensure<CinematicService>();
            Ensure<InsertService>();
            Ensure<InsertionCast>();
            Ensure<ProjectionRig>();
        }

        private T Ensure<T>() where T : Component
        {
            var c = GetComponent<T>();
            return c != null ? c : gameObject.AddComponent<T>();
        }
    }
}
