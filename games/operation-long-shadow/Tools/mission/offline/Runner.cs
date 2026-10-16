// Offline NUnit runner for LongShadow EditMode test assemblies (WP07, Tools/mission/offline/run_offline_tests.sh).
// Loads the compiled test DLL with Unity's managed engine DLLs, routes Debug logs to the console (an Error/Exception log
// fails the test, as in Unity), and runs [Test]/[TestCase]/[TestCaseSource] methods with [SetUp]/[TearDown].
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

public static class Runner
{
    static List<string> s_probe = new List<string>();
    static int s_errorLogs;
    static List<string> s_errorText = new List<string>();
    public static bool Verbose;

    sealed class Handler : UnityEngine.ILogHandler
    {
        public void LogFormat(UnityEngine.LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            var msg = args == null || args.Length == 0 ? format : string.Format(format, args);
            if (logType == UnityEngine.LogType.Error || logType == UnityEngine.LogType.Exception || logType == UnityEngine.LogType.Assert)
            {
                s_errorLogs++;
                s_errorText.Add(msg);
            }
            if (Verbose || logType == UnityEngine.LogType.Error || logType == UnityEngine.LogType.Exception)
                Console.WriteLine("    [" + logType + "] " + msg);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            s_errorLogs++;
            s_errorText.Add(exception.ToString());
            Console.WriteLine("    [Exception] " + exception);
        }
    }

    public static int Main(string[] args)
    {
        var dll = args[0];
        string filter = null;
        foreach (var a in args.Skip(1))
        {
            if (a.StartsWith("--filter=")) filter = a.Substring(9);
            else if (a == "-v") Verbose = true;
            else s_probe.Add(a);
        }
        s_probe.Insert(0, Path.GetDirectoryName(Path.GetFullPath(dll)));
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            foreach (var d in s_probe)
            {
                var p = Path.Combine(d, name.Name + ".dll");
                if (File.Exists(p)) return ctx.LoadFromAssemblyPath(p);
            }
            return null;
        };
        return RunAll(dll, filter);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void InstallLog() => UnityEngine.Debug.unityLogger.logHandler = new Handler();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int RunAll(string dll, string filter)
    {
        InstallLog();
        var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll));
        int pass = 0, fail = 0, skip = 0;
        var failures = new List<string>();
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); foreach (var le in e.LoaderExceptions) Console.WriteLine("load: " + le.Message); }
        foreach (var type in types.OrderBy(t => t.FullName))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            var tests = methods.Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name is "TestAttribute" or "TestCaseAttribute" or "TestCaseSourceAttribute")).ToList();
            if (tests.Count == 0) continue;
            var setUp = methods.FirstOrDefault(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "SetUpAttribute"));
            var tearDown = methods.FirstOrDefault(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TearDownAttribute"));
            foreach (var m in tests)
            {
                foreach (var argsSet in Cases(type, m))
                {
                    var name = type.Name + "." + m.Name + (argsSet.Length > 0 ? "(" + string.Join(",", argsSet) + ")" : "");
                    if (filter != null && !name.Contains(filter)) continue;
                    s_errorLogs = 0;
                    s_errorText.Clear();
                    object inst = m.IsStatic ? null : Activator.CreateInstance(type);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    string result = null;
                    try
                    {
                        setUp?.Invoke(inst, null);
                        m.Invoke(inst, argsSet);
                    }
                    catch (TargetInvocationException tie)
                    {
                        result = Classify(tie.InnerException);
                    }
                    catch (Exception e) { result = "FAIL " + e; }
                    finally
                    {
                        try { tearDown?.Invoke(inst, null); } catch (Exception e) { result ??= "FAIL teardown " + e.InnerException; }
                    }
                    if (result == null && s_errorLogs > 0) result = "FAIL (error logs) " + string.Join(" || ", s_errorText.Take(3));
                    if (result == null) { pass++; Console.WriteLine("PASS " + name + " (" + sw.ElapsedMilliseconds + " ms)"); }
                    else if (result.StartsWith("SKIP")) { skip++; Console.WriteLine(result.Replace("SKIP", "SKIP " + name)); }
                    else { fail++; failures.Add(name); Console.WriteLine("FAIL " + name + "\n  " + result.Replace("\n", "\n  ")); }
                }
            }
        }
        Console.WriteLine($"\n== {pass} passed, {fail} failed, {skip} skipped");
        foreach (var f in failures) Console.WriteLine("  failed: " + f);
        return fail == 0 ? 0 : 1;
    }

    static string Classify(Exception e)
    {
        var n = e.GetType().Name;
        if (n == "InconclusiveException" || n == "IgnoreException") return "SKIP " + e.Message;
        if (e is FileNotFoundException f && (f.FileName ?? "").Contains("UnityEngine.TestRunner")) return "SKIP (needs the Unity test runner)";
        if (n == "SuccessException") return null;
        var msg = e.Message;
        if (n != "AssertionException") msg = e.ToString();
        else
        {
            var st = e.StackTrace ?? "";
            var line = st.Split('\n').FirstOrDefault(l => l.Contains("Tests.EditMode"));
            msg += "\n" + line;
        }
        return "FAIL " + msg;
    }

    static IEnumerable<object[]> Cases(Type type, MethodInfo m)
    {
        var any = false;
        foreach (var a in m.GetCustomAttributes())
        {
            var an = a.GetType().Name;
            if (an == "TestCaseAttribute")
            {
                any = true;
                var argsProp = a.GetType().GetProperty("Arguments");
                yield return ((object[])argsProp.GetValue(a)) ?? new object[0];
            }
            else if (an == "TestCaseSourceAttribute")
            {
                any = true;
                var src = (string)a.GetType().GetProperty("SourceName").GetValue(a);
                var f = type.GetField(src, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                IEnumerable items = f != null ? (IEnumerable)f.GetValue(null) : (IEnumerable)type.GetProperty(src, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                foreach (var it in items) yield return it is object[] arr ? arr : new[] { it };
            }
        }
        if (!any) yield return new object[0];
    }
}
