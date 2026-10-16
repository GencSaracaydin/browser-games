using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Kit
{
    /// <summary>
    /// KitGenerator (TECH_PLAN §7.15): writes the modular Sardahn town kit as standalone pieces — 3 m wall bays in plaster,
    /// mudbrick, sandstone, Art Deco render and concrete (plain, shuttered window, plank door, round-arched arcade bay — never
    /// pointed — and a damaged variant), trims (plinth, string course, cornice, coping), timber balcony, awning, lamp bracket
    /// and post, stairs, ladder, spiral stair, rooftop props (water tank, antenna, stair hut, pigeon loft, sandbag ring),
    /// street pieces (bollard, kerb, fountain) and compound pieces (chain-link, razor wire, barrier boom, flood tower,
    /// goalpost). Pieces are saved as LOD0/LOD1 mesh assets with lightmap UVs under <see cref="LevelPaths.KitMeshes"/> and as
    /// prefabs under <see cref="LevelPaths.KitPrefabs"/>; the section builders generate the same geometry in place (one
    /// mesh per block), so the prefabs serve set dressing and art review (Dev_Kit scene).
    /// </summary>
    public static class KitGenerator
    {
        /// <summary>Bay width of the wall kit.</summary>
        public const float Bay = 3f;
        /// <summary>Storey height of the wall kit.</summary>
        public const float Storey = 3.2f;

        /// <summary>Every kit piece: name, builder, lightmap scale.</summary>
        public static IReadOnlyList<KeyValuePair<string, Action<MeshBuilder>>> Pieces => s_pieces ?? (s_pieces = Define());

        private static List<KeyValuePair<string, Action<MeshBuilder>>> s_pieces;

        /// <summary>Builds every piece into meshes and prefabs; returns the count.</summary>
        public static int BuildAll()
        {
            BuilderUtil.EnsureFolder(LevelPaths.KitMeshes);
            BuilderUtil.EnsureFolder(LevelPaths.KitPrefabs);
            var n = 0;
            foreach (var kv in Pieces)
            {
                var mb = new MeshBuilder();
                kv.Value(mb);
                var go = LevelObjects.Build("KIT_" + kv.Key, null, Vector3.zero, Quaternion.identity, mb, LevelObjectOptions.Architecture(LevelPaths.KitMeshes, WorldConst.LightmapScaleHouse));
                BuilderUtil.SavePrefab(go, LevelPaths.KitPrefabs + "/KIT_" + kv.Key + ".prefab");
                n++;
            }
            AssetDatabase.SaveAssets();
            LSLog.Info(LogCategory.Level, "Kit: " + n + " pieces written to " + LevelPaths.KitPrefabs);
            return n;
        }

        /// <summary>Lays every kit prefab out in a grid in <c>Assets/_Game/Scenes/Dev/Dev_Kit.unity</c>.</summary>
        public static void BuildReviewScene()
        {
            BuildAll();
            BuilderUtil.EnsureFolder(Sections.DevFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var i = 0;
            foreach (var kv in Pieces)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelPaths.KitPrefabs + "/KIT_" + kv.Key + ".prefab");
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3((i % 8) * 6f, 0f, (i / 8) * 8f);
                i++;
            }
            EditorSceneManager.SaveScene(scene, Sections.DevFolder + "/Dev_Kit.unity");
        }

        private static List<KeyValuePair<string, Action<MeshBuilder>>> Define()
        {
            var l = new List<KeyValuePair<string, Action<MeshBuilder>>>();
            void Add(string name, Action<MeshBuilder> a) => l.Add(new KeyValuePair<string, Action<MeshBuilder>>(name, a));
            var styles = new[]
            {
                new KeyValuePair<string, string>("Plaster", KitSlots.Plaster(0)), new KeyValuePair<string, string>("Mudbrick", KitSlots.Mudbrick),
                new KeyValuePair<string, string>("Sandstone", KitSlots.Sandstone), new KeyValuePair<string, string>("Deco", KitSlots.DecoRender),
                new KeyValuePair<string, string>("Concrete", KitSlots.Concrete),
            };
            foreach (var s in styles)
            {
                var slot = s.Value;
                var trim = s.Key == "Deco" ? KitSlots.DecoTrim : s.Key == "Sandstone" || s.Key == "Plaster" ? KitSlots.SandstoneTrim : KitSlots.PlasterRough;
                Add("Wall_" + s.Key + "_Plain", mb => WallBay(mb, slot, trim, null, false));
                Add("Wall_" + s.Key + "_Window", mb => WallBay(mb, slot, trim, WallOpening.Make(0.95f, 2.05f, 1.0f, 2.4f, OpeningShape.Rect, 0.32f, false, OpeningFill.ShutteredWindow, 3), false));
                Add("Wall_" + s.Key + "_Door", mb => WallBay(mb, slot, trim, WallOpening.Make(0.95f, 2.05f, 0f, 2.3f, OpeningShape.Rect, 0.32f, false, OpeningFill.Door, 4, true), false));
                Add("Wall_" + s.Key + "_Arch", mb => WallBay(mb, slot, trim, WallOpening.Make(0.45f, 2.55f, 0f, 2.9f, OpeningShape.RoundArch, 0.32f, false, OpeningFill.None, 5, true), false));
                Add("Wall_" + s.Key + "_Damaged", mb => WallBay(mb, slot, trim, WallOpening.Make(0.95f, 2.05f, 1.0f, 2.4f, OpeningShape.Rect, 0.32f, false, OpeningFill.GrilledWindow, 6), true));
            }
            Add("Deco_FinPanel", mb =>
            {
                var f = new WallFrame(new Vector3(-Bay * 0.5f, 0f, 0f), Vector3.right, Bay);
                WallBuilder.Face(mb, f, 0f, Storey * 2f, null, KitSlots.DecoRender, KitSlots.DecoTrim, null);
                KitTrims.DecoFin(mb, f, Bay * 0.5f, 1.2f, Storey * 2f + 0.6f, 0.35f, 0.3f, KitSlots.DecoTrim);
                KitTrims.SpeedLines(mb, f, Storey * 2f - 0.6f, 0.2f, Bay - 0.2f, KitSlots.DecoTrim);
                mb.AddCollider(new Vector3(0f, Storey, 0.15f), new Vector3(Bay, Storey * 2f, 0.3f), SurfaceType.Concrete);
            });
            Add("Trim_Cornice", mb => Trims(mb, t => KitTrims.Cornice(mb, t, 3.0f, KitSlots.SandstoneTrim)));
            Add("Trim_StringCourse", mb => Trims(mb, t => KitTrims.StringCourse(mb, t, 3.0f, 0.2f, 0.08f, KitSlots.SandstoneTrim)));
            Add("Trim_Plinth", mb => Trims(mb, t => KitTrims.Plinth(mb, t, 0.8f, 0.06f, KitSlots.SandstoneTrim)));
            Add("Trim_Coping", mb => KitTrims.Coping(mb, -1.5f, -0.15f, 1.5f, 0.15f, 1.0f, 0.3f, 0.05f, 0.1f, KitSlots.SandstoneTrim));
            Add("Balcony_3m", mb =>
            {
                var f = new WallFrame(new Vector3(-Bay * 0.5f, 0f, 0f), Vector3.right, Bay);
                KitStructures.Balcony(mb, f, 0.2f, Bay - 0.2f, 3.6f, 1.1f, 7);
            });
            Add("Awning_3m", mb =>
            {
                var wl = new Vector3(-1.5f, 3.0f, 0f);
                var wr = new Vector3(1.5f, 3.0f, 0f);
                var fl = new Vector3(-1.5f, 2.4f, -2f);
                var fr = new Vector3(1.5f, 2.4f, -2f);
                KitStructures.ClothPanel(mb, wl, wr, fl, fr, 0.12f, false, 1, KitSlots.Cloth(1));
                KitStructures.AwningFrame(mb, wl, wr, fl, fr, KitSlots.WoodDark);
            });
            Add("Awning_3m_Torn", mb =>
            {
                var wl = new Vector3(-1.5f, 3.0f, 0f);
                var wr = new Vector3(1.5f, 3.0f, 0f);
                var fl = new Vector3(-1.5f, 2.4f, -2f);
                var fr = new Vector3(1.5f, 2.4f, -2f);
                KitStructures.ClothPanel(mb, wl, wr, fl, fr, 0.2f, true, 8, KitSlots.Cloth(1));
                KitStructures.AwningFrame(mb, wl, wr, fl, fr, KitSlots.WoodDark);
            });
            Add("Lamp_Bracket", mb => KitStreet.LampBracket(mb, Vector3.zero, 180f, LampLayout.BracketHeadHeight, LampLayout.Outreach));
            Add("Lamp_Post", mb => KitStreet.LampPost(mb, Vector3.zero, 0f, LampLayout.PostHeadHeight, LampLayout.Outreach));
            Add("Stairs_3m", mb => KitStructures.Stairs(mb, Vector3.zero, Vector3.forward, 1.1f, 3f, true, KitSlots.ConcreteFloor, KitSlots.Concrete, SurfaceType.Concrete));
            Add("Stairs_Steel_3m", mb => KitStructures.Stairs(mb, Vector3.zero, Vector3.forward, 1.0f, 3f, false, KitSlots.SteelFrame, KitSlots.SteelFrame, SurfaceType.Metal));
            Add("Ladder_4m", mb => KitStructures.Ladder(mb, Vector3.zero, new Vector3(0f, 4f, 0f), Vector3.back, 0.5f, KitSlots.SteelFrame, true));
            Add("SpiralStair_4m", mb => KitStructures.SpiralStair(mb, Vector3.zero, 1.45f, 4.5f, 180f, 15f, KitSlots.SteelFrame, KitSlots.Iron, KitSlots.Iron, SurfaceType.Metal));
            Add("Roof_WaterTank", mb => KitRoofProps.WaterTank(mb, Vector3.zero, 0.6f, 2));
            Add("Roof_WaterTank_Box", mb => KitRoofProps.WaterTank(mb, Vector3.zero, 0.6f, 3));
            Add("Roof_TvAntenna", mb => KitRoofProps.TvAntenna(mb, Vector3.zero, 3f, 4));
            Add("Roof_StairHut", mb => KitRoofProps.StairHut(mb, Vector3.zero, 0f, KitSlots.Plaster(2), 5));
            Add("Roof_PigeonLoft", mb => KitRoofProps.PigeonLoft(mb, Vector3.zero, 0f));
            Add("Roof_SandbagRing", mb => KitRoofProps.SandbagRing(mb, Vector3.zero, 1.35f, 0.8f, 90f, 6));
            Add("Roof_LaundryLine", mb => KitRoofProps.LaundryLine(mb, new Vector3(-4f, 0f, 0f), new Vector3(4f, 0f, 0f), 9, true));
            Add("Street_Bollard", mb => KitStreet.Bollard(mb, Vector3.zero));
            Add("Street_Kerb_3m", mb => KitStreet.Kerb(mb, new Vector3(-1.5f, 0f, 0f), new Vector3(1.5f, 0f, 0f), KitSlots.SandstoneTrim));
            Add("Street_Fountain", mb => KitStreet.Fountain(mb, Vector3.zero, 2f));
            Add("Yard_ChainLink_5m", mb => KitIndustrial.ChainLinkFence(mb, new Vector3(-2.5f, 0f, 0f), new Vector3(2.5f, 0f, 0f), 2.4f, KitSlots.SheetMetal));
            Add("Yard_RazorWire_5m", mb => KitIndustrial.RazorWire(mb, new Vector3(-2.5f, 0.4f, 0f), new Vector3(2.5f, 0.4f, 0f), 0.6f, 1.6f));
            Add("Yard_BarrierBoom", mb => KitIndustrial.BarrierBoom(mb, Vector3.zero, 90f, 6f, false, KitSlots.PaintRed, KitSlots.PaintWhite));
            Add("Yard_FloodTower", mb => KitIndustrial.FloodTower(mb, Vector3.zero, 7.6f, 0f));
            Add("Yard_CompoundWall_6m", mb => Landmarks.CompoundWall(mb, new Vector3(-3f, 0f, 0f), new Vector3(3f, 0f, 0f), true, true));
            Add("Pitch_Goalpost", mb => KitIndustrial.Goalpost(mb, Vector3.zero, 0f));
            return l;
        }

        /// <summary>One 3 m storey bay: face, reveal, fill, trims (sill, lintel/surround), plinth; damage = rough slot + rubble.</summary>
        public static void WallBay(MeshBuilder mb, string slot, string trim, WallOpening? opening, bool damaged)
        {
            var f = new WallFrame(new Vector3(-Bay * 0.5f, 0f, 0f), Vector3.right, Bay);
            var face = damaged ? KitSlots.PlasterDamaged : slot;
            var openings = opening.HasValue ? new[] { opening.Value } : null;
            WallBuilder.Face(mb, f, 0f, Storey, openings, face, slot, KitSlots.InteriorCard);
            if (opening.HasValue)
            {
                var o = opening.Value;
                KitFills.Fill(mb, f, o);
                if (o.Shape == OpeningShape.Rect && o.V0 > 0.1f) KitTrims.Sill(mb, f, o, trim);
                if (o.Shape == OpeningShape.Rect) KitTrims.TimberLintel(mb, f, o, KitSlots.WoodDark);
                else KitTrims.Surround(mb, f, o, 0.3f, 0.08f, trim, true);
            }
            KitTrims.Plinth(mb, f, 0.6f, 0.05f, trim);
            var back = new WallFrame(f.Origin + f.U * Bay + Vector3.forward * 0.3f, Vector3.left, Bay);
            WallBuilder.Face(mb, back, 0f, Storey, null, KitSlots.PlasterRough, KitSlots.PlasterRough, null);
            mb.AddCollider(new Vector3(0f, Storey * 0.5f, 0.15f), new Vector3(Bay, Storey, 0.3f), SurfaceType.Plaster);
            if (!damaged) return;
            var rng = new DetRandom((ulong)(slot.GetHashCode() & 0x7fffffff) + 11UL);
            mb.Slot(slot);
            for (var i = 0; i < 9; i++)
            {
                var p = new Vector3(rng.Range(-1.4f, 1.4f), 0f, rng.Range(-0.9f, -0.15f));
                var s = new Vector3(rng.Range(0.12f, 0.35f), rng.Range(0.06f, 0.18f), rng.Range(0.1f, 0.3f));
                mb.Push(p + Vector3.up * s.y * 0.5f, Quaternion.Euler(rng.Jitter(15f), rng.Range(0f, 360f), rng.Jitter(15f)));
                mb.AddChamferBox(Vector3.zero, s, 0.03f, true, false);
                mb.Pop();
            }
        }

        private static void Trims(MeshBuilder mb, Action<WallFrame> trim)
        {
            var f = new WallFrame(new Vector3(-Bay * 0.5f, 0f, 0f), Vector3.right, Bay);
            trim(f);
        }
    }
}
