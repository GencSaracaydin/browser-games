using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Editor.Builders;
using LongShadow.World.Level.Editor.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Markup
{
    /// <summary>
    /// MarkupTools (TECH_PLAN §7.15): one row per section scene — open it, rebuild it (geometry + markup + NavMesh), or
    /// validate its Appendix A markup — and the last validation results. Placement itself is part of the section build
    /// (markers carry section components), so "Place" is a rebuild.
    /// </summary>
    public sealed class MarkupToolsWindow : EditorWindow
    {
        private readonly Dictionary<SectionId, MarkupReport> _reports = new Dictionary<SectionId, MarkupReport>();
        private Vector2 _scroll;
        private SectionId _selected = SectionId.Market;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Appendix A markup (Assets/_Game/Data/markup_ids.csv)", EditorStyles.boldLabel);
            foreach (var id in SectionBuilders.Order)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = Sections.SceneName(id);
                    if (_reports.TryGetValue(id, out var r)) label += "  —  " + r.Summary;
                    if (GUILayout.Toggle(_selected == id, label, EditorStyles.radioButton)) _selected = id;
                    if (GUILayout.Button("Open", GUILayout.Width(52)) && SaveOpenScenesNoPrompt())
                        EditorSceneManager.OpenScene(Sections.ScenePath(id), OpenSceneMode.Single);
                    if (GUILayout.Button("Rebuild", GUILayout.Width(64))) SectionBuilders.Build(id);
                    if (GUILayout.Button("Validate", GUILayout.Width(64))) Validate(id);
                }
            }
            if (GUILayout.Button("Validate all section scenes")) foreach (var id in SectionBuilders.Order) Validate(id);
            EditorGUILayout.Space();
            if (!_reports.TryGetValue(_selected, out var sel)) return;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var e in sel.Errors) EditorGUILayout.HelpBox(e, MessageType.Error);
            foreach (var w in sel.Warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        private void Validate(SectionId id)
        {
            if (!System.IO.File.Exists(System.IO.Path.Combine(BuilderUtil.ProjectRoot, Sections.ScenePath(id))))
            {
                LSLog.Warn(LogCategory.Level, Sections.ScenePath(id) + " does not exist yet; build the section first.");
                return;
            }
            /* no modal prompt: modal dialogs block the editor and the MCP bridge */ { for (int __i = 0; __i < UnityEngine.SceneManagement.SceneManager.sceneCount; __i++) { var __s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(__i); if (__s.isDirty && !string.IsNullOrEmpty(__s.path)) EditorSceneManager.SaveScene(__s); } }
            var scene = EditorSceneManager.OpenScene(Sections.ScenePath(id), OpenSceneMode.Single);
            var report = MarkupValidator.Validate(scene, id, MarkupIds.Load());
            MarkupValidator.Write(id, report);
            _reports[id] = report;
            _selected = id;
            Repaint();
        }

        // No modal prompt: modal dialogs block the editor and the MCP bridge.
        private static bool SaveOpenScenesNoPrompt()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isDirty && !string.IsNullOrEmpty(s.path)) EditorSceneManager.SaveScene(s);
            }
            return true;
        }
    }
}
