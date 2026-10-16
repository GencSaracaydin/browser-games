using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Layered weapon shots (TECH_PLAN §7.9, GDD §10.2; WP06_SFX import notes):
    /// <list type="bullet">
    /// <item>Player: transient (random of 4, ±3 % pitch, ±1.5 dB), mechanical layer and low body layer at the transient's
    /// pitch, all 2D and sample-aligned; the zone tail of the listener's zone.</item>
    /// <item>AI: the same transient and body in 3D at the muzzle, crossfaded (equal power) into the baked <c>_FAR</c>
    /// variant between 40 and 120 m, delayed by distance / speed of sound, with the tail of the zone at the muzzle.</item>
    /// <item>Tails: on the first round of a burst, every 4th round and on release (<see cref="BurstTracker"/>); the release
    /// tail starts inside its clip by the time since the last round, so it lines up with that round.</item>
    /// </list>
    /// Missing cues are skipped (logged once by the service).
    /// </summary>
    internal sealed class WeaponShotPlayer
    {
        private const int PlayerPriority = 95;

        private readonly AudioService _svc;
        private readonly BurstTracker _bursts = new BurstTracker(16);
        private readonly BurstTracker.Release[] _releases = new BurstTracker.Release[8];
        private readonly Dictionary<string, WeaponSoundProfile> _profiles = new Dictionary<string, WeaponSoundProfile>(16, System.StringComparer.Ordinal);

        public WeaponShotPlayer(AudioService service)
        {
            _svc = service;
        }

        /// <summary>Plays one round.</summary>
        public void Play(string weaponId, Vector3 position, bool isPlayer, float now)
        {
            var p = Profile(weaponId);
            var listener = AudioRuntime.ListenerPosition;
            var key = BurstTracker.KeyOf(weaponId, isPlayer, position);
            var tailNow = _bursts.OnShot(key, weaponId, isPlayer, isPlayer ? listener : position, now, out _);
            if (isPlayer) PlayPlayer(p, tailNow);
            else PlayAI(p, position, Vector3.Distance(position, listener), tailNow);
        }

        /// <summary>Plays release tails of bursts that ended.</summary>
        public void Tick(float now)
        {
            var n = _bursts.CollectReleases(now, _releases);
            for (var i = 0; i < n; i++)
            {
                var r = _releases[i];
                var p = Profile(r.WeaponId);
                if (r.IsPlayer)
                {
                    PlayTail(p, AudioRuntime.ListenerPosition, AudioRuntime.Zone, true, 1f, 0f, r.Offset);
                    continue;
                }
                var dist = Vector3.Distance(r.Position, AudioRuntime.ListenerPosition);
                if (p.Suppressed && dist > 60f) continue;
                AudioMath.FarShare(dist, AudioConstants.WeaponNearDistance, AudioConstants.WeaponFarDistance, out var near, out _);
                var propagation = AudioMath.PropagationDelay(dist);
                var delay = Mathf.Max(0f, propagation - r.Offset);
                var offset = Mathf.Max(0f, r.Offset - propagation);
                PlayTail(p, r.Position, ZoneTag.Resolve(r.Position), false, 0.35f + 0.65f * near, delay, offset);
            }
        }

        /// <summary>Forgets running bursts (checkpoint reload).</summary>
        public void Clear() => _bursts.Clear();

        private WeaponSoundProfile Profile(string weaponId)
        {
            if (weaponId == null) weaponId = WeaponIds.AK;
            if (_profiles.TryGetValue(weaponId, out var p)) return p;
            p = WeaponSoundProfile.For(weaponId);
            _profiles[weaponId] = p;
            return p;
        }

        private void PlayPlayer(WeaponSoundProfile p, bool tailNow)
        {
            var pos = AudioRuntime.ListenerPosition;
            if (!_svc.TryGetCue(p.Shot, out var shot) || !shot.HasClips) return;
            var pitch = AudioMath.JitterPitch(shot.PitchJitter, _svc.Random01());
            var gain = AudioMath.JitterGain(shot.VolumeJitterDb, _svc.Random01());
            var r = Layer2D(_svc.PickClip(shot.Id, shot.Clips), shot, pos, shot.Volume * gain, pitch);
            _svc.StartVoice(ref r);

            if (p.Mech != null && _svc.Library != null && _svc.Library.TryGet(p.Mech, out var mech) && mech != null && mech.HasClips)
            {
                var m = Layer2D(_svc.PickClip(mech.Id, mech.Clips), mech, pos, mech.Volume, AudioMath.JitterPitch(mech.PitchJitter, _svc.Random01()));
                _svc.StartVoice(ref m);
            }
            if (p.Body != null && _svc.TryGetCue(p.Body, out var body) && body.HasClips)
            {
                var b = Layer2D(_svc.PickClip(body.Id, body.Clips), body, pos, body.Volume * gain, pitch);
                _svc.StartVoice(ref b);
            }
            if (tailNow) PlayTail(p, pos, AudioRuntime.Zone, true, 1f, 0f, 0f);
        }

        private void PlayAI(WeaponSoundProfile p, Vector3 pos, float dist, bool tailNow)
        {
            if (p.Suppressed && dist > 80f) return; // a suppressed pistol is inaudible beyond ~80 m
            if (!_svc.TryGetCue(p.Shot, out var shot)) return;
            var t = AudioMath.FarShare(dist, shot.FarCrossfade.x, shot.FarCrossfade.y, out var near, out var far);
            var m = _svc.Muffle;
            if (m * 0.85f > t)
            {
                var tm = m * 0.85f;
                near = Mathf.Cos(tm * Mathf.PI * 0.5f);
                far = Mathf.Sin(tm * Mathf.PI * 0.5f);
            }
            if (!shot.HasFar)
            {
                near = 1f;
                far = 0f;
            }
            if (dist > Mathf.Max(shot.FarMaxDistance, shot.MaxDistance) * 1.1f) return;
            var delay = AudioMath.PropagationDelay(dist);
            var pitch = AudioMath.JitterPitch(shot.PitchJitter, _svc.Random01());
            var gain = AudioMath.JitterGain(shot.VolumeJitterDb, _svc.Random01()) * p.Level;
            var mixGain = AudioRuntime.Mix.Gain(MixCategory.Effects);

            if (near > 0.01f && shot.HasClips)
            {
                var r = Layer3D(_svc.PickClip(shot.Id, shot.Clips), shot, pos, shot.Volume * gain * near, pitch, p.NearMinDistance,
                    shot.MaxDistance, delay, dist, mixGain);
                _svc.StartVoice(ref r);
                if (near > 0.3f && p.Body != null && _svc.TryGetCue(p.Body, out var body) && body.HasClips)
                {
                    var b = Layer3D(_svc.PickClip(body.Id, body.Clips), body, pos, body.Volume * gain * near, pitch, p.NearMinDistance,
                        body.MaxDistance, delay, dist, mixGain);
                    b.Priority = Mathf.Max(0, b.Priority - 15);
                    _svc.StartVoice(ref b);
                }
            }
            if (far > 0.01f && shot.HasFar)
            {
                var f = Layer3D(_svc.PickClip(null, shot.FarClips), shot, pos, shot.Volume * gain * far, pitch, shot.FarMinDistance,
                    shot.FarMaxDistance, delay, dist, mixGain);
                _svc.StartVoice(ref f);
            }
            if (tailNow) PlayTail(p, pos, ZoneTag.Resolve(pos), false, 0.35f + 0.65f * near, delay, 0f);
        }

        private void PlayTail(WeaponSoundProfile p, Vector3 pos, ZoneType zone, bool isPlayer, float level, float delay, float offset)
        {
            if (p.TailGain <= 0f || level <= 0.01f) return;
            if (!_svc.TryGetCue(AudioCues.WeaponTail(zone), out var tail) || !tail.HasClips) return;
            var clip = _svc.PickClip(tail.Id, tail.Clips);
            if (clip == null || offset >= clip.length - 0.05f) return;
            var dist = Vector3.Distance(pos, AudioRuntime.ListenerPosition);
            var vol = tail.Volume * p.TailGain * level;
            var r = new PlayRequest
            {
                Clip = clip, CueId = tail.Id, Category = MixCategory.Effects, Position = pos, Volume = vol,
                Pitch = AudioMath.JitterPitch(tail.PitchJitter, _svc.Random01()),
                // Reflections arrive from everywhere: the player's tail is mostly 2D, an AI tail partly localised.
                SpatialBlend = isPlayer ? 0.25f : 0.6f,
                MinDistance = isPlayer ? 5f : 10f, MaxDistance = isPlayer ? 50f : Mathf.Max(400f, tail.MaxDistance),
                Rolloff = AudioRolloffMode.Logarithmic,
                Priority = isPlayer ? PlayerPriority - 10 : Mathf.Max(0, tail.Priority - 10),
                Delay = delay, StartOffset = offset,
                Audibility = vol * AudioRuntime.Mix.Gain(MixCategory.Effects) * (isPlayer ? 1f : AudioMath.RolloffGain(dist, 10f, 400f, AudioRolloffMode.Logarithmic)),
            };
            _svc.StartVoice(ref r);
        }

        private static PlayRequest Layer2D(AudioClip clip, AudioCue cue, Vector3 pos, float volume, float pitch) =>
            new PlayRequest
            {
                Clip = clip, CueId = cue.Id, Category = MixCategory.Effects, Position = pos, Volume = volume, Pitch = pitch,
                SpatialBlend = 0f, MinDistance = 1f, MaxDistance = 50f, Rolloff = AudioRolloffMode.Logarithmic,
                Priority = PlayerPriority, Audibility = volume,
            };

        private static PlayRequest Layer3D(AudioClip clip, AudioCue cue, Vector3 pos, float volume, float pitch, float min, float max,
                                           float delay, float dist, float mixGain) =>
            new PlayRequest
            {
                Clip = clip, CueId = cue.Id, Category = MixCategory.Effects, Position = pos, Volume = volume, Pitch = pitch,
                SpatialBlend = 1f, MinDistance = min, MaxDistance = max, Rolloff = AudioRolloffMode.Logarithmic,
                Priority = cue.Priority, Delay = delay,
                Audibility = volume * mixGain * AudioMath.RolloffGain(dist, min, max, AudioRolloffMode.Logarithmic),
            };
    }
}
