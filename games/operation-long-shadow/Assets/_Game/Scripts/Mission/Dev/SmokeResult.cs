using System;
using System.Collections.Generic;

namespace LongShadow.Mission.Dev
{
    /// <summary>A whole smoke run (TECH_PLAN §10.3): Logs/smoke/smoke_&lt;ts&gt;.json in the editor, <c>LastResultJson</c> in a web build.</summary>
    [Serializable]
    public sealed class SmokeResult
    {
        public string startedAt;
        public bool pass;
        public bool everyBeatReached;
        public int exceptionCount;
        public List<string> exceptions = new List<string>();
        public float frameMsP50;
        public float frameMsP95;
        public long monoUsedStartBytes;
        public long monoUsedEndBytes;
        public int gcCollections;
        public long totalAllocatedBytes;
        public List<SmokeCheckpointResult> checkpoints = new List<SmokeCheckpointResult>();
    }
}
