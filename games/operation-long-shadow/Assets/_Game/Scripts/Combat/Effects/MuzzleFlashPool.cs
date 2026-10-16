using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Muzzle flashes (TECH_PLAN §7.5 MuzzleFlashPool): crossed additive quads with a random atlas cell, roll and scale per
    /// shot, visible for one to two frames (WP10 sheet note: "pick a random cell per shot, 1-2 frames"). The player's flash
    /// is a single renderer parented to the view-model muzzle on the Viewmodel layer; AI flashes come from a small world
    /// pool. Light pulses are separate (<see cref="FlashLightPool"/>).
    /// </summary>
    public sealed class MuzzleFlashPool
    {
        /// <summary>Base length of a scale-1 side flame in metres.</summary>
        public const float BaseLength = 0.16f;
        /// <summary>How long a flash stays visible.</summary>
        public const float VisibleSeconds = 0.034f;

        private readonly Mesh[] _meshes;
        private readonly Material _material;
        private readonly MeshFilter[] _worldFilters;
        private readonly MeshRenderer[] _world;
        private readonly float[] _worldAge;
        private int _next;
        private MeshFilter _playerFilter;
        private MeshRenderer _player;
        private float _playerAge;
        private int _playerFrames;

        /// <summary>Creates the pool.</summary>
        public MuzzleFlashPool(Transform root, int worldCount, Mesh[] meshes, Material material)
        {
            _meshes = meshes != null && meshes.Length > 0 ? meshes : FxFactory.BuildMuzzleMeshes();
            _material = material;
            _world = new MeshRenderer[worldCount];
            _worldFilters = new MeshFilter[worldCount];
            _worldAge = new float[worldCount];
            for (var i = 0; i < worldCount; i++)
            {
                var r = CreateRenderer(root, "MuzzleFlash_" + i, out var f);
                _world[i] = r;
                _worldFilters[i] = f;
            }
        }

        /// <summary>
        /// Flash at a view-model muzzle (child of it, its layer). <paramref name="weapon"/> (the weapon root, barrel along
        /// +Z) orients the flame along the bore even when the art's muzzle empty carries a rotation.
        /// </summary>
        public void FlashAttached(Transform muzzle, float scale, Transform weapon = null)
        {
            if (muzzle == null || scale <= 0f || _material == null) return;
            if (_player == null) _player = CreateRenderer(null, "MuzzleFlash_Player", out _playerFilter);
            var t = _player.transform;
            if (t.parent != muzzle) t.SetParent(muzzle, false);
            t.gameObject.layer = muzzle.gameObject.layer;
            Pose(t, _playerFilter, scale);
            if (weapon != null) t.rotation = weapon.rotation * t.localRotation;
            t.localPosition = Vector3.zero;
            _player.enabled = true;
            _playerAge = 0f;
            _playerFrames = 0;
        }

        /// <summary>Flash at a world position along a direction (AI, the door gun's world muzzle).</summary>
        public void FlashWorld(Vector3 position, Vector3 direction, float scale)
        {
            if (scale <= 0f || _world.Length == 0 || _material == null) return;
            var i = _next;
            _next = (_next + 1) % _world.Length;
            var r = _world[i];
            if (r == null) return;
            var t = r.transform;
            t.position = position;
            t.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? direction : Vector3.forward);
            Pose(t, _worldFilters[i], scale, false);
            r.enabled = true;
            _worldAge[i] = 0f;
        }

        /// <summary>Hides flashes after their time (always at least one rendered frame).</summary>
        public void Tick(float dt)
        {
            if (_player != null && _player.enabled)
            {
                _playerAge += dt;
                _playerFrames++;
                if (_playerFrames > 1 && _playerAge >= VisibleSeconds) _player.enabled = false;
            }
            for (var i = 0; i < _world.Length; i++)
            {
                var r = _world[i];
                if (r == null || !r.enabled) continue;
                _worldAge[i] += dt;
                if (_worldAge[i] >= VisibleSeconds) r.enabled = false;
            }
        }

        /// <summary>Detaches the player flash (the view model it was parented to is going away).</summary>
        public void DetachPlayer(Transform newParent)
        {
            if (_player == null) return;
            _player.enabled = false;
            _player.transform.SetParent(newParent, false);
        }

        private void Pose(Transform t, MeshFilter f, float scale, bool local = true)
        {
            f.sharedMesh = _meshes[Random.Range(0, _meshes.Length)];
            var roll = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            if (local) t.localRotation = roll;
            else t.rotation *= roll;
            var s = BaseLength * scale * Random.Range(0.85f, 1.15f);
            t.localScale = new Vector3(s, s, s * Random.Range(0.85f, 1.2f));
        }

        private MeshRenderer CreateRenderer(Transform parent, string name, out MeshFilter filter)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = _meshes[0];
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }
    }
}
