using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Registry of <see cref="FeedCamera"/>s by id, plus the one feed that lives outside the booth: the live ridge shot of
    /// intro shot 7 (CRT2 shows "the ridge at golden hour, live: a render texture from a camera at (42, 23.2, 30),
    /// heading 45°, in S1_Approach", GDD §8.2). That camera is created at the <c>CAM.IntroRidge</c> marker when first
    /// acquired and removed when S1_Approach unloads. <see cref="Acquire"/> returns null while a feed is unavailable, and
    /// CRTs show static instead.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Cinematics - 10)]
    [DisallowMultipleComponent]
    public sealed class FeedCameras : MonoBehaviour
    {
        /// <summary>Marker of the ridge feed (TECH_PLAN Appendix A).</summary>
        public const string RidgeMarker = "CAM.IntroRidge";
        /// <summary>Vertical FOV of the ridge feed (a 50 mm look over the town).</summary>
        public const float RidgeFov = 21f;
        /// <summary>Downward pitch of the ridge feed in degrees.</summary>
        public const float RidgePitch = -4f;

        private static readonly Dictionary<string, FeedCamera> s_feeds = new Dictionary<string, FeedCamera>();

        private ServiceRef<IMarkerRegistry> _markers;
        private FeedCamera _ridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_feeds.Clear();

        /// <summary>Registers a feed camera (called by <see cref="FeedCamera"/> on enable).</summary>
        public static void RegisterFeed(FeedCamera feed)
        {
            if (feed == null || string.IsNullOrEmpty(feed.Id)) return;
            s_feeds[feed.Id] = feed;
        }

        /// <summary>Unregisters a feed camera.</summary>
        public static void UnregisterFeed(FeedCamera feed)
        {
            if (feed == null || string.IsNullOrEmpty(feed.Id)) return;
            if (s_feeds.TryGetValue(feed.Id, out var f) && f == feed) s_feeds.Remove(feed.Id);
        }

        /// <summary>True if a feed can be acquired now.</summary>
        public static bool IsAvailable(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (s_feeds.TryGetValue(id, out var f) && f != null) return true;
            return id == BindingIds.FeedRidge && CinematicRuntime.Feeds != null && CinematicRuntime.Feeds.CanCreateRidge();
        }

        /// <summary>Starts a feed and returns its render texture, or null when unavailable.</summary>
        public static RenderTexture Acquire(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!s_feeds.TryGetValue(id, out var f) || f == null)
            {
                if (id == BindingIds.FeedRidge && CinematicRuntime.Feeds != null) f = CinematicRuntime.Feeds.CreateRidge();
                if (f == null) return null;
            }
            return f.Acquire();
        }

        /// <summary>Releases a feed acquired with <see cref="Acquire"/>.</summary>
        public static void Release(string id)
        {
            if (!string.IsNullOrEmpty(id) && s_feeds.TryGetValue(id, out var f) && f != null) f.Release();
        }

        /// <summary>The feed camera of an id (null when not registered).</summary>
        public static FeedCamera Get(string id) => !string.IsNullOrEmpty(id) && s_feeds.TryGetValue(id, out var f) ? f : null;

        private void Awake() => CinematicRuntime.Set(this);

        private void OnEnable() => GameEvents.SectionUnloaded += OnSectionUnloaded;

        private void OnDisable() => GameEvents.SectionUnloaded -= OnSectionUnloaded;

        private void OnDestroy() => CinematicRuntime.Clear(this);

        private bool CanCreateRidge()
        {
            var reg = _markers.Value;
            return reg != null && reg.Has(RidgeMarker);
        }

        private FeedCamera CreateRidge()
        {
            if (_ridge != null) return _ridge;
            var reg = _markers.Value;
            if (reg == null || !reg.TryGet(RidgeMarker, out SceneMarker marker)) return null;
            var go = new GameObject("Cam_Feed_Ridge");
            go.transform.SetParent(transform, false);
            var yaw = marker.Heading;
            go.transform.SetPositionAndRotation(marker.Position, Quaternion.Euler(-RidgePitch, yaw, 0f));
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = RidgeFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 3000f;
            cam.cullingMask = WorldCullingMask;
            cam.clearFlags = CameraClearFlags.Skybox;
            _ridge = go.AddComponent<FeedCamera>();
            _ridge.Setup(BindingIds.FeedRidge, cam, 0f, true);
            return _ridge;
        }

        private void OnSectionUnloaded(SectionId id)
        {
            if (id != SectionId.Approach || _ridge == null) return;
            Destroy(_ridge.gameObject);
            _ridge = null;
        }

        /// <summary>World layers a world-facing camera draws (everything but view model, booth, triggers, UI).</summary>
        public static int WorldCullingMask =>
            ~(LayerIds.Bit(LayerIds.Viewmodel) | LayerIds.Bit(LayerIds.Booth) | LayerIds.Bit(LayerIds.Trigger) |
              LayerIds.Bit(LayerIds.PlayerClip) | LayerIds.Bit(5) | LayerIds.Bit(LayerIds.SmokeLOS));
    }
}
