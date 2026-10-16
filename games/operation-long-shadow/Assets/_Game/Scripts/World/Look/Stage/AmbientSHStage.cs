using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Look.Stage
{
    /// <summary>One captured ambient probe of an <see cref="AmbientSHSet"/> (GDD §9.1).</summary>
    [Serializable]
    public sealed class AmbientSHStage
    {
        [SerializeField] private StageId _stage;
        [SerializeField, Tooltip("Stage value the probe was captured at.")]
        private float _captureS;
        [SerializeField, Tooltip("Linear sky exposure (texture base x tint luminance x 2^EV) at capture; the runtime rescales by the live sky exposure.")]
        private float _captureSkyExposure = 1f;
        [SerializeField, Tooltip("27 SH coefficients (rgb-major), Unity SphericalHarmonicsL2 order.")]
        private float[] _coefficients = new float[AmbientSHMath.CoefficientCount];
        [SerializeField, Tooltip("Where the probe came from (EXR capture, json fallback, gradient fallback).")]
        private string _source;

        /// <summary>Stage.</summary>
        public StageId Stage => _stage;
        /// <summary>Capture s.</summary>
        public float CaptureS => _captureS;
        /// <summary>Linear sky exposure at capture.</summary>
        public float CaptureSkyExposure => _captureSkyExposure;
        /// <summary>Coefficients (27).</summary>
        public float[] Coefficients => _coefficients;
        /// <summary>Source description.</summary>
        public string Source => _source;
        /// <summary>True when the coefficients hold a probe.</summary>
        public bool IsValid => AmbientSHMath.IsValid(_coefficients);

        /// <summary>Creates an entry (editor capture).</summary>
        public AmbientSHStage(StageId stage, float captureS, float captureSkyExposure, float[] coefficients, string source)
        {
            _stage = stage;
            _captureS = captureS;
            _captureSkyExposure = captureSkyExposure;
            _coefficients = coefficients ?? new float[AmbientSHMath.CoefficientCount];
            _source = source;
        }
    }
}
