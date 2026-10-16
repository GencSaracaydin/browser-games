using LongShadow.Core;
using LongShadow.World.Look.Practicals;
using LongShadow.World.Look.Stage;
using UnityEditor;
using UnityEngine;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// "LightStage Scrubber" (TECH_PLAN §7.14, GDD §9.1 "a debug slider on s is required"): one slider over s 0–4 that
    /// drives the live <see cref="ILightStage"/> in play mode (optionally holding the override so the mission driver
    /// stops) or previews the open scene's LightStageController in edit mode, with the keyframe rows as buttons and a
    /// read-out of every curve (sun, sky, fog, clock, AI sight, HUD colour). "Ripple" replays the sodium strike in play mode.
    /// </summary>
    public sealed class LightStageScrubberWindow : EditorWindow
    {
        private static readonly float[] Presets = { 0f, 0.3f, 1.0f, 1.35f, 1.5f, 1.8f, 1.94f, 1.97f, 2.0f, 2.5f, 2.8f, 3.2f, 3.5f, 4.0f };

        private float _s;
        private bool _hold = true;
        private bool _live = true;
        private Vector2 _scroll;

        [MenuItem("LongShadow/Look/LightStage Scrubber", priority = 100)]
        public static void Open() => GetWindow<LightStageScrubberWindow>("LightStage");

        private void OnEnable()
        {
            var c = Object.FindAnyObjectByType<LightStageController>();
            if (c != null) _s = c.S;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                // Never leave the URP asset's shadow distance changed by an edit-mode preview.
                var c = Object.FindAnyObjectByType<LightStageController>();
                if (c != null) c.EditorReleasePreview();
                return;
            }
            if (GameServices.TryGet(out ILightStage ls)) ls.Override(this, false);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField(Application.isPlaying ? "Play mode: drives ILightStage" : "Edit mode: previews the open scene", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _s = EditorGUILayout.Slider("s", _s, LightStageTable.MinS, LightStageTable.MaxS);
            var changed = EditorGUI.EndChangeCheck();
            if (Application.isPlaying)
            {
                _hold = EditorGUILayout.Toggle(new GUIContent("Hold override", "Suspends the mission progress driver while scrubbing"), _hold);
                _live = EditorGUILayout.Toggle(new GUIContent("Continuous (strike events)", "SetTarget(s, 0): crossing 1.97 upward strikes the practicals"), _live);
            }

            EditorGUILayout.BeginHorizontal();
            for (var i = 0; i < Presets.Length; i++)
            {
                if (i == 7)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
                if (GUILayout.Button(Presets[i].ToString("0.00")))
                {
                    _s = Presets[i];
                    changed = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (changed) Apply();

            if (Application.isPlaying && GUILayout.Button("Ripple (strike now)"))
            {
                var ripple = Object.FindAnyObjectByType<LampRipple>();
                if (ripple != null) ripple.Strike();
            }
            if (!Application.isPlaying && GUILayout.Button("Release preview (s = 0)"))
            {
                _s = 0f;
                Apply();
                var c = Object.FindAnyObjectByType<LightStageController>();
                if (c != null) c.EditorReleasePreview();
            }

            var smp = LightStageTable.Sample(_s);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stage", smp.Stage + "   blend " + smp.Blend + "   clock " + LightStageTable.FormatClock(smp.ClockMinutes));
            EditorGUILayout.LabelField("Sun", smp.SunEnabled
                ? "el " + smp.SunElevation.ToString("0.00") + "°  I " + smp.SunIntensity.ToString("0.00") + "  " + smp.SunKelvin.ToString("0") + " K  shadows " + smp.ShadowDistance.ToString("0") + " m / " + smp.ShadowStrength.ToString("0.00")
                : "off");
            EditorGUILayout.LabelField("Sky key", smp.SkyKeyEnabled ? "I " + smp.SkyKeyIntensity.ToString("0.000") + "  " + smp.SkyKeyKelvin.ToString("0") + " K" : "off");
            EditorGUILayout.LabelField("Sky", "blend " + smp.SkyBlend.ToString("0.00") + "  EV " + smp.SkyEv.ToString("0.00") + "  disc " + smp.SunDiscVisibility.ToString("0.00"));
            EditorGUILayout.ColorField(new GUIContent("Sky tint"), smp.SkyTint, false, false, false);
            EditorGUILayout.LabelField("Fog", smp.FogDensity.ToString("0.0000"));
            EditorGUILayout.ColorField(new GUIContent("Fog colour"), smp.FogColor, false, false, false);
            EditorGUILayout.LabelField("Post exposure", smp.StagePostExposure.ToString("+0.00;-0.00") + " EV");
            EditorGUILayout.LabelField("AI sight", smp.AISightTarget.ToString("0") + " m" + (smp.AfterSunset ? "   (after sunset)" : ""));
            EditorGUILayout.ColorField(new GUIContent("HUD colour"), smp.HudColor, false, false, false);
            EditorGUILayout.EndScrollView();
        }

        private void Apply()
        {
            if (Application.isPlaying)
            {
                if (!GameServices.TryGet(out ILightStage ls)) return;
                ls.Override(this, _hold);
                if (_live) ls.SetTarget(_s, 0f);
                else ls.SetImmediate(_s);
                return;
            }
            var c = Object.FindAnyObjectByType<LightStageController>();
            if (c == null)
            {
                ShowNotification(new GUIContent("No LightStageController in the open scenes (Install Look Rig)."));
                return;
            }
            c.PreviewApply(_s);
            var struck = _s >= LightStageTable.StrikeS;
            foreach (var p in Object.FindObjectsByType<PracticalLight>(FindObjectsInactive.Exclude))
            {
                if (p is SodiumLamp lamp)
                {
                    if (struck) lamp.SnapOn();
                    else lamp.SnapOff();
                    lamp.PreviewLevel(struck ? 1f : 0f);
                }
                else p.PreviewLevel(1f);
            }
            SceneView.RepaintAll();
        }
    }
}
