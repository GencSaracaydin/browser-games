using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// Plays the mission in the fake world: looks at what the running beat waits for (the leaves of its current
    /// <see cref="Wait"/>) and, after a short "reaction time", does what a player or the AI would: walk into the trigger,
    /// go near the marker, kill the enemies, use the interaction (only once its gate is open), take the intel (only once
    /// it was spawned), destroy the target (only when vulnerable), glass the POIs. Lines, timers, squad moves and
    /// cinematics simply pass with time.
    /// </summary>
    internal sealed class Autopilot
    {
        public const float Reaction = 0.5f;

        private readonly FakeWorld _w;
        private readonly Dictionary<Wait, float> _seen = new Dictionary<Wait, float>();
        private readonly List<Wait> _leaves = new List<Wait>();
        public readonly List<string> Log = new List<string>();

        public Autopilot(FakeWorld world) => _w = world;

        public void Step()
        {
            _leaves.Clear();
            var task = _w.Director.CurrentBeatTask;
            for (var depth = 0; task != null && depth < 8; depth++)
            {
                if (task.CurrentWait != null)
                {
                    CollectFirst(task.CurrentWait);
                    break;
                }
                task = task.WaitingFor;
            }
            foreach (var leaf in _leaves)
            {
                if (!_seen.TryGetValue(leaf, out var since))
                {
                    _seen[leaf] = _w.RealNow;
                    continue;
                }
                if (_w.RealNow - since < Reaction) continue;
                Act(leaf);
            }
        }

        private void CollectFirst(Wait w)
        {
            if (w.Children.Count == 0)
            {
                _leaves.Add(w);
                return;
            }
            if (w.Kind == WaitKind.All)
            {
                foreach (var c in w.Children) CollectFirst(c);
                return;
            }
            // Any: act on the first actionable leaf (the beat's intended path).
            foreach (var c in w.Children)
            {
                if (!Actionable(c)) continue;
                CollectFirst(c);
                return;
            }
            CollectFirst(w.Children[0]);
        }

        private static bool Actionable(Wait w)
        {
            if (w.Children.Count > 0) return true;
            switch (w.Kind)
            {
                case WaitKind.Trigger:
                case WaitKind.PlayerNear:
                case WaitKind.Encounter:
                case WaitKind.Killed:
                case WaitKind.Interaction:
                case WaitKind.Intel:
                case WaitKind.Destroyed:
                case WaitKind.Glassed:
                    return true;
                default:
                    return false;
            }
        }

        private void Act(Wait w)
        {
            if (w.IsDone) return;
            switch (w.Kind)
            {
                case WaitKind.Trigger:
                    if (MissionTriggers.TryGetShape(w.Id, out var shape) && !shape.Contains(_w.PlayerPos))
                    {
                        Note("enter " + w.Id);
                        _w.PlayerPos = shape.Inside;
                    }
                    break;
                case WaitKind.PlayerNear:
                    if (MissionMarkers.TryGetFallback(w.Id, out var p))
                    {
                        Note("go to " + w.Id);
                        _w.PlayerPos = p;
                    }
                    break;
                case WaitKind.Encounter:
                    Note("clear " + w.Id + (w.Sub != null ? "/" + w.Sub : ""));
                    _w.Encounters.KillAll(w.Id, w.Sub, w.Sub != null ? (int)w.Radius : 0);
                    break;
                case WaitKind.Killed:
                    if (w.Sub != null)
                        foreach (var id in w.Sub.Split(','))
                            _w.Encounters.Kill(id);
                    else _w.Encounters.Kill(w.Id);
                    Note("kill " + (w.Sub ?? w.Id));
                    break;
                case WaitKind.Interaction:
                    if (_w.Director.Gates.IsOpen(w.Id))
                    {
                        Note("use " + w.Id);
                        GameEvents.RaiseInteracted(w.Id);
                    }
                    break;
                case WaitKind.Intel:
                    if (_w.Spawner.Spawned.Contains(w.Id))
                    {
                        Note("take " + w.Id);
                        _w.Director.CollectIntel(w.Id);
                    }
                    break;
                case WaitKind.Destroyed:
                    if (_w.Destructibles.TryGetValue(w.Id, out var d) && !d.Invulnerable)
                    {
                        Note("destroy " + w.Id);
                        d.ForceDestroy();
                    }
                    break;
                case WaitKind.Glassed:
                    Note("glass POIs");
                    GameEvents.RaiseMissionEvent(MissionEvents.Glassed(MissionMarkers.B1POI1));
                    GameEvents.RaiseMissionEvent(MissionEvents.Glassed(MissionMarkers.B1POI2));
                    GameEvents.RaiseMissionEvent(MissionEvents.Glassed(MissionMarkers.B1POI3));
                    break;
            }
        }

        private void Note(string what)
        {
            if (Log.Count > 0 && Log[Log.Count - 1].EndsWith(what)) return;
            Log.Add(_w.Now.ToString("0.0") + " B" + _w.Director.Beat + " " + what);
        }
    }
}
