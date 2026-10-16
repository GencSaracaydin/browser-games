using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// SFX playback (WP06 AudioService): 32-source pool, 24 audible voices, category gains and ducking on
    /// AudioSource.volume (no AudioMixer, TECH_PLAN D9). Category names are in <see cref="AudioCategories"/>.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>3D one-shot cue at a position.</summary>
        void Play(string cueId, Vector3 position, float volume = 1f);
        /// <summary>
        /// 3D one-shot with a pitch multiplier on top of the cue's own random pitch (sprint footsteps +5 %, GDD §10.2).
        /// </summary>
        void PlayPitched(string cueId, Vector3 position, float volume, float pitch);
        /// <summary>2D one-shot cue.</summary>
        void Play2D(string cueId, float volume = 1f);
        /// <summary>Starts a loop that follows a transform (null = 2D); returns a handle.</summary>
        int PlayLoop(string cueId, Transform follow);
        /// <summary>Stops a loop (or an attached one-shot) with a fade in seconds.</summary>
        void StopLoop(int handle, float fade);
        /// <summary>
        /// One-shot cue that follows a transform while it plays (G9: the interval signal from Tinker's PRC-77 speaker);
        /// returns a handle for <see cref="StopLoop"/>.
        /// </summary>
        int PlayAttached(string cueId, Transform follow, float volume = 1f);
        /// <summary>
        /// The game's only AudioListener belongs to the AudioService and follows <see cref="IPlayerRig.Eye"/>. Cinematics
        /// move it to their camera with an override (null returns it to the player). Nobody else adds an AudioListener.
        /// </summary>
        void SetListenerOverride(Transform target);
        /// <summary>Layered weapon shot (transient, mech, body, zone tail, far variant).</summary>
        void PlayWeaponShot(string weaponId, Vector3 position, bool isPlayer);
        /// <summary>Acoustic zone the listener is in.</summary>
        ZoneType CurrentZone { get; }
        /// <summary>Grenade tinnitus (muffle + ring) for seconds.</summary>
        void Tinnitus(float seconds);
        /// <summary>Sets a category gain (linear 0..1).</summary>
        void SetCategoryGain(string category, float linear);
        /// <summary>Ducks a category by dB for seconds.</summary>
        void Duck(string category, float db, float seconds);
        /// <summary>True if the cue exists in LIB_Audio.</summary>
        bool HasCue(string cueId);
    }
}
