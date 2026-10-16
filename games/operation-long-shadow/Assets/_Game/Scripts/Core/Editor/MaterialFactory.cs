using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Builds URP materials from data (TECH_PLAN D4, §8.2.6, §8.3): from texture sets named per §4.2
    /// (<c>T_&lt;Name&gt;_{BC,N,M,E,H}</c> → <c>M_&lt;Name&gt;</c> URP/Lit, mask in <c>_MetallicGlossMap</c> and
    /// <c>_OcclusionMap</c>, smoothness from the mask alpha), or from the content agents' material spec files
    /// (<c>RawAssets/&lt;wp&gt;/ready/materials*.json</c>, <see cref="MaterialSpec"/>), including URP decal materials
    /// and Terrain Layers. Detail normals/albedo come from options or the spec "detail" object.
    /// Every build is idempotent: an existing material is updated in place (GUID kept), so re-running after textures
    /// land upgrades placeholders. Missing textures are logged once and left empty (the material still exists).
    /// Batch API for agents: <see cref="BuildFromReadySpecs"/>, <see cref="BuildAllInFolder"/>,
    /// <see cref="BuildFromTextureSet"/>, then <see cref="ModelMaterialBinder.BindAll"/>.
    /// </summary>
    public static class MaterialFactory
    {
        /// <summary>URP Lit shader name.</summary>
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        /// <summary>URP decal Shader Graph.</summary>
        public const string DecalShaderName = "Shader Graphs/Decal";
        /// <summary>Folder for spec files that live inside the project (optional).</summary>
        public const string ProjectSpecFolder = "Assets/_Game/Data/Materials";
        /// <summary>Default parallax scale for sets with an H map.</summary>
        public const float DefaultParallax = 0.015f;
        /// <summary>Smoothness for materials without a mask (rough dielectric).</summary>
        public const float NoMaskSmoothness = 0.3f;
        /// <summary>Texture kinds whose sets <see cref="BuildAllInFolder"/> turns into URP/Lit materials.</summary>
        public static readonly string[] SurfaceKinds = { "Env", "Props", "Weapons", "Vehicles", "Vegetation", "Characters" };

        private static Dictionary<string, string> s_slotDefaults;

        // ------------------------------------------------------------------ texture sets

        /// <summary>Material asset path for a set: &lt;root&gt;Art/Materials/&lt;Kind&gt;/M_&lt;Name&gt;.mat.</summary>
        public static string MaterialPathFor(TextureSet set)
        {
            var root = ImportRulePaths.RootOf(set.Folder + "/");
            var kind = string.IsNullOrEmpty(set.Kind) ? "Misc" : set.Kind;
            if (string.Equals(kind, "Images", StringComparison.OrdinalIgnoreCase)) kind = "FX";
            return root == null ? set.Folder + "/M_" + set.Name + ".mat" : root + "Art/Materials/" + kind + "/M_" + set.Name + ".mat";
        }

        /// <summary>The spec a set + options describe (no side effects except optional mask packing).</summary>
        public static MaterialSpec SpecFromSet(TextureSet set, MaterialOptions o = null)
        {
            o = o ?? new MaterialOptions();
            var decal = string.Equals(set.Kind, "Decals", StringComparison.OrdinalIgnoreCase);
            var spec = new MaterialSpec
            {
                Material = "M_" + set.Name,
                TargetPath = o.MaterialPath ?? MaterialPathFor(set),
                Shader = decal ? DecalShaderName : LitShaderName,
                TileMeters = o.TileMeters,
                Transparent = o.Transparent,
                DoubleSided = o.DoubleSided,
                DetailNormal = o.DetailNormal,
                DetailNormalScale = o.DetailNormalScale,
                DetailAlbedo = o.DetailAlbedo,
                DetailTileMeters = o.DetailTileMeters,
                DetailTiling = o.DetailTiling,
            };
            if (decal)
            {
                if (set.BaseColor != null) spec.Textures["Base_Map"] = set.BaseColor;
                var n = set.Get(TextureSuffix.N);
                if (n != null) spec.Textures["Normal_Map"] = n;
                spec.Floats["Normal_Blend"] = n != null ? 0.8f : 0f;
                return spec;
            }

            var mask = set.Get(TextureSuffix.M);
            if (mask == null && o.AutoPackMask)
            {
                var loose = MaskPacker.FromLooseMaps(set);
                if (loose != null)
                {
                    var outPath = set.Folder + "/" + TextureNaming.FileStem(set.Name, TextureSuffix.M) + ".png";
                    mask = MaskPacker.Pack(loose, outPath);
                    if (mask != null) set.Maps[TextureSuffix.M] = mask;
                }
            }
            if (set.BaseColor != null) spec.Textures["_BaseMap"] = set.BaseColor;
            var normal = set.Get(TextureSuffix.N);
            if (normal != null) spec.Textures["_BumpMap"] = normal;
            if (mask != null)
            {
                spec.Textures["_MetallicGlossMap"] = mask;
                spec.Textures["_OcclusionMap"] = mask;
                spec.Floats["_Metallic"] = 1f;
                spec.Floats["_Smoothness"] = o.Smoothness ?? 1f;
            }
            else
            {
                spec.Floats["_Metallic"] = 0f;
                spec.Floats["_Smoothness"] = o.Smoothness ?? NoMaskSmoothness;
            }
            spec.Floats["_SmoothnessTextureChannel"] = 0f;
            spec.Floats["_OcclusionStrength"] = 1f;
            spec.Floats["_BumpScale"] = o.BumpScale ?? 1f;
            var height = set.Get(TextureSuffix.H);
            if (height != null && TextureSourceProbe.Probe(height).BitsPerChannel < 16)
            {
                spec.Textures["_ParallaxMap"] = height;
                spec.Floats["_Parallax"] = o.Parallax ?? DefaultParallax;
            }
            var emission = set.Get(TextureSuffix.E);
            if (emission != null)
            {
                spec.EmissionMap = emission;
                spec.EmissionColorLinear = o.EmissionColorLinear ?? Color.white;
            }
            if (o.BaseColor.HasValue) spec.Colors["_BaseColor"] = o.BaseColor.Value;

            if (o.AlphaClip.HasValue) spec.AlphaClip = o.AlphaClip;
            else if (set.BaseColor != null && IsFoliageSet(set) && TextureSourceProbe.Probe(set.BaseColor).AlphaUsed)
            {
                spec.AlphaClip = 0.5f;
                spec.DoubleSided = true;
            }
            return spec;
        }

        /// <summary>Builds (or updates) the material of a set. Returns null when the set has no base colour.</summary>
        public static Material BuildFromTextureSet(TextureSet set, MaterialOptions options = null)
        {
            if (set == null) return null;
            if (set.BaseColor == null)
            {
                LSLog.Warn(LogCategory.Assets, "MaterialFactory: set " + set + " has no _BC map; skipped.");
                return null;
            }
            return Build(SpecFromSet(set, options));
        }

        /// <summary>Builds the material of the set a texture belongs to.</summary>
        public static Material BuildFromBaseMap(string textureAssetPath, MaterialOptions options = null) =>
            BuildFromTextureSet(TextureSet.FromMember(textureAssetPath), options);

        /// <summary>
        /// Builds every set with a base colour under <paramref name="folder"/>. With <paramref name="surfaceKindsOnly"/>,
        /// only Art/Textures/{Env,Props,Weapons,Vehicles,Vegetation,Characters} and Decals sets are built (images, FX and
        /// detail maps in _Shared folders are textures, not materials). Sets whose material is the target of a spec row
        /// (<see cref="SpecTargets"/>) are skipped unless <paramref name="includeSpecTargets"/>: the spec row (tiling,
        /// detail, emission, terrain layer) is authoritative, so the order of "Build From Ready Specs" and this call
        /// does not matter.
        /// </summary>
        public static List<Material> BuildAllInFolder(string folder, bool recursive = true, bool surfaceKindsOnly = true, bool includeSpecTargets = false)
        {
            var result = new List<Material>();
            var specTargets = includeSpecTargets ? null : SpecTargets();
            foreach (var set in TextureSet.FindInFolder(folder, recursive))
            {
                if (set.BaseColor == null) continue;
                if (set.Folder.EndsWith("/_Shared", StringComparison.OrdinalIgnoreCase)) continue;
                if (surfaceKindsOnly && !(In(SurfaceKinds, set.Kind) || string.Equals(set.Kind, "Decals", StringComparison.OrdinalIgnoreCase))) continue;
                if (specTargets != null && specTargets.Contains(MaterialPathFor(set))) continue;
                var m = BuildFromTextureSet(set);
                if (m != null) result.Add(m);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        // ------------------------------------------------------------------ specs

        /// <summary>Builds every row of a spec file (project-relative or absolute path).</summary>
        public static List<Material> BuildFromSpecFile(string path, bool skipUnlanded = false)
        {
            var result = new List<Material>();
            var full = Path.IsPathRooted(path) ? path : Path.Combine(BuilderUtil.ProjectRoot, path);
            List<MaterialSpec> specs;
            try
            {
                specs = MaterialSpec.ListFromJson(File.ReadAllText(full));
            }
            catch (Exception e)
            {
                LSLog.Error(LogCategory.Assets, "MaterialFactory: cannot read spec file " + path + ": " + e.Message);
                return result;
            }
            var skipped = 0;
            foreach (var s in specs)
            {
                if (skipUnlanded && !AnyTextureLanded(s))
                {
                    skipped++;
                    continue;
                }
                var m = Build(s);
                if (m != null) result.Add(m);
            }
            AssetDatabase.SaveAssets();
            s_slotDefaults = null;
            LSLog.Info(LogCategory.Assets, "MaterialFactory: " + Path.GetFileName(path) + ": built " + result.Count + " material(s)" +
                                           (skipped > 0 ? ", " + skipped + " skipped (no texture landed yet)" : ""));
            return result;
        }

        /// <summary>
        /// Builds every spec file the content agents left in <c>RawAssets/*/ready/materials*.json</c> and
        /// <see cref="ProjectSpecFolder"/>, skipping rows none of whose textures have landed in Assets/ yet.
        /// Returns the number of materials built.
        /// </summary>
        public static int BuildFromReadySpecs()
        {
            var count = 0;
            foreach (var f in SpecFiles()) count += BuildFromSpecFile(f, skipUnlanded: true).Count;
            return count;
        }

        /// <summary>
        /// Spec files: every JSON whose name contains "material" anywhere under RawAssets/*/ready (outside the mirrored
        /// Assets/ tree; e.g. wp10/ready/materials_cc0.json, wp09/ready/rows/ph_materials.json) and every JSON in
        /// <see cref="ProjectSpecFolder"/>.
        /// </summary>
        public static List<string> SpecFiles()
        {
            var list = new List<string>();
            var raw = Path.Combine(BuilderUtil.ProjectRoot, "RawAssets");
            if (Directory.Exists(raw))
                foreach (var wp in Directory.GetDirectories(raw))
                {
                    var ready = Path.Combine(wp, "ready");
                    if (!Directory.Exists(ready)) continue;
                    var mirrored = Path.Combine(ready, "Assets") + Path.DirectorySeparatorChar;
                    foreach (var f in Directory.GetFiles(ready, "*.json", SearchOption.AllDirectories))
                    {
                        if (f.StartsWith(mirrored, StringComparison.Ordinal)) continue;
                        if (Path.GetFileName(f).IndexOf("material", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        list.Add(f);
                    }
                }
            var proj = Path.Combine(BuilderUtil.ProjectRoot, ProjectSpecFolder);
            if (Directory.Exists(proj)) list.AddRange(Directory.GetFiles(proj, "*.json", SearchOption.AllDirectories));
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Material asset paths that some spec row builds (case-insensitive; re-read on every call).</summary>
        public static HashSet<string> SpecTargets()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in SpecFiles())
            {
                try
                {
                    foreach (var s in MaterialSpec.ListFromJson(File.ReadAllText(f)))
                    {
                        if (string.IsNullOrEmpty(s.TargetPath)) continue;
                        set.Add(s.TargetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ? s.TargetPath : s.TargetPath + ".mat");
                    }
                }
                catch (Exception e)
                {
                    LSLog.Warn(LogCategory.Assets, "MaterialFactory: spec " + f + " unreadable: " + e.Message);
                }
            }
            return set;
        }

        /// <summary>Material slot → default material path, from every spec row with "assign_to_slot" (first wins).</summary>
        public static IReadOnlyDictionary<string, string> SlotDefaults()
        {
            if (s_slotDefaults != null) return s_slotDefaults;
            s_slotDefaults = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var f in SpecFiles())
            {
                try
                {
                    foreach (var s in MaterialSpec.ListFromJson(File.ReadAllText(f)))
                        if (!string.IsNullOrEmpty(s.AssignToSlot) && !string.IsNullOrEmpty(s.TargetPath) && !s_slotDefaults.ContainsKey(s.AssignToSlot))
                            s_slotDefaults.Add(s.AssignToSlot, s.TargetPath);
                }
                catch (Exception e)
                {
                    LSLog.Warn(LogCategory.Assets, "MaterialFactory: spec " + f + " unreadable: " + e.Message);
                }
            }
            return s_slotDefaults;
        }

        // ------------------------------------------------------------------ build

        /// <summary>Creates or updates the material a spec describes (and its TerrainLayer). Returns null on failure.</summary>
        public static Material Build(MaterialSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.TargetPath))
            {
                LSLog.Error(LogCategory.Assets, "MaterialFactory: spec without target_path (" + spec?.Material + ")");
                return null;
            }
            var shader = Shader.Find(spec.Shader);
            if (shader == null)
            {
                // A project shader that has not landed yet (SG_CRTScreen, SG_FoliageWind): degrade to URP/Lit and rebuild later.
                LSLog.WarnOnce("matfactory-shader:" + spec.Shader, LogCategory.Assets, "MaterialFactory: shader '" + spec.Shader +
                    "' not found; materials that ask for it use URP/Lit until it exists (rebuild them then). First: " + spec.TargetPath);
                shader = Shader.Find(LitShaderName);
                if (shader == null)
                {
                    LSLog.Error(LogCategory.Assets, "MaterialFactory: URP/Lit shader missing.");
                    return null;
                }
            }
            var path = spec.TargetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ? spec.TargetPath : spec.TargetPath + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (shader.name == LitShaderName) ApplyLit(mat, spec);
            else ApplyGeneric(mat, spec);

            EditorUtility.SetDirty(mat);
            if (spec.TerrainLayer != null) TerrainLayerBuilder.Build(spec.TerrainLayer);
            return mat;
        }

        private static void ApplyLit(Material mat, MaterialSpec s)
        {
            mat.SetFloat("_WorkflowMode", 1f); // metallic (D4)
            // Every managed texture slot is written (null when absent) so a rebuild never keeps stale maps.
            SetTex(mat, "_BaseMap", s.Textures, s);
            SetTex(mat, "_BumpMap", s.Textures, s);
            SetTex(mat, "_MetallicGlossMap", s.Textures, s);
            if (!s.Textures.ContainsKey("_OcclusionMap") && s.Textures.TryGetValue("_MetallicGlossMap", out var maskPath))
                s.Textures["_OcclusionMap"] = maskPath; // D4: one mask in both slots
            SetTex(mat, "_OcclusionMap", s.Textures, s);
            SetTex(mat, "_ParallaxMap", s.Textures, s);

            // Detail inputs (spec "detail" object wins over raw texture entries).
            if (s.DetailNormal != null) s.Textures["_DetailNormalMap"] = s.DetailNormal;
            if (s.DetailAlbedo != null) s.Textures["_DetailAlbedoMap"] = s.DetailAlbedo;
            if (s.DetailMask != null) s.Textures["_DetailMask"] = s.DetailMask;
            SetTex(mat, "_DetailNormalMap", s.Textures, s);
            SetTex(mat, "_DetailAlbedoMap", s.Textures, s);
            SetTex(mat, "_DetailMask", s.Textures, s);
            mat.SetFloat("_DetailNormalMapScale", s.DetailNormalScale);
            mat.SetFloat("_DetailAlbedoMapScale", s.DetailAlbedoScale);

            // Emission.
            var emissionPath = s.EmissionMap ?? (s.Textures.TryGetValue("_EmissionMap", out var ep) ? ep : null);
            if (emissionPath != null) s.Textures["_EmissionMap"] = emissionPath;
            SetTex(mat, "_EmissionMap", s.Textures, s);

            // Defaults per D4, then the spec's own values.
            var hasMask = mat.GetTexture("_MetallicGlossMap") != null;
            mat.SetFloat("_Metallic", hasMask ? 1f : 0f);
            mat.SetFloat("_Smoothness", hasMask ? 1f : NoMaskSmoothness);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.SetFloat("_BumpScale", 1f);
            mat.SetFloat("_Parallax", mat.GetTexture("_ParallaxMap") != null ? DefaultParallax : 0.005f);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.SetFloat("_SpecularHighlights", 1f);
            mat.SetFloat("_ReceiveShadows", 1f);
            foreach (var kv in s.Floats)
                if (mat.HasProperty(kv.Key)) mat.SetFloat(kv.Key, kv.Value);
            foreach (var kv in s.Colors)
                if (mat.HasProperty(kv.Key)) mat.SetColor(kv.Key, kv.Value);

            // Tiling: base map (world-UV kit meshes use 1 / tile_m); detail tiling is relative to the base UV in URP/Lit.
            var tiling = s.Tiling ?? (s.TileMeters.HasValue ? 1f / s.TileMeters.Value : 1f);
            mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            float detailTiling;
            if (s.DetailTiling.HasValue) detailTiling = s.DetailTiling.Value;
            else if (s.DetailTileMeters.HasValue) detailTiling = (s.TileMeters ?? (1f / Mathf.Max(tiling, 1e-4f))) / s.DetailTileMeters.Value;
            else detailTiling = 1f;
            mat.SetTextureScale("_DetailAlbedoMap", new Vector2(detailTiling, detailTiling));

            // Surface.
            mat.SetFloat("_Surface", s.Transparent ? 1f : 0f);
            mat.SetFloat("_Blend", BlendIndex(s.Blend));
            mat.SetFloat("_AlphaClip", s.AlphaClip.HasValue ? 1f : 0f);
            if (s.AlphaClip.HasValue) mat.SetFloat("_Cutoff", s.AlphaClip.Value);
            mat.SetFloat("_Cull", s.DoubleSided ? (float)CullMode.Off : s.BackFaceOnly ? (float)CullMode.Front : (float)CullMode.Back);

            // Emission colour: linear HDR value -> stored colour (Unity converts colour properties sRGB->linear).
            var emissionLinear = s.EmissionColorLinear ?? (emissionPath != null ? Color.white : Color.black);
            mat.SetColor("_EmissionColor", new Color(emissionLinear.r, emissionLinear.g, emissionLinear.b, 1f).gamma);
            mat.globalIlluminationFlags = emissionLinear.maxColorComponent > 1f / 255f
                ? MaterialGlobalIlluminationFlags.BakedEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            ApplyUrpKeywords(mat);
            // A material with an emission map keeps _EMISSION even while black, so runtime scripts (practical lamps)
            // can drive _EmissionColor through property blocks and the variant is not stripped from the build.
            if (emissionPath != null) mat.EnableKeyword("_EMISSION");
            // URP/Lit derives _ZWrite from the surface type (transparent = off) and re-derives it whenever the material is
            // validated, so an explicit depth write only sticks on shaders with _ZWriteControl (Shader Graph: 1 = on, 2 = off).
            if (s.ZWrite.HasValue)
            {
                if (mat.HasProperty("_ZWriteControl")) mat.SetFloat("_ZWriteControl", s.ZWrite.Value ? 1f : 2f);
                else if (s.ZWrite.Value && s.Transparent)
                    LSLog.WarnOnce("matfactory-zwrite:" + mat.name, LogCategory.Assets, "MaterialFactory: " + mat.name +
                        " asks for depth write on a transparent URP/Lit surface; URP/Lit cannot force it (use a Shader Graph with ZWrite control).");
            }
        }

        private static void ApplyGeneric(Material mat, MaterialSpec s)
        {
            foreach (var kv in s.Textures)
            {
                if (!mat.HasProperty(kv.Key)) continue;
                mat.SetTexture(kv.Key, LoadTexture(kv.Value, s));
            }
            foreach (var kv in s.Floats)
            {
                // Spec files write the inspector label ("DrawOrder"); the decal graph property is "_DrawOrder".
                var prop = mat.HasProperty(kv.Key) ? kv.Key : mat.HasProperty("_" + kv.Key) ? "_" + kv.Key : null;
                if (prop != null) mat.SetFloat(prop, kv.Value);
            }
            foreach (var kv in s.Colors)
                if (mat.HasProperty(kv.Key)) mat.SetColor(kv.Key, kv.Value);
            if (mat.shader.name == DecalShaderName) mat.enableInstancing = true; // URP decal projectors batch by instancing
        }

        /// <summary>Runs URP's own Lit keyword/surface setup (what the material inspector does), plus detail keywords.</summary>
        public static void ApplyUrpKeywords(Material mat)
        {
            if (mat == null || mat.shader == null || mat.shader.name != LitShaderName) return;
            BaseShaderGUI.SetMaterialKeywords(mat, LitGUI.SetMaterialKeywords, DetailKeywords);
        }

        private static void DetailKeywords(Material m)
        {
            // Same logic as URP's internal LitDetailGUI.SetMaterialKeywords.
            if (!m.HasProperty("_DetailAlbedoMap") || !m.HasProperty("_DetailNormalMap") || !m.HasProperty("_DetailAlbedoMapScale")) return;
            var scaled = !Mathf.Approximately(m.GetFloat("_DetailAlbedoMapScale"), 1f);
            var hasDetail = m.GetTexture("_DetailAlbedoMap") != null || m.GetTexture("_DetailNormalMap") != null;
            CoreUtils.SetKeyword(m, "_DETAIL_MULX2", !scaled && hasDetail);
            CoreUtils.SetKeyword(m, "_DETAIL_SCALED", scaled && hasDetail);
        }

        private static float BlendIndex(string blend)
        {
            switch ((blend ?? "Alpha").ToLowerInvariant())
            {
                case "premultiply": return 1f;
                case "additive": return 2f;
                case "multiply": return 3f;
                default: return 0f;
            }
        }

        private static void SetTex(Material mat, string prop, Dictionary<string, string> textures, MaterialSpec s)
        {
            if (!mat.HasProperty(prop)) return;
            mat.SetTexture(prop, textures.TryGetValue(prop, out var p) ? LoadTexture(p, s) : null);
        }

        private static Texture LoadTexture(string path, MaterialSpec s)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var t = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (t == null) LSLog.MissingAsset(LogCategory.Assets, (s.ManifestId ?? s.Material) + ":" + Path.GetFileName(path), path);
            return t;
        }

        private static bool AnyTextureLanded(MaterialSpec s)
        {
            foreach (var kv in s.Textures)
                if (AssetDatabase.LoadAssetAtPath<Texture>(kv.Value) != null) return true;
            return false;
        }

        private static bool IsFoliageSet(TextureSet set)
        {
            if (string.Equals(set.Kind, "Vegetation", StringComparison.OrdinalIgnoreCase)) return true;
            var n = set.Name;
            return n.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Leaf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Cards", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool In(string[] list, string s)
        {
            foreach (var x in list)
                if (string.Equals(x, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
