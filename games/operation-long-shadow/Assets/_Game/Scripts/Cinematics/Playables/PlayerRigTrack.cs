using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>Player rig actions (no binding; IPlayerRig, IWeaponService, IFirstPersonBody).</summary>
    [TrackColor(0.4f, 0.4f, 0.95f)]
    [TrackClipType(typeof(PlayerRigClip))]
    public sealed class PlayerRigTrack : TrackAsset
    {
    }
}
