using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Mission.Editor
{
    /// <summary>
    /// LongShadow/Mission/Beat Jump (TECH_PLAN §4.1 BeatJumpWindow, §7.17): in play mode, jump to any checkpoint
    /// (ResumeFrom), start the mission, reload the last checkpoint, and watch the director: beat, light s, story clock,
    /// active objectives, flags and what each running script waits for.
    /// </summary>
    public sealed class BeatJumpWindow : EditorWindow
    {
        private Vector2 _scroll;

        /// <summary>Opens the window.</summary>
        [MenuItem("LongShadow/Mission/Beat Jump Window", false, 30)]
        public static void Open() => GetWindow<BeatJumpWindow>("Beat Jump");

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            var host = MissionSystems.Instance;
            var d = host != null ? host.Director : null;
            if (!Application.isPlaying || d == null)
            {
                EditorGUILayout.HelpBox("Enter play mode with PF_MissionSystems in the scene (S_Persistent or a dev scene).", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("Beat", d.Beat + " " + BeatTable.Get(d.Beat).Name + (d.IsRunning ? "" : " (not running)"));
            EditorGUILayout.LabelField("LightStage s", d.CurrentS.ToString("0.000") + "   clock " + StoryClock.Format(d.StoryClockMinutes));
            EditorGUILayout.LabelField("Checkpoint", (d.Checkpoints.Last ?? "-") + (d.Checkpoints.SavePending ? " (pending " + d.Checkpoints.Pending + ": " + d.Checkpoints.PendingReason + ")" : ""));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start Mission")) d.StartMission();
                if (GUILayout.Button("Reload Checkpoint")) d.Checkpoints.Reload(false);
                if (GUILayout.Button("Quit To Session")) d.QuitToSession();
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Jump to", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var i = 0;
                foreach (var cp in SequenceIds.Checkpoints)
                {
                    if (GUILayout.Button(cp)) d.ResumeFrom(cp);
                    if (++i % 6 == 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                    }
                }
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Objectives", EditorStyles.boldLabel);
            foreach (var o in d.Objectives.Active) EditorGUILayout.LabelField(Objectives.ObjectiveFormat.HudLine(o));
            EditorGUILayout.LabelField("Intel " + d.Objectives.IntelCount + "/3, optional " + d.Objectives.OptionalCount + "/2; markers " + d.Objectives.Markers.Count);
            EditorGUILayout.LabelField("Scripts", EditorStyles.boldLabel);
            foreach (var t in d.Runner.Tasks) EditorGUILayout.LabelField(t.ToString(), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }
    }
}
