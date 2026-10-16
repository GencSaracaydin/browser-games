using System;
using System.Collections.Generic;

namespace LongShadow.Mission.Dev
{
    /// <summary>One checkpoint of a smoke run (JSON via JsonUtility).</summary>
    [Serializable]
    public sealed class SmokeCheckpointResult
    {
        public string checkpoint;
        public int beat;
        public bool reached;
        public float loadSeconds;
        public bool objectiveSet;
        public bool markersResolvable;
        public List<string> missingMarkers = new List<string>();
        public bool squadAtNodes;
        public int maxAlive;
        public bool encounterCaps;
        public string musicCue;
        public bool musicOk;
        public float lightS;
        public bool lightInRange;
        public string screenshot;
        public bool pass;
        public List<string> notes = new List<string>();
    }
}
