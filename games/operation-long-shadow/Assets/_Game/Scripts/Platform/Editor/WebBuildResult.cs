using System.Collections.Generic;
using System.Text;

namespace LongShadow.Platform.Editor
{
    /// <summary>Outcome of <see cref="WebBuild.Build"/>: errors, warnings, the step log and the output figures.</summary>
    public sealed class WebBuildResult
    {
        /// <summary>Blocking problems (any error means the build must not be published).</summary>
        public readonly List<string> Errors = new List<string>();
        /// <summary>Non-blocking problems.</summary>
        public readonly List<string> Warnings = new List<string>();
        /// <summary>Step-by-step log (also written to <see cref="WebBuild.BuildLogPath"/>).</summary>
        public readonly StringBuilder Log = new StringBuilder();

        /// <summary>True when every step ran.</summary>
        public bool Completed { get; internal set; }
        /// <summary>True when every step ran and nothing failed.</summary>
        public bool Succeeded => Completed && Errors.Count == 0;
        /// <summary>Absolute output folder.</summary>
        public string OutputPath { get; internal set; } = string.Empty;
        /// <summary>Files in the output folder after post-processing.</summary>
        public int FileCount { get; internal set; }
        /// <summary>Bytes in the output folder after post-processing.</summary>
        public long TotalBytes { get; internal set; }
        /// <summary>Wall-clock seconds of the whole build.</summary>
        public double Seconds { get; internal set; }
        /// <summary>The post-build validation (null when the build did not get that far).</summary>
        public PostBuildValidator.Result Validation { get; internal set; }

        /// <summary>Adds a log line.</summary>
        internal void Step(string line) => Log.AppendLine(line);

        /// <summary>Records an error (and logs it).</summary>
        internal void Fail(string message)
        {
            Errors.Add(message);
            Log.AppendLine("ERROR   " + message);
        }

        /// <summary>Records a warning (and logs it).</summary>
        internal void Warn(string message)
        {
            Warnings.Add(message);
            Log.AppendLine("WARNING " + message);
        }

        /// <summary>One paragraph for the console.</summary>
        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append(Succeeded ? "Web build OK" : "Web build FAILED").Append(": ").Append(FileCount).Append(" files, ")
              .Append((TotalBytes / 1e6).ToString("0.00")).Append(" MB in ").Append(Seconds.ToString("0")).Append(" s -> ").Append(OutputPath);
            foreach (var e in Errors) sb.Append("\nERROR   ").Append(e);
            foreach (var w in Warnings) sb.Append("\nWARNING ").Append(w);
            return sb.ToString();
        }
    }
}
