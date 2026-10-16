using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Look.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LongShadow.World.Look.Lightmaps
{
    /// <summary>
    /// Swaps a section's lightmaps at runtime (TECH_PLAN §7.14, D7, G11, R13). It matches entries of the global
    /// <see cref="LightmapSettings.lightmaps"/> <b>by texture</b>, never by index, so it works with any number of additive
    /// sections loaded: every entry whose colour texture belongs to one of this swapper's sets is replaced by the same
    /// slot of the active set. Unity rebuilds the global array when scenes load or unload, so the active set is
    /// re-applied after every <see cref="GameEvents.SectionLoaded"/>/<see cref="GameEvents.SectionUnloaded"/> and scene
    /// load. Rules: <see cref="LightmapSwapRule.StageThresholds"/> (S9_TownFar GOLDEN → SUNSET at 1.35 → DUSK at 1.97)
    /// or <see cref="LightmapSwapRule.MissionEvent"/> (S6 basement EMERGENCY on <c>B9.TransmitterDead</c>, saved).
    /// Placed in the section scene by <c>SectionBaker</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightmapSwapper : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("Save id (e.g. \"S6.Emergency\").")]
        private string _id;
        [SerializeField, Tooltip("The set the scene's own lighting data uses.")]
        private LightmapSet _base;
        [SerializeField, Tooltip("Alternate sets, in order (thresholds or event apply to these).")]
        private LightmapSet[] _alternates = new LightmapSet[0];
        [SerializeField] private LightmapSwapRule _rule = LightmapSwapRule.StageThresholds;
        [SerializeField, Tooltip("StageThresholds: alternate i is active from s ≥ threshold i (ascending).")]
        private float[] _thresholds = { 1.35f, 1.97f };
        [SerializeField, Tooltip("MissionEvent: activates alternate 0.")]
        private string _event = "B9.TransmitterDead";
        [SerializeField, Tooltip("MissionEvent: back to the base set (optional).")]
        private string _resetEvent;

        private readonly List<LightmapSet> _known = new List<LightmapSet>(4);
        private ServiceRef<ICheckpointService> _checkpoints;
        private int _active = -1;
        private bool _registered;

        /// <summary>Index of the active alternate (-1 = base).</summary>
        public int ActiveIndex => _active;
        /// <summary>Active set.</summary>
        public LightmapSet ActiveSet => _active >= 0 && _active < _alternates.Length ? _alternates[_active] : _base;

        /// <inheritdoc />
        public string SaveKey => "Lightmaps:" + (string.IsNullOrEmpty(_id) ? gameObject.scene.name : _id);

        private void Awake()
        {
            _known.Clear();
            if (_base != null) _known.Add(_base);
            foreach (var a in _alternates)
                if (a != null) _known.Add(a);
            if (_base == null) LSLog.Warn(LogCategory.Look, "LightmapSwapper '" + name + "' has no base set; run the SectionBaker.", this);
        }

        private void OnEnable()
        {
            GameEvents.SectionLoaded += OnSection;
            GameEvents.SectionUnloaded += OnSection;
            GameEvents.LightStageChanged += OnStage;
            GameEvents.MissionEvent += OnMissionEvent;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            if (_rule == LightmapSwapRule.StageThresholds)
            {
                var ls = LightStageController.Active;
                _active = IndexForStage(ls != null ? ls.S : 0f, _thresholds, _alternates.Length);
            }
            Apply();
        }

        private void OnDisable()
        {
            GameEvents.SectionLoaded -= OnSection;
            GameEvents.SectionUnloaded -= OnSection;
            GameEvents.LightStageChanged -= OnStage;
            GameEvents.MissionEvent -= OnMissionEvent;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (_registered) _checkpoints.Value?.Unregister(this);
            _registered = false;
        }

        private void Update()
        {
            if (_registered || _rule != LightmapSwapRule.MissionEvent) return;
            var cp = _checkpoints.Value;
            if (cp == null) return;
            cp.Register(this);
            _registered = true;
        }

        /// <summary>Activates an alternate (-1 = base) and applies it.</summary>
        public void SetActive(int index)
        {
            index = Mathf.Clamp(index, -1, _alternates.Length - 1);
            if (index == _active) return;
            _active = index;
            Apply();
            LSLog.Info(LogCategory.Look, "Lightmaps of " + gameObject.scene.name + " → " + (ActiveSet != null ? ActiveSet.SetName : "?"), this);
        }

        /// <summary>Re-applies the active set to the global lightmap array.</summary>
        public void Apply()
        {
            var target = ActiveSet;
            if (target == null || _known.Count == 0) return;
            var maps = LightmapSettings.lightmaps;
            if (maps == null || maps.Length == 0) return;
            if (Remap(maps, _known, target) > 0) LightmapSettings.lightmaps = maps;
        }

        /// <summary>
        /// Replaces, in place, every entry whose colour texture is a slot of one of <paramref name="known"/> by the same
        /// slot of <paramref name="target"/>. Returns the number of entries changed.
        /// </summary>
        public static int Remap(LightmapData[] maps, IReadOnlyList<LightmapSet> known, LightmapSet target)
        {
            if (maps == null || known == null || target == null) return 0;
            var changed = 0;
            for (var i = 0; i < maps.Length; i++)
            {
                var m = maps[i];
                if (m == null || m.lightmapColor == null) continue;
                var slot = -1;
                for (var k = 0; k < known.Count && slot < 0; k++)
                    if (known[k] != null) slot = known[k].IndexOf(m.lightmapColor);
                if (slot < 0) continue;
                var tex = target.Color(slot);
                if (tex == null || tex == m.lightmapColor) continue;
                m.lightmapColor = tex;
                changed++;
            }
            return changed;
        }

        /// <summary>Alternate index for a stage value (-1 = base).</summary>
        public static int IndexForStage(float s, float[] thresholds, int alternates)
        {
            var idx = -1;
            if (thresholds == null) return idx;
            for (var i = 0; i < thresholds.Length && i < alternates; i++)
                if (s >= thresholds[i]) idx = i;
            return idx;
        }

        private void OnStage(float s)
        {
            if (_rule != LightmapSwapRule.StageThresholds) return;
            SetActive(IndexForStage(s, _thresholds, _alternates.Length));
        }

        private void OnMissionEvent(string id)
        {
            if (_rule != LightmapSwapRule.MissionEvent || string.IsNullOrEmpty(id)) return;
            if (id == _event) SetActive(0);
            else if (!string.IsNullOrEmpty(_resetEvent) && id == _resetEvent) SetActive(-1);
        }

        private void OnSection(SectionId id) => Apply();

        private void OnSceneLoaded(Scene s, LoadSceneMode m) => Apply();

        private void OnSceneUnloaded(Scene s) => Apply();

        /// <inheritdoc />
        public void Capture(SaveWriter w) => w.Write("active", _active);

        /// <inheritdoc />
        public void Restore(SaveReader r)
        {
            var idx = r.ReadInt("active", _active);
            _active = int.MinValue;
            SetActive(idx);
        }

#if UNITY_EDITOR
        /// <summary>Editor baker: configures the swapper.</summary>
        public void EditorSetup(string id, LightmapSet baseSet, LightmapSet[] alternates, LightmapSwapRule rule, float[] thresholds, string missionEvent)
        {
            _id = id;
            _base = baseSet;
            _alternates = alternates ?? new LightmapSet[0];
            _rule = rule;
            _thresholds = thresholds ?? new float[0];
            _event = missionEvent;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
