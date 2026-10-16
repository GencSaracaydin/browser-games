using UnityEngine;
using UnityEngine.TestTools;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>
    /// Probes whether <see cref="CharacterController.Move"/> runs in this edit-mode context. The rig tests tick the motor,
    /// which moves the controller every frame; if an editor version refuses that outside play mode (it logs
    /// "CharacterController.Move called on inactive controller"), failing log messages are ignored for the rest of the
    /// current test (the log scope is per test) so tests that only need the frame loop still run, and movement tests
    /// report Ignore instead of failing.
    /// </summary>
    internal static class EditModeCharacterController
    {
        /// <summary>True when a 5 cm Move moved the controller without logging an error. Restores the position.</summary>
        public static bool Probe(CharacterController cc)
        {
            var t = cc.transform;
            var p0 = t.position;
            var errors = 0;
            Application.LogCallback callback = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
            };
            var previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += callback;
            try
            {
                Physics.SyncTransforms();
                cc.Move(new Vector3(0f, 0f, 0.05f));
            }
            finally
            {
                Application.logMessageReceived -= callback;
            }
            var moved = (t.position - p0).sqrMagnitude > 1e-6f;
            t.position = p0;
            Physics.SyncTransforms();
            if (errors == 0) LogAssert.ignoreFailingMessages = previous;
            return moved && errors == 0;
        }
    }
}
