using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Look.Common;
using LongShadow.World.Look.Practicals;
using LongShadow.World.Look.Sky;
using LongShadow.World.Look.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Look review sheets (TECH_PLAN §10.4, WP10 DoD): renders beauty shots at every <c>CAM.Look.*</c> marker in the open
    /// scenes (plus fixed review views from Appendix A when there are none) at the five stage values
    /// {0.3, 1.2, 1.9, 2.5, 3.5}, through the real URP pipeline with post-processing, to
    /// <c>Builds/captures/look_&lt;timestamp&gt;/</c>: one PNG per shot and s, a contact sheet per shot (5 columns),
    /// <c>index.html</c> and <c>checks.txt</c> (the sun-geometry checks of <see cref="SunGeometryChecks"/>). Practicals are
    /// previewed on after the strike. Needs a LightStageController in the open scenes (S_Persistent or PF_LookRig); one is
    /// instantiated temporarily otherwise. <c>LookCapture.Run(new[]{"S4_Roofs"}, null)</c> from Unity_RunCommand opens the
    /// sections first.
    /// </summary>
    public static class LookCapture
    {
        /// <summary>Review stage values (TECH_PLAN §10.4).</summary>
        public static readonly float[] DefaultS = { 0.3f, 1.2f, 1.9f, 2.5f, 3.5f };
        private const int W = 1920;
        private const int H = 1080;
        private const int ThumbW = 480;
        private const int ThumbH = 270;

        private struct Shot
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public float VerticalFov;
        }

        [MenuItem("LongShadow/Look/Capture Look Review Sheets (open scenes)", priority = 130)]
        private static void CaptureMenu() => Run(null, null);

        [MenuItem("LongShadow/Look/Run Sun Geometry Checks (open scenes)", priority = 131)]
        private static void ChecksMenu()
        {
            var report = new StringBuilder();
            var fails = SunGeometryChecks.Run(report);
            Debug.Log("[LS:Look] Sun geometry checks: " + fails + " failure(s)\n" + report);
        }

        /// <summary>
        /// Captures review sheets. <paramref name="sections"/> (scene names, e.g. "S4_Roofs") are opened first (additive,
        /// after saving the open scenes); null uses the open scenes. <paramref name="sValues"/> defaults to
        /// <see cref="DefaultS"/>. Returns the output folder.
        /// </summary>
        public static string Run(string[] sections, float[] sValues)
        {
            sValues = sValues ?? DefaultS;
            if (sections != null && sections.Length > 0) OpenSections(sections);

            var outDir = Path.Combine(BuilderUtil.ProjectRoot, "Builds/captures/look_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();

            var controller = Object.FindAnyObjectByType<LightStageController>();
            GameObject tempRig = null;
            if (controller == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LookPaths.LookRigPrefab);
                if (prefab == null) prefab = LookRigBuilder.Build(log);
                tempRig = Object.Instantiate(prefab);
                tempRig.hideFlags = HideFlags.DontSave;
                controller = tempRig.GetComponent<LightStageController>();
            }
            var previousSky = RenderSettings.skybox;
            if (controller.EditorSkyMaterial != null) RenderSettings.skybox = controller.EditorSkyMaterial;

            var camGo = new GameObject("LookCaptureCamera") { hideFlags = HideFlags.DontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 3000f;
            cam.allowHDR = true;
            cam.cullingMask = ~((1 << LayerIds.Viewmodel) | (1 << LayerIds.Booth) | (1 << LayerIds.Trigger));
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.volumeLayerMask = 1 << LayerIds.Default;
            data.volumeTrigger = camGo.transform;

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            var shots = CollectShots();
            var html = new StringBuilder("<html><head><meta charset='utf-8'><title>LookCapture</title></head><body style='background:#111;color:#ddd;font-family:monospace'>\n");
            html.Append("<h2>LookCapture ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("</h2>\n");
            try
            {
                foreach (var shot in shots)
                {
                    var thumbs = new List<Texture2D>();
                    html.Append("<h3>").Append(shot.Name).Append("</h3><div>\n");
                    foreach (var s in sValues)
                    {
                        controller.PreviewApply(s);
                        PreviewPracticals(s);
                        camGo.transform.SetPositionAndRotation(shot.Position, shot.Rotation);
                        cam.fieldOfView = shot.VerticalFov;
                        var disc = Object.FindAnyObjectByType<SunDisc>();
                        if (disc != null) disc.PlaceFor(cam);
                        var img = Render(cam, rt);
                        var file = Safe(shot.Name) + "_s" + s.ToString("0.00") + ".png";
                        File.WriteAllBytes(Path.Combine(outDir, file), img.EncodeToPNG());
                        thumbs.Add(Downscale(img, ThumbW, ThumbH));
                        Object.DestroyImmediate(img);
                        html.Append("<figure style='display:inline-block;margin:4px'><img src='").Append(file).Append("' width='384'><figcaption>s ")
                            .Append(s.ToString("0.00")).Append(" · ").Append(LightStageTable.FormatClock(LightStageTable.ClockAt(s)))
                            .Append(" · ").Append(LightStageTable.StageOf(s)).Append("</figcaption></figure>\n");
                    }
                    html.Append("</div>\n");
                    var sheet = Sheet(thumbs);
                    File.WriteAllBytes(Path.Combine(outDir, Safe(shot.Name) + "_sheet.png"), sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);
                    foreach (var t in thumbs) Object.DestroyImmediate(t);
                }

                var checks = new StringBuilder();
                var fails = SunGeometryChecks.Run(checks);
                File.WriteAllText(Path.Combine(outDir, "checks.txt"), checks.ToString());
                html.Append("<h3>Sun geometry checks (").Append(fails).Append(" failure(s))</h3><pre>").Append(checks).Append("</pre></body></html>");
                File.WriteAllText(Path.Combine(outDir, "index.html"), html.ToString());
                log.Append("LookCapture: " + shots.Count + " shot(s) x " + sValues.Length + " stage values → " + outDir + "; geometry checks: " + fails + " failure(s).");
            }
            finally
            {
                RenderTexture.active = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
                controller.PreviewApply(0f);
                PreviewPracticals(0f);
                controller.EditorReleasePreview();
                if (tempRig != null) Object.DestroyImmediate(tempRig);
                RenderSettings.skybox = previousSky;
            }
            Debug.Log("[LS:Look] " + log);
            return outDir;
        }

        private static void OpenSections(string[] sections)
        {
            EditorSceneManager.SaveOpenScenes();
            var first = true;
            foreach (var name in sections)
            {
                if (!Sections.TryParse(name, out var id)) continue;
                var path = Sections.ScenePath(id);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                EditorSceneManager.OpenScene(path, first ? OpenSceneMode.Single : OpenSceneMode.Additive);
                first = false;
            }
        }

        private static List<Shot> CollectShots()
        {
            var shots = new List<Shot>();
            foreach (var m in Object.FindObjectsByType<SceneMarker>(FindObjectsInactive.Exclude))
            {
                if (m == null || string.IsNullOrEmpty(m.Id) || !m.Id.StartsWith(LookIds.LookCameraPrefix, StringComparison.Ordinal)) continue;
                var cam = m.GetComponent<Camera>();
                shots.Add(new Shot { Name = m.Id, Position = m.transform.position, Rotation = m.transform.rotation, VerticalFov = cam != null ? cam.fieldOfView : 55f });
            }
            if (shots.Count > 0)
            {
                shots.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                return shots;
            }
            // Fixed review views (Appendix A positions) until Level places CAM.Look.* markers.
            shots.Add(View("Review.RidgeToTown", new Vector3(42f, 23.7f, 30f), 45f, 2f, 50f));
            shots.Add(View("Review.MarketNorth", new Vector3(185f, 1.7f, 196f), 0f, 0f, 60f));
            shots.Add(View("Review.SunsetLane", new Vector3(186f, 1.7f, 242f), 258f, 2f, 55f));
            shots.Add(View("Review.R4West", R4Eye(), 258f, 1f, 50f));
            shots.Add(View("Review.R4EastShadows", R4Eye(), 78f, -8f, 60f));
            shots.Add(View("Review.YardFloods", new Vector3(318f, 1.7f, 318f), 60f, 4f, 60f));
            shots.Add(View("Review.Pitch", new Vector3(334f, 0.8f, 392f), 40f, 6f, 60f));
            return shots;
        }

        private static Vector3 R4Eye() => SunGeometryChecks.R4Eye;

        private static Shot View(string name, Vector3 pos, float heading, float pitchUp, float vFov) =>
            new Shot { Name = name, Position = pos, Rotation = Quaternion.Euler(-pitchUp, heading, 0f), VerticalFov = vFov };

        private static void PreviewPracticals(float s)
        {
            var struck = s >= LightStageTable.StrikeS;
            foreach (var p in Object.FindObjectsByType<PracticalLight>(FindObjectsInactive.Exclude))
            {
                if (p is SodiumLamp lamp)
                {
                    if (struck) lamp.SnapOn();
                    else lamp.SnapOff();
                    lamp.PreviewLevel(struck ? 1f : 0f);
                }
                else p.PreviewLevel(1f);
            }
        }

        private static Texture2D Render(Camera cam, RenderTexture rt)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(false);
            RenderTexture.active = prev;
            return tex;
        }

        private static Texture2D Downscale(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            t.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        private static Texture2D Sheet(List<Texture2D> thumbs)
        {
            var cols = Mathf.Max(1, thumbs.Count);
            var sheet = new Texture2D(cols * ThumbW + (cols + 1) * 4, ThumbH + 8, TextureFormat.RGB24, false);
            var bg = new Color32[sheet.width * sheet.height];
            for (var i = 0; i < bg.Length; i++) bg[i] = new Color32(16, 16, 16, 255);
            sheet.SetPixels32(bg);
            for (var i = 0; i < thumbs.Count; i++) sheet.SetPixels(4 + i * (ThumbW + 4), 4, ThumbW, ThumbH, thumbs[i].GetPixels());
            sheet.Apply(false);
            return sheet;
        }

        private static string Safe(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (var c in name) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }
    }
}
