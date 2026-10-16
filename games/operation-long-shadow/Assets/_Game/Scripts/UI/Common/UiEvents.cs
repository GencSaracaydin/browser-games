using LongShadow.Core;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Mission event ids the UI raises through <c>GameEvents.RaiseMissionEvent</c> (listeners compare the strings; a
    /// <c>SequenceIds</c> copy is requested in Docs/CORE_CHANGES.md).
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.SequenceIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class UiEvents
    {
        /// <summary>The mission title card finished and its bars retracted (GDD §8.3: then D007 plays).</summary>
        public const string TitleCardFinished = SequenceIds.TitleCardFinished;
        /// <summary>The debrief finished typing and shows its buttons (GDD §8.9).</summary>
        public const string DebriefReady = SequenceIds.DebriefReady;
        /// <summary>
        /// [REPLAY SESSION] was pressed: the UI calls IMissionDirector.QuitToSession and shows the click gate, whose click
        /// (a browser gesture that re-locks the pointer) makes WP01's Bootstrap start again from the intro.
        /// </summary>
        public const string DebriefReplay = SequenceIds.DebriefReplay;
        /// <summary>[END SESSION] was pressed (the UI also calls IMissionDirector.QuitToSession and shows the click gate).</summary>
        public const string DebriefEnd = SequenceIds.DebriefEnd;
        /// <summary>
        /// Prefix of "a beat was selected": "UI.Debrief.Select.CP5". The UI calls IMissionDirector.ResumeFrom and then
        /// pauses, so the pause menu's RESUME click re-locks the pointer.
        /// </summary>
        public const string DebriefSelectPrefix = SequenceIds.DebriefSelectPrefix;
        /// <summary>QUIT TO SESSION was confirmed in the pause menu.</summary>
        public const string PauseQuit = SequenceIds.PauseQuit;
        /// <summary>
        /// The click gate turned ready while shown (boot, QUIT TO SESSION, END SESSION). WP01's Bootstrap can re-arm the
        /// browser gesture (pointer lock, audio, fullscreen) on it, as it does at boot.
        /// </summary>
        public const string GateReady = SequenceIds.UIGateReady;
        /// <summary>RESTART CHECKPOINT was confirmed in the pause menu.</summary>
        public const string PauseRestart = SequenceIds.PauseRestart;
    }
}
