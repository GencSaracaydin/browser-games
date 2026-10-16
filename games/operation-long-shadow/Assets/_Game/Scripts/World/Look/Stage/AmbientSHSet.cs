using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Stage
{
    /// <summary>
    /// The four stage ambient probes (GDD §9.1; asset <c>Assets/_Game/Data/Lighting/AmbientSHSet.asset</c>), captured by
    /// <c>LongShadow/Look/Capture Ambient SH</c> from each stage's sun-free HDRI (EditorOnly EXR), scaled to the runtime
    /// sky calibration. The controller blends them with the same weights as the stage volumes. Missing stages fall back
    /// to a sky/horizon/ground gradient built from the fog colours, so the game never renders with a black ambient.
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Look/Ambient SH Set", fileName = "AmbientSHSet")]
    public sealed class AmbientSHSet : ScriptableObject
    {
        [SerializeField] private List<AmbientSHStage> _stages = new List<AmbientSHStage>();

        [System.NonSerialized] private SphericalHarmonicsL2[] _cache;
        [System.NonSerialized] private float[] _captureExposure;
        [System.NonSerialized] private bool[] _valid;

        /// <summary>Entries in authored order.</summary>
        public IReadOnlyList<AmbientSHStage> Stages => _stages;

        /// <summary>True if the stage has a captured probe.</summary>
        public bool Has(StageId stage)
        {
            EnsureCache();
            return _valid[(int)stage];
        }

        /// <summary>The probe of a stage and its capture exposure (false when missing).</summary>
        public bool TryGet(StageId stage, out SphericalHarmonicsL2 sh, out float captureSkyExposure)
        {
            EnsureCache();
            var i = (int)stage;
            sh = _cache[i];
            captureSkyExposure = _captureExposure[i];
            return _valid[i];
        }

        /// <summary>Editor capture: replaces the entry of a stage.</summary>
        public void SetStage(AmbientSHStage entry)
        {
            if (entry == null) return;
            _stages.RemoveAll(e => e == null || e.Stage == entry.Stage);
            _stages.Add(entry);
            _stages.Sort((a, b) => a.Stage.CompareTo(b.Stage));
            _cache = null;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void OnEnable() => _cache = null;

        private void EnsureCache()
        {
            if (_cache != null) return;
            _cache = new SphericalHarmonicsL2[4];
            _captureExposure = new float[4];
            _valid = new bool[4];
            foreach (var e in _stages)
            {
                if (e == null || !e.IsValid) continue;
                var i = (int)e.Stage;
                if (i < 0 || i > 3) continue;
                _cache[i] = AmbientSHMath.FromArray(e.Coefficients);
                _captureExposure[i] = e.CaptureSkyExposure > 0f ? e.CaptureSkyExposure : 1f;
                _valid[i] = true;
            }
        }
    }
}
