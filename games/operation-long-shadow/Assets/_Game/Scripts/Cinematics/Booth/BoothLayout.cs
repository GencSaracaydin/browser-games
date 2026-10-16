using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// The booth set in its local frame (GDD §8.0, TECH_PLAN Appendix A "Booth (local)"): +Z faces the CRT cart and the
    /// mirror, +X is right, Y is up, the origin is the chair's floor point; the scene root sits at world (−600, 0, −600).
    /// Shared by the scene builder, the camera presets, the inserts and the tests.
    /// Deviation (DECISIONS.md [WP09]): the scrambler trolley and the desk phone do not fit where GDD §8.0 puts them
    /// (they would intersect CRT1 and hang off the 1.7 m cart), so each sits on its own AV trolley left and right of the
    /// CRT cart; the BOOTH.Scrambler, BOOTH.Reel and BOOTH.Phone markers follow the props.
    /// </summary>
    public static class BoothLayout
    {
        /// <summary>World position of the booth root (GDD §8.0).</summary>
        public static readonly Vector3 WorldOrigin = new Vector3(-600f, 0f, -600f);

        // ---- shell (GDD §8.0: x −2.4…2.4, z −2.0…2.6, height 3.0)
        public const float MinX = -2.4f;
        public const float MaxX = 2.4f;
        public const float MinZ = -2.0f;
        public const float MaxZ = 2.6f;
        public const float Height = 3.0f;
        /// <summary>Plaster below, acoustic tile above (WP09_ART: tile on the upper band and the ceiling).</summary>
        public const float WainscotHeight = 1.5f;
        /// <summary>One-way mirror on the far wall (z 2.55, x −1.6…1.6, y 0.9…2.1).</summary>
        public static readonly Rect MirrorRect = new Rect(-1.6f, 0.9f, 3.2f, 1.2f);
        /// <summary>Mirror plane z.</summary>
        public const float MirrorZ = 2.55f;
        /// <summary>Steel door in the back wall (x range, height).</summary>
        public static readonly Vector2 DoorX = new Vector2(1.05f, 1.95f);
        public const float DoorHeight = 2.05f;
        /// <summary>Floor drain (decal centre).</summary>
        public static readonly Vector3 Drain = new Vector3(0f, 0f, 0.8f);

        // ---- people and furniture
        public static readonly Vector3 Chair = Vector3.zero;
        /// <summary>C_POV, Calder's eye.</summary>
        public static readonly Vector3 Pov = new Vector3(0f, 1.18f, 0.05f);
        /// <summary>Vertical FOV of C_POV (GDD §8.0).</summary>
        public const float PovFov = 50f;
        /// <summary>C_CLOCK (GDD §8.0).</summary>
        public static readonly Vector3 ClockCamera = new Vector3(0.9f, 2.33f, 2.20f);
        public static readonly Vector3 CrtCart = new Vector3(0f, 0f, 1.25f);
        /// <summary>Top of the long AV cart (SM_AVCartLong).</summary>
        public const float CrtCartTop = 0.80f;

        /// <summary>CRT1-6 body centres (GDD §8.0); index 0 = CRT1. CRT1-3 bottom row, CRT4-6 top row.</summary>
        public static readonly Vector3[] Crt =
        {
            new Vector3(-0.55f, 1.00f, 1.30f), new Vector3(0.00f, 1.02f, 1.30f), new Vector3(0.56f, 0.98f, 1.30f),
            new Vector3(-0.50f, 1.48f, 1.34f), new Vector3(0.04f, 1.52f, 1.34f), new Vector3(0.55f, 1.47f, 1.34f),
        };
        /// <summary>Per-CRT scale of the mismatched sets (WP09_ART: vary 0.9-1.1; bottom row trimmed to fit the cart).</summary>
        public static readonly float[] CrtScale = { 0.86f, 0.90f, 0.84f, 1.00f, 1.06f, 0.96f };
        /// <summary>Per-CRT yaw jitter in degrees (sets pushed together by hand).</summary>
        public static readonly float[] CrtYaw = { 3f, -1.5f, -4f, 2f, -2.5f, 3.5f };

        public static readonly Vector3 Clock = new Vector3(0.9f, 2.35f, 2.52f);
        public static readonly Vector3 Tube = new Vector3(0f, 2.95f, 0.6f);

        /// <summary>Scrambler trolley (SM_AVCart) left of the CRT cart.</summary>
        public static readonly Vector3 ScramblerTrolley = new Vector3(-1.42f, 0f, 0.98f);
        /// <summary>Top of SM_AVCart.</summary>
        public const float TrolleyTop = 0.85f;
        /// <summary>Scrambler box (base centre) on the trolley's right end: about 47° left of C_POV (left edge of view).</summary>
        public static readonly Vector3 Scrambler = new Vector3(-0.98f, 0.855f, 0.95f);
        /// <summary>Reel-to-reel deck (base centre) on the trolley's left end.</summary>
        public static readonly Vector3 Reel = new Vector3(-1.52f, 0.85f, 1.02f);
        /// <summary>Phone trolley (SM_AVCart) right of the CRT cart.</summary>
        public static readonly Vector3 PhoneTrolley = new Vector3(1.42f, 0f, 0.98f);
        /// <summary>Desk phone (base centre) on the phone trolley's left end.</summary>
        public static readonly Vector3 Phone = new Vector3(1.02f, 0.85f, 0.98f);

        /// <summary>Interrogator's steel table (GDD §8.0), top at <see cref="TableTop"/>.</summary>
        public static readonly Vector3 Table = new Vector3(-0.8f, 0f, -0.7f);
        public const float TableTop = 0.75f;
        public static readonly Vector2 TableSize = new Vector2(0.90f, 0.60f);
        public static readonly Vector3 Pen = new Vector3(-0.7f, 0.76f, -0.65f);
        public static readonly Vector3 Ashtray = new Vector3(-0.58f, 0.75f, -0.52f);
        public static readonly Vector3 Lighter = new Vector3(-0.98f, 0.75f, -0.60f);
        /// <summary>Hand-held cigarette smoke (GDD §8.0).</summary>
        public static readonly Vector3 Smoke = new Vector3(-0.6f, 1.7f, -0.6f);
        /// <summary>Interrogator's mark behind Calder's left shoulder.</summary>
        public static readonly Vector3 InterrogatorMark = new Vector3(-0.55f, 0f, -1.15f);
        /// <summary>Interrogator's mark at the scrambler (outro shot 7: he walks past the chair).</summary>
        public static readonly Vector3 InterrogatorAtScrambler = new Vector3(-0.72f, 0f, 0.55f);
        /// <summary>Interrogator's mark at the phone (outro shot 8).</summary>
        public static readonly Vector3 InterrogatorAtPhone = new Vector3(0.80f, 0f, 0.55f);

        /// <summary>Film camera tripod base (lens at <see cref="FilmCamLens"/>), looking at the chair.</summary>
        public static readonly Vector3 FilmCamBase = new Vector3(-1.6f, 0f, 2.2f);
        public static readonly Vector3 FilmCamLens = new Vector3(-1.6f, 1.9f, 2.2f);
        /// <summary>What the film camera frames (Calder in the chair).</summary>
        public static readonly Vector3 FilmCamTarget = new Vector3(0f, 1.0f, 0f);
        /// <summary>Deck feed camera (looks down at the reels).</summary>
        public static readonly Vector3 FeedDeck = new Vector3(-1.25f, 1.22f, 0.74f);

        // ---- insert ECU set-ups
        public static readonly Vector3 PenEcuCamera = new Vector3(-0.53f, 0.90f, -0.47f);
        public static readonly Vector3 CrtEcuCamera = new Vector3(0.02f, 1.05f, 0.86f);
        public static readonly Vector3 EyeEcuCamera = new Vector3(0.05f, 1.21f, 0.37f);
        public static readonly Vector3 EyePoint = new Vector3(0.03f, 1.18f, 0.07f);

        /// <summary>
        /// Local pose and vertical FOV of a camera preset (booth frame). Booth presets that look at a prop use
        /// look-at rotations so moving a prop in the layout keeps the framing.
        /// </summary>
        public static void Preset(BoothCameraPreset preset, out Vector3 position, out Quaternion rotation, out float verticalFov)
        {
            switch (preset)
            {
                case BoothCameraPreset.Clock:
                    position = ClockCamera;
                    rotation = Look(ClockCamera, Clock);
                    verticalFov = LensMath.VerticalFov(50f);
                    return;
                case BoothCameraPreset.PenEcu:
                    position = PenEcuCamera;
                    rotation = Look(PenEcuCamera, Pen);
                    verticalFov = LensMath.VerticalFov(85f);
                    return;
                case BoothCameraPreset.CrtEcu:
                    position = CrtEcuCamera;
                    rotation = Look(CrtEcuCamera, Crt[1]);
                    verticalFov = LensMath.VerticalFov(35f);
                    return;
                case BoothCameraPreset.EyeEcu:
                    position = EyeEcuCamera;
                    rotation = Look(EyeEcuCamera, EyePoint);
                    verticalFov = LensMath.VerticalFov(85f);
                    return;
                case BoothCameraPreset.FilmCam:
                    position = FilmCamLens + new Vector3(0.12f, 0f, -0.18f);
                    rotation = Look(position, FilmCamTarget);
                    verticalFov = LensMath.VerticalFov(40f);
                    return;
                case BoothCameraPreset.DeckFeed:
                    position = FeedDeck;
                    rotation = Look(FeedDeck, Reel + new Vector3(0f, 0.09f, 0f));
                    verticalFov = LensMath.VerticalFov(35f);
                    return;
                default:
                    position = Pov;
                    rotation = Quaternion.identity;
                    verticalFov = PovFov;
                    return;
            }
        }

        /// <summary>Rotation from <paramref name="from"/> looking at <paramref name="to"/> (Y up).</summary>
        public static Quaternion Look(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return d.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
        }
    }
}
