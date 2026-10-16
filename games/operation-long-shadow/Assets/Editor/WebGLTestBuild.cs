using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace FunUnity.EditorTools
{
    /// <summary>
    /// WebGL feasibility build. Applies the web player settings used for the test,
    /// builds Assets/Scenes/WebGLTest.unity only (via BuildPlayerOptions.scenes, Build Settings untouched)
    /// and writes a plain-text report next to the build folder.
    /// Run from the menu (Tools/Web/Build WebGL Test) or schedule via EditorApplication.delayCall.
    /// </summary>
    public static class WebGLTestBuild
    {
        public const string ScenePath = "Assets/Scenes/WebGLTest.unity";
        public const string ReportSuffix = "_report.txt";

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string DefaultOutput => Path.Combine(ProjectRoot, "Builds", "WebGLTest");

        [MenuItem("Tools/Web/Build WebGL Test")]
        public static void BuildFromMenu() => Build(DefaultOutput);

        public static string DescribeWebSettings()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PlayerSettings.WebGL.compressionFormat=" + PlayerSettings.WebGL.compressionFormat);
            sb.AppendLine("PlayerSettings.WebGL.decompressionFallback=" + PlayerSettings.WebGL.decompressionFallback);
            sb.AppendLine("PlayerSettings.WebGL.dataCaching=" + PlayerSettings.WebGL.dataCaching);
            sb.AppendLine("PlayerSettings.WebGL.nameFilesAsHashes=" + PlayerSettings.WebGL.nameFilesAsHashes);
            sb.AppendLine("PlayerSettings.WebGL.exceptionSupport=" + PlayerSettings.WebGL.exceptionSupport);
            sb.AppendLine("PlayerSettings.WebGL.linkerTarget=" + PlayerSettings.WebGL.linkerTarget);
            sb.AppendLine("PlayerSettings.WebGL.threadsSupport=" + PlayerSettings.WebGL.threadsSupport);
            sb.AppendLine("PlayerSettings.WebGL.initialMemorySize=" + PlayerSettings.WebGL.initialMemorySize);
            sb.AppendLine("PlayerSettings.WebGL.maximumMemorySize=" + PlayerSettings.WebGL.maximumMemorySize);
            sb.AppendLine("PlayerSettings.WebGL.memoryGrowthMode=" + PlayerSettings.WebGL.memoryGrowthMode);
            sb.AppendLine("PlayerSettings.WebGL.geometricMemoryGrowthStep=" + PlayerSettings.WebGL.geometricMemoryGrowthStep);
            sb.AppendLine("PlayerSettings.WebGL.memoryGeometricGrowthCap=" + PlayerSettings.WebGL.memoryGeometricGrowthCap);
            sb.AppendLine("PlayerSettings.WebGL.linearMemoryGrowthStep=" + PlayerSettings.WebGL.linearMemoryGrowthStep);
            sb.AppendLine("PlayerSettings.WebGL.powerPreference=" + PlayerSettings.WebGL.powerPreference);
            sb.AppendLine("PlayerSettings.WebGL.showDiagnostics=" + PlayerSettings.WebGL.showDiagnostics);
            sb.AppendLine("PlayerSettings.WebGL.debugSymbolMode=" + PlayerSettings.WebGL.debugSymbolMode);
            sb.AppendLine("PlayerSettings.WebGL.template=" + PlayerSettings.WebGL.template);
            sb.AppendLine("PlayerSettings.WebGL.analyzeBuildSize=" + PlayerSettings.WebGL.analyzeBuildSize);
            var nt = NamedBuildTarget.WebGL;
            sb.AppendLine("ScriptingBackend=" + PlayerSettings.GetScriptingBackend(nt));
            sb.AppendLine("ManagedStrippingLevel=" + PlayerSettings.GetManagedStrippingLevel(nt));
            sb.AppendLine("Il2CppCodeGeneration=" + PlayerSettings.GetIl2CppCodeGeneration(nt));
            sb.AppendLine("Il2CppCompilerConfiguration=" + PlayerSettings.GetIl2CppCompilerConfiguration(nt));
            sb.AppendLine("ApiCompatibilityLevel=" + PlayerSettings.GetApiCompatibilityLevel(nt));
            sb.AppendLine("StripEngineCode=" + PlayerSettings.stripEngineCode);
            sb.AppendLine("ColorSpace=" + PlayerSettings.colorSpace);
            sb.AppendLine("UseDefaultGraphicsAPIs(WebGL)=" + PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL));
            sb.AppendLine("GraphicsAPIs(WebGL)=" + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL)));
            sb.AppendLine("RunInBackground=" + PlayerSettings.runInBackground);
            sb.AppendLine("DefaultWebScreen=" + PlayerSettings.defaultWebScreenWidth + "x" + PlayerSettings.defaultWebScreenHeight);
            sb.AppendLine("EditorUserBuildSettings.webGLBuildSubtarget=" + EditorUserBuildSettings.webGLBuildSubtarget);
            sb.AppendLine("ActiveBuildTarget=" + EditorUserBuildSettings.activeBuildTarget);
            sb.AppendLine("QualityLevels=" + string.Join(",", QualitySettings.names));
            sb.AppendLine("WebGL active quality levels=" + string.Join(",", QualitySettings.GetActiveQualityLevelsForPlatform("WebGL")));
            sb.AppendLine("DefaultRenderPipeline=" + GraphicsSettings.defaultRenderPipeline);
            return sb.ToString();
        }

        /// <summary>Applies the WebGL test settings. Returns a log of every field set (before -> after).</summary>
        public static string ApplyTestSettings()
        {
            var sb = new StringBuilder();
            void Set<T>(string name, Func<T> get, Action<T> set, T value)
            {
                T before = get();
                set(value);
                sb.AppendLine($"SET {name}: {before} -> {get()}");
            }

            Set("PlayerSettings.WebGL.compressionFormat", () => PlayerSettings.WebGL.compressionFormat, v => PlayerSettings.WebGL.compressionFormat = v, WebGLCompressionFormat.Brotli);
            Set("PlayerSettings.WebGL.decompressionFallback", () => PlayerSettings.WebGL.decompressionFallback, v => PlayerSettings.WebGL.decompressionFallback = v, true);
            Set("PlayerSettings.WebGL.dataCaching", () => PlayerSettings.WebGL.dataCaching, v => PlayerSettings.WebGL.dataCaching = v, true);
            Set("PlayerSettings.WebGL.nameFilesAsHashes", () => PlayerSettings.WebGL.nameFilesAsHashes, v => PlayerSettings.WebGL.nameFilesAsHashes = v, false);
            Set("PlayerSettings.WebGL.showDiagnostics", () => PlayerSettings.WebGL.showDiagnostics, v => PlayerSettings.WebGL.showDiagnostics = v, false);
            Set("PlayerSettings.WebGL.powerPreference", () => PlayerSettings.WebGL.powerPreference, v => PlayerSettings.WebGL.powerPreference = v, WebGLPowerPreference.HighPerformance);
            return sb.ToString();
        }

        public static BuildReport Build(string outputPath)
        {
            string reportPath = outputPath.TrimEnd('/', '\\') + ReportSuffix;
            var log = new StringBuilder();
            log.AppendLine("=== WebGLTestBuild " + DateTime.Now.ToString("s") + " ===");
            log.AppendLine("Unity " + Application.unityVersion);
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    log.AppendLine("WARNING: active build target is " + EditorUserBuildSettings.activeBuildTarget + " (switch to WebGL first to avoid an implicit switch)");

                log.AppendLine("--- settings applied ---");
                log.Append(ApplyTestSettings());
                AssetDatabase.SaveAssets();
                log.AppendLine("--- effective settings ---");
                log.Append(DescribeWebSettings());

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None,
                };

                File.WriteAllText(reportPath, log + "\nBUILD STARTED " + DateTime.Now.ToString("s") + "\n");
                Debug.Log("[WebGLTestBuild] build started -> " + outputPath);
                var sw = Stopwatch.StartNew();
                BuildReport report = BuildPipeline.BuildPlayer(options);
                sw.Stop();

                var s = report.summary;
                log.AppendLine("--- result ---");
                log.AppendLine("result=" + s.result);
                log.AppendLine("wallClockSeconds=" + sw.Elapsed.TotalSeconds.ToString("F1"));
                log.AppendLine("summary.totalTime=" + s.totalTime);
                log.AppendLine("summary.totalSize=" + s.totalSize);
                log.AppendLine("summary.totalErrors=" + s.totalErrors + " totalWarnings=" + s.totalWarnings);
                log.AppendLine("--- report files ---");
                foreach (var f in report.GetFiles().OrderByDescending(f => f.size))
                    log.AppendLine($"{f.size,12}  {f.role,-24} {f.path}");
                log.AppendLine("--- packed assets (top 60 by packed size) ---");
                var packed = report.packedAssets
                    .SelectMany(p => p.contents.Select(c => (file: p.shortPath, c.sourceAssetPath, c.type, c.packedSize)))
                    .OrderByDescending(x => x.packedSize).Take(60);
                foreach (var p in packed)
                    log.AppendLine($"{p.packedSize,12}  {p.type?.Name,-22} {p.sourceAssetPath}  [{p.file}]");
                log.AppendLine("--- packed size by type ---");
                var byType = report.packedAssets
                    .SelectMany(p => p.contents)
                    .GroupBy(c => c.type?.Name ?? "?")
                    .Select(g => (g.Key, total: g.Sum(c => (long)c.packedSize), count: g.Count()))
                    .OrderByDescending(g => g.total);
                foreach (var g in byType)
                    log.AppendLine($"{g.total,12}  x{g.count,-5} {g.Key}");
                log.AppendLine("--- messages (warnings/errors) ---");
                foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type != LogType.Log)
                            log.AppendLine($"[{m.type}] {step.name}: {m.content}");
                log.AppendLine("BUILD FINISHED " + DateTime.Now.ToString("s"));
                File.WriteAllText(reportPath, log.ToString());
                Debug.Log("[WebGLTestBuild] build finished: " + s.result + " in " + sw.Elapsed.TotalSeconds.ToString("F1") + "s, report " + reportPath);
                return report;
            }
            catch (Exception e)
            {
                log.AppendLine("EXCEPTION " + e);
                File.WriteAllText(reportPath, log.ToString());
                Debug.LogError("[WebGLTestBuild] exception: " + e);
                throw;
            }
        }
    }
}
