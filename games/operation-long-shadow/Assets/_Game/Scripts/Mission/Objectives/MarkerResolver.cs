using LongShadow.Core;
using LongShadow.Mission.Data;
using UnityEngine;

namespace LongShadow.Mission.Objectives
{
    /// <summary>
    /// Resolves an <see cref="ObjectiveTarget"/> to something the HUD can follow: a markup id through
    /// <c>IMarkerRegistry</c>, a squad member through <c>ISquadService.Member</c>, an enemy through
    /// <c>IEncounterService.Actor</c>, a destructible through <c>Destructibles</c>. While the target does not resolve
    /// (section not loaded, enemy not spawned yet) the Appendix A fallback position is used, so a marker always has a
    /// bearing.
    /// </summary>
    public sealed class MarkerResolver
    {
        private ServiceRef<IMarkerRegistry> _markers;
        private ServiceRef<ISquadService> _squad;
        private ServiceRef<IEncounterService> _encounters;

        /// <summary>The transform of a target, or null while it does not resolve.</summary>
        public Transform Resolve(in ObjectiveTarget target)
        {
            if (!target.IsValid) return null;
            switch (target.Kind)
            {
                case ObjectiveTargetKind.Marker:
                    return _markers.Value?.Get(target.Id);
                case ObjectiveTargetKind.SquadMember:
                    return _squad.Value?.Member(target.Id);
                case ObjectiveTargetKind.Actor:
                    var actor = _encounters.Value?.Actor(target.Id);
                    return actor != null && actor.IsAlive ? actor.Root : null;
                case ObjectiveTargetKind.Destructible:
                    return Destructibles.TryGet(target.Id, out var d) && !d.IsDestroyed ? d.Root : null;
                default:
                    return null;
            }
        }

        /// <summary>A marker definition for a target: following its transform when resolved, else at the fallback.</summary>
        public MarkerDef Build(string markerId, in ObjectiveTarget target, MarkerKind kind, string label, bool showDistance,
                               bool throughWalls)
        {
            var t = Resolve(target);
            return new MarkerDef
            {
                Id = markerId,
                Target = t,
                Offset = t != null ? target.Offset : target.Fallback,
                Kind = kind,
                Label = label ?? string.Empty,
                ShowDistance = showDistance,
                ThroughWalls = throughWalls,
            };
        }

        /// <summary>True if the target is gone for good (enemy killed, destructible destroyed): its marker is dropped.</summary>
        public bool IsGone(in ObjectiveTarget target, System.Func<string, bool> killed)
        {
            switch (target.Kind)
            {
                case ObjectiveTargetKind.Actor:
                    return killed != null && killed(target.Id);
                case ObjectiveTargetKind.Destructible:
                    return Destructibles.TryGet(target.Id, out var d) && d.IsDestroyed;
                default:
                    return false;
            }
        }
    }
}
