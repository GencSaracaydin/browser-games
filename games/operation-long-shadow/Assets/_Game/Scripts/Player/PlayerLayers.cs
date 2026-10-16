using LongShadow.Core;

namespace LongShadow.Player
{
    /// <summary>Physics masks used by the player (TECH_PLAN §3.9 Player row).</summary>
    public static class PlayerLayers
    {
        /// <summary>Surfaces the player stands on, vaults onto and snaps to: Default | Vehicle.</summary>
        public const int Walkable = LayerIds.Ground;

        /// <summary>
        /// Everything the player capsule collides with: Default | Character | Vehicle | PlayerClip. Used for stand-up
        /// clearance and ledge clearance checks, so the player never mantles into an AI body or through a clip wall.
        /// </summary>
        public const int Solid = (1 << LayerIds.Default) | (1 << LayerIds.Character) | (1 << LayerIds.Vehicle) |
                                 (1 << LayerIds.PlayerClip);

        /// <summary>Interactable colliders (overlap candidates for <see cref="Interaction.PlayerInteractor"/>).</summary>
        public const int InteractCandidates = 1 << LayerIds.Interactable;

        /// <summary>World camera culling: everything except Viewmodel, Booth and Trigger (TECH_PLAN §3.6).</summary>
        public const int WorldCameraCulling = ~((1 << LayerIds.Viewmodel) | (1 << LayerIds.Booth) | (1 << LayerIds.Trigger));

        /// <summary>View-model camera culling: Viewmodel only (TECH_PLAN §3.6).</summary>
        public const int ViewmodelCameraCulling = 1 << LayerIds.Viewmodel;
    }
}
