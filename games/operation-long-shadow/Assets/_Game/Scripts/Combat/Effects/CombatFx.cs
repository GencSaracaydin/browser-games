using System;
using LongShadow.Combat.Data;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Hub of every pooled combat effect (TECH_PLAN §7.5 MuzzleFlashPool, TracerPool, ImpactService; §7.16 pool sizes):
    /// tracers (64), screen-space decals (64), impact bursts per surface profile, muzzle flashes, flash lights (4),
    /// casings (32), explosions and the muzzle smoke puff. Built once from <c>LIB_CombatFx</c> with procedural fallbacks
    /// for missing entries, so nothing is instantiated after warm-up. Lives on the CombatService object in S_Persistent;
    /// <see cref="Instance"/> is null in EditMode tests and scenes without combat, and every caller checks for it.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Weapons + 20)]
    [DisallowMultipleComponent]
    public sealed class CombatFx : MonoBehaviour
    {
        /// <summary>Tracer pool size (TECH_PLAN §7.16).</summary>
        public const int TracerCount = 64;
        /// <summary>Decal pool size (TECH_PLAN §7.5).</summary>
        public const int DecalCount = 64;
        /// <summary>World muzzle flashes (AI and squad) alive at once.</summary>
        public const int WorldMuzzleCount = 12;
        /// <summary>Muzzle and explosion lights alive at once (TECH_PLAN §7.5: at most 4).</summary>
        public const int LightCount = 4;
        /// <summary>Casings alive at once (TECH_PLAN §7.16).</summary>
        public const int CasingCount = 32;
        /// <summary>Muzzle lights farther than this from the listener are skipped (they would not be seen; GDD §9.5 budget).</summary>
        public const float LightRange = 45f;

        private static CombatFx s_instance;

        private CombatFxLibrary _library;
        private FxMaterialSet _materials;
        private ServiceRef<IPlayerRig> _rig;
        private bool _built;
        private Transform _poolRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        /// <summary>The live hub, or null when no combat FX exist (tests, booth-only scenes).</summary>
        public static CombatFx Instance => s_instance != null ? s_instance : null;

        /// <summary>Pooled tracer streaks.</summary>
        public TracerPool Tracers { get; private set; }
        /// <summary>Pooled URP decal projectors.</summary>
        public DecalPool Decals { get; private set; }
        /// <summary>Bullet impacts per surface.</summary>
        public ImpactEffects Impacts { get; private set; }
        /// <summary>Muzzle flashes (player view model + world pool).</summary>
        public MuzzleFlashPool Muzzle { get; private set; }
        /// <summary>Short point-light pulses.</summary>
        public FlashLightPool Lights { get; private set; }
        /// <summary>Physical casings.</summary>
        public ShellEjector Shells { get; private set; }
        /// <summary>Explosion bursts.</summary>
        public ExplosionEffects Explosions { get; private set; }
        /// <summary>Muzzle smoke puff after a burst.</summary>
        public ParticleBurst MuzzleSmoke { get; private set; }
        /// <summary>The library the hub was built from (may be null).</summary>
        public CombatFxLibrary Library => _library;
        /// <summary>Materials used for procedural fallbacks.</summary>
        public FxMaterialSet Materials => _materials;
        /// <summary>Root transform the pooled objects live under.</summary>
        public Transform PoolRoot => _poolRoot;

        /// <summary>The player's eye (listener and culling reference), or the main camera, or the origin.</summary>
        public Vector3 ListenerPosition
        {
            get
            {
                var rig = _rig.Value;
                if (rig != null && rig.Eye != null) return rig.Eye.position;
                var cam = Camera.main;
                return cam != null ? cam.transform.position : Vector3.zero;
            }
        }

        /// <summary>True when a light pulse at <paramref name="position"/> is close enough to matter.</summary>
        public bool WithinLightRange(Vector3 position) => (position - ListenerPosition).sqrMagnitude < LightRange * LightRange;

        /// <summary>
        /// Returns the hub, creating it on its own GameObject when none exists (play mode only; returns null outside play
        /// mode so EditMode tests never spawn scene objects).
        /// </summary>
        public static CombatFx Ensure()
        {
            if (s_instance != null) return s_instance;
            if (!Application.isPlaying) return null;
            var go = new GameObject("CombatFx");
            return go.AddComponent<CombatFx>();
        }

        /// <summary>
        /// Instantiates the library prefab for <paramref name="id"/> under <paramref name="parent"/>, or calls
        /// <paramref name="build"/> (FxFactory) when the entry or its prefab is missing (logged once per id).
        /// </summary>
        public static GameObject InstantiateOrBuild(CombatFxLibrary lib, string id, Transform parent, Func<GameObject> build)
        {
            GameObject go = null;
            if (lib != null && lib.TryGet(id, out var e) && e.Prefab != null)
            {
                go = Instantiate(e.Prefab, parent, false);
                go.name = e.Prefab.name;
            }
            else
            {
                LSLog.MissingAsset(LogCategory.Combat, "LIB_CombatFx/" + id, CombatFxLibrary.AssetPath);
                try
                {
                    go = build != null ? build() : null;
                }
                catch (Exception ex)
                {
                    LSLog.Exception(LogCategory.Combat, ex);
                }
                if (go == null) go = new GameObject("FX_" + id);
                go.transform.SetParent(parent, false);
            }
            return go;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                LSLog.Warn(LogCategory.Combat, "A second CombatFx was created; the first one stays.", this);
                Destroy(this);
                return;
            }
            s_instance = this;
            EnsureBuilt();
        }

        private void OnEnable()
        {
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
            GameEvents.SectionUnloaded += OnSectionUnloaded;
        }

        private void OnDisable()
        {
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            GameEvents.SectionUnloaded -= OnSectionUnloaded;
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        /// <summary>Builds every pool once (idempotent; CombatService calls it before creating its own pools).</summary>
        public void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            _library = Libraries.Get<CombatFxLibrary>();
            _materials = NeedsProcedural(_library) ? FxMaterialSet.Procedural() : new FxMaterialSet();
            _poolRoot = new GameObject("CombatFx_Pools").transform;
            _poolRoot.SetParent(transform, false);

            Material tracer = null;
            if (_library != null && _library.TryGet(FxIds.Tracer, out var te)) tracer = te.Material;
            if (tracer == null) tracer = _materials.Tracer ?? FxMaterialSet.Procedural().Tracer;
            Tracers = new TracerPool(Child("Tracers"), TracerCount, tracer);

            Decals = new DecalPool(Child("Decals"), DecalCount);
            Impacts = new ImpactEffects(Child("Impacts"), _library, _materials, Decals);

            Mesh[] flashMeshes = null;
            Material flashMat = null;
            if (_library != null && _library.TryGet(FxIds.MuzzleFlash, out var me))
            {
                flashMeshes = me.Meshes != null && me.Meshes.Length > 0 ? me.Meshes : null;
                flashMat = me.Material;
            }
            if (flashMat == null) flashMat = _materials.Muzzle ?? FxMaterialSet.Procedural().Muzzle;
            Muzzle = new MuzzleFlashPool(Child("Muzzle"), WorldMuzzleCount, flashMeshes, flashMat);

            Lights = new FlashLightPool(Child("Lights"), LightCount);
            Shells = new ShellEjector(Child("Casings"), CasingCount);
            RegisterCalibre(FxIds.Casing556);
            RegisterCalibre(FxIds.Casing762x39);
            RegisterCalibre(FxIds.Casing762x51);
            RegisterCalibre(FxIds.Casing9mm);

            Explosions = new ExplosionEffects(Child("Explosions"), _library, _materials);
            var smoke = InstantiateOrBuild(_library, FxIds.MuzzleSmoke, Child("MuzzleSmoke"), () => FxFactory.BuildMuzzleSmoke(_materials));
            MuzzleSmoke = new ParticleBurst(smoke);
        }

        private void RegisterCalibre(string id)
        {
            if (_library == null || !_library.TryGet(id, out var e)) return;
            var mesh = e.Meshes != null && e.Meshes.Length > 0 ? e.Meshes[0] : null;
            Shells.SetCalibre(id, mesh, e.Material);
        }

        private Transform Child(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_poolRoot, false);
            return t;
        }

        private static readonly string[] s_prefabIds =
        {
            FxIds.MuzzleSmoke, FxIds.ExplosionFrag, FxIds.ExplosionRocket, FxIds.ExplosionFireball, FxIds.SmokeCloud,
            FxIds.SmokeTrail, FxIds.SparkBurst, FxIds.Fire,
        };

        private static bool NeedsProcedural(CombatFxLibrary lib)
        {
            if (lib == null) return true;
            for (var i = 0; i < s_prefabIds.Length; i++)
                if (!lib.TryGet(s_prefabIds[i], out var e) || e.Prefab == null) return true;
            for (var p = 0; p <= (int)ImpactProfile.Fruit; p++)
                if (!lib.TryGet(FxIds.Impact((ImpactProfile)p), out var e) || e.Prefab == null) return true;
            if (!lib.TryGet(FxIds.Tracer, out var t) || t.Material == null) return true;
            if (!lib.TryGet(FxIds.MuzzleFlash, out var m) || m.Material == null) return true;
            return false;
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            var cam = ListenerPosition;
            Tracers?.Tick(dt, cam);
            Decals?.Tick(dt);
            Impacts?.Tick(dt);
            Muzzle?.Tick(dt);
            Lights?.Tick(dt);
            Shells?.Tick(dt);
        }

        /// <summary>Removes every live effect (checkpoint reload).</summary>
        public void Clear()
        {
            Tracers?.Clear();
            Decals?.Clear();
            Impacts?.Clear();
            Lights?.Clear();
            Shells?.Clear();
            Explosions?.Clear();
            MuzzleSmoke?.Clear();
        }

        private void OnCheckpointLoaded(string cpId) => Clear();

        private void OnSectionUnloaded(SectionId section)
        {
            // Decals that followed a collider of the unloaded section were destroyed with it; the pool recreates them.
            // Static decals left floating over the hole would be wrong, so drop all of them.
            Decals?.Clear();
            Shells?.Clear();
        }
    }
}
