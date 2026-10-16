namespace LongShadow.AI.Enemy
{
    /// <summary>Combat sub-states of <see cref="EnemyBrain"/> (TECH_PLAN §7.6: TakeCover, Peek/Fire, Suppress, Flank, Fallback, ThrowGrenade, FleeGrenade).</summary>
    public enum CombatMode
    {
        /// <summary>Not in combat.</summary>
        None,
        /// <summary>No cover nearby: fights from where it stands and shuffles sideways.</summary>
        Open,
        /// <summary>Running to a claimed cover node (also pushes and fallbacks).</summary>
        MoveToCover,
        /// <summary>Hidden behind cover.</summary>
        Cover,
        /// <summary>Peeking (lean or stand up) and firing bursts.</summary>
        Peek,
        /// <summary>Following a designated flank route.</summary>
        Flank,
        /// <summary>Greycoat pair suppressor: fires at the last known position while the partner flanks.</summary>
        Suppress,
        /// <summary>Throwing a grenade.</summary>
        Throw,
        /// <summary>Running from a grenade.</summary>
        Flee,
        /// <summary>Reloading.</summary>
        Reload,
        /// <summary>Holding a position before pushing (E6-08).</summary>
        Hold,
        /// <summary>Static on a perch (gunners, marksman, roof riflemen, vehicle bed).</summary>
        Perched,
        /// <summary>Stepping from a closet onto its perch.</summary>
        EnterPerch,
        /// <summary>Scripted sprint (sentry running to the gate, dismounts).</summary>
        Run,
    }
}
