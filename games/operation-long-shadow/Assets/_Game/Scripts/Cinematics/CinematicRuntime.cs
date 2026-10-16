using LongShadow.Cinematics.FX;
using LongShadow.Cinematics.Inserts;
using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// The live WP09 runtime objects, for code in this assembly that cannot use GameServices (timeline behaviours, booth
    /// components): the camera arbiter, the overlay canvas, the screen FX, the services and the feed cameras. Each system
    /// registers itself in Awake and clears its slot in OnDestroy. Reset on SubsystemRegistration (R6). Other packages use
    /// the Core interfaces (ICinematicService, IInsertService, IScreenFX) instead.
    /// </summary>
    public static class CinematicRuntime
    {
        /// <summary>Camera arbiter (player / cinematic world / booth).</summary>
        public static CinematicCameras Cameras { get; private set; }
        /// <summary>Full-screen overlay canvas (fades, letterbox, type, skip prompt).</summary>
        public static CinematicOverlay Overlay { get; private set; }
        /// <summary>Screen effects (IScreenFX implementation).</summary>
        public static ScreenFXService ScreenFx { get; private set; }
        /// <summary>Sequence player (ICinematicService implementation).</summary>
        public static CinematicService Service { get; private set; }
        /// <summary>Insert player (IInsertService implementation).</summary>
        public static InsertService Inserts { get; private set; }
        /// <summary>Feed cameras (CRT and screen render textures).</summary>
        public static FeedCameras Feeds { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cameras = null;
            Overlay = null;
            ScreenFx = null;
            Service = null;
            Inserts = null;
            Feeds = null;
        }

        /// <summary>Registers the camera arbiter.</summary>
        public static void Set(CinematicCameras c) => Cameras = c;
        /// <summary>Registers the overlay.</summary>
        public static void Set(CinematicOverlay o) => Overlay = o;
        /// <summary>Registers the screen FX.</summary>
        public static void Set(ScreenFXService s) => ScreenFx = s;
        /// <summary>Registers the cinematic service.</summary>
        public static void Set(CinematicService s) => Service = s;
        /// <summary>Registers the insert service.</summary>
        public static void Set(InsertService s) => Inserts = s;
        /// <summary>Registers the feed cameras.</summary>
        public static void Set(FeedCameras f) => Feeds = f;

        /// <summary>Clears a slot if it still holds <paramref name="o"/>.</summary>
        public static void Clear(Object o)
        {
            if (o == null) return;
            if (ReferenceEquals(Cameras, o)) Cameras = null;
            if (ReferenceEquals(Overlay, o)) Overlay = null;
            if (ReferenceEquals(ScreenFx, o)) ScreenFx = null;
            if (ReferenceEquals(Service, o)) Service = null;
            if (ReferenceEquals(Inserts, o)) Inserts = null;
            if (ReferenceEquals(Feeds, o)) Feeds = null;
        }
    }
}
