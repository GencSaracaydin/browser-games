using System;
using System.Collections;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace LongShadow.Platform
{
    /// <summary>
    /// <see cref="ISectionStreamer"/> (TECH_PLAN §5.1, D1, D20; GDD §6.7). Loads and unloads the section scenes
    /// additively through Addressables (address = scene name, <see cref="Sections.AddressKey"/>), one operation at a
    /// time; after each load or unload it re-tetrahedralises the light probes, logs the time and memory, and raises
    /// <see cref="GameEvents.SectionLoaded"/>/<see cref="GameEvents.SectionUnloaded"/>. Requests coalesce (a load
    /// followed by an unload that has not started yet cancels out). Also loads S_Persistent for <see cref="Bootstrap"/>.
    /// Degrades instead of hanging: a section that is not in the catalog is loaded from its scene path in the editor
    /// (or from the dev placeholder scene), and otherwise marked failed, which <see cref="IsLoaded"/> reports as loaded
    /// (with an error) so beat scripts waiting on it continue.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform)]
    [DisallowMultipleComponent]
    public sealed class SectionStreamer : MonoBehaviour, ISectionStreamer
    {
        /// <summary>Folder of the dev placeholder scenes created by the Platform builders (never the real scenes).</summary>
        public const string PlaceholderFolder = "Assets/_Game/Scenes/Dev/Placeholders";

        /// <summary>Project path of the placeholder for a scene name ("S2_Market" -> ".../PH_S2_Market.unity").</summary>
        public static string PlaceholderPath(string sceneName) => PlaceholderFolder + "/PH_" + sceneName + ".unity";

        /// <summary>Project path of S_Persistent (WP11).</summary>
        public static string PersistentScenePath => Sections.ScenesFolder + "/" + Sections.PersistentScene + ".unity";

        /// <summary>Load state of one section.</summary>
        public enum SlotState
        {
            /// <summary>Not loaded.</summary>
            Unloaded,
            /// <summary>Loading or tetrahedralising.</summary>
            Loading,
            /// <summary>Loaded, probes tetrahedralised.</summary>
            Loaded,
            /// <summary>Unloading.</summary>
            Unloading,
            /// <summary>The load failed (logged); counts as loaded when "failed counts as loaded" is on.</summary>
            Failed,
        }

        private struct Op
        {
            public SectionId Section;
            public bool Load;
        }

        private sealed class SceneLoad
        {
            public bool Ok;
            public bool ViaPath;
            public AsyncOperationHandle<SceneInstance> Handle;
            public Scene Scene;
            public string Error;
        }

        [SerializeField, Tooltip("A section that failed to load reports IsLoaded = true (with an error) so scripts never hang.")]
        private bool _failedCountsAsLoaded = true;
        [SerializeField, Tooltip("Log memory after every load/unload (TECH_PLAN §7.16).")]
        private bool _logMemory = true;
        [SerializeField, Tooltip("Longest wait for LightProbes.TetrahedralizeAsync, seconds.")]
        private float _tetrahedralizeTimeout = 5f;
        [SerializeField, Tooltip("Editor: load scenes from their path (or the dev placeholder) when they are not addressable yet.")]
        private bool _editorPathFallback = true;

        private readonly SlotState[] _state = new SlotState[Sections.Count];
        private readonly AsyncOperationHandle<SceneInstance>[] _handles = new AsyncOperationHandle<SceneInstance>[Sections.Count];
        private readonly Scene[] _pathScenes = new Scene[Sections.Count];
        private readonly float[] _loadSeconds = new float[Sections.Count];
        private readonly List<Op> _queue = new List<Op>(8);
        private bool _running;
        private int _batchTotal;
        private int _batchDone;
        private float _opProgress;
        private SectionId _current;
        private bool _hasCurrent;
        private float _persistentProgress;

        /// <inheritdoc/>
        public float Progress => _batchTotal == 0 ? 1f : Mathf.Clamp01((_batchDone + _opProgress) / _batchTotal);
        /// <inheritdoc/>
        public bool IsBusy => _running || _queue.Count > 0;
        /// <summary>Progress 0..1 of the S_Persistent load (Bootstrap).</summary>
        public float PersistentProgress => _persistentProgress;
        /// <summary>True once S_Persistent is loaded (or replaced by an empty scene after a failure).</summary>
        public bool PersistentLoaded { get; private set; }
        /// <summary>Editor only: scenes that are not addressable yet load from their path (or dev placeholder).</summary>
        public bool EditorPathFallback => _editorPathFallback;
        /// <summary>The section being loaded/unloaded right now (null when idle).</summary>
        public SectionId? Current => _hasCurrent ? _current : (SectionId?)null;

        private void Awake()
        {
            AddressablesRemap.Install();
            GameServices.Register<ISectionStreamer>(this);
        }

        private void OnDestroy() => GameServices.Unregister<ISectionStreamer>(this);

        /// <summary>State of a section.</summary>
        public SlotState StateOf(SectionId section) => Valid(section) ? _state[(int)section] : SlotState.Failed;

        /// <summary>Seconds the last load of the section took (0 if never loaded).</summary>
        public float LoadSeconds(SectionId section) => Valid(section) ? _loadSeconds[(int)section] : 0f;

        /// <summary>Progress 0..1 of a section: 1 when loaded, the operation's progress while it is loading, else 0.</summary>
        public float SectionProgress(SectionId section)
        {
            if (IsLoaded(section)) return 1f;
            return _hasCurrent && _current == section && _state[(int)section] == SlotState.Loading ? _opProgress : 0f;
        }

        /// <inheritdoc/>
        public bool IsLoaded(SectionId section)
        {
            if (!Valid(section)) return _failedCountsAsLoaded;
            var s = _state[(int)section];
            return s == SlotState.Loaded || (_failedCountsAsLoaded && s == SlotState.Failed);
        }

        /// <inheritdoc/>
        public void Request(SectionId section, bool load)
        {
            if (!Valid(section))
            {
                LSLog.Error(LogCategory.Streaming, "Request for unknown section " + (int)section);
                return;
            }
            // Drop pending (not started) operations for this section: the new request states the desired end state.
            for (var i = _queue.Count - 1; i >= 0; i--)
                if (_queue[i].Section == section)
                {
                    _queue.RemoveAt(i);
                    _batchTotal = Mathf.Max(_batchDone, _batchTotal - 1);
                }

            var state = _state[(int)section];
            var inFlight = _hasCurrent && _current == section;
            if (load)
            {
                if ((state == SlotState.Loaded && !inFlight) || (state == SlotState.Loading && inFlight)) return;
            }
            else
            {
                if (state == SlotState.Failed && !inFlight)
                {
                    _state[(int)section] = SlotState.Unloaded;
                    return;
                }
                if ((state == SlotState.Unloaded && !inFlight) || (state == SlotState.Unloading && inFlight)) return;
            }
            _queue.Add(new Op { Section = section, Load = load });
            _batchTotal++;
            LSLog.Verbose(LogCategory.Streaming, (load ? "Queue load " : "Queue unload ") + Sections.SceneName(section));
            if (!_running) StartCoroutine(Process());
        }

        /// <inheritdoc/>
        public IEnumerator WaitLoaded(SectionId section)
        {
            if (!Valid(section)) yield break;
            if (!IsLoaded(section) && !IsPending(section, true)) Request(section, true);
            while (!IsLoaded(section))
            {
                // Someone unloaded it while we waited: ask again rather than wait forever.
                if (_state[(int)section] == SlotState.Unloaded && !IsPending(section, true)) Request(section, true);
                yield return null;
            }
        }

        /// <summary>Loads S_Persistent (address "S_Persistent") and makes it the active scene (TECH_PLAN D1, §5.2).</summary>
        public IEnumerator LoadPersistent()
        {
            if (PersistentLoaded) yield break;
            var t0 = Time.realtimeSinceStartup;
            _persistentProgress = 0f;
            var r = new SceneLoad();
            yield return LoadScene(Sections.PersistentScene, PersistentScenePath, r, p => _persistentProgress = p);
            if (r.Ok && r.Scene.IsValid())
            {
                SceneManager.SetActiveScene(r.Scene);
                yield return Tetrahedralize();
                LSLog.Info(LogCategory.Streaming, "S_Persistent loaded in " + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s" +
                                                   (r.ViaPath ? " (from its scene path)" : string.Empty) + MemorySuffix());
            }
            else
            {
                LSLog.Error(LogCategory.Streaming, "S_Persistent could not be loaded (" + r.Error + "); continuing with an empty scene.");
                var empty = SceneManager.GetSceneByName(Sections.PersistentScene);
                if (!empty.IsValid()) empty = SceneManager.CreateScene(Sections.PersistentScene);
                SceneManager.SetActiveScene(empty);
            }
            _persistentProgress = 1f;
            PersistentLoaded = true;
        }

        private IEnumerator Process()
        {
            _running = true;
            while (_queue.Count > 0)
            {
                var op = _queue[0];
                _queue.RemoveAt(0);
                _current = op.Section;
                _hasCurrent = true;
                _opProgress = 0f;
                if (op.Load) yield return LoadSection(op.Section);
                else yield return UnloadSection(op.Section);
                _hasCurrent = false;
                _batchDone++;
                _opProgress = 0f;
            }
            _running = false;
            _batchTotal = 0;
            _batchDone = 0;
        }

        private IEnumerator LoadSection(SectionId section)
        {
            var i = (int)section;
            if (_state[i] == SlotState.Loaded) yield break;
            _state[i] = SlotState.Loading;
            var name = Sections.SceneName(section);
            var t0 = Time.realtimeSinceStartup;
            var r = new SceneLoad();
            yield return LoadScene(Sections.AddressKey(section), Sections.ScenePath(section), r, p => _opProgress = p * 0.95f);
            if (!r.Ok)
            {
                _state[i] = SlotState.Failed;
                LSLog.ErrorOnce("section-failed:" + name, LogCategory.Streaming, "Section " + name + " failed to load (" + r.Error + ")" +
                                (_failedCountsAsLoaded ? "; treating it as loaded so the mission continues." : "."));
                yield break;
            }
            _handles[i] = r.Handle;
            _pathScenes[i] = r.ViaPath ? r.Scene : default;
            yield return Tetrahedralize();
            _opProgress = 1f;
            _state[i] = SlotState.Loaded;
            _loadSeconds[i] = Time.realtimeSinceStartup - t0;
            LSLog.Info(LogCategory.Streaming, "Loaded " + name + " in " + _loadSeconds[i].ToString("0.00") + " s" +
                                               (r.ViaPath ? " (from its scene path)" : string.Empty) + MemorySuffix());
            GameEvents.RaiseSectionLoaded(section);
        }

        private IEnumerator UnloadSection(SectionId section)
        {
            var i = (int)section;
            if (_state[i] == SlotState.Unloaded) yield break;
            if (_state[i] == SlotState.Failed)
            {
                _state[i] = SlotState.Unloaded;
                yield break;
            }
            _state[i] = SlotState.Unloading;
            var name = Sections.SceneName(section);
            var t0 = Time.realtimeSinceStartup;
            if (_handles[i].IsValid())
            {
                AsyncOperationHandle<SceneInstance> h = default;
                try
                {
                    h = Addressables.UnloadSceneAsync(_handles[i], true);
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Streaming, e, this);
                }
                while (h.IsValid() && !h.IsDone)
                {
                    _opProgress = h.PercentComplete;
                    yield return null;
                }
                _handles[i] = default;
            }
            else if (_pathScenes[i].IsValid() && _pathScenes[i].isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(_pathScenes[i]);
                while (op != null && !op.isDone)
                {
                    _opProgress = op.progress;
                    yield return null;
                }
            }
            _pathScenes[i] = default;
            yield return Tetrahedralize();
            _state[i] = SlotState.Unloaded;
            LSLog.Info(LogCategory.Streaming, "Unloaded " + name + " in " + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s" + MemorySuffix());
            GameEvents.RaiseSectionUnloaded(section);
        }

        private IEnumerator LoadScene(string key, string scenePath, SceneLoad result, Action<float> progress)
        {
            // 1. Addressables (the shipping path).
            var hasKey = false;
            AsyncOperationHandle<IList<IResourceLocation>> locations = default;
            try
            {
                locations = Addressables.LoadResourceLocationsAsync(key, typeof(SceneInstance));
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Streaming, e, this);
            }
            if (locations.IsValid())
            {
                while (!locations.IsDone) yield return null;
                hasKey = locations.Status == AsyncOperationStatus.Succeeded && locations.Result != null && locations.Result.Count > 0;
                Addressables.Release(locations);
            }
            if (hasKey)
            {
                AsyncOperationHandle<SceneInstance> h = default;
                try
                {
                    h = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, true);
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Streaming, e, this);
                }
                while (h.IsValid() && !h.IsDone)
                {
                    progress?.Invoke(h.PercentComplete);
                    yield return null;
                }
                if (h.IsValid() && h.Status == AsyncOperationStatus.Succeeded)
                {
                    result.Ok = true;
                    result.Handle = h;
                    result.Scene = h.Result.Scene;
                    progress?.Invoke(1f);
                    yield break;
                }
                result.Error = h.IsValid() && h.OperationException != null ? h.OperationException.Message : "Addressables load failed";
                if (h.IsValid()) Addressables.Release(h);
            }
            else
            {
                result.Error = "'" + key + "' is not in the Addressables catalog";
            }

#if UNITY_EDITOR
            // 2. Editor: the scene exists but is not addressable yet (groups not built), or only its placeholder exists.
            if (_editorPathFallback)
            {
                var candidates = new[] { scenePath, PlaceholderPath(key) };
                foreach (var path in candidates)
                {
                    if (string.IsNullOrEmpty(path) || UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) == null) continue;
                    LSLog.WarnOnce("scene-path:" + key, LogCategory.Streaming, key + ": " + result.Error + "; editor loads " + path + " directly.");
                    var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
                    while (op != null && !op.isDone)
                    {
                        progress?.Invoke(op.progress);
                        yield return null;
                    }
                    var scene = SceneManager.GetSceneByPath(path);
                    if (!scene.IsValid()) continue;
                    result.Ok = true;
                    result.ViaPath = true;
                    result.Scene = scene;
                    result.Error = null;
                    progress?.Invoke(1f);
                    yield break;
                }
            }
#endif
        }

        private IEnumerator Tetrahedralize()
        {
            var done = false;
            Action onDone = () => done = true;
            LightProbes.tetrahedralizationCompleted += onDone;
            LightProbes.TetrahedralizeAsync();
            var until = Time.realtimeSinceStartup + _tetrahedralizeTimeout;
            while (!done && Time.realtimeSinceStartup < until) yield return null;
            LightProbes.tetrahedralizationCompleted -= onDone;
            if (!done) LSLog.WarnOnce("tetra-timeout", LogCategory.Streaming, "LightProbes.TetrahedralizeAsync did not report completion within " + _tetrahedralizeTimeout + " s");
        }

        private bool IsPending(SectionId section, bool load)
        {
            if (_hasCurrent && _current == section && _state[(int)section] == (load ? SlotState.Loading : SlotState.Unloading)) return true;
            foreach (var op in _queue)
                if (op.Section == section && op.Load == load) return true;
            return false;
        }

        private string MemorySuffix() => _logMemory ? "; " + MemoryStats.Describe() : string.Empty;

        private static bool Valid(SectionId section) => (int)section >= 0 && (int)section < Sections.Count;
    }
}
