using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Ambience
{
    /// <summary>The world state the ambience beds are evaluated against (<see cref="AmbienceBedRule.Weight"/>).</summary>
    public struct AmbienceContext
    {
        /// <summary>LightStage <c>s</c> (GDD §9.1; 0 golden … 4 blue hour).</summary>
        public float S;
        /// <summary>Acoustic zone of the listener (ZoneTag).</summary>
        public ZoneType Zone;
        /// <summary>Current beat (0 intro, 1–11, 12 outro); negative = unknown (beat windows are then ignored).</summary>
        public int Beat;
        /// <summary>Listener position (proximity beds such as the station generator).</summary>
        public Vector3 Listener;
        /// <summary>The station's transmitter racks are still alive.</summary>
        public bool TransmittersOn;

        /// <summary>A neutral context: s 0, open desert, beat unknown, transmitters on.</summary>
        public static AmbienceContext Default => new AmbienceContext { S = 0f, Zone = ZoneType.OpenDesert, Beat = -1, TransmittersOn = true };
    }
}
