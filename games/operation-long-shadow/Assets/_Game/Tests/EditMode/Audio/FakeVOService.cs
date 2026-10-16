using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>Records IVOService calls.</summary>
    internal sealed class FakeVOService : IVOService
    {
        public readonly List<string> Played = new List<string>();
        public readonly List<VOOptions> Options = new List<VOOptions>();

        public int Play(string lineId, VOOptions o = default)
        {
            Played.Add(lineId);
            Options.Add(o);
            return Played.Count;
        }
        public bool IsPlaying(string lineId) => false;
        public bool IsNetBusy => false;
        public void Stop(int handle) { }
        public void StopAll(float fade) { }
        public void PlayBark(string lineId, Transform speaker, Team team) => Played.Add(lineId);
        public void PlayClicks(int count, Transform speaker) { }
        public float Amplitude(string speakerId) => 0f;
        public void PrepareWindow(int fromBeat, int toBeat) { }
        public bool HasPlayed(string lineId) => Played.Contains(lineId);
        public bool HasLine(string lineId) => true;
        public float Duration(string lineId) => 2.2f;
    }
}
