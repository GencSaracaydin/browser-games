using System.Collections.Generic;
using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Foley
{
    /// <summary>
    /// Gives every AI and squad body footsteps without touching other packages' prefabs: once a second it walks the
    /// <see cref="ITargetRegistry"/> (WP04) and adds a <see cref="CharacterFootsteps"/> to each registered actor that
    /// has an <see cref="ICharacterBody"/> and none yet. The player is skipped (WP02's FootstepEmitter does it). Lives on
    /// the audio systems prefab.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Audio + 12)]
    [DisallowMultipleComponent]
    public sealed class FootstepDirector : MonoBehaviour
    {
        [SerializeField] private float _scanInterval = 1f;
        [SerializeField] private bool _enabledForSquad = true;

        private readonly List<ITargetable> _actors = new List<ITargetable>(32);
        private ServiceRef<ITargetRegistry> _targets;
        private float _nextScan;

        /// <summary>Number of footstep components added so far (debug).</summary>
        public int Added { get; private set; }

        private void Update()
        {
            var now = AudioRuntime.Now;
            if (now < _nextScan) return;
            _nextScan = now + _scanInterval;
            Scan();
        }

        /// <summary>Adds footsteps to every registered body that has none (also callable by tests and builders).</summary>
        public int Scan()
        {
            var registry = _targets.Value;
            if (registry == null) return 0;
            _actors.Clear();
            registry.GetAll(_actors);
            var player = registry.Player;
            var added = 0;
            for (var i = 0; i < _actors.Count; i++)
            {
                var t = _actors[i];
                if (t == null || ReferenceEquals(t, player) || t.Team == Team.Player) continue;
                if (!_enabledForSquad && t.Team == Team.Squad) continue;
                var root = t.Root;
                if (root == null || root.TryGetComponent(out CharacterFootsteps _)) continue;
                if (!root.TryGetComponent(out ICharacterBody _)) continue;
                root.gameObject.AddComponent<CharacterFootsteps>();
                added++;
            }
            _actors.Clear();
            Added += added;
            return added;
        }
    }
}
