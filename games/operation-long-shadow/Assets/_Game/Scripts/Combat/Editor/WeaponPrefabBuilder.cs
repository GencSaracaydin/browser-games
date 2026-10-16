using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Combat.Data;
using LongShadow.Combat.Interactables;
using LongShadow.Combat.Viewmodel;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Combat.Editor
{
    /// <summary>
    /// Builds every weapon prefab from the WP03_ART models with a greybox fallback (TECH_PLAN §7.5 WeaponPrefabBuilder),
    /// then fills <c>LIB_WeaponVisuals</c>. Re-run it whenever art lands: prefabs are replaced in place (GUIDs kept).
    /// <list type="bullet">
    /// <item><c>PF_VM_&lt;Weapon&gt;</c> (COMMANDO, HUSH PUPPY, AK, RPK, M60D): layer Viewmodel, no shadow casting,
    /// rendering layers World + Cabin + Viewmodel, <see cref="ViewmodelRig"/> on the root (model, magazine child pivoted
    /// at the well, sockets, IK points, pose values from WeaponTable).</item>
    /// <item><c>PF_VM_M67</c>, <c>PF_VM_Knife</c>: the items the left hand carries.</item>
    /// <item><c>PF_W_&lt;Weapon&gt;</c> (every weapon with a world model): shadows on, <c>Muzzle</c>, <c>RightGrip</c> and
    /// <c>LeftGrip</c> sockets for WP05's BodyWeaponHolder (barrel forward, top up) plus the art's <c>Socket_*</c>.</item>
    /// <item><c>PF_Mag_&lt;Weapon&gt;</c>: the magazine dropped by empty reloads (Rigidbody, Debris layer).</item>
    /// <item><c>PF_Pickup_&lt;Weapon&gt;</c> (COMMANDO, AK, RPK): world model + <see cref="WeaponPickup"/>.</item>
    /// </list>
    /// </summary>
    public static class WeaponPrefabBuilder
    {
        /// <summary>Prefab folder (TECH_PLAN §4.1 Prefabs/Weapons, owned by WP03).</summary>
        public const string PrefabFolder = "Assets/_Game/Prefabs/Weapons";

        /// <summary>Weapons with a first-person view model.</summary>
        public static readonly string[] ViewmodelIds = { WeaponIds.Commando, WeaponIds.HushPuppy, WeaponIds.AK, WeaponIds.RPK, WeaponIds.DoorGun };
        /// <summary>Hand-held items of the left-hand actions.</summary>
        public static readonly string[] ItemIds = { WeaponIds.Frag, WeaponIds.Knife };
        /// <summary>Weapons with a world prefab (bodies, pickups, the helicopter).</summary>
        public static readonly string[] WorldIds =
        {
            WeaponIds.Commando, WeaponIds.HushPuppy, WeaponIds.AK, WeaponIds.RPK, WeaponIds.AKMS, WeaponIds.SVD, WeaponIds.RPG7,
            WeaponIds.DShK, WeaponIds.DoorGun, WeaponIds.Frag, WeaponIds.Smoke,
        };
        /// <summary>Weapons with a separate magazine.</summary>
        public static readonly string[] MagazineIds = { WeaponIds.Commando, WeaponIds.HushPuppy, WeaponIds.AK, WeaponIds.RPK };

        /// <summary>Prefab path for a kind prefix ("PF_VM_") and a weapon id.</summary>
        public static string PathFor(string prefix, string weaponId) => PrefabFolder + "/" + prefix + WeaponArt.Name(weaponId) + ".prefab";

        /// <summary>Builds every weapon prefab and LIB_WeaponVisuals; returns the report text (also logged).</summary>
        [MenuItem("LongShadow/Combat/Build Weapon Prefabs", priority = 110)]
        public static string BuildAll()
        {
            var report = new StringBuilder("[WeaponPrefabBuilder]\n");
            BuilderUtil.EnsureFolder(PrefabFolder);
            var entries = new Dictionary<string, (GameObject vm, GameObject world, GameObject mag, GameObject pickup, bool art)>();

            foreach (var id in ViewmodelIds)
            {
                var vm = BuildViewmodel(id, out var hasArt);
                Note(report, id, "view model", vm, hasArt);
                entries[id] = (vm, null, null, null, hasArt);
            }
            foreach (var id in ItemIds)
            {
                var item = BuildItem(id, out var hasArt);
                Note(report, id, "hand item", item, hasArt);
                entries[id] = (item, null, null, null, hasArt);
            }
            foreach (var id in WorldIds)
            {
                var world = BuildWorld(id, out var hasArt);
                Note(report, id, "world", world, hasArt);
                entries.TryGetValue(id, out var e);
                entries[id] = (e.vm, world, e.mag, e.pickup, e.art || hasArt);
            }
            foreach (var id in MagazineIds)
            {
                var mag = BuildMagazine(id, out var hasArt);
                Note(report, id, "magazine", mag, hasArt);
                var e = entries[id];
                entries[id] = (e.vm, e.world, mag, e.pickup, e.art);
            }
            foreach (var id in WeaponIds.Primaries)
            {
                var e = entries[id];
                var pickup = BuildPickup(id, e.world);
                Note(report, id, "pickup", pickup, e.art);
                entries[id] = (e.vm, e.world, e.mag, pickup, e.art);
            }

            var lib = BuilderUtil.LoadOrCreateAsset<WeaponVisualsLibrary>(WeaponVisualsLibrary.AssetPath);
            var list = new List<WeaponVisualEntry>();
            foreach (var kv in entries)
                list.Add(new WeaponVisualEntry(kv.Key, kv.Value.vm, kv.Value.world, kv.Value.mag, kv.Value.pickup, null, kv.Value.art));
            lib.EditorSetEntries(list);
            AssetDatabase.SaveAssets();
            report.AppendLine("LIB_WeaponVisuals: " + list.Count + " entries at " + WeaponVisualsLibrary.AssetPath);
            var text = report.ToString();
            LSLog.Info(LogCategory.Combat, text);
            return text;
        }

        // ---- view models ----------------------------------------------------------------------------------------------

        /// <summary>Builds <c>PF_VM_&lt;Weapon&gt;</c>.</summary>
        public static GameObject BuildViewmodel(string id, out bool hasArt)
        {
            var spec = WeaponTable.Get(id);
            var root = new GameObject("PF_VM_" + WeaponArt.Name(id));
            Transform model;
            Transform mag = null;
            var fp = WeaponArt.Fp(id);
            hasArt = fp != null;
            if (fp != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(fp);
                inst.name = "Model";
                model = inst.transform;
                model.SetParent(root.transform, false);
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                mag = CombatUtil.FindAny(model, "Mag", "SM_WPN_" + WeaponArt.Name(id) + "_Mag");
                if (mag == null && WeaponArt.Magazine(id) != null)
                {
                    var magInst = (GameObject)PrefabUtility.InstantiatePrefab(WeaponArt.Magazine(id));
                    magInst.name = "Mag";
                    mag = magInst.transform;
                    var well = CombatUtil.FindAny(model, "Socket_MagWell", "MagWell");
                    mag.SetParent(model, false);
                    mag.localPosition = well != null ? model.InverseTransformPoint(well.position) : spec.Viewmodel.Sockets.MagWell;
                    mag.localRotation = Quaternion.identity;
                }
            }
            else
            {
                LSLog.MissingAsset(LogCategory.Combat, WeaponIds.ManifestId(id) + " FP model", WeaponArt.ModelsRoot + "/" + WeaponArt.Folder(id) + "/");
                var gb = WeaponGreybox.Build(id, WeaponArt.Placeholder);
                gb.name = "Model";
                model = gb.transform;
                model.SetParent(root.transform, false);
                mag = model.Find("Mag");
            }
            var rig = root.AddComponent<ViewmodelRig>();
            rig.Configure(id, hasArt, model, mag, spec);
            PrepareViewmodelRenderers(root);
            return BuilderUtil.SavePrefab(root, PathFor("PF_VM_", id));
        }

        /// <summary>Builds the grenade / knife carried by the left hand (<c>PF_VM_M67</c>, <c>PF_VM_Knife</c>).</summary>
        public static GameObject BuildItem(string id, out bool hasArt)
        {
            var root = new GameObject("PF_VM_" + WeaponArt.Name(id));
            var fp = WeaponArt.Fp(id) ?? WeaponArt.World(id);
            hasArt = fp != null;
            if (fp != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(fp);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
                // In the palm: grenade slightly ahead of the fingers; the knife's grip at the origin, blade forward.
                inst.transform.localPosition = id == WeaponIds.Frag ? new Vector3(0f, 0.01f, 0.035f) : new Vector3(0f, 0f, 0.01f);
            }
            else
            {
                LSLog.MissingAsset(LogCategory.Combat, WeaponIds.ManifestId(id) + " FP model", WeaponArt.ModelsRoot + "/" + WeaponArt.Folder(id) + "/");
                if (id == WeaponIds.Frag)
                {
                    BuilderUtil.PlaceholderPrimitive("M67", PrimitiveType.Sphere, new Vector3(0.064f, 0.064f, 0.064f), new Color(0.24f, 0.27f, 0.18f), root.transform)
                        .transform.localPosition = new Vector3(0f, 0.01f, 0.035f);
                }
                else
                {
                    BuilderUtil.PlaceholderPrimitive("KnifeBlade", PrimitiveType.Cube, new Vector3(0.004f, 0.03f, 0.17f), new Color(0.3f, 0.3f, 0.32f), root.transform)
                        .transform.localPosition = new Vector3(0f, 0f, 0.12f);
                    BuilderUtil.PlaceholderPrimitive("KnifeHandle", PrimitiveType.Cube, new Vector3(0.022f, 0.026f, 0.11f), new Color(0.25f, 0.17f, 0.1f), root.transform)
                        .transform.localPosition = new Vector3(0f, 0f, -0.01f);
                }
            }
            PrepareViewmodelRenderers(root);
            return BuilderUtil.SavePrefab(root, PathFor("PF_VM_", id));
        }

        private static void PrepareViewmodelRenderers(GameObject root)
        {
            BuilderUtil.SetLayerRecursively(root, LayerIds.Viewmodel);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off; // TECH_PLAN D5: the view model never casts
                r.receiveShadows = true;
                r.renderingLayerMask = RenderingLayerIds.ViewmodelReceives; // TECH_PLAN §3.8: World + Cabin
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        // ---- world ----------------------------------------------------------------------------------------------------

        /// <summary>Builds <c>PF_W_&lt;Weapon&gt;</c>.</summary>
        public static GameObject BuildWorld(string id, out bool hasArt)
        {
            var root = new GameObject("PF_W_" + WeaponArt.Name(id));
            var world = WeaponArt.World(id);
            hasArt = world != null;
            Transform model;
            if (world != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(world);
                inst.name = "Model";
                model = inst.transform;
                model.SetParent(root.transform, false);
            }
            else
            {
                LSLog.MissingAsset(LogCategory.Combat, WeaponIds.ManifestId(id) + " world model", WeaponArt.ModelsRoot + "/" + WeaponArt.Folder(id) + "/");
                GameObject gb;
                if (id == WeaponIds.Frag || id == WeaponIds.Smoke)
                {
                    gb = new GameObject("Model");
                    var size = id == WeaponIds.Frag ? new Vector3(0.064f, 0.075f, 0.064f) : new Vector3(0.064f, 0.145f, 0.064f);
                    BuilderUtil.PlaceholderPrimitive(id, id == WeaponIds.Frag ? PrimitiveType.Sphere : PrimitiveType.Cylinder,
                        id == WeaponIds.Smoke ? new Vector3(size.x, size.y * 0.5f, size.z) : size, new Color(0.24f, 0.27f, 0.18f), gb.transform);
                }
                else gb = WeaponGreybox.Build(id, WeaponArt.Placeholder);
                gb.name = "Model";
                model = gb.transform;
                model.SetParent(root.transform, false);
            }
            var spec = WeaponTable.Get(id);
            var sockets = spec?.Viewmodel != null ? spec.Viewmodel.Sockets : WeaponGreybox.SocketsFor(id);
            var muzzle = CombatUtil.FindAny(model, "Socket_Muzzle");
            var left = CombatUtil.FindAny(model, "Socket_LeftHand");
            Socket(root.transform, "Muzzle", muzzle != null ? root.transform.InverseTransformPoint(muzzle.position) : sockets.Muzzle);
            Socket(root.transform, "RightGrip", sockets.Grip);
            Socket(root.transform, "LeftGrip", left != null ? root.transform.InverseTransformPoint(left.position) : sockets.LeftHand);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.renderingLayerMask = RenderingLayerIds.WorldMask;
            }
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return BuilderUtil.SavePrefab(root, PathFor("PF_W_", id));
        }

        private static void Socket(Transform root, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
        }

        // ---- magazines and pickups ------------------------------------------------------------------------------------

        /// <summary>Builds <c>PF_Mag_&lt;Weapon&gt;</c> (dropped by empty reloads).</summary>
        public static GameObject BuildMagazine(string id, out bool hasArt)
        {
            var root = new GameObject("PF_Mag_" + WeaponArt.Name(id));
            var mag = WeaponArt.Magazine(id);
            hasArt = mag != null;
            if (mag != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(mag);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
            }
            else
            {
                var gb = WeaponGreybox.BuildMagazineOnly(id, WeaponArt.Placeholder);
                gb.name = "Model";
                gb.transform.SetParent(root.transform, false);
            }
            BuilderUtil.SetLayerRecursively(root, LayerIds.Debris);
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var b = LocalBounds(root);
            var box = root.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = Vector3.Max(b.size, new Vector3(0.015f, 0.03f, 0.015f));
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = id == WeaponIds.RPK ? 1.2f : id == WeaponIds.HushPuppy ? 0.12f : 0.45f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;
            return BuilderUtil.SavePrefab(root, PathFor("PF_Mag_", id));
        }

        /// <summary>Builds <c>PF_Pickup_&lt;Weapon&gt;</c> from the world prefab.</summary>
        public static GameObject BuildPickup(string id, GameObject worldPrefab)
        {
            var root = new GameObject("PF_Pickup_" + WeaponArt.Name(id));
            if (worldPrefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(worldPrefab);
                inst.name = "World";
                inst.transform.SetParent(root.transform, false);
            }
            BuilderUtil.SetLayerRecursively(root, LayerIds.Debris);
            var pickup = root.AddComponent<WeaponPickup>();
            pickup.Setup(id, -1, -1);
            WeaponPickups.EnsurePhysics(root);
            return BuilderUtil.SavePrefab(root, PathFor("PF_Pickup_", id));
        }

        /// <summary>Bounds of every mesh under <paramref name="root"/> in its local space.</summary>
        public static Bounds LocalBounds(GameObject root)
        {
            var has = false;
            var b = new Bounds();
            var inv = root.transform.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = inv * mf.transform.localToWorldMatrix;
                for (var i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!has)
                    {
                        b = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else b.Encapsulate(p);
                }
            }
            return has ? b : new Bounds(Vector3.zero, new Vector3(0.03f, 0.15f, 0.06f));
        }

        private static void Note(StringBuilder sb, string id, string what, GameObject prefab, bool art)
        {
            sb.Append("  ").Append(id).Append(' ').Append(what).Append(": ")
              .Append(prefab != null ? AssetDatabase.GetAssetPath(prefab) : "FAILED")
              .Append(art ? " (art)" : " (greybox)")
              .Append(", ").Append(WeaponArt.Triangles(prefab)).Append(" tris\n");
        }

        // ---- budget ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Budget report (TECH_PLAN §11.4 WP03 DoD: FP weapon ≤ 20 k tris and ≤ 3 MB each): view-model triangles and the
        /// runtime size of the textures in each weapon's texture folder. Written to Logs/combat_budget.txt.
        /// </summary>
        [MenuItem("LongShadow/Combat/Report Weapon Budgets", priority = 140)]
        public static string ReportBudgets()
        {
            var sb = new StringBuilder("Weapon budgets (FP ≤ 20000 tris, ≤ 3 MB textures)\n");
            var ok = true;
            foreach (var id in ViewmodelIds)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PathFor("PF_VM_", id));
                var tris = 0;
                if (prefab != null)
                    foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.name.IndexOf("_LOD", System.StringComparison.Ordinal) < 0 || mf.name.EndsWith("_LOD0")) tris += WeaponArt.Triangles(mf.sharedMesh);
                long bytes = 0;
                var dir = WeaponArt.TexturesRoot + "/" + WeaponArt.Folder(id);
                if (AssetDatabase.IsValidFolder(dir))
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                        if (tex != null) bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                    }
                var mb = bytes / (1024f * 1024f);
                var pass = tris <= 20000 && mb <= 3f;
                ok &= pass;
                sb.AppendFormat("  {0,-10} {1,6} tris  {2,5:0.00} MB  {3}\n", id, tris, mb, pass ? "OK" : "OVER");
            }
            sb.Append(ok ? "All within budget.\n" : "Some weapons are over budget.\n");
            var text = sb.ToString();
            var path = Path.Combine(BuilderUtil.ProjectRoot, "Logs", "combat_budget.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            if (ok) LSLog.Info(LogCategory.Combat, text);
            else LSLog.Warn(LogCategory.Combat, text);
            return text;
        }
    }
}
