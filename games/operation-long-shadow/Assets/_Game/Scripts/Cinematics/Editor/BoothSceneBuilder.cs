using System.Collections.Generic;
using System.IO;
using LongShadow.Cinematics.Booth;
using LongShadow.Core;
using LongShadow.Core.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Scenes/Sections/S0_Booth.unity</c> (TECH_PLAN §7.12; GDD §8.0): the procedural shell
    /// (olive painted plaster below 1.5 m, perforated acoustic tile above and on the ceiling, worn concrete floor with
    /// the drain decal, one-way mirror, steel door), the WP09 booth set from RawAssets/wp09/ready (chair, CRT cart, six
    /// mismatched CRTs, scrambler trolley, reel-to-reel, phone, steel table, pen, ashtray, lighter, wall clock at 6:40,
    /// fluorescent batten, 16 mm film camera), WP05's seated Calder and the interrogator, the tube/fill/CRT lights and a
    /// baked-fill area light, reflection and light probes, Cam_Booth and the deck/film/clock feed cameras, every BOOTH.*
    /// marker and the runtime components. Everything sits under one "BoothRoot" at world (−600, 0, −600) on layer Booth
    /// and rendering layer Booth (the sun never lights it). Re-running replaces BoothRoot; missing content becomes
    /// greybox and is listed in the report; re-run after content lands.
    /// </summary>
    public static class BoothSceneBuilder
    {
        private static readonly List<string> s_report = new List<string>();

        /// <summary>Builds (or rebuilds) the scene. Returns the number of missing content items (placeholders used).</summary>
        public static int Build()
        {
            s_report.Clear();
            var missing = 0;
            var path = CinematicPaths.BoothScene;
            BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
            var existing = SceneManager.GetSceneByPath(path);
            var wasOpen = existing.IsValid() && existing.isLoaded;
            Scene scene;
            if (wasOpen) scene = existing;
            else if (File.Exists(Path.Combine(BuilderUtil.ProjectRoot, path))) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, path);
            }

            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "BoothRoot") Object.DestroyImmediate(go);

            var root = new GameObject("BoothRoot");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(BoothLayout.WorldOrigin, Quaternion.identity);

            ConfigureSceneLighting(scene);
            missing += BuildShell(root.transform);
            var parts = new Parts();
            missing += BuildFurniture(root.transform, parts);
            missing += BuildCrts(root.transform, parts);
            missing += BuildProps(root.transform, parts);
            missing += BuildLights(root.transform, parts);
            missing += BuildBodies(root.transform, parts);
            BuildCameras(root.transform, parts);
            BuildMarkers(root.transform, parts);
            BuildSlate(root.transform, parts);

            var look = new GameObject("BoothLook").AddComponent<BoothLook>();
            look.transform.SetParent(root.transform, false);
            look.gameObject.layer = LayerIds.Booth;
            root.AddComponent<BoothState>();
            root.AddComponent<BoothNavMesh>();
            var booth = root.AddComponent<BoothRoot>();
            var baked = Lightmapping.GetLightingDataAssetForScene(scene) != null;
            booth.Setup(parts.Camera, parts.Crts, parts.Tube, parts.Scrambler, parts.Reel, parts.Clock, parts.Pen, parts.Phone, parts.Lighter,
                parts.Smoke.ToArray(), parts.Calder, parts.Interrogator, look, parts.Slate, baked);

            SetLayerAndRenderingLayer(root, LayerIds.Booth);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);

            s_report.Insert(0, "S0_Booth built: " + missing + " placeholder(s); lighting " + (baked ? "baked" : "NOT baked (run Bake Booth Lighting)"));
            CinematicReport.Section("Booth scene", s_report);
            LSLog.Info(LogCategory.Cinematics, "S0_Booth built (" + missing + " placeholders). Report: " + CinematicPaths.Report);
            return missing;
        }

        private sealed class Parts
        {
            public BoothCameraRig Camera;
            public CRTScreen[] Crts = new CRTScreen[6];
            public FlickerLight Tube;
            public ScramblerBox Scrambler;
            public ReelDeck Reel;
            public WallClock Clock;
            public PenTapper Pen;
            public DeskPhone Phone;
            public SteelLighter Lighter;
            public readonly List<BoothSmoke> Smoke = new List<BoothSmoke>();
            public GameObject Calder;
            public GameObject Interrogator;
            public GameObject Slate;
            public Transform Toggle;
            public Transform ReelCentre;
            public Transform PhoneHandset;
            public Transform PenTransform;
        }

        // ------------------------------------------------------------------ scene settings

        private static void ConfigureSceneLighting(Scene scene)
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(CinematicPaths.BoothLighting);
            if (settings == null)
            {
                BuilderUtil.EnsureFolder(Path.GetDirectoryName(CinematicPaths.BoothLighting));
                settings = new LightingSettings { name = "LS_S0_Booth" };
                AssetDatabase.CreateAsset(settings, CinematicPaths.BoothLighting);
            }
            // GDD §9.4: S0 gets 1 lightmap (D16); the room is small, so a hero texel density still fits in 1024².
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.lightmapMaxSize = 1024;
            settings.lightmapPadding = 4;
            settings.lightmapResolution = 24f;
            settings.indirectResolution = 2f;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            settings.ao = true;
            settings.aoMaxDistance = 1f;
            settings.maxBounces = 2;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            EditorUtility.SetDirty(settings);
            Lightmapping.SetLightingSettingsForScene(scene, settings);

            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.012f, 0.013f, 0.012f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = false;
            if (previous.IsValid() && previous != scene) SceneManager.SetActiveScene(previous);
        }

        // ------------------------------------------------------------------ shell

        private static int BuildShell(Transform root)
        {
            var missing = 0;
            var shell = Child(root, "Shell");
            var plaster = Mat(CinematicPaths.MatPlaster, new Color(0.38f, 0.40f, 0.30f), ref missing);
            var acoustic = Mat(CinematicPaths.MatAcoustic, new Color(0.78f, 0.76f, 0.70f), ref missing);
            var floor = Mat(CinematicPaths.MatFloor, new Color(0.42f, 0.41f, 0.39f), ref missing);
            var mirror = Mat(CinematicPaths.MatMirror, new Color(0.02f, 0.02f, 0.02f), ref missing);
            var steel = SteelMaterial();
            FixTiling(CinematicPaths.MatPlaster, CinematicPaths.PlasterTileMetres);
            FixTiling(CinematicPaths.MatAcoustic, CinematicPaths.AcousticTileMetres);

            float x0 = BoothLayout.MinX, x1 = BoothLayout.MaxX, z0 = BoothLayout.MinZ, z1 = BoothLayout.MaxZ;
            float h = BoothLayout.Height, w = BoothLayout.WainscotHeight;

            Surface(shell, "Floor", BoothMeshes.Panel("SM_Booth_Floor", new Vector3(0f, 0f, 0f), Vector3.right, Vector3.forward, Vector3.up, x0, x1, z0, z1), floor, true);
            Surface(shell, "Ceiling", BoothMeshes.Panel("SM_Booth_Ceiling", new Vector3(0f, h, 0f), Vector3.right, Vector3.forward, Vector3.down, x0, x1, z0, z1), acoustic, false);

            // Far wall (mirror wall, z = MaxZ) faces −Z; the mirror opening is set into it.
            var mirrorRect = BoothLayout.MirrorRect;
            var mirrorHoleLower = new[] { new Rect(mirrorRect.xMin, mirrorRect.yMin, mirrorRect.width, w - mirrorRect.yMin) };
            var mirrorHoleUpper = new[] { new Rect(mirrorRect.xMin, w, mirrorRect.width, mirrorRect.yMax - w) };
            Surface(shell, "Wall_Far_Lower", BoothMeshes.Panel("SM_Booth_WallFarLower", new Vector3(0f, 0f, z1), Vector3.right, Vector3.up, Vector3.back, x0, x1, 0f, w, mirrorHoleLower), plaster, false);
            Surface(shell, "Wall_Far_Upper", BoothMeshes.Panel("SM_Booth_WallFarUpper", new Vector3(0f, 0f, z1), Vector3.right, Vector3.up, Vector3.back, x0, x1, w, h, mirrorHoleUpper), acoustic, false);

            // Back wall (z = MinZ) faces +Z with the door.
            var door = new Rect(BoothLayout.DoorX.x, 0f, BoothLayout.DoorX.y - BoothLayout.DoorX.x, BoothLayout.DoorHeight);
            var doorLower = new[] { new Rect(door.xMin, 0f, door.width, w) };
            var doorUpper = new[] { new Rect(door.xMin, w, door.width, door.yMax - w) };
            Surface(shell, "Wall_Back_Lower", BoothMeshes.Panel("SM_Booth_WallBackLower", new Vector3(0f, 0f, z0), Vector3.right, Vector3.up, Vector3.forward, x0, x1, 0f, w, doorLower), plaster, false);
            Surface(shell, "Wall_Back_Upper", BoothMeshes.Panel("SM_Booth_WallBackUpper", new Vector3(0f, 0f, z0), Vector3.right, Vector3.up, Vector3.forward, x0, x1, w, h, doorUpper), acoustic, false);

            // Side walls.
            Surface(shell, "Wall_Left_Lower", BoothMeshes.Panel("SM_Booth_WallLeftLower", new Vector3(x0, 0f, 0f), Vector3.forward, Vector3.up, Vector3.right, z0, z1, 0f, w), plaster, false);
            Surface(shell, "Wall_Left_Upper", BoothMeshes.Panel("SM_Booth_WallLeftUpper", new Vector3(x0, 0f, 0f), Vector3.forward, Vector3.up, Vector3.right, z0, z1, w, h), acoustic, false);
            Surface(shell, "Wall_Right_Lower", BoothMeshes.Panel("SM_Booth_WallRightLower", new Vector3(x1, 0f, 0f), Vector3.forward, Vector3.up, Vector3.left, z0, z1, 0f, w), plaster, false);
            Surface(shell, "Wall_Right_Upper", BoothMeshes.Panel("SM_Booth_WallRightUpper", new Vector3(x1, 0f, 0f), Vector3.forward, Vector3.up, Vector3.left, z0, z1, w, h), acoustic, false);

            // Dado rail between plaster and tile, the steel door and its frame, the mirror glass and its steel frame.
            var rails = new List<(Vector3, Vector3)>
            {
                (new Vector3(0f, w, z1 - 0.012f), new Vector3(x1 - x0, 0.045f, 0.024f)),
                (new Vector3(0f, w, z0 + 0.012f), new Vector3(x1 - x0, 0.045f, 0.024f)),
                (new Vector3(x0 + 0.012f, w, (z0 + z1) * 0.5f), new Vector3(0.024f, 0.045f, z1 - z0)),
                (new Vector3(x1 - 0.012f, w, (z0 + z1) * 0.5f), new Vector3(0.024f, 0.045f, z1 - z0)),
            };
            Surface(shell, "DadoRail", BoothMeshes.Boxes("SM_Booth_DadoRail", rails), steel, false);
            Surface(shell, "Door", BoothMeshes.Box("SM_Booth_Door", new Vector3(door.center.x, door.height * 0.5f, z0 - 0.02f), new Vector3(door.width, door.height, 0.05f)), steel, false);
            var frame = new List<(Vector3, Vector3)>
            {
                (new Vector3(mirrorRect.center.x, mirrorRect.yMin - 0.025f, z1 - 0.02f), new Vector3(mirrorRect.width + 0.1f, 0.05f, 0.04f)),
                (new Vector3(mirrorRect.center.x, mirrorRect.yMax + 0.025f, z1 - 0.02f), new Vector3(mirrorRect.width + 0.1f, 0.05f, 0.04f)),
                (new Vector3(mirrorRect.xMin - 0.025f, mirrorRect.center.y, z1 - 0.02f), new Vector3(0.05f, mirrorRect.height, 0.04f)),
                (new Vector3(mirrorRect.xMax + 0.025f, mirrorRect.center.y, z1 - 0.02f), new Vector3(0.05f, mirrorRect.height, 0.04f)),
            };
            Surface(shell, "MirrorFrame", BoothMeshes.Boxes("SM_Booth_MirrorFrame", frame), steel, false);
            Surface(shell, "Mirror", BoothMeshes.Panel("SM_Booth_Mirror", new Vector3(0f, 0f, BoothLayout.MirrorZ), Vector3.right, Vector3.up, Vector3.back,
                mirrorRect.xMin, mirrorRect.xMax, mirrorRect.yMin, mirrorRect.yMax), mirror, false);

            // Colliders for the runtime NavMesh and physics (floor + walls).
            var col = shell.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3((x0 + x1) * 0.5f, -0.05f, (z0 + z1) * 0.5f);
            col.size = new Vector3(x1 - x0, 0.1f, z1 - z0);

            // Floor drain: URP decal projector (needs the Decal renderer feature, which PC_Renderer has).
            var drainMat = AssetDatabase.LoadAssetAtPath<Material>(CinematicPaths.MatDrainDecal);
            if (drainMat != null)
            {
                var drain = new GameObject("Drain_Decal");
                drain.transform.SetParent(shell, false);
                drain.transform.localPosition = BoothLayout.Drain + Vector3.up * 0.05f;
                drain.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var dp = drain.AddComponent<DecalProjector>();
                dp.material = drainMat;
                dp.size = new Vector3(0.42f, 0.42f, 0.12f);
                dp.pivot = Vector3.zero;
            }
            else
            {
                s_report.Add("missing: " + CinematicPaths.MatDrainDecal + " (drain decal skipped)");
                missing++;
            }
            return missing;
        }

        private static void Surface(Transform parent, string name, Mesh mesh, Material mat, bool lightmapped)
        {
            var saved = BoothMeshes.Save(mesh, true);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = saved;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.receiveGI = ReceiveGI.Lightmaps;
            r.shadowCastingMode = ShadowCastingMode.TwoSided;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccluderStatic);
            _ = lightmapped;
        }

        // ------------------------------------------------------------------ furniture

        private static int BuildFurniture(Transform root, Parts parts)
        {
            var missing = 0;
            var f = Child(root, "Furniture");
            var chair = Model(CinematicPaths.ModelChair, "Chair", f, BoothLayout.Chair, 0f, new Vector3(0.69f, 0.95f, 0.71f), ref missing);
            MakeStatic(chair);
            MakeStatic(Model(CinematicPaths.ModelCartLong, "CRT_Cart", f, BoothLayout.CrtCart, 180f, new Vector3(1.7f, 0.8f, 0.53f), ref missing));
            MakeStatic(Model(CinematicPaths.ModelCart, "Scrambler_Trolley", f, BoothLayout.ScramblerTrolley, 180f, new Vector3(1.13f, 0.85f, 0.56f), ref missing));
            MakeStatic(Model(CinematicPaths.ModelCart, "Phone_Trolley", f, BoothLayout.PhoneTrolley, 180f, new Vector3(1.13f, 0.85f, 0.56f), ref missing));
            var filmYaw = Quaternion.LookRotation(BoothLayout.FilmCamTarget - BoothLayout.FilmCamLens).eulerAngles.y;
            MakeStatic(Model(CinematicPaths.ModelFilmCamera, "FilmCamera16mm", f, BoothLayout.FilmCamBase, filmYaw, new Vector3(0.6f, 1.95f, 0.6f), ref missing));

            // The interrogator's steel table (procedural: brushed steel top, square legs, stretchers).
            var tt = BoothLayout.TableTop;
            var hw = BoothLayout.TableSize.x * 0.5f - 0.04f;
            var hd = BoothLayout.TableSize.y * 0.5f - 0.04f;
            var boxes = new List<(Vector3, Vector3)>
            {
                (new Vector3(0f, tt - 0.015f, 0f), new Vector3(BoothLayout.TableSize.x, 0.03f, BoothLayout.TableSize.y)),
                (new Vector3(-hw, (tt - 0.03f) * 0.5f, -hd), new Vector3(0.035f, tt - 0.03f, 0.035f)),
                (new Vector3(hw, (tt - 0.03f) * 0.5f, -hd), new Vector3(0.035f, tt - 0.03f, 0.035f)),
                (new Vector3(-hw, (tt - 0.03f) * 0.5f, hd), new Vector3(0.035f, tt - 0.03f, 0.035f)),
                (new Vector3(hw, (tt - 0.03f) * 0.5f, hd), new Vector3(0.035f, tt - 0.03f, 0.035f)),
                (new Vector3(0f, 0.16f, -hd), new Vector3(hw * 2f, 0.025f, 0.02f)),
                (new Vector3(0f, 0.16f, hd), new Vector3(hw * 2f, 0.025f, 0.02f)),
            };
            var table = new GameObject("SteelTable");
            table.transform.SetParent(f, false);
            table.transform.localPosition = BoothLayout.Table;
            table.AddComponent<MeshFilter>().sharedMesh = BoothMeshes.Save(BoothMeshes.Boxes("SM_Booth_SteelTable", boxes), true);
            var tr = table.AddComponent<MeshRenderer>();
            tr.sharedMaterial = SteelMaterial();
            MakeStatic(table);
            return missing;
        }

        // ------------------------------------------------------------------ CRTs

        private static int BuildCrts(Transform root, Parts parts)
        {
            var missing = 0;
            var group = Child(root, "CRTs");
            var bottomTop = new float[3];
            for (var i = 0; i < 6; i++)
            {
                var top = i >= 3;
                var path = top ? CinematicPaths.ModelTv2 : CinematicPaths.ModelTv1;
                var size = top ? new Vector3(0.4f, 0.411f, 0.35f) : new Vector3(0.6f, 0.457f, 0.471f);
                var scale = BoothLayout.CrtScale[i];
                var centre = BoothLayout.Crt[i];
                var baseY = top ? bottomTop[i - 3] : BoothLayout.CrtCartTop;
                var pos = new Vector3(centre.x, baseY, centre.z);
                var tv = Model(path, "CRT" + (i + 1), group, pos, 180f + BoothLayout.CrtYaw[i], size, ref missing);
                tv.transform.localScale = Vector3.one * scale;
                if (!top) bottomTop[i] = baseY + size.y * scale;
                MakeStatic(tv, false);

                // Keep LOD0 (the booth is always close) and find the glass slot.
                foreach (var lg in tv.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(lg);
                Renderer screen = null;
                var slot = -1;
                foreach (var r in tv.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name.Contains("_LOD1") || r.name.Contains("_LOD2"))
                    {
                        r.enabled = false;
                        continue;
                    }
                    var mats = r.sharedMaterials;
                    for (var m = 0; m < mats.Length; m++)
                        if (mats[m] != null && mats[m].name.Contains("Screen"))
                        {
                            screen = r;
                            slot = m;
                        }
                    if (screen == null && mats.Length > 1)
                    {
                        screen = r;
                        slot = mats.Length - 1;
                    }
                }
                if (screen == null)
                {
                    // Placeholder: a glass quad on the front of the greybox set.
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.name = "Screen_PH";
                    quad.transform.SetParent(tv.transform, false);
                    quad.transform.localPosition = new Vector3(0f, size.y * 0.5f, size.z * 0.5f + 0.002f);
                    quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    quad.transform.localScale = new Vector3(size.x * 0.72f, size.y * 0.66f, 1f);
                    screen = quad.GetComponent<Renderer>();
                    slot = 0;
                }

                // Glass centre and size from the slot's submesh bounds.
                var glassCentre = tv.transform.TransformPoint(new Vector3(0f, size.y * 0.55f, size.z * 0.5f));
                var glassSize = new Vector2(size.x * 0.7f * scale, size.y * 0.62f * scale);
                var mf = screen.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && slot < mf.sharedMesh.subMeshCount)
                {
                    var sb = mf.sharedMesh.GetSubMesh(slot).bounds;
                    glassCentre = screen.transform.TransformPoint(sb.center);
                    var ext = Vector3.Scale(sb.size, screen.transform.lossyScale);
                    glassSize = new Vector2(Mathf.Max(ext.x, ext.z), ext.y);
                }
                var anchor = new GameObject("GlassAnchor").transform;
                anchor.SetParent(tv.transform, true);
                anchor.position = glassCentre;
                anchor.rotation = root.rotation * Quaternion.Euler(0f, 180f, 0f);

                var glow = new GameObject("CRT_Glow").AddComponent<Light>();
                glow.transform.SetParent(tv.transform, true);
                glow.transform.position = glassCentre + root.rotation * Vector3.back * 0.25f;
                glow.type = LightType.Point;
                glow.range = 1.7f;
                glow.color = new Color(0.78f, 0.9f, 1f);
                glow.intensity = 0f;
                glow.shadows = LightShadows.None;
                glow.lightmapBakeType = LightmapBakeType.Realtime;
                glow.renderingLayerMask = (int)RenderingLayerIds.BoothMask;

                var crt = tv.AddComponent<CRTScreen>();
                crt.Setup(i + 1, screen, slot, anchor, glassSize, glow);
                parts.Crts[i] = crt;
            }
            return missing;
        }

        // ------------------------------------------------------------------ props

        private static int BuildProps(Transform root, Parts parts)
        {
            var missing = 0;
            var p = Child(root, "Props");

            var faceChair = new System.Func<Vector3, float>(pos => Quaternion.LookRotation(new Vector3(-pos.x, 0f, -pos.z)).eulerAngles.y);

            var scrambler = Model(CinematicPaths.ModelScrambler, "ScramblerBox", p, BoothLayout.Scrambler, faceChair(BoothLayout.Scrambler), new Vector3(0.26f, 0.18f, 0.22f), ref missing);
            parts.Scrambler = scrambler.AddComponent<ScramblerBox>();
            parts.Toggle = FindDeep(scrambler.transform, "Toggle");
            parts.Scrambler.Setup(FindDeep(scrambler.transform, "Needle"), parts.Toggle);

            var reel = Model(CinematicPaths.ModelReel, "ReelToReel", p, BoothLayout.Reel, faceChair(BoothLayout.Reel), new Vector3(0.44f, 0.15f, 0.36f), ref missing);
            parts.Reel = reel.AddComponent<ReelDeck>();
            parts.Reel.Setup(FindDeep(reel.transform, "ReelLeft"), FindDeep(reel.transform, "ReelRight"),
                FindDeep(reel.transform, "TapePackLeft"), FindDeep(reel.transform, "TapePackRight"));
            parts.ReelCentre = reel.transform;

            var phone = Model(CinematicPaths.ModelPhone, "DeskPhone", p, BoothLayout.Phone, faceChair(BoothLayout.Phone), new Vector3(0.28f, 0.15f, 0.2f), ref missing);
            parts.Phone = phone.AddComponent<DeskPhone>();
            parts.PhoneHandset = FindDeep(phone.transform, "Handset");
            parts.Phone.Setup(parts.PhoneHandset);

            // The wall clock on the mirror wall above the mirror, front toward the chair (root pivot = back centre).
            var clock = Model(CinematicPaths.ModelClock, "WallClock", p, new Vector3(BoothLayout.Clock.x, BoothLayout.Clock.y, BoothLayout.MaxZ - 0.012f), 180f, new Vector3(0.32f, 0.32f, 0.05f), ref missing);
            parts.Clock = clock.AddComponent<WallClock>();
            parts.Clock.Setup(FindDeep(clock.transform, "HandHour"), FindDeep(clock.transform, "HandMinute"), FindDeep(clock.transform, "HandSecond"));
            var cracked = AssetDatabase.LoadAssetAtPath<Material>(CinematicPaths.MatClockGlassCracked);
            if (cracked != null)
                foreach (var r in clock.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (var i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name.Contains("Glass")) mats[i] = cracked;
                    r.sharedMaterials = mats;
                }

            var tableTop = BoothLayout.TableTop;
            var pen = Model(CinematicPaths.ModelPen, "Pen", p, BoothLayout.Pen, 25f, new Vector3(0.135f, 0.012f, 0.012f), ref missing);
            parts.Pen = pen.AddComponent<PenTapper>();
            parts.PenTransform = pen.transform;

            var ashtray = Model(CinematicPaths.ModelAshtray, "Ashtray", p, BoothLayout.Ashtray, 0f, new Vector3(0.16f, 0.06f, 0.16f), ref missing);
            var cig = Model(CinematicPaths.ModelCigarette, "Cigarette", ashtray.transform, new Vector3(0.02f, 0.045f, 0f), 15f, new Vector3(0.084f, 0.008f, 0.008f), ref missing, local: true);
            var anchor = FindDeep(cig.transform, "SmokeAnchor");
            parts.Smoke.Add(Smoke(anchor != null ? anchor : cig.transform, "Smoke_Ashtray", false, Vector3.zero));

            var lighter = Model(CinematicPaths.ModelLighter, "SteelLighter", p, BoothLayout.Lighter, 70f, new Vector3(0.04f, 0.065f, 0.014f), ref missing);
            var flameLight = new GameObject("Flame_Light").AddComponent<Light>();
            flameLight.transform.SetParent(lighter.transform, false);
            flameLight.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            flameLight.type = LightType.Point;
            flameLight.range = 1.2f;
            flameLight.intensity = 0.6f;
            flameLight.color = new Color(1f, 0.62f, 0.25f);
            flameLight.shadows = LightShadows.None;
            flameLight.renderingLayerMask = (int)RenderingLayerIds.BoothMask;
            flameLight.lightmapBakeType = LightmapBakeType.Realtime;
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(flame.GetComponent<Collider>());
            flame.name = "Flame";
            flame.transform.SetParent(lighter.transform, false);
            flame.transform.localPosition = new Vector3(0f, 0.078f, 0f);
            flame.transform.localScale = new Vector3(0.007f, 0.018f, 0.007f);
            flame.GetComponent<Renderer>().sharedMaterial = FlameMaterial();
            flame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            parts.Lighter = lighter.AddComponent<SteelLighter>();
            parts.Lighter.Setup(FindDeep(lighter.transform, "Lid"), FindDeep(lighter.transform, "Wheel"), flameLight, flame.transform);

            // The interrogator's hand-held cigarette (GDD §8.0 smoke emitter).
            var smokeRoot = Child(p, "Smoke_Interrogator");
            smokeRoot.localPosition = BoothLayout.Smoke;
            parts.Smoke.Add(Smoke(smokeRoot, "Smoke_HandHeld", true, Vector3.zero));
            _ = tableTop;
            return missing;
        }

        private static BoothSmoke Smoke(Transform parent, string name, bool handHeld, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CinematicPaths.FxMaterials + "/" + CinematicAssetIds.MatSmoke + ".mat");
            var s = go.AddComponent<BoothSmoke>();
            var so = new SerializedObject(s);
            so.FindProperty("_handHeld").boolValue = handHeld;
            so.ApplyModifiedPropertiesWithoutUndo();
            return s;
        }

        // ------------------------------------------------------------------ lights and probes

        private static int BuildLights(Transform root, Parts parts)
        {
            var missing = 0;
            var l = Child(root, "Lighting");

            // Fluorescent batten: only child A (single tube) at (0, 2.95, 0.6), hung from the ceiling.
            var batten = Model(CinematicPaths.ModelBatten, "FluorescentBatten", l, Vector3.zero, 0f, new Vector3(0.9f, 0.035f, 0.05f), ref missing);
            Transform a = null;
            foreach (Transform c in batten.transform)
            {
                if (c.name.Contains("FluorescentBatten_A")) a = c;
                else if (c.name.StartsWith("FluorescentBatten_")) c.gameObject.SetActive(false);
            }
            var target = BoothLayout.Tube;
            if (a != null)
            {
                batten.transform.localPosition = new Vector3(target.x - a.localPosition.x, 0f, target.z - a.localPosition.z);
                var rs = a.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    var topLocal = root.InverseTransformPoint(b.max).y;
                    batten.transform.localPosition += new Vector3(0f, BoothLayout.Height - topLocal, 0f);
                }
            }
            else batten.transform.localPosition = target;
            Renderer glass = null;
            var glassSlot = -1;
            foreach (var r in batten.GetComponentsInChildren<Renderer>(false))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.Contains("Glass"))
                    {
                        glass = r;
                        glassSlot = i;
                    }
            }

            var key = new GameObject("Tube_Key").AddComponent<Light>();
            key.transform.SetParent(l, false);
            key.transform.localPosition = target + Vector3.down * 0.08f;
            key.type = LightType.Point;
            key.range = 6.5f;
            key.intensity = 2.4f;
            key.color = new Color(0.9f, 1f, 0.93f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.85f;
            key.shadowNearPlane = 0.1f;
            key.lightmapBakeType = LightmapBakeType.Realtime;
            key.renderingLayerMask = (int)RenderingLayerIds.BoothMask;

            var fill = new GameObject("Tube_BounceFill").AddComponent<Light>();
            fill.transform.SetParent(l, false);
            fill.transform.localPosition = new Vector3(0f, 1.4f, 0.9f);
            fill.type = LightType.Point;
            fill.range = 4.5f;
            fill.intensity = 0.35f;
            fill.color = new Color(0.82f, 0.86f, 0.74f);
            fill.shadows = LightShadows.None;
            fill.lightmapBakeType = LightmapBakeType.Realtime;
            fill.renderingLayerMask = (int)RenderingLayerIds.BoothMask;

            parts.Tube = batten.AddComponent<FlickerLight>();
            parts.Tube.Setup(key, fill, glass, glassSlot, FlickerMode.On);

            // Baked fill: the tube's bounce in the lightmap (EditorOnly: stripped from the player).
            var bake = new GameObject("BAKE_TubeArea");
            bake.tag = "EditorOnly";
            bake.transform.SetParent(l, false);
            bake.transform.localPosition = target + Vector3.down * 0.03f;
            bake.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var area = bake.AddComponent<Light>();
            area.type = LightType.Rectangle;
            area.areaSize = new Vector2(0.9f, 0.12f);
            area.intensity = 1.6f;
            area.color = new Color(0.9f, 1f, 0.93f);
            area.lightmapBakeType = LightmapBakeType.Baked;
            area.bounceIntensity = 1f;
            area.renderingLayerMask = (int)RenderingLayerIds.BoothMask;

            var probeGo = new GameObject("ReflectionProbe_Booth");
            probeGo.transform.SetParent(l, false);
            probeGo.transform.localPosition = new Vector3(0f, 1.3f, 0.6f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.size = new Vector3(BoothLayout.MaxX - BoothLayout.MinX, BoothLayout.Height, BoothLayout.MaxZ - BoothLayout.MinZ);
            probe.center = new Vector3(0f, BoothLayout.Height * 0.5f - 1.3f, (BoothLayout.MaxZ + BoothLayout.MinZ) * 0.5f - 0.6f);
            probe.resolution = 256;
            probe.cullingMask = 1 << LayerIds.Booth;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = Color.black;
            probe.importance = 2;
            probe.blendDistance = 0.2f;

            var lpg = new GameObject("LightProbes_Booth").AddComponent<LightProbeGroup>();
            lpg.transform.SetParent(l, false);
            var positions = new List<Vector3>();
            foreach (var x in new[] { -2.0f, -0.9f, 0f, 0.9f, 2.0f })
            foreach (var y in new[] { 0.3f, 1.2f, 2.4f })
            foreach (var z in new[] { -1.6f, -0.5f, 0.5f, 1.4f, 2.2f })
                positions.Add(new Vector3(x, y, z));
            lpg.probePositions = positions.ToArray();
            return missing;
        }

        // ------------------------------------------------------------------ bodies

        private static int BuildBodies(Transform root, Parts parts)
        {
            var missing = 0;
            var bodies = Child(root, "Bodies");
            parts.Calder = Body(CinematicPaths.BodyCalderBooth, "Calder_Booth", bodies, BoothLayout.Chair + new Vector3(0f, 0f, -0.02f), 0f, ref missing);
            parts.Interrogator = Body(CinematicPaths.BodyInterrogator, "Interrogator", bodies, BoothLayout.InterrogatorMark, 20f, ref missing);
            // Calder never leaves the chair: no NavMesh agent (the chair is carved out of the booth NavMesh).
            if (parts.Calder != null)
                foreach (var agent in parts.Calder.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) agent.enabled = false;

            // Electrodes taped to the forearms, wires from the armrests (PROC_BOOTH_SMALLS conventions).
            var animator = parts.Calder != null ? parts.Calder.GetComponentInChildren<Animator>() : null;
            foreach (var left in new[] { true, false })
            {
                Transform forearm = null;
                if (animator != null && animator.isHuman)
                    forearm = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                if (forearm != null)
                {
                    var e = Model(CinematicPaths.ModelElectrode, left ? "Electrode_L" : "Electrode_R", forearm, new Vector3(0f, 0.12f, 0.03f), 0f,
                        new Vector3(0.045f, 0.01f, 0.032f), ref missing, local: true);
                    e.transform.localRotation = Quaternion.Euler(0f, 0f, left ? 90f : -90f);
                }
                var wire = Model(CinematicPaths.ModelElectrodeWire, left ? "ElectrodeWire_L" : "ElectrodeWire_R", bodies,
                    new Vector3(left ? -0.235f : 0.235f, 0.62f, 0.12f), 0f, new Vector3(0.01f, 0.68f, 0.38f), ref missing);
                if (left) wire.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
            return missing;
        }

        private static GameObject Body(string path, string name, Transform parent, Vector3 pos, float yaw, ref int missing)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = name;
            }
            else
            {
                missing++;
                s_report.Add("missing: " + path + " (capsule stand-in)");
                go = new GameObject(name + "_PH");
                go.transform.SetParent(parent, false);
                var body = BuilderUtil.PlaceholderPrimitive(name, PrimitiveType.Capsule, new Vector3(0.45f, 0.62f, 0.45f), Placeholders.Greybox, go.transform);
                body.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            }
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        // ------------------------------------------------------------------ cameras, markers, slate

        private static void BuildCameras(Transform root, Parts parts)
        {
            var c = Child(root, "Cameras");
            var cam = MakeCamera(c, "Cam_Booth", BoothLayout.Pov, Quaternion.identity, BoothLayout.PovFov);
            cam.nearClipPlane = 0.02f;
            cam.depth = 10f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.SetRenderer(0);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.volumeLayerMask = 1 << LayerIds.Booth;
            parts.Camera = cam.gameObject.AddComponent<BoothCameraRig>();
            cam.enabled = false;

            Feed(c, "Cam_Feed_Deck", BindingIds.FeedDeck, BoothCameraPreset.DeckFeed, 0f);
            Feed(c, "Cam_Feed_Film", BindingIds.FeedFilm, BoothCameraPreset.FilmCam, 0f);
            Feed(c, "Cam_Feed_Clock", BindingIds.FeedClock, BoothCameraPreset.Clock, 0f);
        }

        private static void Feed(Transform parent, string name, string id, BoothCameraPreset preset, float fps)
        {
            BoothLayout.Preset(preset, out var p, out var r, out var fov);
            var cam = MakeCamera(parent, name, p, r, fov);
            cam.enabled = false;
            var feed = cam.gameObject.AddComponent<FeedCamera>();
            feed.Setup(id, cam, fps, true);
        }

        private static Camera MakeCamera(Transform parent, string name, Vector3 local, Quaternion rot, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 25f;
            cam.cullingMask = 1 << LayerIds.Booth;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            return cam;
        }

        private static void BuildMarkers(Transform root, Parts parts)
        {
            var m = Child(root, "Markers");
            Marker(m, "BOOTH.C_POV", BoothLayout.Pov);
            Marker(m, "BOOTH.C_CLOCK", BoothLayout.ClockCamera);
            for (var i = 0; i < 6; i++) Marker(m, "BOOTH.CRT" + (i + 1), BoothLayout.Crt[i]);
            Marker(m, "BOOTH.Mirror", new Vector3(BoothLayout.MirrorRect.center.x, BoothLayout.MirrorRect.center.y, BoothLayout.MirrorZ));
            Marker(m, "BOOTH.Clock", BoothLayout.Clock);
            Marker(m, "BOOTH.Tube", BoothLayout.Tube);
            // The interrogator reaches for the toggle (WP05 BoothBody uses BOOTH.Scrambler).
            Marker(m, "BOOTH.Scrambler", parts.Toggle != null ? root.InverseTransformPoint(parts.Toggle.position) : BoothLayout.Scrambler + new Vector3(0f, 0.07f, 0f));
            Marker(m, "BOOTH.Reel", BoothLayout.Reel + new Vector3(0f, 0.08f, 0f));
            Marker(m, "BOOTH.FeedDeck", BoothLayout.FeedDeck);
            Marker(m, "BOOTH.Phone", parts.PhoneHandset != null ? root.InverseTransformPoint(parts.PhoneHandset.position) : BoothLayout.Phone);
            Marker(m, "BOOTH.Table", BoothLayout.Table);
            Marker(m, "BOOTH.Pen", BoothLayout.Pen);
            Marker(m, "BOOTH.Smoke", BoothLayout.Smoke);
            Marker(m, "BOOTH.FilmCam", BoothLayout.FilmCamLens);
        }

        private static void Marker(Transform parent, string id, Vector3 local)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.AddComponent<SceneMarker>().SetId(id);
        }

        private static void BuildSlate(Transform root, Parts parts)
        {
            // "S-3 / SESSION 11 / 03-84" burned into the film frame (MID-B shot 4): a card in front of the film camera.
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                s_report.Add("TMP Essential Resources not imported: slate skipped (run WP08's LongShadow/UI/1 Import TMP Essential Resources, then rebuild)");
                return;
            }
            BoothLayout.Preset(BoothCameraPreset.FilmCam, out var p, out var r, out _);
            var go = new GameObject("Slate_Session11");
            go.transform.SetParent(root, false);
            go.transform.localPosition = p + r * new Vector3(-0.17f, -0.12f, 0.55f);
            go.transform.localRotation = r;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "S-3 / SESSION 11 / 03-84";
            tmp.fontSize = 0.32f;
            tmp.color = new Color(0.95f, 0.95f, 0.92f);
            tmp.alignment = TextAlignmentOptions.BottomLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(0.5f, 0.06f);
            go.SetActive(false);
            parts.Slate = go;
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject Model(string path, string name, Transform parent, Vector3 pos, float yaw, Vector3 phSize, ref int missing,
                                        bool local = true)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject go;
            if (model != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                go.name = name;
            }
            else
            {
                missing++;
                s_report.Add("missing: " + path + " (greybox)");
                go = new GameObject(name + "_PH");
                go.transform.SetParent(parent, false);
                var box = BuilderUtil.PlaceholderPrimitive(name, PrimitiveType.Cube, phSize, Placeholders.Greybox, go.transform);
                box.transform.localPosition = new Vector3(0f, phSize.y * 0.5f, 0f);
            }
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            _ = local;
            return go;
        }

        private static void MakeStatic(GameObject go, bool contributeGI = true)
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
            if (contributeGI) flags |= StaticEditorFlags.ContributeGI;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                var r = t.GetComponent<MeshRenderer>();
                if (r != null) r.receiveGI = ReceiveGI.LightProbes;
            }
        }

        private static Material Mat(string path, Color fallback, ref int missing)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            missing++;
            s_report.Add("missing: " + path + " (placeholder colour)");
            return BuilderUtil.PlaceholderMaterial(fallback);
        }

        /// <summary>WP09_ART specs give "tiling_m", which MaterialFactory ignores: tile the WP09 booth materials here.</summary>
        private static void FixTiling(string path, float tileMetres)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || tileMetres <= 0f) return;
            var t = 1f / tileMetres;
            if (Mathf.Abs(m.GetTextureScale("_BaseMap").x - t) < 1e-4f) return;
            m.SetTextureScale("_BaseMap", new Vector2(t, t));
            EditorUtility.SetDirty(m);
            s_report.Add("tiling: " + Path.GetFileName(path) + " = 1/" + tileMetres + " m");
        }

        private static Material SteelMaterial()
        {
            var path = CinematicPaths.BoothMaterials + "/M_BoothSteel.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            BuilderUtil.EnsureFolder(CinematicPaths.BoothMaterials);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_BoothSteel" };
            m.SetColor("_BaseColor", new Color(0.46f, 0.47f, 0.47f));
            m.SetFloat("_Metallic", 0.9f);
            m.SetFloat("_Smoothness", 0.42f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material FlameMaterial()
        {
            var path = CinematicPaths.BoothMaterials + "/M_BoothFlame.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            BuilderUtil.EnsureFolder(CinematicPaths.BoothMaterials);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_BoothFlame" };
            m.SetColor("_BaseColor", new Color(0.3f, 0.12f, 0.02f));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(4.0f, 1.9f, 0.5f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static Transform FindDeep(Transform root, string part)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.EndsWith(part, System.StringComparison.OrdinalIgnoreCase)) return t;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
            return null;
        }

        private static void SetLayerAndRenderingLayer(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
                var r = t.GetComponent<Renderer>();
                if (r != null) r.renderingLayerMask = RenderingLayerIds.BoothMask;
            }
        }
    }
}
