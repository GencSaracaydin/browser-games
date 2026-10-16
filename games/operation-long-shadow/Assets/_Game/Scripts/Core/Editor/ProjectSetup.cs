using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// The one idempotent project-settings script (TECH_PLAN §3.1-3.9, §3.13). Every value is logged before -> after to
    /// <c>Logs/project_setup.txt</c>; a second run reports 0 changes. Nobody edits these settings by hand; changes go
    /// through this file (WP00, later WP11). Menu: <b>LongShadow/Setup/Apply Project Settings</b> and
    /// <b>LongShadow/Setup/Verify Project Settings</b> (dry run). Batch/MCP: <c>ProjectSetup.Apply(false)</c>.
    /// </summary>
    public static class ProjectSetup
    {
        /// <summary>Log file (project-relative).</summary>
        public const string LogPath = "Logs/project_setup.txt";
        /// <summary>URP asset used by the Web_High quality level (path kept, §3.4).</summary>
        public const string RpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        /// <summary>Main renderer (index 0).</summary>
        public const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        /// <summary>Render-texture renderer (index 1, §3.5).</summary>
        public const string RendererRtPath = "Assets/_Game/Settings/URP/PC_Renderer_RT.asset";
        /// <summary>Copy of URP's PostProcessData owned by the project (§3.5).</summary>
        public const string PostProcessDataPath = "Assets/_Game/Settings/URP/PostProcessData_Web.asset";
        /// <summary>Project-wide input actions (§7.2).</summary>
        public const string InputActionsPath = "Assets/_Game/Settings/Input/LongShadow.inputactions";
        /// <summary>WebGL template folder created by WP01.</summary>
        public const string TemplateIndexPath = "Assets/WebGLTemplates/CovertOps/index.html";

        private const string UrpPostProcessDataGuid = "41439944d30ece34e96484bdb6645b55";

        /// <summary>Applies every setting.</summary>
        [MenuItem("LongShadow/Setup/Apply Project Settings", priority = 1)]
        public static void ApplyMenu() => Apply(false);

        /// <summary>Reports differences without changing anything.</summary>
        [MenuItem("LongShadow/Setup/Verify Project Settings", priority = 2)]
        public static void VerifyMenu() => Apply(true);

        /// <summary>Runs the recipe. Returns the log (also written to <see cref="LogPath"/>).</summary>
        public static SettingsLog Apply(bool dryRun)
        {
            var log = new SettingsLog(dryRun);
            Run(log, "3.1 Player settings (Web)", PlayerWeb);
            Run(log, "3.1 Encodings (lightmaps, HDR cubemaps)", Encodings);
            Run(log, "3.1/3.13 Editor settings", EditorSide);
            Run(log, "3.2 Graphics settings", Graphics);
            Run(log, "3.3 Quality", Quality);
            Run(log, "3.5 Renderers", Renderers);
            Run(log, "3.4 URP asset", UrpAsset);
            Run(log, "3.8 Rendering layers / 3.9 Layers and tags", LayersAndTags);
            Run(log, "3.9 Physics", Physics);
            Run(log, "3.13 Time and audio", TimeAndAudio);
            Run(log, "7.2 Project-wide input actions", InputActions);
            if (!dryRun) AssetDatabase.SaveAssets();
            var header = "# ProjectSetup " + (dryRun ? "VERIFY" : "APPLY") + " " + DateTime.Now.ToString("s") + " (Unity " + Application.unityVersion + ")";
            var path = log.WriteTo(LogPath, header);
            var summary = "ProjectSetup " + (dryRun ? "verify" : "apply") + ": checked " + log.Checked + ", " +
                          (dryRun ? "differences " : "changed ") + log.Changes + ", problems " + log.Problems.Count + " -> " + path;
            if (log.Problems.Count > 0) LSLog.Warn(LogCategory.Core, summary + "\n" + string.Join("\n", log.Problems));
            else LSLog.Info(LogCategory.Core, summary);
            return log;
        }

        private static void Run(SettingsLog log, string title, Action<SettingsLog> step)
        {
            log.Section(title);
            try
            {
                step(log);
            }
            catch (Exception e)
            {
                log.Problem(title + " threw " + e.GetType().Name + ": " + e.Message);
                Debug.LogException(e);
            }
        }

        // ------------------------------------------------------------------ 3.1
        private static void PlayerWeb(SettingsLog log)
        {
            var nt = NamedBuildTarget.WebGL;
            log.Set("ScriptingBackend(WebGL)", () => PlayerSettings.GetScriptingBackend(nt), v => PlayerSettings.SetScriptingBackend(nt, v), ScriptingImplementation.IL2CPP);
            log.Set("ApiCompatibilityLevel(WebGL)", () => PlayerSettings.GetApiCompatibilityLevel(nt), v => PlayerSettings.SetApiCompatibilityLevel(nt, v), ApiCompatibilityLevel.NET_Standard);
            log.Set("ManagedStrippingLevel(WebGL)", () => PlayerSettings.GetManagedStrippingLevel(nt), v => PlayerSettings.SetManagedStrippingLevel(nt, v), ManagedStrippingLevel.High);
            log.Set("Il2CppCodeGeneration(WebGL)", () => PlayerSettings.GetIl2CppCodeGeneration(nt), v => PlayerSettings.SetIl2CppCodeGeneration(nt, v), Il2CppCodeGeneration.OptimizeSize);
            log.Set("Il2CppCompilerConfiguration(WebGL)", () => PlayerSettings.GetIl2CppCompilerConfiguration(nt), v => PlayerSettings.SetIl2CppCompilerConfiguration(nt, v), Il2CppCompilerConfiguration.Release);
            log.Set("stripEngineCode", () => PlayerSettings.stripEngineCode, v => PlayerSettings.stripEngineCode = v, true);
            log.Set("stripUnusedMeshComponents", () => PlayerSettings.stripUnusedMeshComponents, v => PlayerSettings.stripUnusedMeshComponents = v, true);
            log.Set("colorSpace", () => PlayerSettings.colorSpace, v => PlayerSettings.colorSpace = v, ColorSpace.Linear);
            log.Set("UseDefaultGraphicsAPIs(WebGL)", () => PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL), v => PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, v), true);
            log.Note("GraphicsAPIs(WebGL) = " + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL)) + " (WebGL2 only for v1, webgl.md §4.1)");
            log.Set("runInBackground", () => PlayerSettings.runInBackground, v => PlayerSettings.runInBackground = v, false);
            log.Set("gcIncremental", () => PlayerSettings.gcIncremental, v => PlayerSettings.gcIncremental = v, true);
            log.Set("SplashScreen.show", () => PlayerSettings.SplashScreen.show, v => PlayerSettings.SplashScreen.show = v, false);
            log.Set("companyName", () => PlayerSettings.companyName, v => PlayerSettings.companyName = v, "LongShadow");
            log.Set("productName", () => PlayerSettings.productName, v => PlayerSettings.productName = v, "Operation Long Shadow");

            log.Set("WebGL.compressionFormat", () => PlayerSettings.WebGL.compressionFormat, v => PlayerSettings.WebGL.compressionFormat = v, WebGLCompressionFormat.Brotli);
            log.Set("WebGL.decompressionFallback", () => PlayerSettings.WebGL.decompressionFallback, v => PlayerSettings.WebGL.decompressionFallback = v, true);
            log.Set("WebGL.dataCaching", () => PlayerSettings.WebGL.dataCaching, v => PlayerSettings.WebGL.dataCaching = v, true);
            log.Set("WebGL.nameFilesAsHashes", () => PlayerSettings.WebGL.nameFilesAsHashes, v => PlayerSettings.WebGL.nameFilesAsHashes = v, false);
            log.Set("WebGL.exceptionSupport", () => PlayerSettings.WebGL.exceptionSupport, v => PlayerSettings.WebGL.exceptionSupport = v, WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly);
            log.Set("WebGL.wasm2023", () => PlayerSettings.WebGL.wasm2023, v => PlayerSettings.WebGL.wasm2023 = v, true);
            log.Set("WebGL.threadsSupport", () => PlayerSettings.WebGL.threadsSupport, v => PlayerSettings.WebGL.threadsSupport = v, false);
            log.Set("WebGL.initialMemorySize", () => PlayerSettings.WebGL.initialMemorySize, v => PlayerSettings.WebGL.initialMemorySize = v, 256);
            log.Set("WebGL.maximumMemorySize", () => PlayerSettings.WebGL.maximumMemorySize, v => PlayerSettings.WebGL.maximumMemorySize = v, 4096);
            log.Set("WebGL.memoryGrowthMode", () => PlayerSettings.WebGL.memoryGrowthMode, v => PlayerSettings.WebGL.memoryGrowthMode = v, WebGLMemoryGrowthMode.Geometric);
            log.Set("WebGL.powerPreference", () => PlayerSettings.WebGL.powerPreference, v => PlayerSettings.WebGL.powerPreference = v, WebGLPowerPreference.HighPerformance);
            log.Set("WebGL.debugSymbolMode", () => PlayerSettings.WebGL.debugSymbolMode, v => PlayerSettings.WebGL.debugSymbolMode = v, WebGLDebugSymbolMode.Off);
            log.Set("WebGL.showDiagnostics", () => PlayerSettings.WebGL.showDiagnostics, v => PlayerSettings.WebGL.showDiagnostics = v, false);
            log.Set("WebGL.progressiveAssetLoading", () => PlayerSettings.WebGL.progressiveAssetLoading, v => PlayerSettings.WebGL.progressiveAssetLoading = v, false);
            if (File.Exists(TemplateIndexPath))
                log.Set("WebGL.template", () => PlayerSettings.WebGL.template, v => PlayerSettings.WebGL.template = v, "PROJECT:CovertOps");
            else
                log.Note("WebGL.template stays '" + PlayerSettings.WebGL.template + "': " + TemplateIndexPath + " does not exist yet (WP01 creates it; re-run this script after).");
            log.Set("EditorUserBuildSettings.webGLBuildSubtarget", () => EditorUserBuildSettings.webGLBuildSubtarget, v => EditorUserBuildSettings.webGLBuildSubtarget = v, WebGLTextureSubtarget.DXT);
            log.Note("Active build target = " + EditorUserBuildSettings.activeBuildTarget + (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL ? " (ok)" : " (expected WebGL; not switched automatically)"));

            var ps = PlayerSettingsObject();
            if (ps != null)
            {
                var so = new SerializedObject(ps);
                log.SetProp(so, "activeInputHandler", 1, "PlayerSettings");
            }
        }

        private static UnityEngine.Object PlayerSettingsObject()
        {
            var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            return all != null && all.Length > 0 ? all[0] : null;
        }

        private static void Encodings(SettingsLog log)
        {
            var ps = PlayerSettingsObject();
            if (ps == null)
            {
                log.Problem("ProjectSettings.asset not loadable");
                return;
            }
            var so = new SerializedObject(ps);
            // LightmapEncodingQuality / HDRCubemapEncodingQuality: Low = 0, Normal = 1, High = 2 (D16: RGBM DXT5).
            SetPerTargetEncoding(log, so, "m_BuildTargetGroupLightmapEncodingQuality", "WebGL", 1);
            SetPerTargetEncoding(log, so, "m_BuildTargetGroupHDRCubemapEncodingQuality", "WebGL", 1);
        }

        private static void SetPerTargetEncoding(SettingsLog log, SerializedObject so, string arrayPath, string target, int quality)
        {
            so.Update();
            var arr = so.FindProperty(arrayPath);
            if (arr == null || !arr.isArray)
            {
                log.Problem(arrayPath + " not found");
                return;
            }
            SerializedProperty el = null;
            for (var i = 0; i < arr.arraySize; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var t = e.FindPropertyRelative("m_BuildTarget");
                if (t != null && t.stringValue == target) el = e;
            }
            if (el == null)
            {
                if (log.DryRun)
                {
                    log.Step(arrayPath + "[" + target + "] add entry with quality " + quality, true);
                    return;
                }
                arr.InsertArrayElementAtIndex(arr.arraySize);
                el = arr.GetArrayElementAtIndex(arr.arraySize - 1);
                var bt = el.FindPropertyRelative("m_BuildTarget");
                if (bt == null)
                {
                    log.Problem(arrayPath + ": element has no m_BuildTarget");
                    return;
                }
                bt.stringValue = target;
                var sv = el.FindPropertyRelative("serializedVersion");
                if (sv != null) sv.intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Step(arrayPath + "[" + target + "] entry added", true);
            }
            var q = el.FindPropertyRelative("m_EncodingQuality");
            if (q == null)
            {
                log.Problem(arrayPath + ": element has no m_EncodingQuality");
                return;
            }
            log.SetProp(q, quality, arrayPath + "[" + target + "].m_EncodingQuality");
        }

        private static void EditorSide(SettingsLog log)
        {
            log.Set("EditorSettings.enterPlayModeOptionsEnabled", () => EditorSettings.enterPlayModeOptionsEnabled, v => EditorSettings.enterPlayModeOptionsEnabled = v, true);
            log.Set("EditorSettings.enterPlayModeOptions", () => EditorSettings.enterPlayModeOptions, v => EditorSettings.enterPlayModeOptions = v, EnterPlayModeOptions.DisableDomainReload);
            log.Set("EditorSettings.serializationMode", () => EditorSettings.serializationMode, v => EditorSettings.serializationMode = v, SerializationMode.ForceText);
            log.Set("VersionControlSettings.mode", () => VersionControlSettings.mode, v => VersionControlSettings.mode = v, "Visible Meta Files");
        }

        // ------------------------------------------------------------------ 3.2
        private static void Graphics(SettingsLog log)
        {
            var gs = GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(gs);
            // Lightmap modes: Custom, keep Baked Non-Directional (plain + dynamic plain) only.
            log.SetProp(so, "m_LightmapStripping", 1, "GraphicsSettings");
            log.SetProp(so, "m_LightmapKeepPlain", true, "GraphicsSettings");
            log.SetProp(so, "m_LightmapKeepDirCombined", false, "GraphicsSettings");
            log.SetProp(so, "m_LightmapKeepDynamicPlain", true, "GraphicsSettings");
            log.SetProp(so, "m_LightmapKeepDynamicDirCombined", false, "GraphicsSettings");
            log.SetProp(so, "m_LightmapKeepShadowMask", false, "GraphicsSettings");
            log.SetProp(so, "m_LightmapKeepSubtractive", false, "GraphicsSettings");
            // Fog: Custom, Exponential Squared only.
            log.SetProp(so, "m_FogStripping", 1, "GraphicsSettings");
            log.SetProp(so, "m_FogKeepLinear", false, "GraphicsSettings");
            log.SetProp(so, "m_FogKeepExp", false, "GraphicsSettings");
            log.SetProp(so, "m_FogKeepExp2", true, "GraphicsSettings");
            // Instancing: Strip Unused (1). BatchRendererGroup: Strip All (UnityEditor.Rendering.BatchRendererGroupStrippingMode.StripAll = 1).
            log.SetProp(so, "m_InstancingStripping", 1, "GraphicsSettings");
            log.SetProp(so, "m_BrgStripping", 1, "GraphicsSettings");
            log.Note("Always-included shaders left as is (trimming is a WP11 audit item, §3.2).");

            if (GraphicsSettings.TryGetRenderPipelineSettings<URPShaderStrippingSetting>(out var urpStrip))
            {
                log.Set("URPShaderStrippingSetting.stripUnusedPostProcessingVariants", () => urpStrip.stripUnusedPostProcessingVariants, v => urpStrip.stripUnusedPostProcessingVariants = v, true);
                log.Set("URPShaderStrippingSetting.stripUnusedVariants", () => urpStrip.stripUnusedVariants, v => urpStrip.stripUnusedVariants = v, true);
            }
            else log.Problem("URPShaderStrippingSetting not found in Graphics Settings");
            if (GraphicsSettings.TryGetRenderPipelineSettings<ShaderStrippingSetting>(out var coreStrip))
                log.Set("ShaderStrippingSetting.stripRuntimeDebugShaders", () => coreStrip.stripRuntimeDebugShaders, v => coreStrip.stripRuntimeDebugShaders = v, true);
            else log.Problem("ShaderStrippingSetting not found in Graphics Settings");
            log.Note("Film grain textures moved from PostProcessData to URP Graphics Settings (UniversalRenderPipelineFilmGrainResources) in URP 17.6 and cannot be trimmed there; see DECISIONS.md.");
        }

        // ------------------------------------------------------------------ 3.3
        private static void Quality(SettingsLog log)
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qs == null || qs.Length == 0)
            {
                log.Problem("QualitySettings.asset not loadable");
                return;
            }
            var so = new SerializedObject(qs[0]);
            var levels = so.FindProperty("m_QualitySettings");
            var webIndex = -1;
            for (var i = 0; i < levels.arraySize; i++)
            {
                var n = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (n == "PC" || n == "Web_High") webIndex = i;
            }
            if (webIndex < 0)
            {
                log.Problem("No 'PC' or 'Web_High' quality level");
                return;
            }
            var lvl = levels.GetArrayElementAtIndex(webIndex);
            string L(string p) => "m_QualitySettings[" + webIndex + "]." + p;
            log.SetProp(lvl.FindPropertyRelative("name"), "Web_High", L("name"));
            log.SetProp(lvl.FindPropertyRelative("globalTextureMipmapLimit"), 0, L("globalTextureMipmapLimit"));
            log.SetProp(lvl.FindPropertyRelative("anisotropicTextures"), 1, L("anisotropicTextures (1 = Per Texture)"));
            log.SetProp(lvl.FindPropertyRelative("lodBias"), 1.25f, L("lodBias"));
            log.SetProp(lvl.FindPropertyRelative("maximumLODLevel"), 0, L("maximumLODLevel"));
            log.SetProp(lvl.FindPropertyRelative("enableLODCrossFade"), true, L("enableLODCrossFade"));
            log.SetProp(lvl.FindPropertyRelative("skinWeights"), 4, L("skinWeights"));
            log.SetProp(lvl.FindPropertyRelative("realtimeReflectionProbes"), false, L("realtimeReflectionProbes"));
            log.SetProp(lvl.FindPropertyRelative("billboardsFaceCameraPosition"), true, L("billboardsFaceCameraPosition"));
            log.SetProp(lvl.FindPropertyRelative("vSyncCount"), 0, L("vSyncCount"));
            log.SetProp(lvl.FindPropertyRelative("asyncUploadTimeSlice"), 4, L("asyncUploadTimeSlice"));
            log.SetProp(lvl.FindPropertyRelative("asyncUploadBufferSize"), 64, L("asyncUploadBufferSize"));
            log.SetProp(lvl.FindPropertyRelative("streamingMipmapsActive"), false, L("streamingMipmapsActive"));
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(RpAssetPath);
            if (rp != null) log.SetProp(lvl.FindPropertyRelative("customRenderPipeline"), rp, L("customRenderPipeline"));
            // WebGL default quality = this level.
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null)
            {
                for (var i = 0; i < perPlatform.arraySize; i++)
                {
                    var e = perPlatform.GetArrayElementAtIndex(i);
                    var key = e.FindPropertyRelative("first");
                    if (key != null && key.stringValue == "WebGL")
                        log.SetProp(e.FindPropertyRelative("second"), webIndex, "m_PerPlatformDefaultQuality[WebGL]");
                }
            }
            log.Note("Quality levels: " + string.Join(", ", QualitySettings.names) + " ('Mobile' stays excluded for WebGL; Performance mode is applied at runtime by QualityApplier, §3.3).");
            log.Note("Application.targetFrameRate = -1 is a runtime call (WP01 Bootstrap).");
        }

        // ------------------------------------------------------------------ 3.4
        private static void UrpAsset(SettingsLog log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
            if (asset == null)
            {
                log.Problem(RpAssetPath + " missing");
                return;
            }
            var so = new SerializedObject(asset);
            const string n = "PC_RPAsset";
            log.SetProp(so, "m_SupportsHDR", true, n);
            log.SetProp(so, "m_HDRColorBufferPrecision", 0, n);          // 32-bit (R11G11B10)
            log.SetProp(so, "m_ColorGradingMode", 1, n);                 // HDR
            log.SetProp(so, "m_ColorGradingLutSize", 32, n);
            log.SetProp(so, "m_MSAA", 1, n);                              // off (SMAA High on the camera)
            log.SetProp(so, "m_RenderScale", 1f, n);
            log.SetProp(so, "m_UpscalingFilter", (int)UpscalingFilterSelection.Linear, n); // Bilinear (no FSR on WebGL2)
            log.SetProp(so, "m_RequireDepthTexture", true, n);
            log.SetProp(so, "m_RequireOpaqueTexture", false, n);
            log.SetProp(so, "m_MainLightRenderingMode", 1, n);           // per pixel
            log.SetProp(so, "m_MainLightShadowsSupported", true, n);
            log.SetProp(so, "m_MainLightShadowmapResolution", 2048, n);
            log.SetProp(so, "m_ShadowDistance", 80f, n);
            log.SetProp(so, "m_ShadowCascadeCount", 4, n);
            log.SetProp(so, "m_Cascade4Split", new Vector3(0.0625f, 0.1875f, 0.4375f), n);
            log.SetProp(so, "m_CascadeBorder", 0.1f, n);
            log.SetProp(so, "m_SoftShadowsSupported", true, n);
            log.SetProp(so, "m_SoftShadowQuality", 2, n);                // Medium
            log.SetProp(so, "m_AdditionalLightsRenderingMode", 1, n);   // per pixel
            log.SetProp(so, "m_AdditionalLightShadowsSupported", true, n);
            log.SetProp(so, "m_AdditionalLightsShadowmapResolution", 2048, n);
            log.SetProp(so, "m_SupportsLightLayers", true, n);
            log.SetProp(so, "m_SupportsLightCookies", false, n);
            log.SetProp(so, "m_MixedLightingSupported", true, n);
            log.SetProp(so, "m_ReflectionProbeBlending", true, n);
            log.SetProp(so, "m_ReflectionProbeBoxProjection", true, n);
            log.SetProp(so, "m_ReflectionProbeAtlas", true, n);
            log.SetProp(so, "m_LightProbeSystem", 0, n);                 // Light Probe Groups (no APV on WebGL2)
            log.SetProp(so, "m_EnableLODCrossFade", true, n);
            log.SetProp(so, "m_UseSRPBatcher", true, n);
            log.SetProp(so, "m_SupportsDynamicBatching", false, n);
            log.SetProp(so, "m_GPUResidentDrawerMode", 0, n);
            log.SetProp(so, "m_UseAdaptivePerformance", false, n);
            log.SetProp(so, "m_VolumeFrameworkUpdateMode", 0, n);        // every frame
            log.Note("Shadow depth/normal bias per LightStage stage is runtime data (LightStageTable, WP10).");

            // Renderer list: [0] PC_Renderer, [1] PC_Renderer_RT.
            var main = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            var rt = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererRtPath);
            var list = so.FindProperty("m_RendererDataList");
            so.Update();
            if (rt == null && log.DryRun && main != null && list != null)
            {
                log.Step("PC_RPAsset.m_RendererDataList -> [PC_Renderer, PC_Renderer_RT] (after " + RendererRtPath + " is created)", true);
                return;
            }
            if (main == null || rt == null || list == null)
            {
                log.Problem("renderer list: missing " + (main == null ? RendererPath : rt == null ? RendererRtPath : "m_RendererDataList"));
                return;
            }
            var ok = list.arraySize == 2 && list.GetArrayElementAtIndex(0).objectReferenceValue == main &&
                     list.GetArrayElementAtIndex(1).objectReferenceValue == rt;
            if (ok)
            {
                log.Step("PC_RPAsset.m_RendererDataList = [PC_Renderer, PC_Renderer_RT]", false);
            }
            else if (log.DryRun)
            {
                log.Step("PC_RPAsset.m_RendererDataList -> [PC_Renderer, PC_Renderer_RT]", true);
            }
            else
            {
                list.arraySize = 2;
                list.GetArrayElementAtIndex(0).objectReferenceValue = main;
                list.GetArrayElementAtIndex(1).objectReferenceValue = rt;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Step("PC_RPAsset.m_RendererDataList set to [PC_Renderer, PC_Renderer_RT]", true);
            }
            log.SetProp(so, "m_DefaultRendererIndex", 0, n);
            if (!log.DryRun) EditorUtility.SetDirty(asset);
        }

        // ------------------------------------------------------------------ 3.5
        private static void Renderers(SettingsLog log)
        {
            EnsureFolder("Assets/_Game/Settings/URP");
            var main = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (main == null)
            {
                log.Problem(RendererPath + " missing");
                return;
            }

            // PostProcessData_Web: project-owned copy of URP's default PostProcessData.
            var ppd = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            if (ppd == null)
            {
                var src = AssetDatabase.GUIDToAssetPath(UrpPostProcessDataGuid);
                if (string.IsNullOrEmpty(src)) log.Problem("URP default PostProcessData not found (guid " + UrpPostProcessDataGuid + ")");
                else if (log.DryRun) log.Step("create " + PostProcessDataPath + " from " + src, true);
                else
                {
                    var copied = AssetDatabase.CopyAsset(src, PostProcessDataPath);
                    ppd = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                    log.Step("created " + PostProcessDataPath + " from " + src + (copied ? "" : " (FAILED)"), true);
                }
            }
            else log.Step(PostProcessDataPath + " exists", false);

            // PC_Renderer_RT: copy of PC_Renderer, then every feature removed.
            var rt = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererRtPath);
            if (rt == null)
            {
                if (log.DryRun) log.Step("create " + RendererRtPath + " (copy of PC_Renderer without features)", true);
                else
                {
                    AssetDatabase.CopyAsset(RendererPath, RendererRtPath);
                    rt = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererRtPath);
                    log.Step("created " + RendererRtPath, true);
                }
            }
            else log.Step(RendererRtPath + " exists", false);
            if (rt != null) RemoveAllFeatures(log, rt);

            // Main renderer settings.
            var so = new SerializedObject(main);
            log.SetProp(so, "m_RenderingMode", (int)RenderingMode.ForwardPlus, "PC_Renderer");
            log.SetProp(so, "m_DepthPrimingMode", 0, "PC_Renderer");
            if (ppd != null) log.SetProp(so, "postProcessData", ppd, "PC_Renderer");

            // SSAO: Depth source, Medium normals, downsample, Medium samples/blur, intensity 0.6, radius 0.35, direct 0.25.
            var ssao = FindFeature(main, "ScreenSpaceAmbientOcclusion");
            if (ssao == null) log.Problem("PC_Renderer has no ScreenSpaceAmbientOcclusion feature");
            else
            {
                var fso = new SerializedObject(ssao);
                const string f = "PC_Renderer/SSAO";
                log.SetProp(fso, "m_Settings.Source", 0, f);
                log.SetProp(fso, "m_Settings.NormalSamples", 1, f);
                log.SetProp(fso, "m_Settings.Downsample", true, f);
                log.SetProp(fso, "m_Settings.Samples", 1, f);
                log.SetProp(fso, "m_Settings.BlurQuality", 1, f);
                log.SetProp(fso, "m_Settings.Intensity", 0.6f, f);
                log.SetProp(fso, "m_Settings.Radius", 0.35f, f);
                log.SetProp(fso, "m_Settings.DirectLightingStrength", 0.25f, f);
                log.Set("PC_Renderer/SSAO.isActive", () => ssao.isActive, v => ssao.SetActive(v), true);
            }

            // Decal: Screen Space, normal blend Low, max draw distance 60 m.
            var decal = FindFeature(main, "DecalRendererFeature") ?? AddFeature<DecalRendererFeature>(log, main, "Decal");
            if (decal != null)
            {
                var dso = new SerializedObject(decal);
                const string f = "PC_Renderer/Decal";
                log.SetProp(dso, "m_Settings.technique", 2, f);              // ScreenSpace
                log.SetProp(dso, "m_Settings.maxDrawDistance", 60f, f);
                log.SetProp(dso, "m_Settings.screenSpaceSettings.normalBlend", 0, f); // Low
                log.SetProp(dso, "m_Settings.decalLayers", false, f);
            }

            // FullScreenPass "Glitch": After Post Processing, requires Color, inactive (WP09 sets passMaterial at runtime).
            var glitch = FindFeature(main, "FullScreenPassRendererFeature", "Glitch") ?? AddFeature<FullScreenPassRendererFeature>(log, main, "Glitch");
            if (glitch is FullScreenPassRendererFeature fs)
            {
                log.Set("PC_Renderer/Glitch.injectionPoint", () => fs.injectionPoint, v => fs.injectionPoint = v, FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing);
                log.Set("PC_Renderer/Glitch.requirements", () => fs.requirements, v => fs.requirements = v, ScriptableRenderPassInput.Color);
                log.Set("PC_Renderer/Glitch.fetchColorBuffer", () => fs.fetchColorBuffer, v => fs.fetchColorBuffer = v, true);
                log.Set("PC_Renderer/Glitch.isActive", () => fs.isActive, v => fs.SetActive(v), false);
                if (!log.DryRun) EditorUtility.SetDirty(fs);
            }

            // RT renderer: Forward+, same post-process data, no features.
            if (rt != null)
            {
                var rso = new SerializedObject(rt);
                log.SetProp(rso, "m_RenderingMode", (int)RenderingMode.ForwardPlus, "PC_Renderer_RT");
                log.SetProp(rso, "m_DepthPrimingMode", 0, "PC_Renderer_RT");
                if (ppd != null) log.SetProp(rso, "postProcessData", ppd, "PC_Renderer_RT");
                if (!log.DryRun) EditorUtility.SetDirty(rt);
            }
            if (!log.DryRun) EditorUtility.SetDirty(main);
        }

        private static ScriptableRendererFeature FindFeature(ScriptableRendererData data, string typeName, string name = null)
        {
            foreach (var f in data.rendererFeatures)
            {
                if (f == null) continue;
                if (f.GetType().Name == typeName && (name == null || f.name == name)) return f;
            }
            return null;
        }

        private static T AddFeature<T>(SettingsLog log, ScriptableRendererData data, string name) where T : ScriptableRendererFeature
        {
            if (log.DryRun)
            {
                log.Step(data.name + ": add " + typeof(T).Name + " '" + name + "'", true);
                return null;
            }
            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, data);
            var so = new SerializedObject(data);
            so.Update();
            var feats = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            feats.arraySize++;
            feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = feature;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId) && map != null)
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            log.Step(data.name + ": added " + typeof(T).Name + " '" + name + "'", true);
            return feature;
        }

        private static void RemoveAllFeatures(SettingsLog log, ScriptableRendererData data)
        {
            var count = data.rendererFeatures.Count;
            var subs = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(data));
            var subFeatures = new List<ScriptableRendererFeature>();
            foreach (var s in subs)
                if (s is ScriptableRendererFeature f) subFeatures.Add(f);
            if (count == 0 && subFeatures.Count == 0)
            {
                log.Step(data.name + ": no renderer features", false);
                return;
            }
            if (log.DryRun)
            {
                log.Step(data.name + ": remove " + Math.Max(count, subFeatures.Count) + " renderer feature(s)", true);
                return;
            }
            var so = new SerializedObject(data);
            so.Update();
            so.FindProperty("m_RendererFeatures").arraySize = 0;
            var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null) map.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var f in subFeatures) UnityEngine.Object.DestroyImmediate(f, true);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            log.Step(data.name + ": removed " + subFeatures.Count + " renderer feature(s)", true);
        }

        // ------------------------------------------------------------------ 3.8 / 3.9
        private static void LayersAndTags(SettingsLog log)
        {
            var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tm == null || tm.Length == 0)
            {
                log.Problem("TagManager.asset not loadable");
                return;
            }
            var so = new SerializedObject(tm[0]);
            var layers = so.FindProperty("layers");
            for (var i = 6; i < LayerIds.Names.Length; i++)
                log.SetProp(layers.GetArrayElementAtIndex(i), LayerIds.Names[i], "layers[" + i + "]");
            var rl = so.FindProperty("m_RenderingLayers");
            if (rl == null) log.Problem("TagManager.m_RenderingLayers not found");
            else
            {
                for (var i = 1; i < RenderingLayerIds.Names.Length; i++)
                {
                    if (rl.arraySize <= i)
                    {
                        log.Problem("m_RenderingLayers has only " + rl.arraySize + " entries");
                        break;
                    }
                    log.SetProp(rl.GetArrayElementAtIndex(i), RenderingLayerIds.Names[i], "renderingLayers[" + i + "]");
                }
                log.Note("Rendering layer 0 keeps Unity's name 'Default' and is used as World (RenderingLayerIds.World).");
            }
            var tags = so.FindProperty("tags");
            log.Note("Custom tags: " + (tags != null ? tags.arraySize.ToString() : "?") + " (only built-in Player and MainCamera are used, §3.9).");
        }

        /// <summary>Collision pairs of TECH_PLAN §3.9 (symmetric). Built-in layers 1, 2, 4 behave like Default.</summary>
        public static bool ShouldCollide(int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            bool Is(int x, params int[] set) => Array.IndexOf(set, x) >= 0;
            int D = LayerIds.Default, P = LayerIds.Player, Ch = LayerIds.Character, Rg = LayerIds.Ragdoll, Pr = LayerIds.Projectile,
                Db = LayerIds.Debris, Tr = LayerIds.Trigger, Pc = LayerIds.PlayerClip, V = LayerIds.Vehicle;
            int Norm(int x) => x == 1 || x == 2 || x == 4 ? D : x;
            var na = Norm(a);
            var nb = Norm(b);
            if (na > nb) (na, nb) = (nb, na);
            var ours = Is(a, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20) || Is(b, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20);
            if (!ours) return true; // pairs among built-in / unnamed layers keep Unity's default
            if (a == 3 || a == 5 || a >= 21 || b == 3 || b == 5 || b >= 21) return false;
            switch (na)
            {
                case 0: return Is(nb, D, P, Ch, Rg, Pr, Db, V);
                case 6: return Is(nb, Ch, V, Pc, Tr);
                case 10: return Is(nb, Ch, V, Pr, Tr);
                case 11: return Is(nb, Rg, Db, V, Pr);
                case 12: return Is(nb, V, Db);
                case 13: return Is(nb, Db);
                default: return false;
            }
        }

        private static void Physics(SettingsLog log)
        {
            var dm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if (dm == null || dm.Length == 0)
            {
                log.Problem("DynamicsManager.asset not loadable");
                return;
            }
            var so = new SerializedObject(dm[0]);
            log.SetProp(so, "m_QueriesHitTriggers", false, "Physics");
            log.SetProp(so, "m_QueriesHitBackfaces", false, "Physics");
            log.SetProp(so, "m_AutoSyncTransforms", false, "Physics");
            log.SetProp(so, "m_DefaultContactOffset", 0.01f, "Physics");
            var changed = 0;
            for (var a = 0; a < 32; a++)
            for (var b = a; b < 32; b++)
            {
                var want = ShouldCollide(a, b);
                var have = !UnityEngine.Physics.GetIgnoreLayerCollision(a, b);
                if (want == have) continue;
                changed++;
                if (!log.DryRun) UnityEngine.Physics.IgnoreLayerCollision(a, b, !want);
            }
            log.Step("layer collision matrix: " + changed + " pair(s) " + (log.DryRun ? "differ" : "changed"), changed > 0);
            if (!log.DryRun && changed > 0) EditorUtility.SetDirty(dm[0]);
            var mismatch = 0;
            for (var a = 0; a < 32; a++)
            for (var b = a; b < 32; b++)
                if (ShouldCollide(a, b) == UnityEngine.Physics.GetIgnoreLayerCollision(a, b)) mismatch++;
            if (!log.DryRun && mismatch > 0) log.Problem("collision matrix still differs in " + mismatch + " pair(s)");
            log.Note("Matrix (ours): Player-{Default,Character,Vehicle,PlayerClip,Trigger}; Character-{Default,Player,Character,Vehicle,Projectile,Trigger}; " +
                     "Ragdoll-{Default,Vehicle,Ragdoll,Debris,Projectile}; Projectile-{Default,Vehicle,Character,Ragdoll,Debris}; Debris-{Default,Ragdoll,Debris,Projectile}; " +
                     "Vehicle-{Default,Player,Character,Ragdoll,Projectile}; Viewmodel/Hitbox/SquadHitbox/Interactable/SmokeLOS/FarProxy/Booth-none.");
        }

        private static void TimeAndAudio(SettingsLog log)
        {
            var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset");
            if (tm != null && tm.Length > 0)
            {
                var so = new SerializedObject(tm[0]);
                // "Fixed Timestep" is a RationalTime (m_Count / m_Rate) in Unity 6: go through the API, not the property.
                log.Set("Time.fixedDeltaTime", () => Time.fixedDeltaTime, v =>
                {
                    Time.fixedDeltaTime = v;
                    EditorUtility.SetDirty(tm[0]);
                }, 0.02f);
                so.Update();
                log.SetProp(so, "Maximum Allowed Timestep", 0.1f, "Time");
            }
            else log.Problem("TimeManager.asset not loadable");
            var am = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (am != null && am.Length > 0)
            {
                var so = new SerializedObject(am[0]);
                log.SetProp(so, "m_RealVoiceCount", 32, "Audio");
                log.SetProp(so, "m_VirtualVoiceCount", 64, "Audio");
                log.SetProp(so, "Default Speaker Mode", 2, "Audio"); // Stereo
                log.SetProp(so, "m_SpatializerPlugin", "", "Audio");
            }
            else log.Problem("AudioManager.asset not loadable");
        }

        // ------------------------------------------------------------------ 7.2
        private static void InputActions(SettingsLog log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                log.Problem(InputActionsPath + " missing (import it first)");
                return;
            }
            log.Set("InputSystem.actions (project-wide)", () => InputSystem.actions, v => InputSystem.actions = v, asset);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
