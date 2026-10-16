using LongShadow.Core;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// The squad kill-share rule (GDD §15, about 25 %): squad rounds are lethal only on actors flagged
    /// <see cref="ITargetable.SquadTarget"/> (scripted kills, the RPG gunner after the wave-off, the R3 nest after 20 s)
    /// and on Legion soldiers the player has not engaged for 8 s; everyone else (Greycoats, anyone the player is fighting)
    /// only gets suppressive fire (×0.5 rate and accuracy for 2 s, GDD §14.3). Pure.
    /// </summary>
    public static class KillShare
    {
        /// <summary>Seconds since the player last engaged a Legion soldier before the squad may kill him (GDD §15).</summary>
        public const float PlayerEngagementSeconds = 8f;

        /// <summary>True when a squad round at this target may kill it.</summary>
        public static bool IsLethal(ITargetable target, float now)
        {
            if (target == null) return false;
            if (target.SquadTarget) return true;
            return IsLethal(target.Team, false, target.LastEngagedByPlayer, now);
        }

        /// <summary>The rule on raw values (tests).</summary>
        public static bool IsLethal(Team team, bool squadTarget, float lastEngagedByPlayer, float now)
        {
            if (squadTarget) return true;
            if (team != Team.Legion) return false;
            return now - lastEngagedByPlayer >= PlayerEngagementSeconds;
        }
    }
}
