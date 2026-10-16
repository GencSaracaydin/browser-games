using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// A vehicle on a spline (WP10 SplineVehicle: trucks, technical drive-in, heli approach/exfil). AI dismounts and
    /// mission scripts drive it through this interface; find it with <c>IMarkerRegistry.TryGet(id)</c> + GetComponent.
    /// </summary>
    public interface ISplineVehicle
    {
        /// <summary>Id (e.g. "B10.Truck1", "B11.Exfil").</summary>
        string Id { get; }
        /// <summary>Vehicle root.</summary>
        Transform Root { get; }
        /// <summary>Normalised position along the spline 0..1.</summary>
        float Progress01 { get; }
        /// <summary>Seconds since <see cref="Play"/> started (B11 windows are timed on this).</summary>
        float Elapsed { get; }
        /// <summary>True while moving.</summary>
        bool IsMoving { get; }
        /// <summary>True once the end of the spline is reached.</summary>
        bool Arrived { get; }
        /// <summary>Starts moving (speed in m/s; 0 = authored timing).</summary>
        void Play(float speed = 0f);
        /// <summary>Stops.</summary>
        void Stop();
        /// <summary>Jumps to a normalised position.</summary>
        void SetProgress(float t01);
        /// <summary>Seat or dismount points (tailgate, door gun pivot).</summary>
        Transform GetSeat(int index);
        /// <summary>Number of seats.</summary>
        int SeatCount { get; }
    }
}
