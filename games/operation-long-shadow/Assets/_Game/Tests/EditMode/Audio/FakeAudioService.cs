using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>Records IAudioService calls (components that talk to the service through GameServices).</summary>
    internal sealed class FakeAudioService : IAudioService
    {
        public readonly List<string> Played = new List<string>();
        public readonly List<string> Loops = new List<string>();
        public readonly List<int> Stopped = new List<int>();
        public readonly HashSet<string> Known = new HashSet<string>();
        private int _next = 1;

        public void Play(string cueId, Vector3 position, float volume = 1f) => Played.Add(cueId);
        public void Play2D(string cueId, float volume = 1f) => Played.Add(cueId);
        public void PlayPitched(string cueId, Vector3 position, float volume, float pitch) => Played.Add(cueId);
        public int PlayLoop(string cueId, Transform follow)
        {
            Loops.Add(cueId);
            return _next++;
        }
        public void StopLoop(int handle, float fade) => Stopped.Add(handle);
        public int PlayAttached(string cueId, Transform follow, float volume = 1f)
        {
            Played.Add(cueId);
            return _next++;
        }
        public void SetListenerOverride(Transform target) { }
        public void PlayWeaponShot(string weaponId, Vector3 position, bool isPlayer) => Played.Add("SHOT:" + weaponId);
        public ZoneType CurrentZone => ZoneType.OpenDesert;
        public void Tinnitus(float seconds) { }
        public void SetCategoryGain(string category, float linear) { }
        public void Duck(string category, float db, float seconds) { }
        public bool HasCue(string cueId) => Known.Contains(cueId);
    }
}
