using System;
using UnityEngine;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// Socket positions of a weapon model in its own space (metres, Unity axes: barrel along +Z, up +Y, origin at the
    /// pistol grip), exactly the convention of the WP03 art pipeline (<c>Tools/mesh/weapons/bl_weapon.py</c> exports
    /// <c>Socket_Muzzle</c>, <c>Socket_SightFront</c>, <c>Socket_SightRear</c>, <c>Socket_Grip</c>, <c>Socket_LeftHand</c>,
    /// <c>Socket_Eject</c>, <c>Socket_MagWell</c> empties). <see cref="WeaponTable"/> keeps defaults per weapon, used when a
    /// model has no sockets yet and to size the greybox placeholder.
    /// </summary>
    [Serializable]
    public struct WeaponSockets
    {
        /// <summary>Muzzle (flash, tracer start).</summary>
        public Vector3 Muzzle;
        /// <summary>Front sight post.</summary>
        public Vector3 SightFront;
        /// <summary>Rear sight aperture / notch (aligned with the eye when aiming down sights).</summary>
        public Vector3 SightRear;
        /// <summary>Right-hand grip (the model origin for hand-held weapons).</summary>
        public Vector3 Grip;
        /// <summary>Left-hand support grip (handguard).</summary>
        public Vector3 LeftHand;
        /// <summary>Ejection port.</summary>
        public Vector3 Eject;
        /// <summary>Magazine well entry (the magazine's pivot).</summary>
        public Vector3 MagWell;
        /// <summary>Rear end of the stock along Z (negative; greybox only).</summary>
        public float StockEnd;

        /// <summary>Creates a socket set.</summary>
        public WeaponSockets(Vector3 muzzle, Vector3 sightFront, Vector3 sightRear, Vector3 grip, Vector3 leftHand, Vector3 eject,
                             Vector3 magWell, float stockEnd)
        {
            Muzzle = muzzle;
            SightFront = sightFront;
            SightRear = sightRear;
            Grip = grip;
            LeftHand = leftHand;
            Eject = eject;
            MagWell = magWell;
            StockEnd = stockEnd;
        }

        /// <summary>Overall length (stock end to muzzle).</summary>
        public float Length => Muzzle.z - StockEnd;
    }
}
