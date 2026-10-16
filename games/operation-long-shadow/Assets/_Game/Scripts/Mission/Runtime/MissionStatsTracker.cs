using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// Debrief statistics (TECH_PLAN §7.11 MissionStats, GDD §8.9): player kills and headshots (ActorKilled with
    /// ByPlayer), shots fired (ShotFired with IsPlayer) and hits (HitConfirmed), grenades thrown by the player, deaths,
    /// gameplay time in beats 1-11 excluding pause, intel counts. Counters are cumulative over checkpoint reloads (a
    /// re-fought enemy counts again, like the CoD after-action report); deaths include every reload by death.
    /// </summary>
    public sealed class MissionStatsTracker
    {
        private bool _attached;
        private bool _paused;

        /// <summary>The statistics (live object; clone it for the debrief).</summary>
        public MissionStats Stats { get; } = new MissionStats();

        /// <summary>True while mission time is counted (beats 1-11).</summary>
        public bool Counting { get; set; }

        /// <summary>Subscribes to GameEvents.</summary>
        public void Attach()
        {
            if (_attached) return;
            _attached = true;
            GameEvents.ActorKilled += OnKilled;
            GameEvents.ShotFired += OnShot;
            GameEvents.HitConfirmed += OnHit;
            GameEvents.GrenadeThrown += OnGrenade;
            GameEvents.PlayerDied += OnDied;
            GameEvents.PauseChanged += OnPause;
        }

        /// <summary>Unsubscribes.</summary>
        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            GameEvents.ActorKilled -= OnKilled;
            GameEvents.ShotFired -= OnShot;
            GameEvents.HitConfirmed -= OnHit;
            GameEvents.GrenadeThrown -= OnGrenade;
            GameEvents.PlayerDied -= OnDied;
            GameEvents.PauseChanged -= OnPause;
        }

        /// <summary>Resets every counter (mission start).</summary>
        public void Clear()
        {
            Stats.Clear();
            _paused = false;
        }

        /// <summary>Adds real time while counting and not paused.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (Counting && !_paused) Stats.MissionSeconds += Mathf.Max(0f, unscaledDeltaTime);
        }

        /// <summary>Sets the intel counters (from the objective service).</summary>
        public void SetIntel(int main, int optional)
        {
            Stats.Intel = main;
            Stats.OptionalIntel = optional;
        }

        private void OnKilled(KillInfo k)
        {
            if (!k.ByPlayer || !Teams.IsEnemySide(k.VictimTeam)) return;
            Stats.Kills++;
            if (k.Headshot) Stats.Headshots++;
        }

        private void OnShot(ShotEvent s)
        {
            if (s.IsPlayer) Stats.ShotsFired++;
        }

        private void OnHit(bool kill, bool headshot) => Stats.ShotsHit++;

        private void OnGrenade(Vector3 from, Team team)
        {
            if (team == Team.Player) Stats.GrenadesThrown++;
        }

        private void OnDied(DamageInfo d) => Stats.Deaths++;
        private void OnPause(bool paused) => _paused = paused;
    }
}
