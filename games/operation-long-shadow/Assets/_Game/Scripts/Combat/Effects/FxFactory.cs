using LongShadow.Combat.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Builds the combat particle effects in code (TECH_PLAN §7.5 impacts per SurfaceType, muzzle smoke, explosions, smoke,
    /// sparks, fire) and the muzzle-flash meshes. The editor FX builder calls the same functions with saved materials and
    /// stores the results as prefabs in <c>Prefabs/Weapons/FX</c>; at runtime the functions are the fallback when
    /// <c>LIB_CombatFx</c> lacks an entry. Burst counts live in each system's first emission burst (min/max), so they can be
    /// tuned in the prefab; <see cref="ParticleBurst"/> reads them and calls <c>Emit</c> (systems never Play).
    /// Every system simulates in world space with playOnAwake off.
    /// </summary>
    public static class FxFactory
    {
        // Surface tints (WP10 notes: sand C9A87A, plaster D8D0C0, stone A99A86).
        private static readonly Color SandTint = new Color32(0xC9, 0xA8, 0x7A, 0xE6);
        private static readonly Color PlasterTint = new Color32(0xD8, 0xD0, 0xC0, 0xF0);
        private static readonly Color StoneTint = new Color32(0xA9, 0x9A, 0x86, 0xE6);

        /// <summary>Impact burst for a profile.</summary>
        public static GameObject BuildImpact(ImpactProfile p, FxMaterialSet m)
        {
            var root = new GameObject("PF_FX_Impact_" + p);
            var t = root.transform;
            switch (p)
            {
                case ImpactProfile.Dust:
                    Puff(t, "Puff", m.Dust, m.DustTiles, SandTint, 0.22f, 0.5f, 2, 4, 0.7f, 1.3f, 0.4f, 1.6f, true);
                    Chips(t, "Grit", m.Soft, new Color(0.36f, 0.29f, 0.2f, 1f), 0.008f, 0.022f, 8, 16, 2f, 5f, 0.35f, 0.7f, 30f);
                    break;
                case ImpactProfile.Stone:
                    Puff(t, "Puff", m.Dust, m.DustTiles, StoneTint, 0.18f, 0.42f, 2, 3, 0.6f, 1.1f, 0.5f, 1.8f, true);
                    Chips(t, "Chips", m.Soft, new Color(0.45f, 0.42f, 0.38f, 1f), 0.008f, 0.02f, 6, 12, 3f, 7f, 0.4f, 0.9f, 35f);
                    Sparks(t, "Glint", m, 0, 2, 0.06f, 0.14f, 3f, 6f, new Color(2.2f, 1.6f, 1.0f, 1f));
                    break;
                case ImpactProfile.Plaster:
                    Puff(t, "Puff", m.Dust, m.DustTiles, PlasterTint, 0.26f, 0.6f, 2, 4, 0.8f, 1.5f, 0.6f, 2.0f, true);
                    Chips(t, "Chips", m.Soft, new Color(0.86f, 0.83f, 0.77f, 1f), 0.008f, 0.024f, 8, 14, 2.5f, 6f, 0.4f, 0.9f, 35f);
                    break;
                case ImpactProfile.Wood:
                    Puff(t, "Puff", m.Dust, m.DustTiles, new Color(0.55f, 0.44f, 0.32f, 0.85f), 0.12f, 0.28f, 1, 2, 0.5f, 0.9f, 0.3f, 1.2f, true);
                    var splinters = Chips(t, "Splinters", m.Soft, new Color(0.36f, 0.25f, 0.15f, 1f), 0.01f, 0.025f, 6, 10, 2f, 4.5f, 0.5f, 1.1f, 30f);
                    Stretch(splinters, 0.02f, 3.5f);
                    break;
                case ImpactProfile.Metal:
                    Sparks(t, "Sparks", m, 10, 20, 0.15f, 0.42f, 4f, 10f, new Color(4f, 2.2f, 0.9f, 1f));
                    Puff(t, "Smoke", m.Smoke, m.SmokeTiles, new Color(0.5f, 0.5f, 0.5f, 0.45f), 0.1f, 0.22f, 1, 2, 0.6f, 1.1f, 0.2f, 0.6f, false);
                    break;
                case ImpactProfile.Fabric:
                    Puff(t, "Puff", m.Dust, m.DustTiles, new Color(0.78f, 0.74f, 0.66f, 0.7f), 0.1f, 0.22f, 1, 2, 0.4f, 0.8f, 0.3f, 1.0f, true);
                    Chips(t, "Fibres", m.Soft, new Color(0.7f, 0.66f, 0.58f, 1f), 0.005f, 0.012f, 4, 8, 0.8f, 2f, 0.6f, 1.2f, 40f);
                    break;
                case ImpactProfile.Glass:
                    var shards = Chips(t, "Shards", m.SoftAdditive, new Color(1.4f, 1.5f, 1.6f, 1f), 0.006f, 0.018f, 8, 14, 1.5f, 4f, 0.5f, 1.0f, 40f);
                    shards.GetComponent<ParticleSystemRenderer>().sharedMaterial = m.SoftAdditive;
                    Puff(t, "Dust", m.Dust, m.DustTiles, new Color(0.85f, 0.88f, 0.9f, 0.4f), 0.08f, 0.18f, 1, 1, 0.4f, 0.7f, 0.2f, 0.8f, true);
                    break;
                case ImpactProfile.Flesh:
                    Puff(t, "Mist", m.Smoke, m.SmokeTiles, new Color(0.33f, 0.02f, 0.02f, 0.85f), 0.12f, 0.3f, 2, 4, 0.22f, 0.45f, 0.8f, 2.2f, false);
                    Chips(t, "Droplets", m.Soft, new Color(0.28f, 0.01f, 0.01f, 1f), 0.008f, 0.02f, 6, 12, 1f, 3.2f, 0.3f, 0.6f, 35f);
                    break;
                case ImpactProfile.Fruit:
                    Chips(t, "Juice", m.Soft, new Color(0.85f, 0.32f, 0.08f, 1f), 0.01f, 0.026f, 10, 18, 1.2f, 3.5f, 0.35f, 0.7f, 45f);
                    Puff(t, "Pulp", m.Smoke, m.SmokeTiles, new Color(0.75f, 0.2f, 0.12f, 0.6f), 0.08f, 0.16f, 1, 2, 0.25f, 0.45f, 0.5f, 1.2f, false);
                    break;
            }
            return root;
        }

        /// <summary>Small grey puff at the muzzle after a shot.</summary>
        public static GameObject BuildMuzzleSmoke(FxMaterialSet m)
        {
            var root = new GameObject("PF_FX_MuzzleSmoke");
            var ps = Puff(root.transform, "Smoke", m.Smoke, m.SmokeTiles, new Color(0.78f, 0.76f, 0.72f, 0.28f), 0.05f, 0.11f, 1, 2,
                0.6f, 1.1f, 0.25f, 0.8f, false);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 4.5f));
            return root;
        }

        /// <summary>Explosion burst: fireball, smoke, debris, sparks (frag, rocket or vehicle fireball sizes).</summary>
        public static GameObject BuildExplosion(string fxId, FxMaterialSet m)
        {
            var big = fxId == FxIds.ExplosionFireball;
            var rocket = fxId == FxIds.ExplosionRocket;
            var root = new GameObject("PF_FX_" + fxId.Replace('.', '_'));
            var t = root.transform;
            var up = Quaternion.Euler(-90f, 0f, 0f);

            var fire = System(t, "Fireball", m.Fire, ParticleSystemRenderMode.Billboard);
            fire.transform.localRotation = up;
            var main = fire.main;
            main.startLifetime = big ? new ParticleSystem.MinMaxCurve(0.9f, 1.5f) : new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            main.startSize = big ? new ParticleSystem.MinMaxCurve(3.2f, 5f) : rocket ? new ParticleSystem.MinMaxCurve(2f, 3.2f) : new ParticleSystem.MinMaxCurve(1.3f, 2.3f);
            main.startSpeed = big ? new ParticleSystem.MinMaxCurve(1f, 3f) : new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startColor = new Color(3f, 2.1f, 1.3f, 1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = big ? -0.15f : 0f;
            Shape(fire, ParticleSystemShapeType.Hemisphere, 0f, big ? 1.2f : 0.35f);
            Burst(fire, big ? 6 : 3, big ? 10 : 5);
            Flipbook(fire, m.FireTiles, false);
            Fade(fire, 1f, 0.15f);
            Grow(fire, 0.6f, 1.35f);

            var smoke = System(t, "Smoke", m.Smoke, ParticleSystemRenderMode.Billboard);
            smoke.transform.localRotation = up;
            main = smoke.main;
            main.startLifetime = big ? new ParticleSystem.MinMaxCurve(5f, 8f) : new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = big ? new ParticleSystem.MinMaxCurve(3f, 6f) : rocket ? new ParticleSystem.MinMaxCurve(2f, 4f) : new ParticleSystem.MinMaxCurve(1.5f, 3.4f);
            main.startSpeed = big ? new ParticleSystem.MinMaxCurve(1f, 3.5f) : new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startColor = big ? new Color(0.11f, 0.1f, 0.09f, 0.9f) : new Color(0.44f, 0.39f, 0.32f, 0.85f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = big ? -0.06f : -0.02f;
            main.maxParticles = 80;
            Shape(smoke, ParticleSystemShapeType.Hemisphere, 0f, big ? 1.5f : 0.6f);
            Burst(smoke, big ? 16 : 10, big ? 24 : 16);
            Flipbook(smoke, m.SmokeTiles, false);
            Fade(smoke, 1f, 0.25f);
            Grow(smoke, 0.6f, 2.1f);
            Damp(smoke, 0.45f);
            Drift(smoke, 0.5f);

            var debris = System(t, "Debris", m.Soft, ParticleSystemRenderMode.Billboard);
            debris.transform.localRotation = up;
            main = debris.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startColor = new Color(0.2f, 0.17f, 0.13f, 1f);
            main.gravityModifier = 1f;
            main.maxParticles = 120;
            Shape(debris, ParticleSystemShapeType.Cone, 65f, 0.3f);
            Burst(debris, big ? 30 : 20, big ? 45 : 34);

            Sparks(t, "Sparks", m, big ? 25 : 14, big ? 40 : 24, 0.3f, 0.9f, 6f, 15f, new Color(4f, 2.2f, 0.8f, 1f)).transform.localRotation = up;
            return root;
        }

        /// <summary>Looping coloured smoke cloud (squad smoke grenades); tint via start colour at spawn.</summary>
        public static GameObject BuildSmokeCloud(FxMaterialSet m)
        {
            var root = new GameObject("PF_FX_SmokeCloud");
            var ps = System(root.transform, "Smoke", m.Smoke, ParticleSystemRenderMode.Billboard);
            ps.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
            main.startColor = new Color(0.86f, 0.86f, 0.84f, 0.85f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.004f;
            main.maxParticles = 160;
            var em = ps.emission;
            em.rateOverTime = 14f;
            Shape(ps, ParticleSystemShapeType.Hemisphere, 0f, 0.6f);
            Flipbook(ps, m.SmokeTiles, false);
            Fade(ps, 1f, 0.15f);
            Grow(ps, 0.5f, 2.2f);
            Damp(ps, 0.3f);
            Drift(ps, 0.7f);
            return root;
        }

        /// <summary>Looping rocket smoke trail (rate over distance) with a hot motor glow.</summary>
        public static GameObject BuildSmokeTrail(FxMaterialSet m)
        {
            var root = new GameObject("PF_FX_SmokeTrail");
            var ps = System(root.transform, "Trail", m.Smoke, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
            main.startSpeed = 0f;
            main.startColor = new Color(0.84f, 0.82f, 0.78f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.01f;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverDistance = 8f;
            Shape(ps, ParticleSystemShapeType.Sphere, 0f, 0.05f);
            Flipbook(ps, m.SmokeTiles, false);
            Fade(ps, 1f, 0.2f);
            Grow(ps, 1f, 4.5f);
            Drift(ps, 0.6f);

            var glow = System(root.transform, "Motor", m.SoftAdditive, ParticleSystemRenderMode.Billboard);
            var gm = glow.main;
            gm.loop = true;
            gm.simulationSpace = ParticleSystemSimulationSpace.Local;
            gm.startLifetime = 0.06f;
            gm.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            gm.startSpeed = 0f;
            gm.startColor = new Color(4f, 2.4f, 1f, 1f);
            var gem = glow.emission;
            gem.rateOverTime = 40f;
            Shape(glow, ParticleSystemShapeType.Sphere, 0f, 0.01f);
            return root;
        }

        /// <summary>Spark burst (destructible hits and deaths).</summary>
        public static GameObject BuildSparkBurst(FxMaterialSet m)
        {
            var root = new GameObject("PF_FX_SparkBurst");
            Sparks(root.transform, "Sparks", m, 18, 34, 0.25f, 0.8f, 3f, 9f, new Color(4f, 2.6f, 1.1f, 1f));
            var flash = System(root.transform, "Flash", m.SoftAdditive, ParticleSystemRenderMode.Billboard);
            var main = flash.main;
            main.startLifetime = 0.08f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = 0f;
            main.startColor = new Color(3f, 2.6f, 2f, 1f);
            Burst(flash, 1, 1);
            Fade(flash, 1f, 0f);
            return root;
        }

        /// <summary>Looping fire with dark smoke (vehicles after their fireball).</summary>
        public static GameObject BuildFire(FxMaterialSet m)
        {
            var root = new GameObject("PF_FX_Fire");
            var up = Quaternion.Euler(-90f, 0f, 0f);
            var fire = System(root.transform, "Flames", m.Fire, ParticleSystemRenderMode.Billboard);
            fire.transform.localRotation = up;
            var main = fire.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startColor = new Color(2.6f, 1.7f, 1f, 1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            main.maxParticles = 60;
            var em = fire.emission;
            em.rateOverTime = 14f;
            Shape(fire, ParticleSystemShapeType.Cone, 12f, 0.6f);
            Flipbook(fire, m.FireTiles, false);
            Fade(fire, 1f, 0f);
            Grow(fire, 1f, 0.6f);

            var smoke = System(root.transform, "Smoke", m.Smoke, ParticleSystemRenderMode.Billboard);
            smoke.transform.localRotation = up;
            smoke.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            main = smoke.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startColor = new Color(0.08f, 0.075f, 0.07f, 0.75f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 50;
            em = smoke.emission;
            em.rateOverTime = 5f;
            Shape(smoke, ParticleSystemShapeType.Cone, 10f, 0.5f);
            Flipbook(smoke, m.SmokeTiles, false);
            Fade(smoke, 1f, 0.2f);
            Grow(smoke, 0.6f, 2.4f);
            Drift(smoke, 0.8f);
            return root;
        }

        /// <summary>
        /// Four muzzle-flash meshes (one per atlas column): two crossed side quads along +Z with the side-flame cell (row 0,
        /// the top half, flame along +U from the muzzle at U = 0) and one front quad with the star cell (row 1). Unit size:
        /// side flame 1 long × 0.5 wide; scale per weapon at runtime.
        /// </summary>
        public static Mesh[] BuildMuzzleMeshes()
        {
            var meshes = new Mesh[4];
            for (var c = 0; c < 4; c++)
            {
                var u0 = c * 0.25f;
                var u1 = u0 + 0.25f;
                var verts = new Vector3[12];
                var uvs = new Vector2[12];
                var tris = new int[18];
                // Side quad in the YZ plane.
                verts[0] = new Vector3(0f, -0.25f, 0f); verts[1] = new Vector3(0f, 0.25f, 0f);
                verts[2] = new Vector3(0f, 0.25f, 1f); verts[3] = new Vector3(0f, -0.25f, 1f);
                // Side quad in the XZ plane.
                verts[4] = new Vector3(-0.25f, 0f, 0f); verts[5] = new Vector3(0.25f, 0f, 0f);
                verts[6] = new Vector3(0.25f, 0f, 1f); verts[7] = new Vector3(-0.25f, 0f, 1f);
                for (var q = 0; q < 2; q++)
                {
                    var b = q * 4;
                    uvs[b + 0] = new Vector2(u0, 0.5f); uvs[b + 1] = new Vector2(u0, 1f);
                    uvs[b + 2] = new Vector2(u1, 1f); uvs[b + 3] = new Vector2(u1, 0.5f);
                }
                // Front star facing +Z.
                verts[8] = new Vector3(-0.3f, -0.3f, 0.02f); verts[9] = new Vector3(-0.3f, 0.3f, 0.02f);
                verts[10] = new Vector3(0.3f, 0.3f, 0.02f); verts[11] = new Vector3(0.3f, -0.3f, 0.02f);
                uvs[8] = new Vector2(u0, 0f); uvs[9] = new Vector2(u0, 0.5f); uvs[10] = new Vector2(u1, 0.5f); uvs[11] = new Vector2(u1, 0f);
                for (var q = 0; q < 3; q++)
                {
                    var b = q * 4;
                    var i = q * 6;
                    tris[i + 0] = b; tris[i + 1] = b + 1; tris[i + 2] = b + 2;
                    tris[i + 3] = b; tris[i + 4] = b + 2; tris[i + 5] = b + 3;
                }
                var mesh = new Mesh { name = "MSH_FX_MuzzleFlash_" + c };
                mesh.vertices = verts;
                mesh.uv = uvs;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                meshes[c] = mesh;
            }
            return meshes;
        }

        /// <summary>A simple 8-sided cartridge case (fallback when SM_WPN_Casings has not landed).</summary>
        public static Mesh BuildCasingMesh(float length, float radius)
        {
            const int sides = 8;
            var verts = new Vector3[sides * 2 + 2];
            var tris = new int[sides * 12];
            for (var i = 0; i < sides; i++)
            {
                var a = i * Mathf.PI * 2f / sides;
                var x = Mathf.Cos(a) * radius;
                var y = Mathf.Sin(a) * radius;
                verts[i] = new Vector3(x, y, -length * 0.5f);
                verts[i + sides] = new Vector3(x * 0.92f, y * 0.92f, length * 0.5f);
            }
            verts[sides * 2] = new Vector3(0f, 0f, -length * 0.5f);
            verts[sides * 2 + 1] = new Vector3(0f, 0f, length * 0.5f);
            var k = 0;
            for (var i = 0; i < sides; i++)
            {
                var n = (i + 1) % sides;
                tris[k++] = i; tris[k++] = i + sides; tris[k++] = n;
                tris[k++] = n; tris[k++] = i + sides; tris[k++] = n + sides;
                tris[k++] = sides * 2; tris[k++] = n; tris[k++] = i;
                tris[k++] = sides * 2 + 1; tris[k++] = i + sides; tris[k++] = n + sides;
            }
            var mesh = new Mesh { name = "MSH_FX_Casing" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- building blocks -------------------------------------------------------------------------------------------

        /// <summary>Adds a stopped, world-space particle system child with a renderer material.</summary>
        public static ParticleSystem System(Transform parent, string name, Material material, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new ParticleSystem.Burst[0]);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = mode;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }

        private static ParticleSystem Puff(Transform parent, string name, Material mat, Vector2Int tiles, Color color, float sizeMin,
                                           float sizeMax, int countMin, int countMax, float lifeMin, float lifeMax, float speedMin,
                                           float speedMax, bool bottomPivot)
        {
            var ps = System(parent, name, mat, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startColor = color;
            main.startRotation = bottomPivot ? new ParticleSystem.MinMaxCurve(-0.15f, 0.15f) : new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.02f;
            Shape(ps, ParticleSystemShapeType.Cone, 22f, 0.02f);
            Burst(ps, countMin, countMax);
            Flipbook(ps, tiles, false);
            Fade(ps, 1f, 0.3f);
            Grow(ps, 0.7f, 1.8f);
            Damp(ps, 0.35f);
            if (bottomPivot && tiles.x > 1) ps.GetComponent<ParticleSystemRenderer>().pivot = new Vector3(0f, 0.5f, 0f);
            return ps;
        }

        private static ParticleSystem Chips(Transform parent, string name, Material mat, Color color, float sizeMin, float sizeMax,
                                            int countMin, int countMax, float speedMin, float speedMax, float lifeMin, float lifeMax,
                                            float coneAngle)
        {
            var ps = System(parent, name, mat, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startColor = color;
            main.gravityModifier = 1f;
            Shape(ps, ParticleSystemShapeType.Cone, coneAngle, 0.01f);
            Burst(ps, countMin, countMax);
            Fade(ps, 1f, 0.6f);
            return ps;
        }

        private static ParticleSystem Sparks(Transform parent, string name, FxMaterialSet m, int countMin, int countMax, float lifeMin,
                                             float lifeMax, float speedMin, float speedMax, Color hdr)
        {
            var ps = System(parent, name, m.Sparks, ParticleSystemRenderMode.Stretch);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startColor = hdr;
            main.gravityModifier = 0.8f;
            Shape(ps, ParticleSystemShapeType.Cone, 55f, 0.01f);
            if (countMax > 0) Burst(ps, countMin, countMax);
            Fade(ps, 1f, 0f);
            Stretch(ps, 0.025f, 1.2f);
            if (m.SparkTiles.x * m.SparkTiles.y >= 16)
            {
                // Cells 0-7 of T_VFX_Sparks_4x4_E are horizontal streaks: one random streak per particle.
                var tsa = ps.textureSheetAnimation;
                tsa.enabled = true;
                tsa.numTilesX = m.SparkTiles.x;
                tsa.numTilesY = m.SparkTiles.y;
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.49f);
            }
            return ps;
        }

        private static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float angle, float radius)
        {
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = type;
            sh.angle = angle;
            sh.radius = Mathf.Max(0.0001f, radius);
        }

        private static void Burst(ParticleSystem ps, int min, int max)
        {
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(0, min), (short)Mathf.Max(min, max)) });
        }

        private static void Flipbook(ParticleSystem ps, Vector2Int tiles, bool randomFrame)
        {
            if (tiles.x * tiles.y <= 1) return;
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.numTilesX = tiles.x;
            tsa.numTilesY = tiles.y;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.frameOverTime = randomFrame ? new ParticleSystem.MinMaxCurve(0f, 1f) : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            tsa.cycleCount = 1;
        }

        private static void Fade(ParticleSystem ps, float startAlpha, float holdUntil)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, Mathf.Clamp01(holdUntil)), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        private static void Grow(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, from, 0f, (to - from) * 2f), new Keyframe(1f, to, 0f, 0f)));
        }

        private static void Damp(ParticleSystem ps, float dampen)
        {
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 0.05f;
            lv.dampen = dampen;
        }

        private static void Drift(ParticleSystem ps, float eastSpeed)
        {
            // GDD §9.7: wind from the west at 3-5 m/s; smoke and dust drift east (+X).
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(eastSpeed * 0.6f, eastSpeed);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        }

        private static void Stretch(ParticleSystem ps, float velocityScale, float lengthScale)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = velocityScale;
            r.lengthScale = lengthScale;
        }
    }
}
