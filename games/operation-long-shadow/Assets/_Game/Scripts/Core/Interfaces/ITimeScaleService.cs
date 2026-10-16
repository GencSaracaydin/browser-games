namespace LongShadow.Core
{
    /// <summary>
    /// Prioritised time-scale requests (TECH_PLAN §5.4). Effective <c>Time.timeScale</c> = the request with the highest
    /// priority (see <see cref="TimePriority"/>), else 1.
    /// </summary>
    public interface ITimeScaleService
    {
        /// <summary>Pushes a request; returns a handle for <see cref="Pop"/>.</summary>
        int Push(float scale, int priority, string reason);
        /// <summary>Removes a request.</summary>
        void Pop(int handle);
        /// <summary>
        /// Changes the scale of a live request (ramps such as Death 0.3 over 0.6 s, TECH_PLAN §5.4) without reordering it
        /// among equal-priority requests. Returns false for unknown handles.
        /// </summary>
        bool SetScale(int handle, float scale);
        /// <summary>Effective scale this frame.</summary>
        float Current { get; }
    }
}
