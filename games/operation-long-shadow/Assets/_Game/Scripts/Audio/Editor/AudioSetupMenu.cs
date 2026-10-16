using System.Text;
using LongShadow.Audio.Ambience;
using LongShadow.Audio.Core;
using LongShadow.Audio.Foley;
using LongShadow.Audio.Music;
using LongShadow.Audio.VO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Audio.Editor
{
    /// <summary>
    /// One-click setup of the audio package for the editor holder (menus under <c>LongShadow/Audio/</c>): LIB_Audio,
    /// LIB_Dialogue, the <c>PF_AudioSystems</c> prefab (AudioService with the game's only AudioListener, VOManager,
    /// MusicDirector, AmbienceDirector, NumbersBroadcast, PAHorns, FootstepDirector; WP11 places it in S_Persistent) and helpers
    /// that add emitters to the selection. Every step is idempotent and callable from <c>Unity_RunCommand</c>.
    /// </summary>
    public static class AudioSetupMenu
    {
        /// <summary>The systems prefab (in the Audio folder WP06 owns).</summary>
        public const string SystemsPrefabPath = "Assets/_Game/Audio/Systems/PF_AudioSystems.prefab";

        /// <summary>Library → dialogue → systems prefab; returns the combined report.</summary>
        [MenuItem("LongShadow/Audio/Build All", priority = 100)]
        public static string BuildAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine(AudioLibraryBuilder.Build());
            sb.AppendLine(DialogueImporter.Import());
            sb.AppendLine(BuildSystemsPrefab());
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>Builds <c>PF_AudioSystems</c> with the libraries assigned (re-run after the libraries change).</summary>
        [MenuItem("LongShadow/Audio/Build Audio Systems Prefab", priority = 120)]
        public static string BuildSystemsPrefab()
        {
            var audioLib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioLibraryBuilder.LibraryPath);
            var dialogueLib = AssetDatabase.LoadAssetAtPath<DialogueLibrary>(DialogueImporter.LibraryPath);
            var go = new GameObject("PF_AudioSystems");
            var service = go.AddComponent<AudioService>();
            var vo = go.AddComponent<VOManager>();
            var music = go.AddComponent<MusicDirector>();
            var amb = go.AddComponent<AmbienceDirector>();
            go.AddComponent<NumbersBroadcast>();
            go.AddComponent<PAHorns>();
            go.AddComponent<FootstepDirector>();
            // The listener exists in the prefab so scene views show it; AudioService adopts it in Awake.
            var listener = new GameObject("AudioListener");
            listener.transform.SetParent(go.transform, false);
            listener.AddComponent<AudioListener>();
            AssignLibrary(service, audioLib);
            AssignLibrary(music, audioLib);
            AssignLibrary(amb, audioLib);
            AssignLibrary(vo, dialogueLib);
            BuilderUtil.EnsureFolder(System.IO.Path.GetDirectoryName(SystemsPrefabPath)?.Replace('\\', '/'));
            var prefab = BuilderUtil.SavePrefab(go, SystemsPrefabPath);
            var text = "[Audio] systems prefab: " + (prefab != null ? SystemsPrefabPath : "FAILED") +
                       " (LIB_Audio " + (audioLib != null ? "assigned" : "missing: run Build Audio Library") +
                       ", LIB_Dialogue " + (dialogueLib != null ? "assigned" : "missing: run Import Dialogue") + ")";
            LSLog.Info(LogCategory.Audio, text);
            return text;
        }

        /// <summary>Adds a VU-needle <see cref="AmplitudeFollower"/> (interrogator/TALLOW) to the selected needle transform.</summary>
        [MenuItem("LongShadow/Audio/Add VU Needle Follower To Selection", priority = 140)]
        public static void AddNeedleFollower()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                LSLog.Warn(LogCategory.Audio, "Select the VU needle transform (booth scrambler box) first.");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(go, "Add VU Needle Follower");
            var f = go.GetComponent<AmplitudeFollower>() ?? go.AddComponent<AmplitudeFollower>();
            // A VU meter integrates over ~300 ms (IEC 60268-17); the needle swings ±42° about its rest.
            f.Configure(new[] { SpeakerIds.Interrogator, SpeakerIds.Tallow }, go.transform, Vector3.forward, -42f, 42f, 0.3f, 0.3f);
            EditorUtility.SetDirty(go);
        }

        /// <summary>Adds a <see cref="CivilianEmitter"/> child at the selected object (a wall or window of a market house).</summary>
        [MenuItem("LongShadow/Audio/Add Civilian Emitter (G5)", priority = 141)]
        public static void AddCivilianEmitter() => AddChild<CivilianEmitter>("CIV_Emitter");

        /// <summary>Adds a <see cref="LoopEmitter"/> child at the selected object (lamp, generator, transmitter, radio).</summary>
        [MenuItem("LongShadow/Audio/Add Loop Emitter", priority = 142)]
        public static void AddLoopEmitter() => AddChild<LoopEmitter>("SND_Loop");

        /// <summary>Writes the manifest coverage of LIB_Audio to the console and returns it.</summary>
        [MenuItem("LongShadow/Audio/Report Audio Coverage", priority = 150)]
        public static string ReportCoverage()
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioLibraryBuilder.LibraryPath);
            var sb = new StringBuilder("[Audio] coverage of " + AudioLibraryBuilder.LibraryPath + "\n");
            if (lib == null) sb.AppendLine("  LIB_Audio does not exist yet (LongShadow/Audio/Build Audio Library).");
            else
            {
                var cues = new System.Collections.Generic.List<AudioCue>();
                foreach (var c in lib.Entries) cues.Add(c);
                AudioLibraryBuilder.AppendCoverage(cues, sb);
            }
            var text = sb.ToString();
            LSLog.Info(LogCategory.Audio, text);
            return text;
        }

        private static void AssignLibrary(Object component, Object library)
        {
            if (component == null || library == null) return;
            var so = new SerializedObject(component);
            var p = so.FindProperty("_library");
            if (p == null) return;
            p.objectReferenceValue = library;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddChild<T>(string name) where T : Component
        {
            var parent = Selection.activeGameObject;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add " + typeof(T).Name);
            if (parent != null)
            {
                go.transform.SetParent(parent.transform, false);
                go.transform.position = parent.transform.position;
            }
            go.AddComponent<T>();
            Selection.activeGameObject = go;
        }
    }
}
