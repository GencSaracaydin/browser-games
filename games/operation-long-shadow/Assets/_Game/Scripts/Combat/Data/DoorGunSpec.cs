using System;

namespace LongShadow.Combat.Data
{
    /// <summary>Beat 11 left-door gun mount (GDD §13.3, §7.11). Fire numbers live in <see cref="WeaponTable.DoorGun"/>. Immutable.</summary>
    [Serializable]
    public sealed class DoorGunSpec
    {
        /// <summary>Heat added per shot.</summary>
        public float HeatPerShot;
        /// <summary>Heat at which the gun overheats (≈ 6 s of fire at 550 RPM).</summary>
        public float OverheatAt;
        /// <summary>Seconds until the gun can fire again after an overheat.</summary>
        public float OverheatRecovery;
        /// <summary>Seconds to cool from full to zero while idle.</summary>
        public float IdleRecovery;
        /// <summary>Seconds without a shot before idle cooling starts (tuning; otherwise sustained fire would never overheat).</summary>
        public float IdleDelay = 0.2f;
        /// <summary>Yaw limit either side of the door normal.</summary>
        public float YawLimit;
        /// <summary>Pitch limit up.</summary>
        public float PitchUp;
        /// <summary>Pitch limit down.</summary>
        public float PitchDown;
        /// <summary>Incoming damage scale while mounted (Beat 11).</summary>
        public float IncomingDamageScale;
        /// <summary>Health floor while mounted (the player cannot die).</summary>
        public float HealthFloor;
        /// <summary>Eye position relative to the gun pivot (metres; behind and above the spade grips).</summary>
        public UnityEngine.Vector3 EyeOffset = new UnityEngine.Vector3(0f, 0.17f, -0.62f);
    }
}
