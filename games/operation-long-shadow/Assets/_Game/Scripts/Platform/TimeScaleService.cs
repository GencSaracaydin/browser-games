using System.Text;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// <see cref="ITimeScaleService"/> (TECH_PLAN §5.4): <c>Time.timeScale</c> = the highest-priority request
    /// (<see cref="TimePriority"/>), else 1. While slowed down, the physics step shrinks with the scale so slow motion
    /// stays smooth (fixed step 0.02 s x scale, never below 2 ms of game time); it is restored at scale 1.
    /// <see cref="SetScale"/> changes a live request (ramps such as "Death 0.3 over 0.6 s"); see Docs/CORE_CHANGES.md.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform)]
    [DisallowMultipleComponent]
    public sealed class TimeScaleService : MonoBehaviour, ITimeScaleService
    {
        [SerializeField, Tooltip("Scale Time.fixedDeltaTime with the time scale while slowed (smooth slow motion).")]
        private bool _scalePhysicsStep = true;
        [SerializeField, Tooltip("Smallest physics step in game seconds while slowed.")]
        private float _minFixedStep = 0.002f;

        private readonly TimeScaleStack _stack = new TimeScaleStack();
        private float _baseFixedDelta = 0.02f;
        private float _current = 1f;

        /// <inheritdoc/>
        public float Current => _current;

        /// <summary>Number of live requests (diagnostics).</summary>
        public int RequestCount => _stack.Count;

        private void Awake()
        {
            _baseFixedDelta = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            GameServices.Register<ITimeScaleService>(this);
            Apply();
        }

        private void OnDestroy()
        {
            GameServices.Unregister<ITimeScaleService>(this);
            _stack.Clear();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }

        /// <inheritdoc/>
        public int Push(float scale, int priority, string reason)
        {
            var h = _stack.Push(scale, priority, reason);
            Apply();
            return h;
        }

        /// <inheritdoc/>
        public void Pop(int handle)
        {
            if (_stack.Pop(handle)) Apply();
        }

        /// <summary>Changes the scale of a live request (ramps). Returns false for unknown handles.</summary>
        public bool SetScale(int handle, float scale)
        {
            if (!_stack.SetScale(handle, scale)) return false;
            Apply();
            return true;
        }

        /// <summary>Lists live requests (dev console).</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("timeScale ").Append(_current.ToString("0.###")).Append('\n');
            _stack.Describe(sb);
            return sb.ToString();
        }

        private void Apply()
        {
            _current = _stack.Evaluate();
            Time.timeScale = _current;
            if (!_scalePhysicsStep) return;
            if (_current > 0f && _current < 1f)
                Time.fixedDeltaTime = Mathf.Max(_baseFixedDelta * _current, _minFixedStep);
            else
                Time.fixedDeltaTime = _baseFixedDelta;
        }
    }
}
