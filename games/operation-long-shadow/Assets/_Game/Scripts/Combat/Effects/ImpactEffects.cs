using LongShadow.Combat.Data;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Bullet impacts per <see cref="SurfaceType"/> (TECH_PLAN §7.5 ImpactService): a pooled particle burst per profile,
    /// a bullet-hole decal from the WP10 atlas for hard surfaces, blood decals on walls behind body hits, and the surface
    /// impact cue (rate-limited and distance-culled so AI fire cannot flood the 24-voice budget).
    /// </summary>
    public sealed class ImpactEffects
    {
        /// <summary>Impact sounds allowed per second.</summary>
        public const float SoundsPerSecond = 24f;
        /// <summary>Impacts farther than this from the listener are silent.</summary>
        public const float SoundRange = 70f;
        /// <summary>Bullet-hole life before fading.</summary>
        public const float HoleLife = 30f;

        private readonly ParticleBurst[] _bursts = new ParticleBurst[9];
        private readonly DecalPool _decals;
        private readonly DecalAtlas _holes;
        private readonly DecalAtlas _blood;
        private readonly DecalAtlas _scorch;
        private float _soundTokens = SoundsPerSecond;

        /// <summary>Builds the bursts (library prefab or FxFactory fallback) under <paramref name="root"/>.</summary>
        public ImpactEffects(Transform root, CombatFxLibrary lib, FxMaterialSet mats, DecalPool decals)
        {
            _decals = decals;
            for (var i = 0; i < _bursts.Length; i++)
            {
                var p = (ImpactProfile)i;
                var go = CombatFx.InstantiateOrBuild(lib, FxIds.Impact(p), root, () => FxFactory.BuildImpact(p, mats));
                _bursts[i] = new ParticleBurst(go);
            }
            _holes = Atlas(lib, FxIds.DecalBulletHoles, true);
            _blood = Atlas(lib, FxIds.DecalBlood, false);
            _scorch = Atlas(lib, FxIds.DecalScorch, false);
        }

        /// <summary>Scorch atlas (explosions).</summary>
        public DecalAtlas ScorchAtlas => _scorch;

        /// <summary>Plays an impact.</summary>
        /// <param name="surface">Surface of the hit collider.</param>
        /// <param name="point">Hit point.</param>
        /// <param name="normal">Surface normal.</param>
        /// <param name="collider">Hit collider (decals follow vehicles and rigidbodies).</param>
        /// <param name="decal">Spawn a bullet hole on hard surfaces.</param>
        /// <param name="audio">Audio service (may be null).</param>
        /// <param name="listener">Listener position for sound culling.</param>
        /// <param name="volume">Cue volume.</param>
        public void Play(SurfaceType surface, Vector3 point, Vector3 normal, Collider collider, bool decal, IAudioService audio,
                         Vector3 listener, float volume = 1f)
        {
            var n = normal.sqrMagnitude > 1e-6f ? normal : Vector3.up;
            var b = _bursts[(int)FxIds.ProfileOf(surface)];
            b?.Emit(point + n * 0.01f, Quaternion.LookRotation(n));
            if (decal) Hole(surface, point, n, collider);
            if (audio != null && _soundTokens >= 1f && (point - listener).sqrMagnitude < SoundRange * SoundRange)
            {
                _soundTokens -= 1f;
                audio.Play(AudioCues.Impact(surface), point, volume);
            }
        }

        /// <summary>Bullet-hole decal for a hard surface (no-op for sand, flesh, fabric, glass, fruit).</summary>
        public void Hole(SurfaceType surface, Vector3 point, Vector3 normal, Collider collider)
        {
            var group = FxIds.BulletHoleGroup(surface);
            if (group == null || _holes == null || !_holes.IsValid || _decals == null) return;
            if (!_holes.TryPick(group, out var rect, out var size)) return;
            var s = Random.Range(0.85f, 1.15f);
            _decals.Spawn(_holes.Material, rect, size * s, point, normal, 0.08f, HoleLife, Follow(collider));
        }

        private static readonly string[] s_wallBlood = { "spatter", "droplets", "drip", "drops" };

        /// <summary>Blood spatter on a wall behind a body hit (wall elements of the WP10 atlas; never the floor pool).</summary>
        public void BloodSplat(Vector3 point, Vector3 normal, Collider collider)
        {
            if (_blood == null || !_blood.IsValid || _decals == null) return;
            if (!_blood.TryPick(s_wallBlood[Random.Range(0, s_wallBlood.Length)], out var rect, out var size)) return;
            _decals.Spawn(_blood.Material, rect, size * Random.Range(0.8f, 1.2f), point, normal, 0.1f, HoleLife, Follow(collider));
        }

        /// <summary>Scorch mark under an explosion.</summary>
        public void Scorch(Vector3 point, Vector3 normal, float diameter, Collider collider)
        {
            if (_scorch == null || !_scorch.IsValid || _decals == null) return;
            if (!_scorch.TryPick("blast", out var rect, out _)) return; // WP10 atlas "blast_large" (2.5-3.5 m)
            _decals.Spawn(_scorch.Material, rect, new Vector2(diameter, diameter), point, normal, 0.6f, 60f, Follow(collider));
        }

        /// <summary>Regenerates the impact-sound budget.</summary>
        public void Tick(float dt) => _soundTokens = Mathf.Min(SoundsPerSecond * 0.5f, _soundTokens + SoundsPerSecond * dt);

        /// <summary>Clears every live particle.</summary>
        public void Clear()
        {
            for (var i = 0; i < _bursts.Length; i++) _bursts[i]?.Clear();
        }

        private static Transform Follow(Collider c)
        {
            if (c == null) return null;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return c.transform;
            return c.gameObject.layer == LayerIds.Vehicle ? c.transform : null;
        }

        private static DecalAtlas Atlas(CombatFxLibrary lib, string id, bool proceduralFallback)
        {
            if (lib != null && lib.TryGet(id, out var e) && e.Material != null) return new DecalAtlas(e);
            if (!proceduralFallback) return null;
            var mat = FxMaterials.CreateDecal("M_FX_Decal_BulletHole_PH", FxTextures.BulletHole, null);
            if (mat == null)
            {
                LSLog.WarnOnce("combat-decal-shader", LogCategory.Combat, "Decal shader '" + FxMaterials.DecalShader + "' unavailable; bullet holes are disabled until LIB_CombatFx is built.");
                return null;
            }
            return new DecalAtlas(mat, null, null, null);
        }
    }
}
