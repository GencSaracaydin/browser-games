using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>Recording <see cref="IVOService"/> for rig tests.</summary>
    internal sealed class FakeVOService : IVOService
    {
        public readonly List<string> Played = new List<string>();

        public int Play(string lineId, VOOptions o = default)
        {
            Played.Add(lineId);
            return Played.Count;
        }
        public bool IsPlaying(string lineId) => false;
        public bool IsNetBusy => false;
        public void Stop(int handle) { }
        public void StopAll(float fade) { }
        public void PlayBark(string lineId, Transform speaker, Team team) { }
        public void PlayClicks(int count, Transform speaker) { }
        public float Amplitude(string speakerId) => 0f;
        public void PrepareWindow(int fromBeat, int toBeat) { }
        public bool HasPlayed(string lineId) => Played.Contains(lineId);
        public bool HasLine(string lineId) => true;
        public float Duration(string lineId) => 1f;
    }
}
