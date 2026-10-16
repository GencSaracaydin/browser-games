using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Look.Atmosphere;
using LongShadow.World.Look.Common;
using LongShadow.World.Look.FX;
using LongShadow.World.Look.Practicals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Builds the Look prefabs that level builders (WP10 Level) and <see cref="PracticalBinder"/> place:
    /// sodium lamp head, aviation lights, floodlights F1–F4 (with WP03's Destructible added by type name when it exists),
    /// emergency lights and tungsten fills, the parachute flare, the burning-technical fire (event-lit and lit variants),
    /// light shaft, projector beam, dust motes, rotor downwash and the rotor shadow strobe. Missing art (PH_SECURITY_LIGHT,
    /// helicopter rotor, flipbooks) degrades to greybox primitives; re-run after it lands (GUIDs are kept).
    /// </summary>
    public static class FXPrefabBuilder
    {
        /// <summary>WP03's destructible (added by type name: Look cannot reference Combat).</summary>
        public const string DestructibleType = "LongShadow.Combat.Targets.Destructible, LongShadow.Combat";

        [MenuItem("LongShadow/Look/Build FX and Practical Prefabs", priority = 113)]
        public static void BuildMenu()
        {
            var log = new StringBuilder();
            BuildAll(log);
            Debug.Log("[LS:Look] " + log);
        }

        /// <summary>Builds every prefab.</summary>
        public static void BuildAll(StringBuilder log)
        {
            BuilderUtil.EnsureFolder(LookPaths.FxPrefabFolder);
            BuilderUtil.EnsureFolder(LookPaths.LampPrefabFolder);
            BuildSodiumLampHead();
            BuildSodiumLampSprite();
            BuildAviationLights();
            for (var i = 0; i < LookIds.Floods.Length; i++) BuildFloodlight(LookIds.Floods[i], i, log);
            BuildEmergencyLight();
            BuildTungstenFill();
            BuildParachuteFlare();
            BuildFire(false);
            BuildFire(true);
            BuildLightShaft(false);
            BuildLightShaft(true);
            BuildDustMotes();
            BuildRotorDownwash();
            BuildRotorShadow(log);
            AssetDatabase.SaveAssets();
            log.Append("FX/practical prefabs built. ");
        }

        /// <summary>Path of a flood variant.</summary>
        public static string FloodPath(string id) => LookPaths.LampPrefabFolder + "/PF_Floodlight_" + id.Replace("B6.", "") + ".prefab";

        /// <summary>Path of the fire variant.</summary>
        public static string FirePath(bool lit) => lit ? LookPaths.FxPrefabFolder + "/PF_FX_FireTechnical_Lit.prefab" : LookPaths.FireTechnicalPrefab;

        private static Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        // ------------------------------------------------------------------ lamps
        private static void BuildSodiumLampHead()
        {
            var root = new GameObject("PF_SodiumLamp_Head");
            // Cobra-head housing (greybox until PROC_LAMP_POSTS heads exist; Level may hide it under its own fixture).
            LookBuildUtil.Primitive("Housing", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.42f, 0.06f, 0.42f), Mat(LookPaths.MatLampHousing));
            var glass = LookBuildUtil.Primitive("Glass", PrimitiveType.Sphere, root.transform, new Vector3(0f, -0.02f, 0f), new Vector3(0.34f, 0.12f, 0.34f), Mat(LookPaths.MatLampGlass), ShadowCastingMode.Off);
            var glow = LookBuildUtil.Quad("Glow", root.transform, new Vector3(0f, -0.08f, 0f), 1f, Mat(LookPaths.MatLampGlow));
            var light = LookBuildUtil.Light("Light", root.transform, LightType.Point, Color.white, 8f, 18f);
            light.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            var lamp = root.AddComponent<SodiumLamp>();
            lamp.EditorConfigure(null, light, new Renderer[] { glass.GetComponent<Renderer>(), glow }, new Color(6f, 6f, 6f, 1f), 8f,
                PracticalSwitch.AfterStrike, FlickerMode.Steady, budgeted: true);
            SetSwitchFade(lamp, 0f);
            BuilderUtil.SavePrefab(root, LookPaths.SodiumLampHeadPrefab);
        }

        /// <summary>Distant lamp (S9_TownFar "sodium-lamp sprites", GDD §6.7/§9.5): glow sprite only, no light, same ripple.</summary>
        private static void BuildSodiumLampSprite()
        {
            var root = new GameObject("PF_SodiumLamp_Sprite");
            var glow = LookBuildUtil.Quad("Glow", root.transform, Vector3.zero, 1.6f, Mat(LookPaths.MatLampGlow));
            var lamp = root.AddComponent<SodiumLamp>();
            lamp.EditorConfigure(null, null, new Renderer[] { glow }, new Color(6f, 6f, 6f, 1f), 0f,
                PracticalSwitch.AfterStrike, FlickerMode.Steady, budgeted: false);
            SetSwitchFade(lamp, 0f);
            BuilderUtil.SavePrefab(root, LookPaths.SodiumLampSpritePrefab);
        }

        private static void BuildAviationLights()
        {
            var root = new GameObject("PF_AviationLights");
            // GDD §6.6 BLD-12: 90 m mast, 5 lights; heights top → bottom.
            float[] heights = { 90f, 72f, 54f, 36f, 18f };
            var lamps = new Renderer[heights.Length];
            for (var i = 0; i < heights.Length; i++)
            {
                var q = LookBuildUtil.Quad("Lens_" + (i + 1), root.transform, new Vector3(0f, heights[i], 0f), 1f, Mat(LookPaths.MatLampGlow));
                lamps[i] = q;
            }
            var av = root.AddComponent<AviationLights>();
            av.EditorSetup(lamps);
            BuilderUtil.SavePrefab(root, LookPaths.AviationLightsPrefab);
        }

        private static void BuildFloodlight(string id, int index, StringBuilder log)
        {
            var root = new GameObject("PF_Floodlight_" + id.Replace("B6.", ""));
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            var model = AssetManifest.Find<GameObject>(LookPaths.PropSecurityLight, "SM_");
            if (LookBuildUtil.InstantiateModel(model, head) == null)
                LookBuildUtil.Primitive("PH_FloodHousing", PrimitiveType.Cube, head, new Vector3(0f, 0f, -0.15f), new Vector3(0.7f, 0.5f, 0.3f), Mat(LookPaths.MatLampHousing));
            var lens = LookBuildUtil.Quad("Lens", head, new Vector3(0f, 0f, 0.02f), 0.55f, Mat(LookPaths.MatFloodLens));
            lens.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // quad front faces -Z: turn it to face the beam
            var glow = LookBuildUtil.Quad("Glow", head, new Vector3(0f, 0f, 0.15f), 2.2f, Mat(LookPaths.MatLampGlow));
            var spot = LookBuildUtil.Light("Spot", head, LightType.Spot, LookBuildUtil.Kelvin(4200f), 900f, 75f, LightShadows.None, 50f);
            spot.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            var col = head.gameObject.AddComponent<BoxCollider>(); // WP03 hit radius 0.4 m around the head
            col.size = new Vector3(0.7f, 0.5f, 0.45f);
            var zoneGo = new GameObject("IlluminationZone");
            zoneGo.transform.SetParent(head, false);
            var zone = zoneGo.AddComponent<IlluminationZone>();
            zone.Configure(IlluminationShape.Cone, 70f, 50f, id);
            zone.SetActive(false); // dark until B6.FloodsOn: no AI sight bonus from an unlit flood
            var flood = root.AddComponent<FloodLight>();
            flood.EditorSetup(id, spot, new Renderer[] { lens, glow }, zone, 0.3f * index);

            var d = LookBuildUtil.AddByTypeName(head.gameObject, DestructibleType, so =>
            {
                LookBuildUtil.Set(so, "_id", p => p.stringValue = id);
                LookBuildUtil.Set(so, "_kind", p => p.intValue = 1); // DestructibleKind.Flood
                LookBuildUtil.Set(so, "_hitRadius", p => p.floatValue = 0.4f);
                LookBuildUtil.Set(so, "_core", p => p.objectReferenceValue = head);
                LookBuildUtil.Set(so, "_dieOffSeconds", p => p.floatValue = 0.6f);
                LookBuildUtil.Set(so, "_lights", p =>
                {
                    p.arraySize = 1;
                    p.GetArrayElementAtIndex(0).objectReferenceValue = spot;
                });
                LookBuildUtil.Set(so, "_emissive", p =>
                {
                    p.arraySize = 2;
                    p.GetArrayElementAtIndex(0).objectReferenceValue = lens;
                    p.GetArrayElementAtIndex(1).objectReferenceValue = glow;
                });
            });
            if (d == null && index == 0) log.Append("WP03 Destructible not compiled yet: floods are indestructible until rebuilt. ");
            BuilderUtil.SavePrefab(root, FloodPath(id));
        }

        private static void BuildEmergencyLight()
        {
            var root = new GameObject("PF_EmergencyLight");
            var bulb = LookBuildUtil.Primitive("Lens", PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one * 0.14f, Mat(LookPaths.MatEmergencyLens), ShadowCastingMode.Off);
            var light = LookBuildUtil.Light("Light", root.transform, LightType.Point, new Color(1f, 0.12f, 0.06f), 3f, 9f);
            var p = root.AddComponent<PracticalLight>();
            // GDD §7.9: two red realtime lights at the stair feet after the transmitter dies (with the emergency lightmap set).
            p.EditorConfigure(null, light, new[] { bulb.GetComponent<Renderer>() }, new Color(8f, 0.6f, 0.3f, 1f), 3f,
                PracticalSwitch.MissionEvent, FlickerMode.Steady, budgeted: false, onEvent: LookIds.TransmitterDead);
            BuilderUtil.SavePrefab(root, LookPaths.EmergencyLightPrefab);
        }

        private static void BuildTungstenFill()
        {
            var root = new GameObject("PF_TungstenFill");
            var bulb = LookBuildUtil.Primitive("Bulb", PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one * 0.1f, Mat(LookPaths.MatTungstenBulb), ShadowCastingMode.Off);
            var light = LookBuildUtil.Light("Light", root.transform, LightType.Point, LookBuildUtil.Kelvin(2700f), 2.5f, 7f);
            var p = root.AddComponent<PracticalLight>();
            // GDD §7.7 Relay Hall: 2 realtime unshadowed fills over the teletype rows (fixed lights, not budgeted).
            p.EditorConfigure(null, light, new[] { bulb.GetComponent<Renderer>() }, new Color(5f, 3.4f, 1.6f, 1f), 2.5f,
                PracticalSwitch.AlwaysOn, FlickerMode.Steady, budgeted: false);
            BuilderUtil.SavePrefab(root, LookPaths.TungstenFillPrefab);
        }

        // ------------------------------------------------------------------ flare and fire
        private static void BuildParachuteFlare()
        {
            var root = new GameObject("PF_FX_ParachuteFlare");
            var swing = new GameObject("Swing").transform;
            swing.SetParent(root.transform, false);
            LookBuildUtil.Primitive("Chute", PrimitiveType.Sphere, swing, new Vector3(0f, 1.2f, 0f), new Vector3(1.4f, 0.35f, 1.4f), Mat(LookPaths.MatLampHousing));
            var canister = LookBuildUtil.Primitive("Canister", PrimitiveType.Cylinder, swing, new Vector3(0f, -0.4f, 0f), new Vector3(0.07f, 0.14f, 0.07f), Mat(LookPaths.MatFlareEmissive), ShadowCastingMode.Off);
            var glow = LookBuildUtil.Quad("Glow", swing, new Vector3(0f, -0.55f, 0f), 3f, Mat(LookPaths.MatLampGlow));
            var spot = LookBuildUtil.Light("Spot", swing, LightType.Spot, LookBuildUtil.Kelvin(3200f), 2400f, 110f, LightShadows.Hard, 120f);
            spot.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            spot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            spot.shadowResolution = LightShadowResolution.Medium;
            var fill = LookBuildUtil.Light("Fill", swing, LightType.Point, LookBuildUtil.Kelvin(3200f), 450f, 60f);
            fill.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            var smoke = LookBuildUtil.Particles("Smoke", swing, Mat(LookPaths.MatSmokeParticles), 10f, 7f, 1.2f, 0.3f, new Color(0.85f, 0.85f, 0.85f, 0.5f));
            smoke.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            LookBuildUtil.Flipbook(smoke, 8, 8);
            LookBuildUtil.Grow(smoke, 0.6f, 3.5f);
            LookBuildUtil.FadeInOut(smoke, 0.55f);
            LookBuildUtil.Wind(smoke, 0.6f);
            var zoneGo = new GameObject("IlluminationZone");
            zoneGo.transform.SetParent(root.transform, false);
            var zone = zoneGo.AddComponent<IlluminationZone>();
            zone.Configure(IlluminationShape.Cone, 100f, 120f, "B10.FL");
            zone.SetActive(false);
            var flare = root.AddComponent<ParachuteFlare>();
            flare.Configure(swing, spot, fill, new Renderer[] { canister.GetComponent<Renderer>(), glow }, smoke, zone);
            BuilderUtil.SavePrefab(root, LookPaths.ParachuteFlarePrefab);
        }

        private static void BuildFire(bool lit)
        {
            var root = new GameObject(lit ? "PF_FX_FireTechnical_Lit" : "PF_FX_FireTechnical");
            var flames = LookBuildUtil.Particles("Flames", root.transform, Mat(LookPaths.MatFireParticles), 22f, 0.9f, 1.4f, 1.2f, new Color(1f, 0.8f, 0.6f, 1f), 120);
            LookBuildUtil.Flipbook(flames, 8, 8, 28f);
            LookBuildUtil.Grow(flames, 0.7f, 1.3f);
            LookBuildUtil.FadeInOut(flames);
            var shape = flames.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.6f, 0.2f, 1.0f);
            var smoke = LookBuildUtil.Particles("Smoke", root.transform, Mat(LookPaths.MatSmokeParticles), 7f, 8f, 2.2f, 1.8f, new Color(0.12f, 0.11f, 0.1f, 0.75f), 120);
            smoke.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            LookBuildUtil.Flipbook(smoke, 8, 8);
            LookBuildUtil.Grow(smoke, 0.8f, 3.2f);
            LookBuildUtil.FadeInOut(smoke, 0.8f);
            LookBuildUtil.Wind(smoke, 1f);
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            var light = LookBuildUtil.Light("Light", lightGo.transform, LightType.Point, LookBuildUtil.Kelvin(1900f), 6f, 12f);
            var p = lightGo.AddComponent<PracticalLight>();
            p.EditorConfigure(null, light, null, Color.black, 6f, PracticalSwitch.Manual, FlickerMode.Fire, budgeted: true, priority: 1.5f);
            var fire = root.AddComponent<FireProp>();
            fire.EditorSetup(null, lit ? null : LookIds.TechnicalIgnited, lit, new[] { flames, smoke }, p);
            BuilderUtil.SavePrefab(root, FirePath(lit));
        }

        // ------------------------------------------------------------------ atmosphere
        private static void BuildLightShaft(bool projector)
        {
            var root = new GameObject(projector ? "PF_FX_ProjectorBeam" : "PF_FX_LightShaft");
            var card = LookBuildUtil.Quad("Card", root.transform, Vector3.zero, 1f, Mat(projector ? LookPaths.MatProjectorBeam : LookPaths.MatLightShaft));
            var motes = LookBuildUtil.Particles("Motes", root.transform, Mat(LookPaths.MatDustMotes), 18f, 9f, 0.02f, 0.02f, Color.white, 300);
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = projector ? new Vector3(1.2f, 1.2f, 6f) : new Vector3(1.2f, 1.2f, 8f);
            shape.position = projector ? new Vector3(0f, 0f, 3f) : new Vector3(0f, 0f, 0f);
            LookBuildUtil.FadeInOut(motes);
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.3f;
            var shaft = root.AddComponent<LightShaft>();
            shaft.EditorSetup(card, motes, projector ? 6f : 8f, projector ? 1.4f : 1.2f, !projector);
            BuilderUtil.SavePrefab(root, projector ? LookPaths.ProjectorBeamPrefab : LookPaths.LightShaftPrefab);
        }

        private static void BuildDustMotes()
        {
            var root = new GameObject("PF_FX_DustMotes");
            var ps = root.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.025f);
            main.startSpeed = 0.02f;
            main.maxParticles = 400;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(8f, 4f, 8f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.01f, 0.02f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            LookBuildUtil.FadeInOut(ps);
            var r = root.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat(LookPaths.MatDustMotes);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            root.AddComponent<DustMotes>();
            BuilderUtil.SavePrefab(root, LookPaths.DustMotesPrefab);
        }

        private static void BuildRotorDownwash()
        {
            var root = new GameObject("PF_FX_RotorDownwash");
            var dustMat = Mat(LookPaths.MatDustParticles);
            var ring = LookBuildUtil.Particles("Ring", root.transform, dustMat, 90f, 2.2f, 2.5f, 9f, new Color(0.79f, 0.66f, 0.48f, 0.7f), 400);
            var rs = ring.shape;
            rs.shapeType = ParticleSystemShapeType.Circle;
            rs.radius = 7.3f;
            rs.radiusThickness = 0.25f;
            rs.rotation = new Vector3(90f, 0f, 0f);
            LookBuildUtil.Flipbook(ring, 8, 8);
            LookBuildUtil.Grow(ring, 0.6f, 3f);
            LookBuildUtil.FadeInOut(ring, 0.8f);
            var streaks = LookBuildUtil.Particles("Streaks", root.transform, dustMat, 40f, 1.4f, 1.2f, 14f, new Color(0.79f, 0.66f, 0.48f, 0.5f), 300);
            var ss = streaks.shape;
            ss.shapeType = ParticleSystemShapeType.Circle;
            ss.radius = 6f;
            ss.radiusThickness = 0.1f;
            ss.rotation = new Vector3(90f, 0f, 0f);
            var sr = streaks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.08f;
            sr.lengthScale = 2f;
            LookBuildUtil.Flipbook(streaks, 8, 8);
            LookBuildUtil.FadeInOut(streaks, 0.6f);
            var sheet = LookBuildUtil.Particles("Sheet", root.transform, dustMat, 14f, 6f, 6f, 1.2f, new Color(0.79f, 0.66f, 0.48f, 0.45f), 120);
            var sh = sheet.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 11f;
            sh.rotation = new Vector3(90f, 0f, 0f);
            LookBuildUtil.Flipbook(sheet, 8, 8);
            LookBuildUtil.Grow(sheet, 0.7f, 2f);
            LookBuildUtil.FadeInOut(sheet, 0.5f);
            LookBuildUtil.Wind(sheet, 1f);
            var dw = root.AddComponent<RotorDownwash>();
            dw.EditorSetup(root.transform, ring, streaks, sheet);
            BuilderUtil.SavePrefab(root, LookPaths.RotorDownwashPrefab);
        }

        private static void BuildRotorShadow(StringBuilder log)
        {
            var root = new GameObject("PF_FX_RotorShadow");
            var rotor = AssetManifest.Find<GameObject>(LookPaths.VehicleHeli, "SM_HeliMainRotor");
            Renderer[] blades;
            var model = LookBuildUtil.InstantiateModel(rotor, root.transform);
            if (model != null) blades = model.GetComponentsInChildren<Renderer>(true);
            else
            {
                // Two 7.3 m blades (Bell 212 class), greybox.
                var a = LookBuildUtil.Primitive("Blade_A", PrimitiveType.Cube, root.transform, new Vector3(3.65f, 0f, 0f), new Vector3(7.3f, 0.04f, 0.53f), Mat(LookPaths.MatLampHousing), ShadowCastingMode.ShadowsOnly);
                var b = LookBuildUtil.Primitive("Blade_B", PrimitiveType.Cube, root.transform, new Vector3(-3.65f, 0f, 0f), new Vector3(7.3f, 0.04f, 0.53f), Mat(LookPaths.MatLampHousing), ShadowCastingMode.ShadowsOnly);
                blades = new Renderer[] { a.GetComponent<Renderer>(), b.GetComponent<Renderer>() };
                log.Append("Rotor shadow uses greybox blades (SM_HeliMainRotor not landed). ");
            }
            var rs = root.AddComponent<RotorShadowCaster>();
            rs.EditorSetup(blades, 324f);
            BuilderUtil.SavePrefab(root, LookPaths.RotorShadowPrefab);
        }

        private static void SetSwitchFade(PracticalLight p, float seconds)
        {
            var so = new SerializedObject(p);
            var prop = so.FindProperty("_switchFade");
            if (prop == null) return;
            prop.floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
