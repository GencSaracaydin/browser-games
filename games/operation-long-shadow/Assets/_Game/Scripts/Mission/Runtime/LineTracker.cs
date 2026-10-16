using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// Tracks the lines the mission requests through <see cref="IVOService"/>: when each was requested, started and
    /// finished. With a VO service a line is finished once the service no longer reports it playing or queued (or raised
    /// VOLineFinished); a request the service skips (played before the restored checkpoint) or rejects is finished at once.
    /// Without a VO service the line is "virtual": it lasts its <see cref="LineDurations"/> length so pacing and MID-A keep
    /// their real timing (the subtitle is logged). A safety timeout ends a line that never reports back.
    /// </summary>
    public sealed class LineTracker
    {
        /// <summary>Extra seconds after the expected end before a line that never reports back is treated as finished.</summary>
        public const float SafetySeconds = 20f;

        private sealed class State
        {
            public int Handle;
            public int RequestSeq;
            public float RequestedAt;
            public float VirtualStart;
            public float VirtualEnd;
            public bool Virtual;
            public bool Skipped;
            public float Expected;
        }

        private readonly Dictionary<string, State> _lines = new Dictionary<string, State>(StringComparer.Ordinal);
        private readonly HashSet<string> _requested = new HashSet<string>(StringComparer.Ordinal);
        private readonly MissionEventLog _log;
        private readonly Func<float> _clock;
        private ServiceRef<IVOService> _vo;

        /// <summary>Creates a tracker on the mission clock and event log.</summary>
        public LineTracker(MissionEventLog log, Func<float> clock)
        {
            _log = log;
            _clock = clock ?? (() => 0f);
        }

        /// <summary>Every line id the mission requested this session (tests check them against DIALOGUE.json).</summary>
        public IReadOnlyCollection<string> Requested => _requested;

        /// <summary>Records a line played by someone else on the mission's behalf (inserts, cinematic fallbacks).</summary>
        public void NoteRequested(string lineId)
        {
            if (!string.IsNullOrEmpty(lineId)) _requested.Add(lineId);
        }

        /// <summary>Requests a line. Returns the VO handle (0 when virtual, skipped or rejected).</summary>
        public int Play(string lineId, VOOptions options = default)
        {
            if (string.IsNullOrEmpty(lineId)) return 0;
            _requested.Add(lineId);
            _log?.Record(EventChannel.LineRequested, lineId);
            var now = _clock();
            var st = new State { RequestSeq = _log?.Seq ?? 0, RequestedAt = now };
            var vo = _vo.Value;
            var len = Length(lineId);
            st.Expected = now + Math.Max(0f, options.Delay) + len;
            if (vo == null)
            {
                st.Virtual = true;
                st.VirtualStart = now + Math.Max(0f, options.Delay);
                st.VirtualEnd = st.VirtualStart + len;
                LSLog.Verbose(LogCategory.VO, "[no VO service] " + lineId + " (" + len.ToString("0.0") + " s)");
            }
            else
            {
                st.Handle = vo.Play(lineId, options);
                if (st.Handle == 0) st.Skipped = true;
            }
            _lines[lineId] = st;
            return st.Handle;
        }

        /// <summary>Stops a line the mission requested.</summary>
        public void Stop(string lineId)
        {
            if (lineId == null || !_lines.TryGetValue(lineId, out var st)) return;
            if (st.Virtual)
            {
                st.VirtualEnd = Math.Min(st.VirtualEnd, _clock());
                return;
            }
            if (st.Handle != 0) _vo.Value?.Stop(st.Handle);
            st.Skipped = true;
        }

        /// <summary>True once the line was requested (this session, since the last <see cref="Clear"/>).</summary>
        public bool WasRequested(string lineId) => lineId != null && _lines.ContainsKey(lineId);

        /// <summary>True once the line has finished (or was never requested).</summary>
        public bool IsDone(string lineId)
        {
            if (lineId == null || !_lines.TryGetValue(lineId, out var st)) return true;
            var now = _clock();
            if (st.Virtual) return now >= st.VirtualEnd;
            if (st.Skipped) return true;
            if (_log != null && _log.LastSeq(EventChannel.LineFinished, lineId) > st.RequestSeq) return true;
            var vo = _vo.Value;
            if (vo == null) return now >= st.Expected;
            if (!vo.IsPlaying(lineId)) return true;
            return now >= st.Expected + SafetySeconds;
        }

        /// <summary>True once the line has started (VOLineStarted after the request; virtual lines at their start time).</summary>
        public bool HasStarted(string lineId)
        {
            if (lineId == null || !_lines.TryGetValue(lineId, out var st)) return false;
            if (st.Virtual) return _clock() >= st.VirtualStart;
            if (st.Skipped) return true;
            return _log != null && _log.LastSeq(EventChannel.LineStarted, lineId) > st.RequestSeq || IsDone(lineId);
        }

        /// <summary>Mission time the line started, or -1.</summary>
        public float StartTime(string lineId)
        {
            if (lineId == null || !_lines.TryGetValue(lineId, out var st)) return -1f;
            if (st.Virtual) return st.VirtualStart;
            if (_log != null && _log.LastSeq(EventChannel.LineStarted, lineId) > st.RequestSeq)
                return _log.LastTime(EventChannel.LineStarted, lineId);
            return st.Skipped ? st.RequestedAt : -1f;
        }

        /// <summary>The line's length: the VO service's, else <see cref="LineDurations"/>.</summary>
        public float Length(string lineId)
        {
            var vo = _vo.Value;
            var d = vo != null ? vo.Duration(lineId) : 0f;
            return d > 0f ? d : LineDurations.Of(lineId);
        }

        /// <summary>Forgets the per-line state (checkpoint reload); <see cref="Requested"/> is kept.</summary>
        public void Clear() => _lines.Clear();
    }
}
