using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Foley
{
    /// <summary>
    /// Footsteps and the body fall of an AI or squad body (GDD §10.2 "footsteps by surface"; the player's are WP02's
    /// FootstepEmitter). Steps come from the body's <c>Foot_L</c>/<c>Foot_R</c> animation events
    /// (<see cref="ICharacterBody.AnimEvent"/>, WP05); a body that raises none while moving gets a stride clock instead.
    /// The surface under the foot comes from <see cref="SurfaceTag"/>, the physic material or the terrain layer, else the
    /// zone's typical floor; the level follows the body's speed. Steps beyond 35 m are skipped (they would be inaudible
    /// and cost voices). When the body's <see cref="Health"/> dies, <c>SFX_BODYFALL</c> plays as it hits the ground.
    /// Added at runtime by the <see cref="FootstepDirector"/> to every registered non-player actor with a body, or by a
    /// prefab builder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterFootsteps : MonoBehaviour
    {
        [SerializeField, Tooltip("Steps farther than this from the listener are skipped (m).")] private float _maxDistance = 35f;
        [SerializeField] private float _walkSpeed = 1.6f;
        [SerializeField] private float _runSpeed = 4.5f;
        [SerializeField] private float _walkVolume = 0.55f;
        [SerializeField] private float _runVolume = 1f;
        [SerializeField, Tooltip("Metres per step when the body raises no foot events (walk; running uses 1.35×).")]
        private float _fallbackStride = 0.75f;
        [SerializeField] private string _bodyFallCue = "SFX_BODYFALL";
        [SerializeField, Tooltip("Seconds from death to the body hitting the ground (min, max).")]
        private Vector2 _bodyFallDelay = new Vector2(0.45f, 0.8f);

        private ICharacterBody _body;
        private Health _health;
        private ServiceRef<IAudioService> _audio;
        private Vector3 _lastPos;
        private float _speed;
        private float _lastStepAt = -100f;
        private float _lastAnimFootAt = -100f;
        private float _strideAccum;
        private float _bodyFallAt = -1f;
        private bool _dead;

        /// <summary>Smoothed horizontal speed (m/s).</summary>
        public float Speed => _speed;

        private void OnEnable()
        {
            _body = GetComponent<ICharacterBody>() ?? GetComponentInParent<ICharacterBody>();
            if (_body != null) _body.AnimEvent += OnAnimEvent;
            _health = GetComponent<Health>();
            if (_health != null) _health.Killed += OnKilled;
            _dead = _health != null && !_health.IsAlive;
            _lastPos = transform.position;
        }

        private void OnDisable()
        {
            if (_body != null) _body.AnimEvent -= OnAnimEvent;
            if (_health != null) _health.Killed -= OnKilled;
            _body = null;
            _health = null;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            var pos = transform.position;
            var d = pos - _lastPos;
            d.y = 0f;
            var step = d.magnitude;
            _lastPos = pos;
            // Teleports (checkpoint restore, spawning) are not walking.
            if (step > 3f) step = 0f;
            _speed = Mathf.Lerp(_speed, step / dt, 1f - Mathf.Exp(-dt * 8f));

            if (_bodyFallAt >= 0f && Time.time >= _bodyFallAt)
            {
                _bodyFallAt = -1f;
                if (InRange(pos, _maxDistance * 1.5f)) _audio.Value?.Play(_bodyFallCue, pos);
            }
            // Restored by a checkpoint (Health.Restore) or pooled and respawned.
            if (_dead && _bodyFallAt < 0f && _health != null && _health.IsAlive) _dead = false;
            if (_dead) return;
            if (_health != null && !_health.IsAlive)
            {
                _dead = true;
                return;
            }
            if (Time.time - _lastAnimFootAt < 1.5f || _speed < 0.5f)
            {
                _strideAccum = 0f;
                return;
            }
            _strideAccum += step;
            var stride = _speed >= _runSpeed ? _fallbackStride * 1.35f : _fallbackStride;
            if (_strideAccum < stride) return;
            _strideAccum = 0f;
            Step();
        }

        private void OnAnimEvent(string id)
        {
            if (id != AnimEventIds.FootL && id != AnimEventIds.FootR) return;
            _lastAnimFootAt = Time.time;
            Step();
        }

        private void OnKilled(Health h, DamageInfo d)
        {
            _dead = true;
            _bodyFallAt = Time.time + Random.Range(_bodyFallDelay.x, _bodyFallDelay.y);
        }

        /// <summary>Plays one step now (if alive and within range).</summary>
        public void Step()
        {
            if (_dead) return;
            var now = Time.time;
            if (now - _lastStepAt < 0.14f) return;
            _lastStepAt = now;
            var pos = transform.position;
            if (!InRange(pos, _maxDistance)) return;
            var audio = _audio.Value;
            if (audio == null) return;
            var cue = AudioCues.Footstep(ResolveSurface(pos));
            if (!audio.HasCue(cue)) cue = AudioCues.FootstepFallback;
            var vol = FootstepSurfaces.Volume(_speed, _walkSpeed, _runSpeed, _walkVolume, _runVolume);
            audio.Play(cue, pos + Vector3.up * 0.05f, vol);
        }

        private static bool InRange(Vector3 pos, float distance) =>
            (pos - AudioRuntime.ListenerPosition).sqrMagnitude <= distance * distance;

        private static SurfaceType ResolveSurface(Vector3 pos)
        {
            var hit = SurfaceType.Default;
            if (Physics.Raycast(pos + Vector3.up * 0.4f, Vector3.down, out var rh, 1.2f, LayerIds.Ground, QueryTriggerInteraction.Ignore))
            {
                if (rh.collider is TerrainCollider tc && tc.TryGetComponent(out Terrain terrain))
                    hit = TerrainSurfaces.Resolve(terrain, rh.point);
                else hit = SurfaceTag.Resolve(rh.collider);
            }
            if (hit != SurfaceType.Default && hit != SurfaceType.Flesh) return hit;
            return FootstepSurfaces.Choose(hit, ZoneTag.Resolve(pos));
        }
    }
}
