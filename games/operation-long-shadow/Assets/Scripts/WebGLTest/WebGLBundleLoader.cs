using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace FunUnity.WebGLTest
{
    /// <summary>
    /// Feasibility test: loads AssetBundles from a URL relative to the page (StreamingAssets/bundles/),
    /// applies the loaded PBR material to a cube and plays audio clips after user clicks
    /// (browsers block audio until a user gesture). Logs timings so the console shows whether a
    /// static host without Content-Encoding headers can serve split content.
    /// Audio variants: A = LoadAudioData, wait, then bundle.Unload(false); B = bundle kept loaded.
    /// </summary>
    public class WebGLBundleLoader : MonoBehaviour
    {
        [SerializeField] private string bundleFolder = "bundles";
        [SerializeField] private Vector3 cubePosition = new Vector3(2.5f, 1f, -1f);

        private AudioClip clipUnloadedBundle;
        private AudioClip clipKeptBundle;
        private AssetBundle keptAudioBundle;
        private AudioSource audioSource;
        private int clicks;

        private IEnumerator Start()
        {
            string baseUrl = Application.streamingAssetsPath + "/" + bundleFolder + "/";
            Debug.Log("[WebGLBundle] base url " + baseUrl);

            float t0 = Time.realtimeSinceStartup;
            using (var req = UnityWebRequestAssetBundle.GetAssetBundle(baseUrl + "wall_material.bundle"))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[WebGLBundle] wall_material.bundle failed: " + req.error);
                }
                else
                {
                    AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(req);
                    var matReq = bundle.LoadAssetAsync<Material>("BeigeWall_Lit");
                    yield return matReq;
                    var mat = matReq.asset as Material;
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "BundleWallCube";
                    cube.transform.position = cubePosition;
                    cube.transform.localScale = new Vector3(2f, 2f, 0.3f);
                    cube.GetComponent<Renderer>().sharedMaterial = mat;
                    Debug.Log($"[WebGLBundle] material loaded ok={(mat != null)} shader={(mat != null ? mat.shader.name : "-")} " +
                              $"bytes={req.downloadedBytes} in {(Time.realtimeSinceStartup - t0):F2}s");
                    bundle.Unload(false); // textures/material stay alive; bundle file memory is freed
                }
            }

            // Variant A: load audio data, then unload the bundle.
            t0 = Time.realtimeSinceStartup;
            using (var req = UnityWebRequestAssetBundle.GetAssetBundle(baseUrl + "audio_test.bundle"))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[WebGLBundle] audio_test.bundle failed: " + req.error);
                    yield break;
                }
                AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(req);
                var clipReq = bundle.LoadAssetAsync<AudioClip>("test_noise_10s_stereo");
                yield return clipReq;
                clipUnloadedBundle = clipReq.asset as AudioClip;
                bool started = clipUnloadedBundle != null && clipUnloadedBundle.LoadAudioData();
                float wait = 0f;
                while (clipUnloadedBundle != null && clipUnloadedBundle.loadState == AudioDataLoadState.Loading && wait < 5f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log($"[WebGLBundle] A: audio loaded ok={(clipUnloadedBundle != null)} LoadAudioData={started} state={(clipUnloadedBundle != null ? clipUnloadedBundle.loadState.ToString() : "-")} " +
                          $"bytes={req.downloadedBytes} in {(Time.realtimeSinceStartup - t0):F2}s -> unloading bundle");
                bundle.Unload(false);
            }

            // Variant B: same bundle again (served from the IndexedDB cache), kept loaded.
            t0 = Time.realtimeSinceStartup;
            using (var req = UnityWebRequestAssetBundle.GetAssetBundle(baseUrl + "audio_test.bundle"))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    keptAudioBundle = DownloadHandlerAssetBundle.GetContent(req);
                    var clipReq = keptAudioBundle.LoadAssetAsync<AudioClip>("test_noise_10s_stereo");
                    yield return clipReq;
                    clipKeptBundle = clipReq.asset as AudioClip;
                    Debug.Log($"[WebGLBundle] B: audio loaded ok={(clipKeptBundle != null)} (bundle kept) in {(Time.realtimeSinceStartup - t0):F2}s");
                }
            }
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Update()
        {
            if (audioSource == null || clicks >= 2) return;
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            bool gesture = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (kb != null && kb.anyKey.wasPressedThisFrame);
            if (!gesture) return;
            clicks++;
            AudioClip clip = clicks == 1 ? clipUnloadedBundle : clipKeptBundle;
            if (clip == null) return;
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log($"[WebGLBundle] click {clicks}: play variant {(clicks == 1 ? "A (bundle unloaded)" : "B (bundle kept)")} isPlaying={audioSource.isPlaying}");
        }

        private void OnDestroy()
        {
            if (keptAudioBundle != null) keptAudioBundle.Unload(true);
        }
    }
}
