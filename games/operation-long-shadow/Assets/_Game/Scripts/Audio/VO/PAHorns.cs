using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Non-VO sounds from Harrou's three PA horns (TECH_PLAN §7.9 PAHorn; GDD §7.1: horns at (185, 8, 200),
    /// (220, 20, 275), (346, 6, 294), markers <c>PA.Horn1..3</c>). Harrou's lines themselves play through the VOManager
    /// (PA_HORN chain from all three horns with their propagation delays). This component plays a cue from every horn
    /// position on the mission events <see cref="AudioEvents.PASiren"/> (the curfew siren) and
    /// <c>"PA.Play." + cueId</c>; each copy arrives after its own distance / speed of sound when the cue has that flag,
    /// so the town hears the horns as a ragged echo, as real PA networks sound. Lives on the audio systems prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PAHorns : MonoBehaviour
    {
        [SerializeField] private string _sirenCue = "SFX_CURFEW_SIREN";
        [SerializeField, Tooltip("Level of each horn (three copies add up).")] private float _volume = 0.8f;

        private ServiceRef<IAudioService> _audio;

        private void OnEnable() => GameEvents.MissionEvent += OnMissionEvent;

        private void OnDisable() => GameEvents.MissionEvent -= OnMissionEvent;

        /// <summary>Plays <paramref name="cueId"/> from every horn; returns how many copies were requested.</summary>
        public int PlayFromHorns(string cueId)
        {
            var audio = _audio.Value;
            if (audio == null || string.IsNullOrEmpty(cueId)) return 0;
            var n = 0;
            for (var i = 0; i < VOAnchors.Horns.Length; i++)
            {
                if (!VOAnchors.TryResolve(VOAnchors.Horns[i], out var p)) continue;
                audio.Play(cueId, p, _volume);
                n++;
            }
            return n;
        }

        private void OnMissionEvent(string id)
        {
            if (id == AudioEvents.PASiren) PlayFromHorns(_sirenCue);
            else if (id != null && id.StartsWith(AudioEvents.PAPlayPrefix, System.StringComparison.Ordinal))
                PlayFromHorns(id.Substring(AudioEvents.PAPlayPrefix.Length));
        }
    }
}
