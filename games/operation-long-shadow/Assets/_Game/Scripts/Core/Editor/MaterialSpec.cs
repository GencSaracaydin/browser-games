using System;
using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// One material to build: the row format of the content agents' <c>RawAssets/&lt;wp&gt;/ready/materials*.json</c> files
    /// (WP10/WP09 schema: manifest_id, material, target_path, shader, textures{prop: path}, floats, colors, tile_m,
    /// world_uv_tiling, alpha_clip, surface, blend, render_face ("Both"/"Front"/"Back"), zwrite, emission{_EmissionMap,
    /// _EmissionColor_linear}, terrain_layer, assign_to_slot) plus optional "detail" {normal, normal_scale, albedo,
    /// albedo_scale, mask, tile_m, tiling} and "double_sided". Informational keys (workflow, use, layout, projector,
    /// toggles, affect, optional, shader_alt, submesh_slot, note) are kept in <see cref="Note"/>/<see cref="ShaderAlt"/>
    /// or ignored. A shader written as "Name (owner note)" is read as "Name". <see cref="MaterialFactory.Build(MaterialSpec)"/>
    /// turns a spec into a material asset.
    /// </summary>
    public sealed class MaterialSpec
    {
        /// <summary>Manifest id (for logs).</summary>
        public string ManifestId;
        /// <summary>Material name ("M_AmmoBox").</summary>
        public string Material;
        /// <summary>Material asset path.</summary>
        public string TargetPath;
        /// <summary>Shader name (default URP/Lit).</summary>
        public string Shader = MaterialFactory.LitShaderName;
        /// <summary>Texture property → asset path.</summary>
        public Dictionary<string, string> Textures = new Dictionary<string, string>();
        /// <summary>Float property → value.</summary>
        public Dictionary<string, float> Floats = new Dictionary<string, float>();
        /// <summary>Colour property → colour (as typed in the inspector, sRGB).</summary>
        public Dictionary<string, Color> Colors = new Dictionary<string, Color>();
        /// <summary>World size of one texture repeat in metres (tiling = 1 / tile_m).</summary>
        public float? TileMeters;
        /// <summary>Explicit base-map tiling (wins over <see cref="TileMeters"/>).</summary>
        public float? Tiling;
        /// <summary>Alpha-clip threshold, null = no clipping.</summary>
        public float? AlphaClip;
        /// <summary>Transparent surface.</summary>
        public bool Transparent;
        /// <summary>Blend mode for transparent surfaces: Alpha, Premultiply, Additive, Multiply.</summary>
        public string Blend = "Alpha";
        /// <summary>Render both faces.</summary>
        public bool DoubleSided;
        /// <summary>Render back faces only ("render_face": "Back").</summary>
        public bool BackFaceOnly;
        /// <summary>Explicit depth write for transparent surfaces (null = URP default: off when transparent).</summary>
        public bool? ZWrite;
        /// <summary>Shader the owner plans to switch to later (e.g. "SG_FoliageWind"); informational.</summary>
        public string ShaderAlt;
        /// <summary>Emission map path.</summary>
        public string EmissionMap;
        /// <summary>Emission colour in linear HDR values (null = white x1 when a map exists).</summary>
        public Color? EmissionColorLinear;
        /// <summary>Detail normal map path.</summary>
        public string DetailNormal;
        /// <summary>Detail normal strength.</summary>
        public float DetailNormalScale = 1f;
        /// <summary>Detail albedo (linear, 0.5 = neutral) path.</summary>
        public string DetailAlbedo;
        /// <summary>Detail albedo strength.</summary>
        public float DetailAlbedoScale = 1f;
        /// <summary>Detail mask path (alpha channel).</summary>
        public string DetailMask;
        /// <summary>World size of one detail repeat in metres (converted relative to the base tiling).</summary>
        public float? DetailTileMeters;
        /// <summary>Explicit detail tiling relative to the base UV (wins over <see cref="DetailTileMeters"/>).</summary>
        public float? DetailTiling;
        /// <summary>TerrainLayer to build too.</summary>
        public TerrainLayerSpec TerrainLayer;
        /// <summary>Model material slot this material is the default for ("M_Barrel01" for its skins).</summary>
        public string AssignToSlot;
        /// <summary>Free text.</summary>
        public string Note;

        /// <summary>Reads a spec file: a JSON array of rows, or an object with a "materials" array.</summary>
        public static List<MaterialSpec> ListFromJson(string json)
        {
            var root = MiniJson.Parse(json);
            var rows = root as List<object> ?? MiniJson.Arr(root as Dictionary<string, object>, "materials");
            var list = new List<MaterialSpec>();
            if (rows == null) throw new FormatException("Material spec: expected an array or {\"materials\": [...]}");
            foreach (var r in rows)
                if (r is Dictionary<string, object> o) list.Add(FromJson(o));
            return list;
        }

        /// <summary>Reads one row.</summary>
        public static MaterialSpec FromJson(Dictionary<string, object> o)
        {
            var s = new MaterialSpec
            {
                ManifestId = MiniJson.Str(o, "manifest_id"),
                Material = MiniJson.Str(o, "material"),
                TargetPath = MiniJson.Str(o, "target_path"),
                Shader = CleanShaderName(MiniJson.Str(o, "shader")) ?? MaterialFactory.LitShaderName,
                ShaderAlt = MiniJson.Str(o, "shader_alt"),
                AssignToSlot = MiniJson.Str(o, "assign_to_slot"),
                Note = MiniJson.Str(o, "note"),
                DoubleSided = MiniJson.Bool(o, "double_sided") ?? false,
                ZWrite = MiniJson.Bool(o, "zwrite"),
            };
            var face = MiniJson.Str(o, "render_face");
            if (string.Equals(face, "Both", StringComparison.OrdinalIgnoreCase)) s.DoubleSided = true;
            else if (string.Equals(face, "Back", StringComparison.OrdinalIgnoreCase)) s.BackFaceOnly = true;
            var tex = MiniJson.Obj(o, "textures");
            if (tex != null)
                foreach (var kv in tex)
                    if (kv.Value is string p && p.Length > 0) s.Textures[kv.Key] = p;
            var fl = MiniJson.Obj(o, "floats");
            if (fl != null)
                foreach (var kv in fl)
                    if (kv.Value is double d) s.Floats[kv.Key] = (float)d;
            var col = MiniJson.Obj(o, "colors");
            if (col != null)
                foreach (var kv in col)
                    if (ToColor(kv.Value, out var c)) s.Colors[kv.Key] = c;
            var tile = MiniJson.Num(o, "tile_m");
            if (tile.HasValue && tile.Value > 0) s.TileMeters = (float)tile.Value;
            var tiling = MiniJson.Num(o, "world_uv_tiling");
            if (tiling.HasValue && tiling.Value > 0) s.Tiling = (float)tiling.Value;
            var clip = MiniJson.Num(o, "alpha_clip");
            if (clip.HasValue) s.AlphaClip = (float)clip.Value;
            s.Transparent = string.Equals(MiniJson.Str(o, "surface"), "Transparent", StringComparison.OrdinalIgnoreCase);
            s.Blend = MiniJson.Str(o, "blend") ?? "Alpha";

            var em = MiniJson.Obj(o, "emission");
            if (em != null)
            {
                s.EmissionMap = MiniJson.Str(em, "_EmissionMap");
                if (em.TryGetValue("_EmissionColor_linear", out var ec) && ToColor(ec, out var c)) s.EmissionColorLinear = c;
            }

            var det = MiniJson.Obj(o, "detail");
            if (det != null)
            {
                s.DetailNormal = MiniJson.Str(det, "normal");
                s.DetailNormalScale = (float)(MiniJson.Num(det, "normal_scale") ?? 1.0);
                s.DetailAlbedo = MiniJson.Str(det, "albedo");
                s.DetailAlbedoScale = (float)(MiniJson.Num(det, "albedo_scale") ?? 1.0);
                s.DetailMask = MiniJson.Str(det, "mask");
                var dt = MiniJson.Num(det, "tile_m");
                if (dt.HasValue && dt.Value > 0) s.DetailTileMeters = (float)dt.Value;
                var dti = MiniJson.Num(det, "tiling");
                if (dti.HasValue && dti.Value > 0) s.DetailTiling = (float)dti.Value;
            }

            var tl = MiniJson.Obj(o, "terrain_layer");
            if (tl != null)
            {
                s.TerrainLayer = new TerrainLayerSpec
                {
                    AssetPath = MiniJson.Str(tl, "asset"),
                    Diffuse = MiniJson.Str(tl, "diffuse"),
                    Normal = MiniJson.Str(tl, "normal"),
                    Mask = MiniJson.Str(tl, "mask"),
                    TileSizeMeters = (float)(MiniJson.Num(tl, "tile_size_m") ?? s.TileMeters ?? 2f),
                    NormalScale = (float)(MiniJson.Num(tl, "normal_scale") ?? 1.0),
                };
            }
            if (string.IsNullOrEmpty(s.Material) && !string.IsNullOrEmpty(s.TargetPath))
                s.Material = System.IO.Path.GetFileNameWithoutExtension(s.TargetPath);
            return s;
        }

        /// <summary>"Shader Graphs/Decal" stays; "LongShadow/SG_CRTScreen (WP09); placeholder URP/Lit" becomes "LongShadow/SG_CRTScreen".</summary>
        public static string CleanShaderName(string shader)
        {
            if (string.IsNullOrWhiteSpace(shader)) return null;
            var cut = shader.IndexOf(" (", StringComparison.Ordinal);
            if (cut < 0) cut = shader.IndexOf(';');
            return (cut > 0 ? shader.Substring(0, cut) : shader).Trim();
        }

        private static bool ToColor(object v, out Color c)
        {
            c = Color.white;
            if (!(v is List<object> a) || a.Count < 3) return false;
            float F(int i) => i < a.Count && a[i] is double d ? (float)d : 1f;
            c = new Color(F(0), F(1), F(2), a.Count > 3 ? F(3) : 1f);
            return true;
        }
    }
}
