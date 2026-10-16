namespace LongShadow.Characters.Editor
{
    /// <summary>What <see cref="BodyPrefabBuilder"/> assembles for a <see cref="BodySpec"/>.</summary>
    public enum BodyKind
    {
        /// <summary>Enemy: HumanoidBody + Health + NavMeshAgent + capsule + ragdoll/hitboxes (layer Hitbox).</summary>
        Enemy = 0,
        /// <summary>Squadmate: like an enemy but invulnerable, hitboxes on SquadHitbox, jaw flap.</summary>
        Squad = 1,
        /// <summary>Cinematic-only third-person body: HumanoidBody without agent, health or ragdoll.</summary>
        Cinematic = 2,
        /// <summary>Booth body (layer Booth, unscaled time, BoothBody).</summary>
        Booth = 3,
        /// <summary>The player's shadow-only full body (PFB_Calder_Shadow, D19).</summary>
        Shadow = 4,
        /// <summary>The player's first-person arms (PFB_Calder_FP, D19).</summary>
        FirstPerson = 5,
    }
}
