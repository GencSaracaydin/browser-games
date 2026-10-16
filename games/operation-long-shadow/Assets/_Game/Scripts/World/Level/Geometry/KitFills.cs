using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// What fills a wall opening (TECH_PLAN §7.15 kit: windows, doors, shutters): timber casements with louvred shutters
    /// (closed, ajar or folded back by seed), iron grilles, planked doors, steel roller shutters (shops shut for the
    /// curfew, GDD §2.2), glazed balcony doors, Crittall-style steel windows (Relay Hall) and shopfronts. Slots: "Wood",
    /// "WoodPainted", "Glass", "Iron", "MetalShutter", "SteelFrame" (<see cref="KitSlots"/>).
    /// </summary>
    public static class KitFills
    {
        /// <summary>Writes the fill of an opening.</summary>
        public static void Fill(MeshBuilder mb, WallFrame f, WallOpening o, ShutterState? forced = null)
        {
            var keep = mb.LodMask;
            switch (o.Fill)
            {
                case OpeningFill.ShutteredWindow:
                    Casement(mb, f, o);
                    Shutters(mb, f, o, forced ?? StateOf(o.Seed));
                    break;
                case OpeningFill.GrilledWindow:
                    Casement(mb, f, o);
                    Grille(mb, f, o);
                    break;
                case OpeningFill.Vent:
                    Louvre(mb, f, o);
                    break;
                case OpeningFill.Door:
                    PlankDoor(mb, f, o);
                    break;
                case OpeningFill.RollerShutter:
                    RollerShutter(mb, f, o);
                    break;
                case OpeningFill.BalconyDoor:
                    Casement(mb, f, o, true);
                    Shutters(mb, f, o, forced ?? StateOf(o.Seed + 3));
                    break;
                case OpeningFill.SteelWindow:
                    SteelWindow(mb, f, o);
                    break;
                case OpeningFill.SteelDoor:
                    SteelDoorLeaves(mb, f, o, 0.06f);
                    break;
                case OpeningFill.Shopfront:
                    Shopfront(mb, f, o);
                    break;
            }
            mb.LodMask = keep;
        }

        /// <summary>Shutter state from a seed (about half closed: the curfew, GDD §2.2).</summary>
        public static ShutterState StateOf(int seed)
        {
            var r = new DetRandom((ulong)(seed * 7919 + 17)).Value01();
            return r < 0.55f ? ShutterState.Closed : r < 0.75f ? ShutterState.Ajar : ShutterState.Open;
        }

        private static float Top(WallOpening o) => o.Shape == OpeningShape.Rect ? o.V1 : o.Springline;

        /// <summary>Timber casement: frame, a mullion and transom, glazing set back in the reveal.</summary>
        public static void Casement(MeshBuilder mb, WallFrame f, WallOpening o, bool door = false)
        {
            var top = Top(o);
            var setBack = -Mathf.Min(o.Depth - 0.04f, 0.16f);
            const float fw = 0.065f;
            mb.LodMask = MeshBuilder.LodAll;
            mb.Slot(KitSlots.Glass);
            KitTrims.FrameBox(mb, f, o.U0, o.U1, o.V0, top, setBack - 0.01f, setBack, 0x3F);
            mb.LodMask = MeshBuilder.LodDetail;
            mb.Slot(KitSlots.WoodPainted);
            KitTrims.FrameBox(mb, f, o.U0, o.U0 + fw, o.V0, top, setBack - 0.03f, setBack + 0.03f);
            KitTrims.FrameBox(mb, f, o.U1 - fw, o.U1, o.V0, top, setBack - 0.03f, setBack + 0.03f);
            KitTrims.FrameBox(mb, f, o.U0, o.U1, top - fw, top, setBack - 0.03f, setBack + 0.03f);
            if (!door) KitTrims.FrameBox(mb, f, o.U0, o.U1, o.V0, o.V0 + fw, setBack - 0.03f, setBack + 0.03f);
            KitTrims.FrameBox(mb, f, o.CenterU - 0.025f, o.CenterU + 0.025f, o.V0, top, setBack - 0.025f, setBack + 0.025f);
            var transom = o.V0 + (top - o.V0) * (door ? 0.62f : 0.68f);
            KitTrims.FrameBox(mb, f, o.U0, o.U1, transom - 0.02f, transom + 0.02f, setBack - 0.025f, setBack + 0.025f);
            if (door) KitTrims.FrameBox(mb, f, o.U0, o.U1, o.V0, o.V0 + 0.75f, setBack - 0.02f, setBack + 0.02f);
            if (o.Shape != OpeningShape.Rect)
            {
                // Fanlight infill above the springline (glass + radial bars).
                mb.Slot(KitSlots.Glass);
                mb.LodMask = MeshBuilder.LodAll;
                var pts = WallBuilder.ArchPoints(o);
                for (var i = 0; i < pts.Length - 1; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    mb.AddQuad(f.P(a.x, o.Springline, setBack), f.P(a.x, a.y, setBack), f.P(b.x, b.y, setBack), f.P(b.x, o.Springline, setBack));
                }
                mb.LodMask = MeshBuilder.LodDetail;
                mb.Slot(KitSlots.WoodPainted);
                for (var i = 0; i <= 4; i++)
                {
                    var p = pts[i * (pts.Length - 1) / 4];
                    var c = new Vector2(o.CenterU, o.Springline);
                    var from = f.P(c.x, c.y, setBack);
                    var to = f.P(p.x, p.y, setBack);
                    mb.AddTube(from, to, 0.015f, 4);
                }
            }
        }

        /// <summary>Louvred timber shutters (two leaves hinged on the jambs, opening outward and folding back flat).</summary>
        public static void Shutters(MeshBuilder mb, WallFrame f, WallOpening o, ShutterState state)
        {
            var top = Top(o);
            var h = top - o.V0;
            var leaf = o.Width * 0.5f;
            const float t = 0.035f;
            const float hingeZ = 0.03f;
            mb.Slot(KitSlots.WoodPainted);
            for (var side = 0; side < 2; side++)
            {
                var left = side == 0;
                float angle;
                switch (state)
                {
                    case ShutterState.Closed: angle = 0f; break;
                    case ShutterState.Open: angle = 174f; break;
                    default: angle = left ? 64f : 0f; break;
                }
                // Frame-local space: x along the wall, y up, z into the wall. A +Y rotation swings +x towards −z (outward).
                mb.Push(f.Matrix);
                mb.Push(Matrix4x4.TRS(new Vector3(left ? o.U0 : o.U1, o.V0, hingeZ), Quaternion.Euler(0f, left ? angle : -angle, 0f), Vector3.one));
                var sign = left ? 1f : -1f;
                mb.LodMask = MeshBuilder.LodAll;
                mb.AddBox(new Vector3(sign * leaf * 0.5f, h * 0.5f, -t * 0.5f), new Vector3(leaf - 0.01f, h, t));
                mb.LodMask = MeshBuilder.LodDetail;
                var slats = Mathf.Max(4, Mathf.RoundToInt(h / 0.09f));
                for (var i = 1; i < slats; i++)
                    mb.AddBox(new Vector3(sign * leaf * 0.5f, h * i / slats, -t - 0.006f), new Vector3(leaf - 0.1f, 0.018f, 0.012f), 0x3F & ~16);
                mb.AddBox(new Vector3(sign * 0.03f, h * 0.5f, -t - 0.01f), new Vector3(0.05f, h, 0.02f), 0x3F & ~16);
                mb.AddBox(new Vector3(sign * (leaf - 0.03f), h * 0.5f, -t - 0.01f), new Vector3(0.05f, h, 0.02f), 0x3F & ~16);
                mb.Pop();
                mb.Pop();
            }
        }

        /// <summary>Wrought-iron grille: vertical bars and two flat bars, set in the reveal in front of the glazing.</summary>
        public static void Grille(MeshBuilder mb, WallFrame f, WallOpening o)
        {
            var top = Top(o);
            mb.Slot(KitSlots.Iron);
            mb.LodMask = MeshBuilder.LodDetail;
            var w = -0.05f;
            var count = Mathf.Max(3, Mathf.RoundToInt(o.Width / 0.13f));
            for (var i = 1; i < count; i++)
            {
                var u = o.U0 + o.Width * i / count;
                mb.AddTube(f.P(u, o.V0, w), f.P(u, top, w), 0.008f, 4);
            }
            for (var k = 1; k <= 2; k++)
            {
                var v = o.V0 + (top - o.V0) * k / 3f;
                KitTrims.FrameBox(mb, f, o.U0, o.U1, v - 0.02f, v + 0.02f, w - 0.006f, w + 0.006f);
            }
        }

        /// <summary>Timber louvre for vents and small high windows.</summary>
        public static void Louvre(MeshBuilder mb, WallFrame f, WallOpening o)
        {
            var top = Top(o);
            mb.Slot(KitSlots.Wood);
            mb.LodMask = MeshBuilder.LodAll;
            var slats = Mathf.Max(2, Mathf.RoundToInt((top - o.V0) / 0.08f));
            for (var i = 0; i < slats; i++)
            {
                var v = o.V0 + (top - o.V0) * (i + 0.5f) / slats;
                mb.Push(f.Matrix);
                mb.Push(Matrix4x4.TRS(new Vector3(o.CenterU, v, 0.1f), Quaternion.Euler(-35f, 0f, 0f), Vector3.one));
                mb.AddBox(Vector3.zero, new Vector3(o.Width, 0.012f, 0.09f));
                mb.Pop();
                mb.Pop();
            }
        }

        /// <summary>Planked timber door with a frame, ledges on the face and an iron ring pull.</summary>
        public static void PlankDoor(MeshBuilder mb, WallFrame f, WallOpening o)
        {
            var top = Top(o);
            var w = -Mathf.Min(o.Depth - 0.04f, 0.12f);
            mb.Slot(KitSlots.WoodPainted);
            mb.LodMask = MeshBuilder.LodAll;
            KitTrims.FrameBox(mb, f, o.U0, o.U1, o.V0, top, w - 0.05f, w);
            if (o.Shape != OpeningShape.Rect)
            {
                var pts = WallBuilder.ArchPoints(o);
                for (var i = 0; i < pts.Length - 1; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    mb.AddQuad(f.P(a.x, o.Springline, w), f.P(a.x, a.y, w), f.P(b.x, b.y, w), f.P(b.x, o.Springline, w));
                }
            }
            mb.LodMask = MeshBuilder.LodDetail;
            mb.Slot(KitSlots.Wood);
            var planks = Mathf.Max(3, Mathf.RoundToInt(o.Width / 0.16f));
            for (var i = 1; i < planks; i++)
            {
                var u = o.U0 + o.Width * i / planks;
                KitTrims.FrameBox(mb, f, u - 0.006f, u + 0.006f, o.V0 + 0.02f, top - 0.02f, w, w + 0.006f);
            }
            KitTrims.FrameBox(mb, f, o.U0 + 0.05f, o.U1 - 0.05f, o.V0 + 0.25f, o.V0 + 0.37f, w, w + 0.025f);
            KitTrims.FrameBox(mb, f, o.U0 + 0.05f, o.U1 - 0.05f, top - 0.45f, top - 0.33f, w, w + 0.025f);
            mb.Slot(KitSlots.Iron);
            mb.AddTube(f.P(o.U1 - 0.18f, o.V0 + 1.05f, w), f.P(o.U1 - 0.18f, o.V0 + 1.05f, w + 0.05f), 0.035f, 6, true);
        }

        /// <summary>Steel roller shutter: corrugated curtain (horizontal ribs), guide rails, bottom bar and the roller box.</summary>
        public static void RollerShutter(MeshBuilder mb, WallFrame f, WallOpening o)
        {
            var top = Top(o);
            var w = -0.09f;
            mb.Slot(KitSlots.MetalShutter);
            mb.LodMask = MeshBuilder.LodAll;
            // Explicit UVs rotate the corrugated texture so the ribs run horizontally.
            mb.AddQuad(f.P(o.U0, o.V0, w), f.P(o.U0, top - 0.3f, w), f.P(o.U1, top - 0.3f, w), f.P(o.U1, o.V0, w),
                new Vector2(o.V0, f.UvU + o.U0), new Vector2(top - 0.3f, f.UvU + o.U0), new Vector2(top - 0.3f, f.UvU + o.U1), new Vector2(o.V0, f.UvU + o.U1));
            mb.Slot(KitSlots.SteelFrame);
            KitTrims.FrameBox(mb, f, o.U0, o.U1, top - 0.32f, top, w - 0.08f, w + 0.12f);
            mb.LodMask = MeshBuilder.LodDetail;
            KitTrims.FrameBox(mb, f, o.U0, o.U0 + 0.05f, o.V0, top - 0.3f, w - 0.03f, w + 0.03f);
            KitTrims.FrameBox(mb, f, o.U1 - 0.05f, o.U1, o.V0, top - 0.3f, w - 0.03f, w + 0.03f);
            KitTrims.FrameBox(mb, f, o.U0, o.U1, o.V0, o.V0 + 0.06f, w - 0.01f, w + 0.03f);
            mb.Slot(KitSlots.Iron);
            KitTrims.FrameBox(mb, f, o.CenterU - 0.08f, o.CenterU + 0.08f, o.V0 + 0.07f, o.V0 + 0.11f, w + 0.03f, w + 0.06f);
        }

        /// <summary>Crittall-style steel window (Art Deco Relay Hall, gatehouse): thin mullion/transom grid over glass.</summary>
        public static void SteelWindow(MeshBuilder mb, WallFrame f, WallOpening o)
        {
            var top = Top(o);
            var w = -Mathf.Min(o.Depth - 0.03f, 0.12f);
            mb.Slot(KitSlots.Glass);
            mb.LodMask = MeshBuilder.LodAll;
            KitTrims.FrameBox(mb, f, o.U0, o.U1, o.V0, top, w - 0.008f, w, 0x3F);
            mb.Slot(KitSlots.SteelFrame);
            KitTrims.FrameBox(mb, f, o.U0, o.U1, o.V0, o.V0 + 0.05f, w - 0.02f, w + 0.02f);
            KitTrims.FrameBox(mb, f, o.U0, o.U1, top - 0.05f, top, w - 0.02f, w + 0.02f);
            KitTrims.FrameBox(mb, f, o.U0, o.U0 + 0.05f, o.V0, top, w - 0.02f, w + 0.02f);
            KitTrims.FrameBox(mb, f, o.U1 - 0.05f, o.U1, o.V0, top, w - 0.02f, w + 0.02f);
            mb.LodMask = MeshBuilder.LodDetail;
            var cols = Mathf.Max(2, Mathf.RoundToInt(o.Width / 0.42f));
            var rows = Mathf.Max(2, Mathf.RoundToInt((top - o.V0) / 0.36f));
            for (var i = 1; i < cols; i++)
            {
                var u = o.U0 + o.Width * i / cols;
                KitTrims.FrameBox(mb, f, u - 0.012f, u + 0.012f, o.V0, top, w - 0.015f, w + 0.015f);
            }
            for (var j = 1; j < rows; j++)
            {
                var v = o.V0 + (top - o.V0) * j / rows;
                KitTrims.FrameBox(mb, f, o.U0, o.U1, v - 0.012f, v + 0.012f, w - 0.015f, w + 0.015f);
            }
        }

        /// <summary>Static double steel door with raised panels (doors that never open).</summary>
        public static void SteelDoorLeaves(MeshBuilder mb, WallFrame f, WallOpening o, float thickness)
        {
            var top = Top(o);
            var w = -Mathf.Min(o.Depth - 0.04f, 0.14f);
            mb.Slot(KitSlots.SteelFrame);
            mb.LodMask = MeshBuilder.LodAll;
            KitTrims.FrameBox(mb, f, o.U0, o.CenterU - 0.004f, o.V0, top, w - thickness, w);
            KitTrims.FrameBox(mb, f, o.CenterU + 0.004f, o.U1, o.V0, top, w - thickness, w);
            mb.LodMask = MeshBuilder.LodDetail;
            for (var leaf = 0; leaf < 2; leaf++)
            {
                var a = leaf == 0 ? o.U0 : o.CenterU;
                var b = leaf == 0 ? o.CenterU : o.U1;
                KitTrims.FrameBox(mb, f, a + 0.1f, b - 0.1f, o.V0 + 0.2f, o.V0 + (top - o.V0) * 0.45f, w, w + 0.015f);
                KitTrims.FrameBox(mb, f, a + 0.1f, b - 0.1f, o.V0 + (top - o.V0) * 0.52f, top - 0.2f, w, w + 0.015f);
            }
        }

        /// <summary>Glazed shopfront with a timber frame, stall riser and transom.</summary>
        public static void Shopfront(MeshBuilder mb, WallFrame f, WallOpening o)
        {
            var top = Top(o);
            var w = -Mathf.Min(o.Depth - 0.04f, 0.14f);
            mb.Slot(KitSlots.WoodPainted);
            mb.LodMask = MeshBuilder.LodAll;
            KitTrims.FrameBox(mb, f, o.U0, o.U1, o.V0, o.V0 + 0.55f, w - 0.06f, w + 0.02f);
            mb.Slot(KitSlots.Glass);
            KitTrims.FrameBox(mb, f, o.U0, o.U1, o.V0 + 0.55f, top, w - 0.01f, w, 0x3F);
            mb.LodMask = MeshBuilder.LodDetail;
            mb.Slot(KitSlots.WoodPainted);
            var panes = Mathf.Max(2, Mathf.RoundToInt(o.Width / 0.8f));
            for (var i = 0; i <= panes; i++)
            {
                var u = o.U0 + o.Width * i / panes;
                KitTrims.FrameBox(mb, f, u - 0.03f, u + 0.03f, o.V0 + 0.55f, top, w - 0.03f, w + 0.03f);
            }
            var transom = top - 0.45f;
            KitTrims.FrameBox(mb, f, o.U0, o.U1, transom - 0.03f, transom + 0.03f, w - 0.03f, w + 0.03f);
        }
    }
}
