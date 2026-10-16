using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.Interaction
{
    /// <summary>
    /// Use key (TECH_PLAN §7.3 PlayerInteractor, GDD §11 Prompts): finds the best <see cref="IInteractable"/> within
    /// 1.8 m of the eye and 40° of the view centre with line of sight (Interact mask), shows its prompt through
    /// <see cref="IHud"/> (id "Interact", with the radial progress for hold prompts) and calls <c>Interact</c> on E
    /// (press) or after <c>HoldSeconds</c> of holding E. Overlap scans run at 10 Hz into a fixed buffer
    /// (allocation-free); the current target is re-validated every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField, Tooltip("GDD §11: interact radius 1.8 m.")]
        private float _radius = 1.8f;
        [SerializeField, Tooltip("Largest angle between the view centre and the target (degrees).")]
        private float _maxAngle = 40f;
        [SerializeField, Tooltip("Seconds between candidate scans.")]
        private float _scanInterval = 0.1f;

        private readonly Collider[] _overlap = new Collider[24];
        private ServiceRef<IHud> _hud;
        private IInteractable _current;
        private float _hold;
        private float _cooldown;
        private float _scanTimer;
        private bool _promptVisible;
        private string _shownText;
        private bool _shownHold;
        private float _shownProgress = -1f;

        /// <summary>Target under the crosshair, or null.</summary>
        public IInteractable Current => _current;
        /// <summary>Hold progress 0..1 of a hold interaction.</summary>
        public float HoldProgress01 => _hold;

        /// <summary>Per-frame update (called by PlayerRig). <paramref name="allowed"/> false clears the prompt.</summary>
        public void Tick(Transform eye, IPlayerRig rig, bool allowed, bool interactDown, bool interactHeld, float dt)
        {
            _cooldown = Mathf.Max(0f, _cooldown - dt);
            if (!allowed || eye == null)
            {
                SetCurrent(null);
                return;
            }

            _scanTimer -= dt;
            if (_current != null && !StillValid(_current, eye, rig)) _scanTimer = 0f;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval;
                SetCurrent(FindBest(eye, rig));
            }
            if (_current == null) return;

            if (_current.Hold && _current.HoldSeconds > 0f)
            {
                if (interactHeld && _cooldown <= 0f)
                {
                    _hold += dt / _current.HoldSeconds;
                    if (_hold >= 1f)
                    {
                        Use(rig);
                        return;
                    }
                }
                else
                {
                    _hold = 0f;
                }
            }
            else if (interactDown && _cooldown <= 0f)
            {
                Use(rig);
                return;
            }
            UpdatePrompt();
        }

        /// <summary>Drops the target and hides the prompt.</summary>
        public void Clear() => SetCurrent(null);

        private void OnDisable() => SetCurrent(null);

        private void Use(IPlayerRig rig)
        {
            var target = _current;
            _hold = 0f;
            _cooldown = 0.3f;
            SetCurrent(null);
            _scanTimer = 0.25f;
            target.Interact(rig);
        }

        private bool StillValid(IInteractable it, Transform eye, IPlayerRig rig)
        {
            if (it is Object o && o == null) return false;
            if (!it.CanInteract(rig)) return false;
            var anchor = it.Anchor;
            if (anchor == null) return false;
            return (anchor.position - eye.position).sqrMagnitude <= (_radius + 0.6f) * (_radius + 0.6f);
        }

        private IInteractable FindBest(Transform eye, IPlayerRig rig)
        {
            var origin = eye.position;
            var forward = eye.forward;
            var count = Physics.OverlapSphereNonAlloc(origin, _radius, _overlap, PlayerLayers.InteractCandidates, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            var bestScore = float.MaxValue;
            var cosMax = Mathf.Cos(_maxAngle * Mathf.Deg2Rad);
            for (var i = 0; i < count; i++)
            {
                var c = _overlap[i];
                _overlap[i] = null;
                if (c == null) continue;
                var it = c.GetComponentInParent<IInteractable>();
                if (it == null || ReferenceEquals(it, best) || !it.CanInteract(rig)) continue;
                var point = c.bounds.ClosestPoint(origin);
                var toPoint = point - origin;
                var distance = toPoint.magnitude;
                if (distance > _radius) continue;
                var anchor = it.Anchor != null ? it.Anchor.position : c.bounds.center;
                var toAnchor = anchor - origin;
                var cos = toAnchor.sqrMagnitude > 1e-6f ? Vector3.Dot(forward, toAnchor.normalized) : 1f;
                if (cos < cosMax && distance > 0.35f) continue;
                if (!HasLineOfSight(origin, point, it)) continue;
                var score = (1f - cos) * 10f + distance;
                if (score >= bestScore) continue;
                bestScore = score;
                best = it;
            }
            return best;
        }

        private static bool HasLineOfSight(Vector3 origin, Vector3 point, IInteractable target)
        {
            var d = point - origin;
            var len = d.magnitude;
            if (len < 0.05f) return true;
            if (!Physics.Raycast(origin, d / len, out var hit, len - 0.02f, LayerIds.Interact, QueryTriggerInteraction.Ignore)) return true;
            var hitTarget = hit.collider.GetComponentInParent<IInteractable>();
            return ReferenceEquals(hitTarget, target);
        }

        private void SetCurrent(IInteractable it)
        {
            if (ReferenceEquals(it, _current))
            {
                if (it == null && _promptVisible) HidePrompt();
                return;
            }
            _current = it;
            _hold = 0f;
            if (it == null) HidePrompt();
            else UpdatePrompt();
        }

        private void UpdatePrompt()
        {
            var hud = _hud.Value;
            if (hud == null || _current == null) return;
            var text = _current.PromptText;
            var hold = _current.Hold;
            var progress = hold ? Mathf.Clamp01(_hold) : 0f;
            if (_promptVisible && ReferenceEquals(text, _shownText) && hold == _shownHold && Mathf.Abs(progress - _shownProgress) < 0.01f) return;
            hud.ShowPrompt(new PromptData { Id = PlayerEvents.InteractPromptId, Text = text, Hold = hold, Progress01 = progress });
            _promptVisible = true;
            _shownText = text;
            _shownHold = hold;
            _shownProgress = progress;
        }

        private void HidePrompt()
        {
            if (!_promptVisible) return;
            _promptVisible = false;
            _shownText = null;
            _shownProgress = -1f;
            _hud.Value?.HidePrompt(PlayerEvents.InteractPromptId);
        }
    }
}
