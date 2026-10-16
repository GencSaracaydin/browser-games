using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Combat.Interactables;
using LongShadow.Combat.Projectiles;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LongShadow.Combat.Editor
{
    /// <summary>
    /// Builds <c>LIB_CombatFx</c> and everything it references (TECH_PLAN §7.5 pooled tracers, impacts and decals): particle
    /// materials from the WP10 VFX sheets (<c>Art/Textures/FX/T_VFX_*</c>: dust and smoke 8×8, sparks 4×4, fire 8×8,
    /// muzzle 4×2, tracer) or procedural stand-ins, impact/explosion/smoke/spark/fire prefabs from <see cref="FxFactory"/>,
    /// the muzzle-flash meshes, the decal atlases with their WP10 layout JSON (bullet holes, blood, scorch), the casing meshes
    /// (<c>SM_WPN_Casings</c>), the frag and rocket projectiles and the resupply crate. Idempotent: re-run when textures or
    /// models land (assets are updated in place).
    /// </summary>
    public static class CombatFxBuilder
    {
        /// <summary>Particle and decal materials (Art/Materials/Weapons is WP03's).</summary>
        public const string MaterialFolder = "Assets/_Game/Art/Materials/Weapons/FX";
        /// <summary>Procedural fallback textures.</summary>
        public const string TextureFolder = "Assets/_Game/Art/Textures/Weapons/FX";
        /// <summary>Generated meshes (muzzle flash quads).</summary>
        public const string MeshFolder = "Assets/_Game/Art/Models/Weapons/FX";
        /// <summary>FX prefabs.</summary>
        public const string PrefabFolder = WeaponPrefabBuilder.PrefabFolder + "/FX";
        /// <summary>WP10 VFX textures.</summary>
        public const string VfxTextureFolder = "Assets/_Game/Art/Textures/FX";
        /// <summary>WP10 decal textures (one sub-folder per atlas).</summary>
        public const string DecalTextureFolder = "Assets/_Game/Art/Textures/Decals";
        /// <summary>WP10 decal materials (MaterialFactory output), used when present.</summary>
        public const string DecalMaterialFolder = "Assets/_Game/Art/Materials/Decals";

        [Serializable]
        private class Layout
        {
            public string texture;
            public int[] size;
            public LayoutElement[] elements;
        }

        [Serializable]
        private class LayoutElement
        {
            public string name;
            public int[] px;
            public float[] uv;
        }

        /// <summary>Builds every FX asset and LIB_CombatFx; returns the report (also logged).</summary>
        [MenuItem("LongShadow/Combat/Build FX Library", priority = 120)]
        public static string BuildAll()
        {
            var sb = new StringBuilder("[CombatFxBuilder]\n");
            foreach (var f in new[] { MaterialFolder, TextureFolder, MeshFolder, PrefabFolder }) BuilderUtil.EnsureFolder(f);
            var mats = BuildMaterials(sb);
            var entries = new List<CombatFxEntry>
            {
                new CombatFxEntry(FxIds.Tracer, null, mats.Tracer),
                new CombatFxEntry(FxIds.MuzzleFlash, null, mats.Muzzle, MuzzleMeshes()),
            };

            for (var p = 0; p <= (int)ImpactProfile.Fruit; p++)
            {
                var profile = (ImpactProfile)p;
                entries.Add(new CombatFxEntry(FxIds.Impact(profile), Save(FxFactory.BuildImpact(profile, mats), "PF_FX_Impact_" + profile), null));
            }
            entries.Add(new CombatFxEntry(FxIds.MuzzleSmoke, Save(FxFactory.BuildMuzzleSmoke(mats), "PF_FX_MuzzleSmoke"), null));
            foreach (var id in new[] { FxIds.ExplosionFrag, FxIds.ExplosionRocket, FxIds.ExplosionFireball })
                entries.Add(new CombatFxEntry(id, Save(FxFactory.BuildExplosion(id, mats), "PF_FX_" + id.Replace('.', '_')), null));
            entries.Add(new CombatFxEntry(FxIds.SmokeCloud, Save(FxFactory.BuildSmokeCloud(mats), "PF_FX_SmokeCloud"), null));
            entries.Add(new CombatFxEntry(FxIds.SmokeTrail, Save(FxFactory.BuildSmokeTrail(mats), "PF_FX_SmokeTrail"), null));
            entries.Add(new CombatFxEntry(FxIds.SparkBurst, Save(FxFactory.BuildSparkBurst(mats), "PF_FX_SparkBurst"), null));
            entries.Add(new CombatFxEntry(FxIds.Fire, Save(FxFactory.BuildFire(mats), "PF_FX_Fire"), null));

            entries.Add(Decal(FxIds.DecalBulletHoles, "bullet_holes", "T_Decal_BulletHoles", sb));
            entries.Add(Decal(FxIds.DecalBlood, "blood_small", "T_Decal_BloodSmall", sb));
            entries.Add(Decal(FxIds.DecalScorch, "scorch", "T_Decal_Scorch", sb));

            AddCasings(entries, sb);
            entries.Add(new CombatFxEntry(FxIds.GrenadeProjectile, BuildGrenadeProjectile(sb), null));
            entries.Add(new CombatFxEntry(FxIds.RocketProjectile, BuildRocketProjectile(sb), null));
            entries.Add(new CombatFxEntry(FxIds.AmmoCrate, BuildCrate(sb), null));

            var lib = BuilderUtil.LoadOrCreateAsset<CombatFxLibrary>(CombatFxLibrary.AssetPath);
            lib.EditorSetEntries(entries);
            AssetDatabase.SaveAssets();
            sb.AppendLine("LIB_CombatFx: " + entries.Count + " entries at " + CombatFxLibrary.AssetPath);
            var text = sb.ToString();
            LSLog.Info(LogCategory.Combat, text);
            return text;
        }

        // ---- materials ------------------------------------------------------------------------------------------------

        private static FxMaterialSet BuildMaterials(StringBuilder sb)
        {
            var set = new FxMaterialSet();
            var dust = Vfx("T_VFX_DustImpact_8x8_BC");
            var smoke = Vfx("T_VFX_SmokePuff_8x8_BC");
            var sparks = Vfx("T_VFX_Sparks_4x4_E");
            var fire = Vfx("T_VFX_Fire_8x8_E");
            var muzzle = Vfx("T_VFX_MuzzleFlash_4x2_E");
            var tracer = Vfx("T_VFX_Tracer_E");
            var dot = Procedural("T_FX_SoftDot", () => FxTextures.BuildSoftDot(64));
            sb.AppendLine("  VFX sheets: dust " + Has(dust) + ", smoke " + Has(smoke) + ", sparks " + Has(sparks) + ", fire " + Has(fire) +
                          ", muzzle " + Has(muzzle) + ", tracer " + Has(tracer));

            set.Dust = Particle("M_FX_Dust", dust ?? dot, Color.white, false, true);
            set.DustTiles = dust != null ? new Vector2Int(8, 8) : Vector2Int.one;
            set.Smoke = Particle("M_FX_Smoke", smoke ?? dot, Color.white, false, true); // lit: smoke reads the sun (CINEMATIC_DIRECTION §1.3)
            set.SmokeTiles = smoke != null ? new Vector2Int(8, 8) : Vector2Int.one;
            set.Sparks = Particle("M_FX_Sparks", sparks ?? dot, new Color(3f, 3f, 3f, 1f), true, false);
            set.SparkTiles = sparks != null ? new Vector2Int(4, 4) : Vector2Int.one;
            set.Fire = Particle("M_FX_Fire", fire ?? dot, new Color(3f, 3f, 3f, 1f), true, false); // WP10 note: HDR ×2-4 for bloom
            set.FireTiles = fire != null ? new Vector2Int(8, 8) : Vector2Int.one;
            set.Soft = Particle("M_FX_Soft", dot, Color.white, false, false);
            set.SoftAdditive = Particle("M_FX_SoftAdditive", dot, Color.white, true, false);
            set.Muzzle = Particle("M_FX_MuzzleFlash", muzzle ?? Procedural("T_FX_MuzzleFlash", FxTextures.BuildMuzzle), new Color(3f, 2.6f, 2.1f, 1f), true, false);
            set.Tracer = Particle("M_FX_Tracer", tracer ?? Procedural("T_FX_Tracer", FxTextures.BuildTracer), new Color(4f, 4f, 4f, 1f), true, false);
            return set;
        }

        private static string Has(Object o) => o != null ? "yes" : "procedural";

        private static Texture2D Vfx(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(VfxTextureFolder + "/" + name + ".png");

        /// <summary>Saves a procedural texture as a PNG once (alpha is transparency, clamp) and returns the asset.</summary>
        private static Texture2D Procedural(string name, Func<Texture2D> build)
        {
            var path = TextureFolder + "/" + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            var tex = build();
            File.WriteAllBytes(Path.Combine(BuilderUtil.ProjectRoot, path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.sRGBTexture = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Particle(string name, Texture tex, Color tint, bool additive, bool lit)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = FxMaterials.CreateParticle(name, tex, tint, additive, lit);
                if (m == null)
                {
                    LSLog.Error(LogCategory.Combat, "Particle shader not found; " + name + " not built.");
                    return null;
                }
                AssetDatabase.CreateAsset(m, path);
            }
            else FxMaterials.Configure(m, tex, tint, additive);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Mesh[] MuzzleMeshes()
        {
            var built = FxFactory.BuildMuzzleMeshes();
            var result = new Mesh[built.Length];
            for (var i = 0; i < built.Length; i++)
                result[i] = BuilderUtil.CreateOrReplaceAsset(built[i], MeshFolder + "/" + built[i].name + ".asset");
            return result;
        }

        private static GameObject Save(GameObject go, string name) => BuilderUtil.SavePrefab(go, PrefabFolder + "/" + name + ".prefab");

        // ---- decals ---------------------------------------------------------------------------------------------------

        private static CombatFxEntry Decal(string id, string folder, string stem, StringBuilder sb)
        {
            var dir = DecalTextureFolder + "/" + folder;
            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + stem + "_BC.png");
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + stem + "_N.png");
            var matName = "M_" + stem.Substring(2); // T_Decal_BulletHoles → M_Decal_BulletHoles
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DecalMaterialFolder + "/" + matName + ".mat");
            if (mat == null)
            {
                var path = MaterialFolder + "/M_FX_" + stem.Substring(2) + ".mat";
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                var baseMap = bc != null ? bc : id == FxIds.DecalBulletHoles ? Procedural("T_FX_BulletHole", () => FxTextures.BuildHole(128)) : null;
                if (baseMap != null)
                {
                    if (mat == null)
                    {
                        mat = FxMaterials.CreateDecal(Path.GetFileNameWithoutExtension(path), baseMap, n);
                        if (mat != null) AssetDatabase.CreateAsset(mat, path);
                    }
                    else FxMaterials.ConfigureDecal(mat, baseMap, n, 0.6f);
                }
            }
            string[] names = null;
            Vector4[] rects = null;
            Vector2[] sizes = null;
            var layoutPath = Path.Combine(BuilderUtil.ProjectRoot, dir, stem + "_layout.json");
            if (File.Exists(layoutPath))
            {
                try
                {
                    var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(layoutPath));
                    if (layout?.elements != null)
                    {
                        names = new string[layout.elements.Length];
                        rects = new Vector4[layout.elements.Length];
                        sizes = new Vector2[layout.elements.Length];
                        for (var i = 0; i < layout.elements.Length; i++)
                        {
                            var e = layout.elements[i];
                            names[i] = e.name;
                            rects[i] = e.uv != null && e.uv.Length == 4 ? new Vector4(e.uv[0], e.uv[1], e.uv[2], e.uv[3]) : new Vector4(0f, 0f, 1f, 1f);
                            var w = e.px != null && e.px.Length == 4 ? Mathf.Max(1, e.px[2] - e.px[0]) : 1;
                            var h = e.px != null && e.px.Length == 4 ? Mathf.Max(1, e.px[3] - e.px[1]) : 1;
                            var s = DecalSize(id, e.name);
                            sizes[i] = w >= h ? new Vector2(s, s * h / w) : new Vector2(s * w / h, s);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LSLog.Exception(LogCategory.Combat, ex);
                }
            }
            sb.AppendLine("  decal " + id + ": material " + (mat != null ? AssetDatabase.GetAssetPath(mat) : "none") + ", " +
                          (rects != null ? rects.Length + " elements" : "whole texture"));
            return new CombatFxEntry(id, null, mat, null, names, rects, sizes);
        }

        /// <summary>Real-world size (metres, longest side) of a decal element (WP10 layout notes).</summary>
        public static float DecalSize(string id, string element)
        {
            element = element ?? string.Empty;
            if (id == FxIds.DecalBulletHoles)
            {
                if (element.StartsWith("wood")) return 0.08f;
                if (element.StartsWith("metal")) return 0.07f;
                return 0.1f; // plaster / concrete chips ~0.10 m
            }
            if (id == FxIds.DecalScorch)
            {
                if (element.StartsWith("blast")) return 3f;
                if (element.StartsWith("burn")) return 1f;
                if (element.StartsWith("fire")) return 1.75f;
                return 1.25f;
            }
            switch (element)
            {
                case "drip": return 0.35f;
                case "pool": return 0.4f;
                case "smear": return 0.3f;
                case "spatter": return 0.3f;
                case "drops": return 0.2f;
                default: return 0.25f;
            }
        }

        // ---- casings, projectiles, crate ------------------------------------------------------------------------------

        private static void AddCasings(List<CombatFxEntry> entries, StringBuilder sb)
        {
            var path = WeaponArt.ModelsRoot + "/Casings/SM_WPN_Casings.fbx";
            var meshes = new Dictionary<string, Mesh>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh m) meshes[m.name] = m;
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Weapons/M_WPN_Casings.mat");
            var pairs = new[]
            {
                (FxIds.Casing556, "SM_WPN_Casing_556x45"), (FxIds.Casing762x39, "SM_WPN_Casing_762x39"),
                (FxIds.Casing762x51, "SM_WPN_Casing_762x51"), (FxIds.Casing9mm, "SM_WPN_Casing_9x19"),
            };
            foreach (var (id, meshName) in pairs)
            {
                meshes.TryGetValue(meshName, out var mesh);
                entries.Add(new CombatFxEntry(id, null, mat, mesh != null ? new[] { mesh } : null));
            }
            sb.AppendLine("  casings: " + (meshes.Count > 0 ? meshes.Count + " meshes from " + path : "missing (generated cylinders at runtime)") +
                          (mat != null ? ", material M_WPN_Casings" : ", brass placeholder"));
        }

        private static GameObject BuildGrenadeProjectile(StringBuilder sb)
        {
            var root = new GameObject("PF_Projectile_M67");
            var model = WeaponArt.World(WeaponIds.Frag);
            if (model != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
            }
            else
            {
                BuilderUtil.PlaceholderPrimitive("M67", PrimitiveType.Sphere, new Vector3(0.064f, 0.075f, 0.064f), new Color(0.24f, 0.27f, 0.18f), root.transform);
            }
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            BuilderUtil.SetLayerRecursively(root, LayerIds.Projectile);
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.035f;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.angularDamping = 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            root.AddComponent<Grenade>();
            sb.AppendLine("  frag projectile: " + (model != null ? WeaponArt.PathOf(model) : "placeholder sphere"));
            return Save(root, "PF_Projectile_M67");
        }

        private static GameObject BuildRocketProjectile(StringBuilder sb)
        {
            var model = WeaponArt.LoadStem("RPG7", "SM_WPN_RPG7_Rocket_W") ?? WeaponArt.LoadStem("RPG7", "SM_WPN_RPG7_Rocket");
            if (model == null)
            {
                sb.AppendLine("  rocket projectile: missing (the pool builds a placeholder)");
                return null;
            }
            var root = new GameObject("PF_Projectile_RPG7");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            sb.AppendLine("  rocket projectile: " + WeaponArt.PathOf(model));
            return Save(root, "PF_Projectile_RPG7");
        }

        private static GameObject BuildCrate(StringBuilder sb)
        {
            var root = new GameObject("PF_AmmoCrate");
            var crate = AssetManifest.FindAll<GameObject>("PH_OLD_MILITARY_CRATE");
            var box = AssetManifest.FindAll<GameObject>("PH_AMMO_BOX");
            if (crate.Count > 0)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(crate[0]);
                inst.name = "Crate";
                inst.transform.SetParent(root.transform, false);
                var top = WeaponPrefabBuilder.LocalBounds(root).max.y;
                if (box.Count > 0)
                {
                    var b = (GameObject)PrefabUtility.InstantiatePrefab(box[0]);
                    b.name = "AmmoBox";
                    b.transform.SetParent(root.transform, false);
                    b.transform.localPosition = new Vector3(0.12f, top, 0f);
                    b.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
                }
                foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var bounds = WeaponPrefabBuilder.LocalBounds(root);
                var body = new GameObject("Body") { layer = LayerIds.Default };
                body.transform.SetParent(root.transform, false);
                var bc = body.AddComponent<BoxCollider>();
                bc.center = bounds.center;
                bc.size = bounds.size;
                body.AddComponent<SurfaceTag>().Surface = SurfaceType.Wood;
            }
            else
            {
                var ph = AmmoCrate.BuildPlaceholder();
                ph.transform.SetParent(root.transform, false);
                foreach (var r in ph.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = WeaponArt.Placeholder(new Color(0.27f, 0.29f, 0.18f));
            }
            root.AddComponent<AmmoCrate>();
            AmmoCrate.EnsureColliders(root);
            sb.AppendLine("  ammo crate: " + (crate.Count > 0 ? WeaponArt.PathOf(crate[0]) + (box.Count > 0 ? " + " + WeaponArt.PathOf(box[0]) : "") : "placeholder"));
            return BuilderUtil.SavePrefab(root, WeaponPrefabBuilder.PrefabFolder + "/PF_AmmoCrate.prefab");
        }
    }
}
