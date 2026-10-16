using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Runs EditMode tests synchronously and writes <c>Logs/tests/editmode_&lt;timestamp&gt;.xml</c> and a .txt summary
    /// (TECH_PLAN §10.2). Callable from <c>Unity_RunCommand</c>: <c>TestRunnerBridge.RunEditMode("LongShadow.Tests.EditMode.Core")</c>.
    /// </summary>
    public static class TestRunnerBridge
    {
        /// <summary>Summary of the last run ("passed 12, failed 0, skipped 0 -> path").</summary>
        public static string LastSummary { get; private set; } = "(no run)";
        /// <summary>Failed test count of the last run.</summary>
        public static int LastFailed { get; private set; }

        /// <summary>Runs every LongShadow EditMode test assembly.</summary>
        [MenuItem("LongShadow/Tests/Run EditMode Tests", priority = 100)]
        public static void RunAllMenu() => RunEditMode(null);

        /// <summary>
        /// Runs EditMode tests (all, or the given assembly names separated by ';') synchronously. Returns the summary.
        /// </summary>
        public static string RunEditMode(string assemblyNames)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dir = Path.Combine(BuilderUtil.ProjectRoot, "Logs", "tests");
            Directory.CreateDirectory(dir);
            var cb = new Callbacks(Path.Combine(dir, "editmode_" + stamp));
            api.RegisterCallbacks(cb);
            var filter = new Filter { testMode = TestMode.EditMode };
            if (!string.IsNullOrEmpty(assemblyNames)) filter.assemblyNames = assemblyNames.Split(';');
            else filter.groupNames = new[] { "^LongShadow\\." };
            try
            {
                api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
            }
            finally
            {
                api.UnregisterCallbacks(cb);
                UnityEngine.Object.DestroyImmediate(api);
            }
            LastSummary = cb.Summary;
            LastFailed = cb.Failed;
            Debug.Log("[LS:Tests] " + LastSummary);
            return LastSummary;
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly string _basePath;
            public string Summary = "(not finished)";
            public int Failed;

            public Callbacks(string basePath) => _basePath = basePath;

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var xml = new StringBuilder();
                var txt = new StringBuilder();
                xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                Write(result, xml, txt, 0);
                Failed = result.FailCount;
                Summary = "EditMode tests: passed " + result.PassCount + ", failed " + result.FailCount + ", skipped " +
                          result.SkipCount + ", inconclusive " + result.InconclusiveCount + " (" + result.Duration.ToString("0.00") + " s) -> " +
                          _basePath + ".xml";
                txt.Insert(0, Summary + "\n");
                File.WriteAllText(_basePath + ".xml", xml.ToString());
                File.WriteAllText(_basePath + ".txt", txt.ToString());
            }

            private static void Write(ITestResultAdaptor r, StringBuilder xml, StringBuilder txt, int depth)
            {
                var pad = new string(' ', depth * 2);
                var isLeaf = !r.HasChildren;
                xml.Append(pad).Append(isLeaf ? "<test-case" : "<test-suite")
                   .Append(" name=\"").Append(Esc(r.Name)).Append('"')
                   .Append(" fullname=\"").Append(Esc(r.FullName)).Append('"')
                   .Append(" result=\"").Append(r.TestStatus).Append('"')
                   .Append(" duration=\"").Append(r.Duration.ToString("0.000")).Append('"');
                if (isLeaf)
                {
                    if (!string.IsNullOrEmpty(r.Message))
                        xml.Append(">\n").Append(pad).Append("  <message>").Append(Esc(r.Message)).Append("</message>\n").Append(pad).Append("</test-case>\n");
                    else xml.Append("/>\n");
                    txt.Append(r.TestStatus == TestStatus.Passed ? "  ok   " : "  " + r.TestStatus.ToString().ToUpperInvariant() + " ")
                       .Append(r.FullName);
                    if (r.TestStatus == TestStatus.Failed && !string.IsNullOrEmpty(r.Message)) txt.Append(" :: ").Append(r.Message.Trim());
                    txt.Append('\n');
                    return;
                }
                xml.Append(">\n");
                foreach (var c in r.Children) Write(c, xml, txt, depth + 1);
                xml.Append(pad).Append("</test-suite>\n");
            }

            private static string Esc(string s) =>
                string.IsNullOrEmpty(s) ? "" : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
