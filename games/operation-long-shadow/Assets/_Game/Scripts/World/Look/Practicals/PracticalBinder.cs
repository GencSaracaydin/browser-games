using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Look.Atmosphere;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// Attaches Look's practical prefabs to level markers that do not have one yet (WP10 Level places markers and may
    /// already instantiate the prefabs at build time; this is the safety net so the lighting works either way):
    /// <c>LAMP.*</c> → sodium lamp head, <c>B6.F1..F4</c> → floodlight aimed at (345, 0, 325), <c>B4.FireProp</c> /
    /// <c>B11.FireProp</c> → burning-technical fire, <c>B9.Emergency1/2</c> → red emergency lights, <c>B7.Fill1/2</c> →
    /// tungsten fills. Runs on start and after every <see cref="GameEvents.SectionLoaded"/>. A marker counts as bound when
    /// its hierarchy already holds a Look practical component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PracticalBinder : MonoBehaviour
    {
        [SerializeField] private PracticalBindRule[] _rules = new PracticalBindRule[0];

        private readonly List<SceneMarker> _markers = new List<SceneMarker>(512);
        private float _bindAt = -1f;

        /// <summary>Prefabs attached since start.</summary>
        public int Attached { get; private set; }

        private void OnEnable()
        {
            GameEvents.SectionLoaded += OnSectionLoaded;
            _bindAt = Time.unscaledTime + 0.1f;
        }

        private void OnDisable() => GameEvents.SectionLoaded -= OnSectionLoaded;

        private void OnSectionLoaded(SectionId id) => _bindAt = Time.unscaledTime + 0.1f;

        private void Update()
        {
            if (_bindAt < 0f || Time.unscaledTime < _bindAt) return;
            _bindAt = -1f;
            BindAll();
        }

        /// <summary>Binds every matching marker now.</summary>
        public void BindAll()
        {
            if (_rules == null || _rules.Length == 0) return;
            MarkerRegistry.Instance.GetAll(_markers);
            for (var i = 0; i < _markers.Count; i++)
            {
                var m = _markers[i];
                if (m == null || string.IsNullOrEmpty(m.Id)) continue;
                var rule = RuleFor(m.Id);
                if (rule == null || rule.Prefab == null) continue;
                if (IsBound(m.gameObject)) continue;
                var go = Instantiate(rule.Prefab, m.transform, false);
                go.name = rule.Prefab.name;
                if (rule.Aim)
                {
                    var dir = rule.AimPoint - go.transform.position;
                    if (dir.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
                Attached++;
                LSLog.Verbose(LogCategory.Look, "PracticalBinder: " + rule.Prefab.name + " → " + m.Id);
            }
            _markers.Clear();
        }

        private PracticalBindRule RuleFor(string id)
        {
            foreach (var r in _rules)
                if (r != null && r.Matches(id))
                    return r;
            return null;
        }

        private static bool IsBound(GameObject go) =>
            go.GetComponentInChildren<PracticalLight>(true) != null || go.GetComponentInChildren<FloodLight>(true) != null ||
            go.GetComponentInChildren<FireProp>(true) != null || go.GetComponentInChildren<LightShaft>(true) != null;

#if UNITY_EDITOR
        /// <summary>Editor builders.</summary>
        public void EditorSetRules(PracticalBindRule[] rules)
        {
            _rules = rules ?? new PracticalBindRule[0];
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
