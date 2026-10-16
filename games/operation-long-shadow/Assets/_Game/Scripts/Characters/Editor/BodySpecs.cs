using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Every body prefab WP05 ships (TECH_PLAN §7.8, §8.6; ASSET_MANIFEST CHR_*), mirroring the content pipeline's
    /// Tools/characters/bodies_spec.py (which FBX carries which avatar, props and retexture). HP and armour are the GDD
    /// §14.1 defaults (WP04 overrides per archetype through Health.OverrideMax). Fallback models keep roles playable while
    /// a P1/P2 body (Harrou, Magpie) has not been made; with no model at all the generated mannequin stands in.
    /// </summary>
    public static class BodySpecs
    {
        private static readonly Color Friendly = Placeholders.Friendly;
        private static readonly Color Hostile = Placeholders.Hostile;

        private static readonly BodySpec[] s_all =
        {
            // ---- the player (D19) and Calder's other bodies
            new BodySpec { Prefab = "PFB_Calder_FP", Manifest = "CHR_CALDER", Kind = BodyKind.FirstPerson, Team = Team.Player,
                           Models = new[] { "Calder/SK_Calder_FPArms.fbx", "Calder/SK_Calder.fbx" }, Weapon = WeaponIds.Commando, Placeholder = Friendly,
                           Note = "First-person arms on Viewmodel; head, neck and thighs scaled to 0; FPArmsIK to IWeaponGrips; dive watch." },
            new BodySpec { Prefab = "PFB_Calder_Shadow", Manifest = "CHR_CALDER", Kind = BodyKind.Shadow, Team = Team.Player, Controller = ControllerRole.Squad,
                           Models = new[] { "Calder/SK_Calder.fbx" }, Weapon = WeaponIds.Commando, Placeholder = Friendly, Cabin = true,
                           Note = "ShadowsOnly on Player; AC_ShadowProxy; cinematic mode (insertion) switches to AOC_Squad." },
            new BodySpec { Prefab = "PFB_Calder", Manifest = "CHR_CALDER", Kind = BodyKind.Cinematic, Team = Team.Player, Controller = ControllerRole.Squad,
                           Models = new[] { "Calder/SK_Calder.fbx" }, Weapon = WeaponIds.Commando, Speaker = SpeakerIds.Calder, Placeholder = Friendly,
                           Cabin = true, Note = "Third-person Calder for cinematics (intro CRT feeds, ID photo renders)." },
            new BodySpec { Prefab = "PFB_Calder_Booth", Manifest = "CHR_CALDER", Kind = BodyKind.Booth, Controller = ControllerRole.Civilian,
                           Models = new[] { "Calder/SK_Calder_Booth.fbx", "Calder/SK_Calder.fbx" }, Speaker = SpeakerIds.Calder, BoothCalder = true,
                           Placeholder = Friendly, Note = "Seated, cuffed prisoner (Session 12); Session 11 reel head via SetSession11." },
            // ---- squad (GDD §4.1, §15)
            new BodySpec { Prefab = "PFB_Hollis", Manifest = "CHR_HOLLIS", Kind = BodyKind.Squad, Controller = ControllerRole.Squad, Team = Team.Squad,
                           Models = new[] { "Hollis/SK_Hollis.fbx" }, Weapon = WeaponIds.Commando, Speaker = SpeakerIds.Hollis, ActorId = SquadIds.Hollis,
                           Hp = 100f, Placeholder = Friendly, Cabin = true },
            new BodySpec { Prefab = "PFB_Sparrow", Manifest = "CHR_SPARROW", Kind = BodyKind.Squad, Controller = ControllerRole.Squad, Team = Team.Squad,
                           Models = new[] { "Sparrow/SK_Sparrow.fbx" }, Weapon = WeaponIds.Commando, Speaker = SpeakerIds.Sparrow, ActorId = SquadIds.Sparrow,
                           Hp = 100f, Placeholder = Friendly, Cabin = true },
            new BodySpec { Prefab = "PFB_Tinker", Manifest = "CHR_TINKER", Kind = BodyKind.Squad, Controller = ControllerRole.Squad, Team = Team.Squad,
                           Models = new[] { "Tinker/SK_Tinker.fbx" }, Weapon = WeaponIds.Commando, Speaker = SpeakerIds.Tinker, ActorId = SquadIds.Tinker,
                           Hp = 100f, Placeholder = Friendly, Cabin = true },
            // ---- Harrou Customs Legion (GDD §3, §14.1: rifleman 100 HP, AK)
            new BodySpec { Prefab = "PFB_LegionA", Manifest = "CHR_LEGION_A", Kind = BodyKind.Enemy, Controller = ControllerRole.Legion, Team = Team.Legion,
                           Models = new[] { "LegionA/SK_LegionA.fbx" }, Weapon = WeaponIds.AK, Hp = 100f, Placeholder = Hostile },
            new BodySpec { Prefab = "PFB_LegionB", Manifest = "CHR_LEGION_B", Kind = BodyKind.Enemy, Controller = ControllerRole.Legion, Team = Team.Legion,
                           Models = new[] { "LegionB/SK_LegionB.fbx", "LegionA/SK_LegionA.fbx" }, Weapon = WeaponIds.AK, Hp = 100f, Placeholder = Hostile },
            new BodySpec { Prefab = "PFB_LegionC", Manifest = "CHR_LEGION_C", Kind = BodyKind.Enemy, Controller = ControllerRole.Legion, Team = Team.Legion,
                           Models = new[] { "LegionC/SK_LegionC.fbx", "LegionB/SK_LegionB.fbx" }, Weapon = WeaponIds.AK, Hp = 100f, Placeholder = Hostile },
            // ---- Greycoats (GDD §14.1: 150 HP, vest torso x0.8, AKMS)
            new BodySpec { Prefab = "PFB_GreycoatA", Manifest = "CHR_GREYCOAT_A", Kind = BodyKind.Enemy, Controller = ControllerRole.Greycoat, Team = Team.Greycoat,
                           Models = new[] { "GreycoatA/SK_GreycoatA.fbx" }, Weapon = WeaponIds.AKMS, Hp = 150f, TorsoArmour = 0.8f, Placeholder = Hostile },
            new BodySpec { Prefab = "PFB_GreycoatB", Manifest = "CHR_GREYCOAT_B", Kind = BodyKind.Enemy, Controller = ControllerRole.Greycoat, Team = Team.Greycoat,
                           Models = new[] { "GreycoatB/SK_GreycoatB.fbx", "GreycoatA/SK_GreycoatA.fbx" }, Weapon = WeaponIds.AKMS, Hp = 150f, TorsoArmour = 0.8f,
                           Placeholder = Hostile, Note = "Also the marksman (B5): WP04 swaps the weapon to SVD." },
            new BodySpec { Prefab = "PFB_GreycoatB_NVG", Manifest = "CHR_GREYCOAT_B", Kind = BodyKind.Enemy, Controller = ControllerRole.Greycoat, Team = Team.Greycoat,
                           Models = new[] { "GreycoatB/SK_GreycoatB_NVG.fbx", "GreycoatB/SK_GreycoatB.fbx" }, Weapon = WeaponIds.AKMS, Hp = 150f, TorsoArmour = 0.8f,
                           Placeholder = Hostile, Note = "NVG flanker E10-04 (B10)." },
            // ---- supporting cast
            new BodySpec { Prefab = "PFB_Interrogator", Manifest = "CHR_INTERROGATOR", Kind = BodyKind.Booth, Controller = ControllerRole.Civilian,
                           Models = new[] { "Interrogator/SK_Interrogator.fbx" }, Speaker = SpeakerIds.Interrogator, Placeholder = Placeholders.Greybox,
                           Note = "Booth (TALLOW): stands; ReachToggle to BOOTH.Scrambler, PhoneTalk." },
            new BodySpec { Prefab = "PFB_Harrou", Manifest = "CHR_HARROU", Kind = BodyKind.Cinematic, Controller = ControllerRole.Civilian,
                           Models = new[] { "Harrou/SK_Harrou.fbx", "Interrogator/SK_Interrogator.fbx" }, Speaker = SpeakerIds.Harrou,
                           Note = "8 mm footage only (face scratched out on film); P1, falls back to the interrogator's body." },
            new BodySpec { Prefab = "PFB_Magpie", Manifest = "CHR_MAGPIE", Kind = BodyKind.Cinematic, Controller = ControllerRole.Squad, Team = Team.Squad,
                           Models = new[] { "Magpie/SK_Magpie.fbx" }, Speaker = SpeakerIds.Magpie, Placeholder = Friendly, Cabin = true,
                           Note = "Pilot in the cockpit (P2): mannequin until a body is made." },
        };

        /// <summary>Every body.</summary>
        public static IReadOnlyList<BodySpec> All => s_all;

        /// <summary>A body by prefab name, or null.</summary>
        public static BodySpec Find(string prefab)
        {
            foreach (var s in s_all)
                if (s.Prefab == prefab) return s;
            return null;
        }
    }
}
