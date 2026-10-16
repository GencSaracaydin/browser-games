using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Mission.Objectives
{
    /// <summary>
    /// LIB_Mission (TECH_PLAN D12): the mission's asset references by id (intel pickup models and intel-card photos).
    /// Asset: <c>Assets/_Game/Data/Libraries/LIB_Mission.asset</c>, built by LongShadow/Mission/Build Mission Library and
    /// registered by the S_Persistent LibraryRegistry (the editor also finds it in the folder). Missing entries fall back
    /// to greybox pickups and cards without a photo.
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Mission/Mission Library", fileName = "LIB_Mission")]
    public sealed class MissionAssetLibrary : AssetLibrary<MissionAssetEntry>
    {
        /// <summary>Asset path.</summary>
        public const string AssetPath = "Assets/_Game/Data/Libraries/LIB_Mission.asset";
    }
}
