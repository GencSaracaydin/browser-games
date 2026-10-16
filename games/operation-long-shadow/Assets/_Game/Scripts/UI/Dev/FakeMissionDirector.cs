using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.UI.Dev
{
    /// <summary><see cref="IMissionDirector"/> stand-in for the UI test bed (stats for the debrief, run state for the menus).</summary>
    public sealed class FakeMissionDirector : IMissionDirector
    {
        private readonly HashSet<string> _flags = new HashSet<string>();

        public int Beat { get; set; } = 3;
        public bool IsRunning { get; set; } = true;
        public MissionStats Stats { get; } = new MissionStats
        {
            Kills = 44, Headshots = 17, ShotsFired = 1184, ShotsHit = 450, GrenadesThrown = 6, Deaths = 2,
            MissionSeconds = 14 * 60 + 2, Intel = 3, OptionalIntel = 1,
        };
        public float StoryClockMinutes { get; set; } = 17 * 60 + 58;

        /// <summary>Last call, for the test bed log and tests.</summary>
        public string LastCall { get; private set; }

        public void StartMission()
        {
            IsRunning = true;
            LastCall = "StartMission";
        }

        public void ResumeFrom(string cpId)
        {
            IsRunning = true;
            LastCall = "ResumeFrom " + cpId;
        }

        public void QuitToSession()
        {
            IsRunning = false;
            LastCall = "QuitToSession";
        }

        public void Raise(string eventId) => GameEvents.RaiseMissionEvent(eventId);
        public bool Flag(string id) => _flags.Contains(id);

        public void SetFlag(string id, bool value)
        {
            if (value) _flags.Add(id);
            else _flags.Remove(id);
        }
    }
}
