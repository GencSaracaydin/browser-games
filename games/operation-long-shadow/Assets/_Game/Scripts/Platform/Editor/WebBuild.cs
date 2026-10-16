using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// The web build (TECH_PLAN §9.1; webgl.md §6-7). Runs **synchronously** (BuildPipeline and the Addressables
    /// content build block), because <c>EditorApplication.delayCall</c> never fires while the editor is unfocused, so an
    /// editor agent can call it from <c>Unity_RunCommand</c>:
    /// <c>LongShadow.Platform.Editor.WebBuild.Build(LongShadow.Platform.Editor.WebBuildOptions.Release())</c>.
    /// <list type="number">
    /// <item>Prepare: S_Boot (built from code when missing), dev placeholders for S_Persistent and missing sections.</item>
    /// <item>Assert the §3.1 recipe (ProjectSetup verify, applied when it drifted) and the CovertOps template; release
    /// builds name files by hash; QA builds add the diagnostics overlay and external symbols. These per-build
    /// PlayerSettings are restored afterwards, so ProjectSetup never sees drift.</item>
    /// <item>Addressables: settings (JSON catalog, local LZ4 *.bundle), group rules, duplicate check, clean + build.</item>
    /// <item>Delete <see cref="OutputFolder"/> (stale files survive rebuilds) and build the player with S_Boot only (D1).</item>
    /// <item>Post-process: LS:VARIANT marker, remove *.manifest, foreign files, unused TemplateData, AddressablesLink.</item>
    /// <item><see cref="PostBuildValidator"/> (files + content rules), <see cref="ReportPath"/>, and
    /// <c>Tools/build/publish_plan.py</c> (publish mirror with served extensions + <see cref="PublishPlanPath"/>).</item>
    /// </list>
    /// The build never publishes anything (ORCHESTRATION O5).
    /// </summary>
    public static class WebBuild
    {
        /// <summary>Output folder (project-relative, TECH_PLAN §9.1).</summary>
        public const string OutputFolder = "Builds/Web";
        /// <summary>Report written after every build (TECH_PLAN §9.1 step 6).</summary>
        public const string ReportPath = "Builds/Web_report.txt";
        /// <summary>Validation report (also written by the "Validate Last Web Build" menu).</summary>
        public const string ValidationReportPath = "Builds/Web_validation.txt";
        /// <summary>Publish plan written by publish_plan.py (D22).</summary>
        public const string PublishPlanPath = "Builds/Web_publish_plan.json";
        /// <summary>Step log of the last build.</summary>
        public const string BuildLogPath = "Builds/Web_build_log.txt";
        /// <summary>The template every shipped build uses (TECH_PLAN §3.1).</summary>
        public const string TemplateName = "PROJECT:CovertOps";
        /// <summary>The build-variant marker in the template's index.html.</summary>
        public const string VariantMarker = "/*LS:VARIANT*/";
        /// <summary>The publish planner, relative to the project root.</summary>
        public const string PublishPlanScript = "Tools/build/publish_plan.py";

        /// <summary>Menu: release build (hashed names, no diagnostics).</summary>
        [MenuItem("LongShadow/Build/Web Release", priority = 100)]
        public static void ReleaseMenu() => BuildAndLog(WebBuildOptions.Release());

        /// <summary>Menu: QA build (diagnostics overlay, external symbols, ?dev=1 on).</summary>
        [MenuItem("LongShadow/Build/Web QA (diagnostics)", priority = 101)]
        public static void QaMenu() => BuildAndLog(WebBuildOptions.QaBuild());

        /// <summary>Menu: (re)writes the publish mirror and plan for the last build without rebuilding.</summary>
        [MenuItem("LongShadow/Build/Write Publish Plan (last build)", priority = 121)]
        public static void PublishPlanMenu()
        {
            var r = WritePublishPlan();
            if (r.Errors.Count == 0) Debug.Log("[LS:Platform] " + r.Log);
            else Debug.LogError("[LS:Platform] " + r.Log);
        }

        /// <summary>Runs <see cref="PublishPlanScript"/> on <see cref="OutputFolder"/> (publish mirror + <see cref="PublishPlanPath"/>).</summary>
        public static WebBuildResult WritePublishPlan()
        {
            var r = new WebBuildResult();
            var root = BuilderUtil.ProjectRoot;
            var outDir = Path.Combine(root, OutputFolder);
            r.OutputPath = outDir;
            if (!Directory.Exists(outDir)) r.Fail("No build at " + outDir + "; run LongShadow/Build/Web Release first.");
            else RunPublishPlan(root, outDir, r);
            r.Completed = true;
            return r;
        }

        /// <summary>Builds and logs the summary (error log entry when it failed).</summary>
        public static WebBuildResult BuildAndLog(WebBuildOptions options)
        {
            var r = Build(options);
            if (r.Succeeded) Debug.Log("[LS:Platform] " + r.Summary());
            else Debug.LogError("[LS:Platform] " + r.Summary());
            return r;
        }

        /// <summary>Runs the whole build synchronously. Never throws; read <see cref="WebBuildResult.Errors"/>.</summary>
        public static WebBuildResult Build(WebBuildOptions options)
        {
            options = options ?? WebBuildOptions.Release();
            var r = new WebBuildResult();
            var clock = Stopwatch.StartNew();
            var root = BuilderUtil.ProjectRoot;
            var outDir = Path.GetFullPath(Path.IsPathRooted(options.OutputFolder) ? options.OutputFolder : Path.Combine(root, options.OutputFolder));
            r.OutputPath = outDir;
            r.Step("# Web build " + DateTime.Now.ToString("s") + ": " + options);
            var saved = PerBuildSettings.Capture();
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new StepFailed("Leave play mode before building.");
                if (EditorApplication.isCompiling) throw new StepFailed("Scripts are compiling; wait and run the build again.");

                Prepare(options, r);
                AssertSettings(options, r);
                var content = options.BuildContent ? BuildContent(options, r) : null;
                var player = BuildPlayer(outDir, r);
                PostProcess(outDir, options, r);

                var limits = BuildLimits.Load(root);
                var validation = PostBuildValidator.ValidateOutput(outDir, limits);
                var aa = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                PostBuildValidator.ValidateContent(validation, PostBuildValidator.CollectBundleAssets(aa), limits, "bundles");
                PostBuildValidator.ValidateContent(validation, PostBuildValidator.CollectBootAssets(BootSceneBuilder.ScenePath), limits, "boot data");
                File.WriteAllText(Path.Combine(root, ValidationReportPath), validation.ToText());
                r.Validation = validation;
                r.FileCount = validation.FileCount;
                r.TotalBytes = validation.TotalBytes;
                foreach (var e in validation.Errors) r.Fail("validator: " + e);
                foreach (var w in validation.Warnings) r.Warn("validator: " + w);
                r.Step("Validation: " + (validation.Ok ? "PASS" : "FAIL") + " (" + ValidationReportPath + ")");

                WebBuildReport.Write(Path.Combine(root, ReportPath), outDir, player, content, validation, options);
                r.Step("Report: " + ReportPath);

                if (options.RunPublishPlan && validation.Ok) RunPublishPlan(root, outDir, r);
                r.Completed = true;
            }
            catch (StepFailed e)
            {
                r.Fail(e.Message);
            }
            catch (Exception e)
            {
                r.Fail(e.GetType().Name + ": " + e.Message);
                r.Step(e.ToString());
            }
            finally
            {
                saved.Restore(r);
                r.Seconds = clock.Elapsed.TotalSeconds;
                r.Step("Finished in " + r.Seconds.ToString("0") + " s: " + (r.Succeeded ? "OK" : "FAILED"));
                TryWrite(Path.Combine(root, BuildLogPath), r.Log.ToString());
            }
            return r;
        }

        // ---- 1. content prerequisites ------------------------------------------------------------------------------

        private static void Prepare(WebBuildOptions options, WebBuildResult r)
        {
            var log = new StringBuilder();
            if (options.RebuildBootScene || !BootSceneBuilder.Exists)
            {
                BootSceneBuilder.Build(log);
                r.Step("S_Boot built from code");
            }
            if (options.EnsurePlaceholders)
            {
                var n = PlaceholderScenes.EnsureAll(false, log);
                r.Step("Placeholders: " + n + " created (existing ones kept; menu LongShadow/Platform/Build Placeholder Scenes rebuilds them)");
            }
            if (log.Length > 0) r.Step(log.ToString().TrimEnd());
            if (!BootSceneBuilder.Exists) throw new StepFailed("S_Boot could not be built (" + BootSceneBuilder.ScenePath + ").");
        }

        // ---- 2. settings ---------------------------------------------------------------------------------------------

        private static void AssertSettings(WebBuildOptions options, WebBuildResult r)
        {
            if (options.EnforceProjectSettings)
            {
                var verify = ProjectSetup.Apply(true);
                if (verify.Changes > 0)
                {
                    var applied = ProjectSetup.Apply(false);
                    r.Warn("ProjectSetup reported " + verify.Changes + " difference(s) from the TECH_PLAN §3 recipe; applied them (" +
                           applied.Changes + " changed, see " + ProjectSetup.LogPath + ")");
                }
                else r.Step("ProjectSetup: no drift (" + verify.Checked + " values checked)");
                foreach (var p in verify.Problems) r.Warn("ProjectSetup: " + p);
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                r.Warn("The active build target is " + EditorUserBuildSettings.activeBuildTarget + "; BuildPipeline will switch to WebGL (slow, reimports).");

            var templateFile = Path.Combine(BuilderUtil.ProjectRoot, ProjectSetup.TemplateIndexPath);
            if (PlayerSettings.WebGL.template != TemplateName)
            {
                var msg = "WebGL template is '" + PlayerSettings.WebGL.template + "', not " + TemplateName +
                          (File.Exists(templateFile) ? " (run LongShadow/Setup/Apply Project Settings)" : " (" + ProjectSetup.TemplateIndexPath + " has not landed: land.py --assets --wp WP01)");
                if (options.RequireCovertOpsTemplate) throw new StepFailed(msg);
                r.Warn(msg + "; building with it anyway (no CovertOps errorHandler: pointer-lock refusals may show alert()).");
            }

            if (PlayerSettings.WebGL.compressionFormat != WebGLCompressionFormat.Brotli || !PlayerSettings.WebGL.decompressionFallback)
                throw new StepFailed("WebGL compression must be Brotli with Decompression Fallback (the host cannot send Content-Encoding; TECH_PLAN §3.1).");

            // Per-build values (restored in Build's finally).
            PlayerSettings.WebGL.nameFilesAsHashes = !options.Qa;
            PlayerSettings.WebGL.showDiagnostics = options.Qa;
            PlayerSettings.WebGL.debugSymbolMode = options.Qa ? WebGLDebugSymbolMode.External : WebGLDebugSymbolMode.Off;
            r.Step("Player: nameFilesAsHashes " + PlayerSettings.WebGL.nameFilesAsHashes + ", showDiagnostics " + PlayerSettings.WebGL.showDiagnostics +
                   ", symbols " + PlayerSettings.WebGL.debugSymbolMode + ", template " + PlayerSettings.WebGL.template);
        }

        // ---- 3. Addressables -----------------------------------------------------------------------------------------

        private static AddressablesPlayerBuildResult BuildContent(WebBuildOptions options, WebBuildResult r)
        {
            var log = new StringBuilder();
            var settings = AddressablesSetup.Ensure(log, out var definesChanged);
            if (log.Length > 0) r.Step("Addressables settings:\n" + log.ToString().TrimEnd());
            if (definesChanged || (settings.EnableJsonCatalog && !AddressablesSetup.JsonCatalogCompiled))
                throw new StepFailed(AddressablesSetup.JsonCatalogDefine + " was just added to the WebGL defines: wait for the script " +
                                     "compilation to finish, then run the build again (the content build must write catalog.json, TECH_PLAN §9.2).");

            if (options.ApplyGroupRules)
            {
                var report = AddressablesGroupRules.Apply(settings, new AddressablesGroupRules.Options { UsePlaceholders = options.EnsurePlaceholders });
                r.Step("Group rules: " + report.Log.ToString().TrimEnd());
                foreach (var w in report.Warnings) r.Warn("group rules: " + w);
            }

            var dupes = FindDuplicates(settings);
            if (dupes.Count > 0)
            {
                var list = string.Join("\n  ", dupes.Take(25)) + (dupes.Count > 25 ? "\n  ... " + (dupes.Count - 25) + " more" : string.Empty);
                var msg = "Addressables duplicate dependencies (" + dupes.Count + "):\n  " + list;
                if (options.FailOnDuplicates) throw new StepFailed(msg + "\nFix the group rules or pass FailOnDuplicates = false.");
                r.Warn(msg);
            }
            else r.Step("Duplicate bundle dependencies: none");

            var t0 = Stopwatch.StartNew();
            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (result == null) throw new StepFailed("Addressables content build returned no result.");
            if (!string.IsNullOrEmpty(result.Error)) throw new StepFailed("Addressables content build failed: " + result.Error);
            r.Step("Addressables content: " + result.AssetBundleBuildResults.Count + " bundles in " + t0.Elapsed.TotalSeconds.ToString("0.0") + " s (" + result.OutputPath + ")");
            return result;
        }

        /// <summary>Runs the Addressables "Check Duplicate Bundle Dependencies" rule; returns the issues (empty when clean).</summary>
        public static List<string> FindDuplicates(AddressableAssetSettings settings)
        {
            var issues = new List<string>();
            if (settings == null) return issues;
            var rule = new CheckBundleDupeDependencies();
            var results = rule.RefreshAnalysis(settings);
            if (results == null) return issues;
            foreach (var res in results)
                if (res != null && (res.severity == MessageType.Warning || res.severity == MessageType.Error))
                    issues.Add(res.resultName);
            return issues;
        }

        // ---- 4. player -----------------------------------------------------------------------------------------------

        private static BuildReport BuildPlayer(string outDir, WebBuildResult r)
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            var bpo = new BuildPlayerOptions
            {
                scenes = new[] { BootSceneBuilder.ScenePath }, // D1: S_Boot is the only player scene
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None, // QA too: development builds are never hostable (webgl.md §7.1)
            };
            var t0 = Stopwatch.StartNew();
            var report = BuildPipeline.BuildPlayer(bpo);
            var s = report.summary;
            r.Step("Player: " + s.result + " in " + t0.Elapsed.TotalSeconds.ToString("0") + " s, " + s.totalErrors + " errors, " + s.totalWarnings + " warnings");
            if (s.result != BuildResult.Succeeded)
            {
                var messages = report.steps.SelectMany(st => st.messages).Where(m => m.type == UnityEngine.LogType.Error || m.type == UnityEngine.LogType.Exception)
                                     .Select(m => m.content).Take(10).ToList();
                throw new StepFailed("Player build " + s.result + (messages.Count > 0 ? ":\n  " + string.Join("\n  ", messages) : string.Empty));
            }
            return report;
        }

        // ---- 5. post-process -----------------------------------------------------------------------------------------

        private static void PostProcess(string outDir, WebBuildOptions options, WebBuildResult r)
        {
            var limits = BuildLimits.Load();
            var index = Path.Combine(outDir, "index.html");
            var html = File.Exists(index) ? File.ReadAllText(index) : null;
            if (html != null && options.Qa)
            {
                var patched = PatchVariant(html, "qa");
                if (patched == html) r.Warn("index.html has no " + VariantMarker + " marker (not the CovertOps template?): QA flag not set");
                else File.WriteAllText(index, html = patched);
            }

            var removed = new List<string>();
            var root = outDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var abs in Directory.GetFiles(outDir, "*", SearchOption.AllDirectories))
            {
                var rel = abs.Substring(root.Length).Replace('\\', '/');
                var name = Path.GetFileName(rel);
                string why = null;
                if (Matches(name, limits.strip_patterns)) why = "build by-product";
                else if (!Allowed(rel, limits.allowed_prefixes)) why = "foreign file";
                else if (rel.StartsWith("StreamingAssets/aa/AddressablesLink/", StringComparison.Ordinal)) why = "build-time link.xml";
                else if (rel.StartsWith("TemplateData/", StringComparison.Ordinal) && html != null && html.IndexOf(name, StringComparison.Ordinal) < 0) why = "unused template file";
                if (why == null) continue;
                File.Delete(abs);
                removed.Add(rel + " (" + why + ")");
            }
            DeleteEmptyDirectories(outDir);
            r.Step("Post-process: removed " + removed.Count + (removed.Count > 0 ? ": " + string.Join(", ", removed) : string.Empty));
        }

        /// <summary>Sets the build variant in a CovertOps index.html (returns the text unchanged when the marker is missing).</summary>
        public static string PatchVariant(string html, string variant)
        {
            if (string.IsNullOrEmpty(html)) return html;
            var i = html.IndexOf(VariantMarker, StringComparison.Ordinal);
            if (i < 0) return html;
            var q0 = i + VariantMarker.Length;
            if (q0 >= html.Length || html[q0] != '"') return html;
            var q1 = html.IndexOf('"', q0 + 1);
            if (q1 < 0) return html;
            return html.Substring(0, q0) + "\"" + variant + "\"" + html.Substring(q1 + 1);
        }

        // ---- 6. publish plan -----------------------------------------------------------------------------------------

        private static void RunPublishPlan(string root, string outDir, WebBuildResult r)
        {
            var script = Path.Combine(root, PublishPlanScript);
            if (!File.Exists(script))
            {
                r.Warn(PublishPlanScript + " is missing; no publish plan written.");
                return;
            }
            var python = FindPython();
            if (python == null)
            {
                r.Warn("python3 not found; run `python3 " + PublishPlanScript + " --build " + outDir + "` by hand.");
                return;
            }
            var psi = new ProcessStartInfo(python, "\"" + script + "\" --build \"" + outDir + "\"")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                if (p == null)
                {
                    r.Warn("Could not start " + python);
                    return;
                }
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(180_000))
                {
                    try { p.Kill(); } catch (Exception) { /* already gone */ }
                    r.Warn("publish_plan.py timed out after 180 s");
                    return;
                }
                var text = (stdout.Result + stderr.Result).TrimEnd();
                r.Step("publish_plan.py (exit " + p.ExitCode + "):\n" + text);
                if (p.ExitCode == 1) r.Fail("publish_plan.py found host-limit problems (see " + BuildLogPath + ")");
                else if (p.ExitCode != 0) r.Warn("publish_plan.py exited with " + p.ExitCode);
            }
        }

        private static string FindPython()
        {
            foreach (var c in new[] { "/usr/bin/python3", "/opt/homebrew/bin/python3", "/usr/local/bin/python3", "/Library/Frameworks/Python.framework/Versions/Current/bin/python3" })
                if (File.Exists(c)) return c;
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var candidate = Path.Combine(dir, "python3");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        // ---- helpers -------------------------------------------------------------------------------------------------

        private static bool Matches(string name, string[] patterns)
        {
            if (patterns == null) return false;
            foreach (var p in patterns)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (p.StartsWith("*", StringComparison.Ordinal) ? name.EndsWith(p.Substring(1), StringComparison.OrdinalIgnoreCase)
                                                                : string.Equals(name, p, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool Allowed(string rel, string[] prefixes)
        {
            if (prefixes == null || prefixes.Length == 0) return true;
            foreach (var p in prefixes)
                if (!string.IsNullOrEmpty(p) && (rel == p || rel.StartsWith(p, StringComparison.Ordinal))) return true;
            return false;
        }

        private static void DeleteEmptyDirectories(string dir)
        {
            foreach (var d in Directory.GetDirectories(dir))
            {
                DeleteEmptyDirectories(d);
                if (Directory.GetFileSystemEntries(d).Length == 0) Directory.Delete(d);
            }
        }

        private static void TryWrite(string path, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, text);
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Platform, "Could not write " + path + ": " + e.Message);
            }
        }

        /// <summary>A step failed; the message says what to do.</summary>
        private sealed class StepFailed : Exception
        {
            public StepFailed(string message) : base(message) { }
        }

        /// <summary>PlayerSettings the build changes for one run and restores afterwards (no ProjectSetup drift).</summary>
        private readonly struct PerBuildSettings
        {
            private readonly bool _hashes;
            private readonly bool _diagnostics;
            private readonly WebGLDebugSymbolMode _symbols;

            private PerBuildSettings(bool hashes, bool diagnostics, WebGLDebugSymbolMode symbols)
            {
                _hashes = hashes;
                _diagnostics = diagnostics;
                _symbols = symbols;
            }

            public static PerBuildSettings Capture() =>
                new PerBuildSettings(PlayerSettings.WebGL.nameFilesAsHashes, PlayerSettings.WebGL.showDiagnostics, PlayerSettings.WebGL.debugSymbolMode);

            public void Restore(WebBuildResult r)
            {
                try
                {
                    PlayerSettings.WebGL.nameFilesAsHashes = _hashes;
                    PlayerSettings.WebGL.showDiagnostics = _diagnostics;
                    PlayerSettings.WebGL.debugSymbolMode = _symbols;
                }
                catch (Exception e)
                {
                    r.Warn("Could not restore the per-build PlayerSettings: " + e.Message);
                }
            }
        }
    }
}
