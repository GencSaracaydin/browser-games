namespace LongShadow.Characters.Body
{
    /// <summary>What a body prefab is for (set by <c>BodyPrefabBuilder</c>; drives layers and defaults).</summary>
    public enum BodyRole
    {
        /// <summary>Legion/Greycoat enemy: hitboxes on Hitbox, NavMeshAgent, ragdoll, corpse cap.</summary>
        Enemy = 0,
        /// <summary>Hollis/Sparrow/Tinker: hitboxes on SquadHitbox (VFX only), NavMeshAgent, never dies.</summary>
        Squad = 1,
        /// <summary>Booth set (seated Calder, interrogator): layer Booth, unscaled time, no agent.</summary>
        Booth = 2,
        /// <summary>Cinematic-only body (Magpie in the cockpit, Harrou in the 8 mm footage).</summary>
        Cinematic = 3,
        /// <summary>The player's shadow-only body (D19).</summary>
        ShadowProxy = 4,
        /// <summary>The player's first-person arms (D19).</summary>
        FirstPerson = 5,
    }
}
