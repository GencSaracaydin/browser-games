using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.Characters.Animation
{
    /// <summary>
    /// Maps every action id (Core <see cref="ActionIds"/> + <see cref="ExtraActionIds"/>) to how WP05 bodies play it
    /// (TECH_PLAN §7.7 scripted actions, §7.8 AC_Soldier action sub-state, CINEMATIC_DIRECTION §2 insertion needs).
    /// Full-body rows are Base-layer states selected by the <c>Action</c> int; arms rows are Arms-layer states selected by
    /// the <c>ArmsAction</c> int, so an arms action (handset, camera, "one minute" finger) plays over a held full-body
    /// action (seated in the helicopter) without ending it. The controller builder creates one state per animator-backed
    /// row; <c>HumanoidBody</c> looks rows up by id. An unknown id logs once and completes immediately (WP00 contract).
    /// Timings cite their source.
    /// </summary>
    public static class ActionTable
    {
        private static readonly ActionDef[] s_rows =
        {
            // ================================================================ full body (Base layer, Action int)
            // ---- squad scripted actions (TECH_PLAN §7.7)
            // B2 Weighbridge gate chain: 3 s kneeling cut (UAL Fixing_Kneeling loop), the chain parts at 80 % (GDD §7.2).
            new ActionDef(ActionIds.BoltCut, 1, ActionLayer.FullBody, ClipKeys.KneelWork, seconds: 3f, stow: true, hitAt: 0.8f),
            // B3 white smoke across the street (the smoke itself lasts 20 s, WP03).
            new ActionDef(ActionIds.ThrowSmokeWhite, 2, ActionLayer.FullBody, ClipKeys.ThrowFull, fallbackSeconds: 1.4f, stow: true,
                          event1: AnimEventIds.ThrowRelease, event1At: 0.45f),
            // B6 breach charge: 3 s (TECH_PLAN §7.7), HY-Motion PlantCharge fitted (≤ 1.5x), charge placed at 75 %.
            new ActionDef(ActionIds.PlantSatchel, 3, ActionLayer.FullBody, ClipKeys.PlantCharge, seconds: 3f, stow: true, hitAt: 0.75f),
            // B10 green smoke at the LZ.
            new ActionDef(ActionIds.PopSmokeGreen, 4, ActionLayer.FullBody, ClipKeys.ThrowFull, fallbackSeconds: 1.4f, stow: true,
                          event1: AnimEventIds.ThrowRelease, event1At: 0.45f),
            // B9 safe drill (UAL Fixing_Kneeling loop, GDD §15).
            new ActionDef(ActionIds.DrillLoop, 5, ActionLayer.FullBody, ClipKeys.KneelWork, loop: true, stow: true),
            // Hall door kick with the stack behind (Meshy Simple_Kick); the door gives at 40 %.
            new ActionDef(ActionIds.KickDoorStack, 6, ActionLayer.FullBody, ClipKeys.Kick, fallbackSeconds: 1.2f,
                          leftHandOnWeapon: true, hitAt: 0.4f),
            // B11 Hollis hauls Sparrow's bag into the cabin (HY-Motion HaulBag); the bag is in at 90 %.
            new ActionDef(ActionIds.HaulBag, 7, ActionLayer.FullBody, ClipKeys.Carry, fallbackSeconds: 2.5f, stow: true, hitAt: 0.9f),

            // ---- traversal (TECH_PLAN §7.15 NavMeshLinks: vault 0.6-1.4 m, climb 1.4-2.1 m, drops; GDD §15)
            new ActionDef(ActionIds.Vault, 8, ActionLayer.FullBody, ClipKeys.Vault, fallbackSeconds: 1.0f, leftHandOnWeapon: true,
                          rootMotion: true),
            new ActionDef(ActionIds.Climb, 9, ActionLayer.FullBody, ClipKeys.Climb, fallbackSeconds: 1.4f, stow: true, rootMotion: true),
            new ActionDef(ActionIds.Drop, 10, ActionLayer.FullBody, ClipKeys.Drop, fallbackSeconds: 0.8f, leftHandOnWeapon: true,
                          rootMotion: true),
            new ActionDef(ActionIds.LadderClimb, 11, ActionLayer.FullBody, ClipKeys.Ladder, loop: true, stow: true),

            // ---- insertion cold open (CINEMATIC_DIRECTION §2)
            new ActionDef(ActionIds.SitHeli, 12, ActionLayer.FullBody, ClipKeys.SitChair, loop: true, leftHandOnWeapon: true),
            new ActionDef(ActionIds.SkidDrop, 13, ActionLayer.FullBody, ClipKeys.Drop, fallbackSeconds: 0.8f, leftHandOnWeapon: true,
                          rootMotion: true),
            new ActionDef(ActionIds.ProneIdle, 14, ActionLayer.FullBody, ClipKeys.Prone, loop: true, leftHandOnWeapon: true),

            // ---- WP05 extras (ExtraActionIds; Core request pending)
            new ActionDef(ExtraActionIds.SitIdle, 15, ActionLayer.FullBody, ClipKeys.SitNervous, loop: true),
            new ActionDef(ExtraActionIds.SitTalk, 16, ActionLayer.FullBody, ClipKeys.SitTalk, loop: true),
            new ActionDef(ExtraActionIds.SitArmsCrossed, 17, ActionLayer.FullBody, ClipKeys.SitArmsCrossed, loop: true),
            new ActionDef(ExtraActionIds.LookAround, 18, ActionLayer.FullBody, ClipKeys.LookAround, fallbackSeconds: 4f,
                          leftHandOnWeapon: true),
            new ActionDef(ExtraActionIds.Kneel, 19, ActionLayer.FullBody, ClipKeys.Kneel, loop: true, leftHandOnWeapon: true),
            new ActionDef(ExtraActionIds.Talk, 20, ActionLayer.FullBody, ClipKeys.StandTalk, loop: true, stow: true),
            // B2 sentries' smoking idle (WP04 SentryBehaviour, GDD §7.2). No smoking clip exists in the content yet: a relaxed
            // neutral idle with the rifle slung reads as an unaware sentry; ended by Stop or any other action.
            new ActionDef(ActionIds.SmokeIdle, 21, ActionLayer.FullBody, ClipKeys.StandIdle, loop: true, stow: true),

            // ================================================================ arms (Arms layer, ArmsAction int)
            // B5 MID-A: Hollis raises a fist and holds it (GDD §8.5 row 1; HY-Motion FistHold).
            new ActionDef(ActionIds.FistHold, 1, ActionLayer.Arms, ClipKeys.FistHoldArms, loop: true, gesture: Gesture.FistHold,
                          handPoseOverride: (int)HandPose.Fist),
            // B1/B8 Sparrow photographs: camera up, shutter at 60 %.
            new ActionDef(ActionIds.Photograph, 2, ActionLayer.Arms, ClipKeys.CameraArms, seconds: 2.5f, stow: true,
                          gesture: Gesture.RaiseCamera, hitAt: 0.6f),
            // B6 chemlights: bend and shake, 1.6 s, snap at 40 %.
            new ActionDef(ActionIds.CrackChemlight, 3, ActionLayer.Arms, ClipKeys.InteractArms, seconds: 1.6f,
                          gesture: Gesture.CrackChemlight, hitAt: 0.4f),
            // Upper-body reload (Meshy Standing_Reload); magazine out 35 %, in 70 %.
            new ActionDef(ActionIds.Reload, 4, ActionLayer.Arms, ClipKeys.Reload, fallbackSeconds: 2.6f,
                          event1: AnimEventIds.MagOut, event1At: 0.35f, event2: AnimEventIds.MagIn, event2At: 0.7f),
            // Overhand grenade throw (UAL2 OverhandThrow); release at 45 %.
            new ActionDef(ActionIds.Throw, 5, ActionLayer.Arms, ClipKeys.ThrowArms, fallbackSeconds: 1.2f,
                          event1: AnimEventIds.ThrowRelease, event1At: 0.45f),
            // "Move up" signal with the support hand (HY-Motion MoveUp).
            new ActionDef(ActionIds.HandSignal, 6, ActionLayer.Arms, ClipKeys.SignalArms, fallbackSeconds: 1.6f, gesture: Gesture.HandSignal),
            // RPG raise telegraph: 1.2 s (GDD §14.1).
            new ActionDef(ActionIds.RpgRaise, 7, ActionLayer.Arms, ClipKeys.ArmsLauncher, seconds: 1.2f, leftHandOnWeapon: true),
            // Insertion I-6: Tinker keys the PRC-77 handset (seated or crouched; arms only).
            new ActionDef(ActionIds.HandsetToEar, 8, ActionLayer.Arms, ClipKeys.PhoneArms, loop: true, gesture: Gesture.HandsetToEar),
            // Insertion I-5: Sparrow raises the camera (arms only).
            new ActionDef(ActionIds.RaiseCamera, 9, ActionLayer.Arms, ClipKeys.CameraArms, loop: true, stow: true,
                          gesture: Gesture.RaiseCamera),
            new ActionDef(ExtraActionIds.PhoneTalk, 10, ActionLayer.Arms, ClipKeys.PhoneArms, loop: true, gesture: Gesture.HandsetToEar),
            // Outro row 7: the interrogator reaches to the scrambler toggle; the IK always aims at BOOTH.Scrambler.
            new ActionDef(ExtraActionIds.ReachToggle, 11, ActionLayer.Arms, ClipKeys.InteractArms, seconds: 1.4f, gesture: Gesture.Reach,
                          hitAt: 0.5f, gestureAlways: true),
            new ActionDef(ExtraActionIds.CheckWatch, 12, ActionLayer.Arms, ClipKeys.WatchArms, fallbackSeconds: 2f),
            new ActionDef(ExtraActionIds.OneFinger, 13, ActionLayer.Arms, ClipKeys.OneFingerArms, fallbackSeconds: 2f),
            new ActionDef(ExtraActionIds.Wave, 14, ActionLayer.Arms, ClipKeys.WaveArms, fallbackSeconds: 2f, gesture: Gesture.HandSignal),

            // ================================================================ no state
            // Crouch-run is crouched locomotion at run speed (no transition clip needed: cut-on-action design).
            new ActionDef(ActionIds.CrouchRun, 0, ActionLayer.Stance, null),
            new ActionDef(ExtraActionIds.Fire, 0, ActionLayer.Procedural, null),
            new ActionDef(ExtraActionIds.HeadLift, 0, ActionLayer.Procedural, null, seconds: 1.5f),
            new ActionDef(ExtraActionIds.GoLimp, 0, ActionLayer.Procedural, null, seconds: 1.2f),
            new ActionDef(ExtraActionIds.Stop, 0, ActionLayer.Procedural, null),
        };

        private static Dictionary<string, ActionDef> s_byId;
        private static ActionDef[] s_byBaseIndex;
        private static ActionDef[] s_byArmsIndex;

        /// <summary>Every row in declaration order.</summary>
        public static IReadOnlyList<ActionDef> All => s_rows;

        /// <summary>Highest animator action index on a layer (FullBody or Arms).</summary>
        public static int MaxIndex(ActionLayer layer)
        {
            Ensure();
            return (layer == ActionLayer.Arms ? s_byArmsIndex : s_byBaseIndex).Length - 1;
        }

        /// <summary>Looks an action up by id (case-sensitive; no logging).</summary>
        public static bool TryGet(string id, out ActionDef def)
        {
            Ensure();
            if (string.IsNullOrEmpty(id))
            {
                def = null;
                return false;
            }
            return s_byId.TryGetValue(id, out def);
        }

        /// <summary>Looks an animator-backed action up by layer and index (null if none).</summary>
        public static ActionDef ByIndex(ActionLayer layer, int index)
        {
            Ensure();
            var table = layer == ActionLayer.Arms ? s_byArmsIndex : layer == ActionLayer.FullBody ? s_byBaseIndex : null;
            return table != null && index > 0 && index < table.Length ? table[index] : null;
        }

        private static void Ensure()
        {
            if (s_byId != null) return;
            var byId = new Dictionary<string, ActionDef>(s_rows.Length);
            int maxBase = 0, maxArms = 0;
            foreach (var r in s_rows)
            {
                if (r.Layer == ActionLayer.FullBody && r.Index > maxBase) maxBase = r.Index;
                if (r.Layer == ActionLayer.Arms && r.Index > maxArms) maxArms = r.Index;
            }
            var baseIdx = new ActionDef[maxBase + 1];
            var armsIdx = new ActionDef[maxArms + 1];
            foreach (var r in s_rows)
            {
                byId[r.Id] = r;
                if (!r.HasState) continue;
                if (r.Layer == ActionLayer.FullBody) baseIdx[r.Index] = r;
                else armsIdx[r.Index] = r;
            }
            s_byBaseIndex = baseIdx;
            s_byArmsIndex = armsIdx;
            s_byId = byId;
        }
    }
}
