using System.Text;
using UnityEngine.Profiling;

namespace LongShadow.Platform
{
    /// <summary>Memory figures for the per-load log (TECH_PLAN §7.16: log memory per section load; target peak ≤ 1.8 GB).</summary>
    public static class MemoryStats
    {
        private const double Mb = 1024.0 * 1024.0;

        /// <summary>WebAssembly heap size in bytes (0 outside the web).</summary>
        public static long WasmHeapBytes => WebNative.HeapBytes();

        /// <summary>One line: wasm heap, Unity allocated/reserved, managed heap (MB). Allocates; call at load points only.</summary>
        public static string Describe()
        {
            var sb = new StringBuilder(96);
            var heap = WasmHeapBytes;
            if (heap > 0) sb.Append("wasm heap ").Append((heap / Mb).ToString("0")).Append(" MB, ");
            sb.Append("allocated ").Append((Profiler.GetTotalAllocatedMemoryLong() / Mb).ToString("0")).Append(" MB, reserved ")
              .Append((Profiler.GetTotalReservedMemoryLong() / Mb).ToString("0")).Append(" MB, managed ")
              .Append((Profiler.GetMonoUsedSizeLong() / Mb).ToString("0")).Append('/')
              .Append((Profiler.GetMonoHeapSizeLong() / Mb).ToString("0")).Append(" MB");
            return sb.ToString();
        }
    }
}
