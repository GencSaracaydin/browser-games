using System.Collections.Generic;
using System.IO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Geometry;
using UnityEditor;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>
    /// Maps the kit's material slots (<see cref="KitSlots"/>) to materials: the CC0/fal env and prop materials that
    /// MaterialFactory builds from WP10_CC0 / WP10_FAL specs (by name, first match wins), level variants it derives from
    /// them (six lime-wash tints of the painted plaster, laundry cloth tints), a few purpose materials it owns (dark glass,
    /// interior darks, emissive lenses, the clock enamel) and URP Baked Lit copies for the far proxies (TECH_PLAN D7).
    /// Missing textures fall back to saved placeholder materials, so the generated scenes render now and pick up the real
    /// materials when the builders are re-run after the content lands.
    /// </summary>
    public static class LevelMaterials
    {
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string BakedLitShader = "Universal Render Pipeline/Baked Lit";

        private static readonly Dictionary<string, Material> s_lit = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Material> s_proxy = new Dictionary<string, Material>();
        private static readonly HashSet<string> s_reported = new HashSet<string>();

        /// <summary>Lime-wash tints multiplied over the painted plaster (cream, ochre, sand, pale blue-grey, faded pink, white).</summary>
        public static readonly Color[] PlasterTints =
        {
            new Color(1.0f, 0.95f, 0.86f), new Color(0.94f, 0.80f, 0.58f), new Color(0.90f, 0.83f, 0.70f),
            new Color(0.80f, 0.85f, 0.88f), new Color(0.95f, 0.82f, 0.77f), new Color(1.0f, 0.99f, 0.96f),
        };

        /// <summary>Laundry cloth tints (faded indigo, rust, cream, olive).</summary>
        public static readonly Color[] ClothTints =
        {
            new Color(0.35f, 0.42f, 0.58f), new Color(0.66f, 0.36f, 0.24f), new Color(0.88f, 0.84f, 0.74f), new Color(0.5f, 0.52f, 0.36f),
        };

        // Slot → material names, first existing wins (WP10_CC0 / WP10_FAL material specs).
        private static readonly Dictionary<string, string[]> s_names = new Dictionary<string, string[]>
        {
            { KitSlots.PlasterBase, new[] { "M_DamagedPlaster", "M_ClayPlaster", "M_PaintedPlaster006" } },
            { KitSlots.PlasterRough, new[] { "M_WhiteRoughPlaster", "M_Plaster001" } },
            { KitSlots.PlasterDamaged, new[] { "M_PeelingPaintedWall", "M_DamagedPlaster" } },
            { KitSlots.Mudbrick, new[] { "M_Mudbrick", "M_ClayPlaster" } },
            { KitSlots.Sandstone, new[] { "M_SandstoneBlocks05" } },
            { KitSlots.SandstoneTrim, new[] { "M_SandstoneBlocks05", "M_BeigeWall001" } },
            { KitSlots.Concrete, new[] { "M_ConcreteWall007", "M_Concrete042B", "M_CrackedConcreteWall" } },
            { KitSlots.ConcreteFloor, new[] { "M_ConcreteFloorWorn001", "M_Concrete042B" } },
            { KitSlots.RoofDeck, new[] { "M_ClayPlaster", "M_MudCrackedDry03", "M_ConcreteFloorWorn001" } },
            { KitSlots.DecoRender, new[] { "M_PaintedConcrete", "M_BeigeWall001" } },
            { KitSlots.DecoTrim, new[] { "M_BeigeWall001", "M_PaintedConcrete" } },
            { KitSlots.Paving, new[] { "M_StonePaving", "M_RoadDamaged" } },
            { KitSlots.Road, new[] { "M_RoadDamaged", "M_StonePaving" } },
            { KitSlots.Dirt, new[] { "M_Ground097", "M_DryGroundRocks", "M_Sand01" } },
            { KitSlots.Sand, new[] { "M_Sand01", "M_AerialSand" } },
            { KitSlots.Gravel, new[] { "M_GravellySand", "M_RockyTrail02" } },
            { KitSlots.SaltCrust, new[] { "M_SaltCrust", "M_MudCrackedDry03" } },
            { KitSlots.MudCracked, new[] { "M_MudCrackedDry03" } },
            { KitSlots.Rock, new[] { "M_RockFace03", "M_Rocks017" } },
            { KitSlots.Terrazzo, new[] { "M_TerrazzoTiles", "M_Terrazzo003" } },
            { KitSlots.FloorTiles, new[] { "M_FloorTiles08", "M_TerrazzoTiles" } },
            { KitSlots.Linoleum, new[] { "M_OldLinoleumFlooring01" } },
            { KitSlots.Carpet, new[] { "M_CinemaCarpet", "M_Fabric044" } },
            { KitSlots.Wood, new[] { "M_WeatheredPlanks", "M_BrownPlanks03" } },
            { KitSlots.WoodPainted, new[] { "M_WoodPeelingPaintWeathered", "M_WeatheredPlanks" } },
            { KitSlots.WoodDark, new[] { "M_BrownPlanks03", "M_WeatheredPlanks" } },
            { KitSlots.Corrugated, new[] { "M_RustyCorrugatedIron", "M_CorrugatedIron" } },
            { KitSlots.CorrugatedWorn, new[] { "M_WornCorrugatedIron", "M_CorrugatedIron" } },
            { KitSlots.MetalShutter, new[] { "M_CorrugatedSteel005", "M_CorrugatedSteel006A", "M_CorrugatedIron" } },
            { KitSlots.SteelFrame, new[] { "M_PaintedMetal006", "M_PaintedMetal014" } },
            { KitSlots.SteelGreen, new[] { "M_GreenMetalRust", "M_PaintedMetal006" } },
            { KitSlots.Iron, new[] { "M_RustyMetal02", "M_GreenMetalRust" } },
            { KitSlots.SteelPlate, new[] { "M_MetalPlate", "M_MetalPlates013" } },
            { KitSlots.SheetMetal, new[] { "M_SheetMetal001", "M_MetalPlates013" } },
            { KitSlots.Awning, new[] { "M_AwningStripes", "M_Fabric066" } },
            { KitSlots.Canvas, new[] { "M_Fabric066", "M_Hessian230" } },
            { KitSlots.Hessian, new[] { "M_Hessian230", "M_Fabric066" } },
            { KitSlots.Rope, new[] { "M_Rope001" } },
            { KitSlots.Brick, new[] { "M_Bricks043" } },
            { KitSlots.Velvet, new[] { "M_Velvet", "M_Fabric044" } },
        };

        private static readonly Dictionary<string, Color> s_fallback = new Dictionary<string, Color>
        {
            { KitSlots.PlasterBase, new Color(0.6f, 0.52f, 0.42f) }, { KitSlots.PlasterRough, new Color(0.82f, 0.8f, 0.75f) },
            { KitSlots.PlasterDamaged, new Color(0.7f, 0.64f, 0.55f) }, { KitSlots.Mudbrick, new Color(0.56f, 0.44f, 0.32f) },
            { KitSlots.Sandstone, new Color(0.76f, 0.64f, 0.47f) }, { KitSlots.SandstoneTrim, new Color(0.8f, 0.7f, 0.54f) },
            { KitSlots.Concrete, new Color(0.6f, 0.59f, 0.56f) }, { KitSlots.ConcreteFloor, new Color(0.5f, 0.49f, 0.47f) },
            { KitSlots.RoofDeck, new Color(0.66f, 0.56f, 0.44f) }, { KitSlots.DecoRender, new Color(0.84f, 0.79f, 0.69f) },
            { KitSlots.DecoTrim, new Color(0.74f, 0.69f, 0.6f) }, { KitSlots.Paving, new Color(0.62f, 0.56f, 0.48f) },
            { KitSlots.Road, new Color(0.45f, 0.43f, 0.41f) }, { KitSlots.Dirt, new Color(0.58f, 0.49f, 0.38f) },
            { KitSlots.Sand, new Color(0.76f, 0.65f, 0.48f) }, { KitSlots.Gravel, new Color(0.62f, 0.55f, 0.45f) },
            { KitSlots.SaltCrust, new Color(0.86f, 0.84f, 0.79f) }, { KitSlots.MudCracked, new Color(0.6f, 0.5f, 0.38f) },
            { KitSlots.Rock, new Color(0.3f, 0.28f, 0.26f) }, { KitSlots.Terrazzo, new Color(0.72f, 0.68f, 0.62f) },
            { KitSlots.FloorTiles, new Color(0.62f, 0.58f, 0.5f) }, { KitSlots.Linoleum, new Color(0.42f, 0.4f, 0.32f) },
            { KitSlots.Carpet, new Color(0.38f, 0.12f, 0.12f) }, { KitSlots.Wood, new Color(0.45f, 0.36f, 0.27f) },
            { KitSlots.WoodPainted, new Color(0.32f, 0.45f, 0.42f) }, { KitSlots.WoodDark, new Color(0.3f, 0.21f, 0.15f) },
            { KitSlots.Corrugated, new Color(0.47f, 0.33f, 0.24f) }, { KitSlots.CorrugatedWorn, new Color(0.55f, 0.55f, 0.53f) },
            { KitSlots.MetalShutter, new Color(0.5f, 0.52f, 0.52f) }, { KitSlots.SteelFrame, new Color(0.3f, 0.35f, 0.33f) },
            { KitSlots.SteelGreen, new Color(0.27f, 0.33f, 0.27f) }, { KitSlots.Iron, new Color(0.25f, 0.2f, 0.17f) },
            { KitSlots.SteelPlate, new Color(0.42f, 0.42f, 0.41f) }, { KitSlots.SheetMetal, new Color(0.55f, 0.56f, 0.56f) },
            { KitSlots.Awning, new Color(0.7f, 0.42f, 0.28f) }, { KitSlots.Canvas, new Color(0.7f, 0.66f, 0.54f) },
            { KitSlots.Hessian, new Color(0.62f, 0.53f, 0.38f) }, { KitSlots.Rope, new Color(0.55f, 0.48f, 0.36f) },
            { KitSlots.Brick, new Color(0.55f, 0.32f, 0.24f) }, { KitSlots.Velvet, new Color(0.35f, 0.06f, 0.08f) },
        };

        /// <summary>Forgets cached materials (after MaterialFactory ran).</summary>
        public static void Reload()
        {
            s_lit.Clear();
            s_proxy.Clear();
            s_reported.Clear();
        }

        /// <summary>Materials for a builder's slots (submesh order).</summary>
        public static Material[] Resolve(IReadOnlyList<string> slots, bool proxy)
        {
            var mats = new Material[slots.Count];
            for (var i = 0; i < slots.Count; i++) mats[i] = proxy ? GetProxy(slots[i]) : Get(slots[i]);
            return mats;
        }

        /// <summary>The lit material of a slot (created/updated on demand).</summary>
        public static Material Get(string slot)
        {
            if (string.IsNullOrEmpty(slot)) slot = "Default";
            if (s_lit.TryGetValue(slot, out var m) && m != null) return m;
            m = Create(slot);
            s_lit[slot] = m;
            return m;
        }

        /// <summary>The Baked Lit copy of a slot's material for far proxies (TECH_PLAN D7).</summary>
        public static Material GetProxy(string slot)
        {
            if (string.IsNullOrEmpty(slot)) slot = "Default";
            if (s_proxy.TryGetValue(slot, out var m) && m != null) return m;
            var src = Get(slot);
            var shader = Shader.Find(BakedLitShader) ?? src.shader;
            var proxy = new Material(shader) { name = "M_Proxy_" + Safe(slot) };
            CopyIfPresent(src, proxy, "_BaseMap");
            CopyIfPresent(src, proxy, "_BumpMap");
            if (src.HasProperty("_BaseColor") && proxy.HasProperty("_BaseColor")) proxy.SetColor("_BaseColor", src.GetColor("_BaseColor"));
            if (src.HasProperty("_BaseMap") && proxy.HasProperty("_BaseMap"))
            {
                proxy.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
                proxy.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
            }
            if (slot == KitSlots.SodiumLens || slot == KitSlots.AviationLens) proxy.SetColor("_BaseColor", src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor") : Color.white);
            m = Save(proxy, LevelPaths.Materials + "/Proxy/M_Proxy_" + Safe(slot) + ".mat");
            s_proxy[slot] = m;
            return m;
        }

        private static Material Create(string slot)
        {
            // Lime-wash tints derived from the painted plaster.
            if (slot.StartsWith("Plaster") && slot.Length == 8 && char.IsDigit(slot[7]))
            {
                var tint = PlasterTints[(slot[7] - '0') % PlasterTints.Length];
                return Variant(new[] { "M_PaintedPlasterWall", "M_WhiteRoughPlaster", "M_Plaster001" }, "M_Level_Plaster_T" + slot[7], tint, new Color(0.78f, 0.72f, 0.62f) * tint);
            }
            if (slot.StartsWith("Cloth") && slot.Length == 6 && char.IsDigit(slot[5]))
            {
                var tint = ClothTints[(slot[5] - '0') % ClothTints.Length];
                var m = Variant(new[] { "M_Fabric066", "M_Hessian230" }, "M_Level_Cloth_T" + slot[5], tint, tint);
                SetFloat(m, "_Cull", 0f);
                return m;
            }
            switch (slot)
            {
                case KitSlots.Glass: return Own("M_Level_GlassDark", new Color(0.06f, 0.07f, 0.075f), 0.92f, 0f, Color.black);
                case KitSlots.InteriorCard: return Own("M_Level_InteriorDark", new Color(0.018f, 0.016f, 0.014f), 0.05f, 0f, Color.black);
                case KitSlots.SodiumLens: return Own("M_Level_SodiumLens", new Color(0.55f, 0.45f, 0.3f), 0.8f, 0f, Color.black);
                case KitSlots.AviationLens: return Own("M_Level_AviationLens", new Color(0.5f, 0.05f, 0.04f), 0.85f, 0f, Color.black);
                case KitSlots.ExitSign: return Own("M_Level_ExitSign", new Color(0.12f, 0.35f, 0.15f), 0.6f, 0f, new Color(0.25f, 1.8f, 0.4f));
                case KitSlots.ClockFace: return Own("M_Level_Enamel", new Color(0.86f, 0.84f, 0.77f), 0.55f, 0f, Color.black);
                case KitSlots.Screen: return Own("M_Level_Screen", new Color(0.72f, 0.71f, 0.68f), 0.1f, 0f, Color.black);
                case KitSlots.FloodLens: return Own("M_Level_FloodLens", new Color(0.8f, 0.82f, 0.8f), 0.9f, 0f, Color.black);
                case KitSlots.ProxyWindow: return Own("M_Level_WindowDark", new Color(0.035f, 0.033f, 0.03f), 0.4f, 0f, Color.black);
                case KitSlots.Cable: return Own("M_Level_Cable", new Color(0.04f, 0.04f, 0.04f), 0.3f, 0f, Color.black);
                case KitSlots.PaintRed: return Own("M_Level_PaintRed", new Color(0.55f, 0.08f, 0.06f), 0.35f, 0f, Color.black);
                case KitSlots.PaintWhite: return Own("M_Level_PaintWhite", new Color(0.85f, 0.84f, 0.8f), 0.35f, 0f, Color.black);
            }
            if (s_names.TryGetValue(slot, out var names))
            {
                var found = FindNamed(names);
                if (found != null) return found;
            }
            if (s_reported.Add(slot))
                LSLog.MissingAsset(LogCategory.Level, "material slot " + slot, names != null ? string.Join(" | ", names) : null);
            var c = s_fallback.TryGetValue(slot, out var col) ? col : Placeholders.Greybox;
            return BuilderUtil.PlaceholderMaterial(c);
        }

        /// <summary>Finds the first existing material by name in the env, prop and level material folders.</summary>
        public static Material FindNamed(IEnumerable<string> names)
        {
            foreach (var n in names)
            {
                foreach (var folder in new[] { LevelPaths.EnvMaterials, LevelPaths.PropMaterials, "Assets/_Game/Art/Materials/Vegetation", "Assets/_Game/Art/Materials/Vehicles" })
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + n + ".mat");
                    if (m != null) return m;
                }
            }
            return null;
        }

        private static Material Variant(string[] baseNames, string assetName, Color tint, Color fallback)
        {
            var src = FindNamed(baseNames);
            Material m;
            if (src != null)
            {
                m = new Material(src) { name = assetName };
                var baseColor = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor * tint);
            }
            else
            {
                m = new Material(Shader.Find(LitShader)) { name = assetName };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", fallback);
                SetFloat(m, "_Smoothness", 0.15f);
            }
            return Save(m, LevelPaths.Materials + "/" + assetName + ".mat");
        }

        private static Material Own(string assetName, Color baseColor, float smoothness, float metallic, Color emission)
        {
            var m = new Material(Shader.Find(LitShader)) { name = assetName };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            SetFloat(m, "_Smoothness", smoothness);
            SetFloat(m, "_Metallic", metallic);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return Save(m, LevelPaths.Materials + "/" + assetName + ".mat");
        }

        private static Material Save(Material m, string path)
        {
            BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
            return BuilderUtil.CreateOrReplaceAsset(m, path);
        }

        private static void SetFloat(Material m, string prop, float v)
        {
            if (m != null && m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        private static void CopyIfPresent(Material src, Material dst, string prop)
        {
            if (src.HasProperty(prop) && dst.HasProperty(prop)) dst.SetTexture(prop, src.GetTexture(prop));
        }

        private static string Safe(string s) => s.Replace('/', '_').Replace(' ', '_');

        /// <summary>Surface type of a slot (impacts and footsteps through SurfaceTag).</summary>
        public static SurfaceType SurfaceOf(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return SurfaceType.Default;
            if (slot.StartsWith("Plaster") || slot == KitSlots.PlasterBase || slot == KitSlots.DecoRender || slot == KitSlots.DecoTrim) return SurfaceType.Plaster;
            if (slot == KitSlots.Sand || slot == KitSlots.Dirt || slot == KitSlots.MudCracked) return SurfaceType.Sand;
            if (slot == KitSlots.Gravel) return SurfaceType.Gravel;
            if (slot == KitSlots.SaltCrust) return SurfaceType.SaltCrust;
            if (slot == KitSlots.Sandstone || slot == KitSlots.SandstoneTrim || slot == KitSlots.Paving || slot == KitSlots.Rock || slot == KitSlots.Mudbrick || slot == KitSlots.Brick) return SurfaceType.Stone;
            if (slot == KitSlots.Concrete || slot == KitSlots.ConcreteFloor || slot == KitSlots.Road || slot == KitSlots.RoofDeck) return SurfaceType.Concrete;
            if (slot == KitSlots.Terrazzo || slot == KitSlots.FloorTiles || slot == KitSlots.Linoleum) return SurfaceType.Tile;
            if (slot.StartsWith("Wood")) return SurfaceType.Wood;
            if (slot == KitSlots.Glass) return SurfaceType.Glass;
            if (slot == KitSlots.Awning || slot == KitSlots.Canvas || slot == KitSlots.Hessian || slot == KitSlots.Carpet || slot == KitSlots.Velvet || slot.StartsWith("Cloth")) return SurfaceType.Fabric;
            return SurfaceType.Metal;
        }

        /// <summary>Path of the placeholder material folder (for reports).</summary>
        public static string PlaceholderFolder => BuilderUtil.PlaceholderMaterialFolder;
    }
}
