using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>What PlayerWeapon tells the ViewmodelController every frame.</summary>
    public struct ViewmodelState
    {
        /// <summary>The view model renders (false while holstered or fully lowered for a cinematic).</summary>
        public bool Visible;
        /// <summary>ADS blend 0..1.</summary>
        public float AdsBlend;
        /// <summary>Lowered 0..1 (switching, holstering; eased by the caller).</summary>
        public float Lowered01;
        /// <summary>Mounted on the door gun (no sprint pose, little bob).</summary>
        public bool Mounted;
        /// <summary>A reload runs.</summary>
        public bool Reloading;
        /// <summary>Seconds into the reload.</summary>
        public float ReloadElapsed;
        /// <summary>The running reload's keys.</summary>
        public ReloadSpec Reload;
        /// <summary>The running reload is the empty one (magazine drops free, charge).</summary>
        public bool EmptyReload;
        /// <summary>Grenade phase.</summary>
        public GrenadePhase Grenade;
        /// <summary>Seconds into the grenade phase.</summary>
        public float GrenadePhaseTime;
        /// <summary>Knife action running.</summary>
        public bool Knife;
        /// <summary>Seconds into the knife action.</summary>
        public float KnifeTime;
        /// <summary>The knife lunges.</summary>
        public bool KnifeLunge;
    }
}
