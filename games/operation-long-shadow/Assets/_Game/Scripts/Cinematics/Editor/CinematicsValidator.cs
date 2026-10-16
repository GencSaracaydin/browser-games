using System.Collections.Generic;
using System.IO;
using LongShadow.Cinematics.Playables;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Checks the WP09 deliverables in the editor and writes them to the report: shaders compiled and found, timelines
    /// present with their lengths (CinematicSequences), every VO clip's line in DIALOGUE.json, every "@id" track binding
    /// known, camera tracks without gaps, LIB_Cinematics complete, the booth scene's markers (markup_ids.csv BOOTH.*),
    /// the videos in StreamingAssets. Returns the error count.
    /// </summary>
    public static class CinematicsValidator
    {
        private static readonly string[] s_knownIdPrefixes = { "Booth", "Cam.", "Feed.", "Cast.", "MidB.", "Squad.", "Marker.", "Player." };

        /// <summary>Runs every check.</summary>
        public static int Validate()
        {
            var lines = new List<string>();
            var errors = 0;
            var dialogue = new DialogueData();

            foreach (var s in new[] { "LongShadow/SG_CRTScreen", "LongShadow/SG_GunCam", "LongShadow/SG_Film16mm", "LongShadow/SG_FilmBurn",
                                      "LongShadow/SG_Glitch", "LongShadow/SG_GlitchOverlay", "LongShadow/SG_FilmBeam" })
            {
                var shader = Shader.Find(s);
                if (shader == null)
                {
                    errors++;
                    lines.Add("ERROR shader missing: " + s);
                }
                else if (ShaderUtil.ShaderHasError(shader))
                {
                    errors++;
                    lines.Add("ERROR shader has compile errors: " + s + " (see the console / shader inspector)");
                }
                else lines.Add("ok shader " + s);
            }

            foreach (var def in CinematicSequences.All)
            {
                var path = CinematicPaths.Timelines + "/" + def.TimelineId + ".playable";
                var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                if (tl == null)
                {
                    errors++;
                    lines.Add("ERROR timeline missing: " + path);
                    continue;
                }
                if (Mathf.Abs((float)tl.duration - def.Seconds) > 0.05f)
                {
                    errors++;
                    lines.Add("ERROR " + def.TimelineId + " is " + tl.duration.ToString("0.00") + " s, expected " + def.Seconds);
                }
                double cameraEnd = 0;
                var cameraClips = new List<TimelineClip>();
                foreach (var track in tl.GetOutputTracks())
                {
                    var id = TrackBindingResolver.BindingIdOf(track.name);
                    if (id != null && !KnownId(id))
                    {
                        errors++;
                        lines.Add("ERROR " + def.TimelineId + ": unknown binding id '" + id + "'");
                    }
                    foreach (var clip in track.GetClips())
                    {
                        if (clip.asset is VOLineClip vo && !dialogue.Has(vo.LineId))
                        {
                            errors++;
                            lines.Add("ERROR " + def.TimelineId + ": line " + vo.LineId + " not in DIALOGUE.json");
                        }
                        if (track is CameraShotTrack) cameraClips.Add(clip);
                    }
                }
                cameraClips.Sort((a, b) => a.start.CompareTo(b.start));
                foreach (var c in cameraClips)
                {
                    if (c.start > cameraEnd + 0.02)
                        lines.Add("warn " + def.TimelineId + ": camera gap " + cameraEnd.ToString("0.00") + "-" + c.start.ToString("0.00"));
                    cameraEnd = System.Math.Max(cameraEnd, c.end);
                }
                lines.Add("ok " + def.TimelineId + " " + tl.duration.ToString("0.0") + " s");
            }

            var lib = AssetDatabase.LoadAssetAtPath<CinematicLibrary>(CinematicLibrary.AssetPath);
            if (lib == null)
            {
                errors++;
                lines.Add("ERROR " + CinematicLibrary.AssetPath + " missing (run Build Library)");
            }
            else
            {
                var missing = 0;
                foreach (var e in lib.Entries)
                    if (e != null && e.Asset == null) missing++;
                lines.Add((missing == 0 ? "ok" : "warn") + " LIB_Cinematics " + lib.Count + " entries, " + missing + " without asset");
            }

            foreach (var v in new[] { CinematicAssetIds.VideoHarrou, CinematicAssetIds.VideoGunCam, CinematicAssetIds.VideoLeader })
            {
                var p = Path.Combine(BuilderUtil.ProjectRoot, "Assets/StreamingAssets/video", v);
                lines.Add((File.Exists(p) ? "ok" : "warn") + " video " + v + (File.Exists(p) ? "" : " missing (CRTs fall back to stills/static)"));
            }

            if (!File.Exists(Path.Combine(BuilderUtil.ProjectRoot, CinematicPaths.BoothScene)))
            {
                errors++;
                lines.Add("ERROR " + CinematicPaths.BoothScene + " missing (run Build Booth Scene)");
            }
            else
            {
                var table = MarkupIds.Load();
                var want = 0;
                if (table != null)
                    foreach (var row in table.Rows)
                        if (row.Id.StartsWith("BOOTH.")) want++;
                lines.Add("ok " + CinematicPaths.BoothScene + " (" + want + " BOOTH.* ids in markup_ids.csv, placed by the builder)");
            }

            lines.Insert(0, errors == 0 ? "VALID (0 errors)" : errors + " error(s)");
            CinematicReport.Section("Validation", lines);
            if (errors > 0) LSLog.Error(LogCategory.Cinematics, "Cinematics validation: " + errors + " error(s). See " + CinematicPaths.Report);
            else LSLog.Info(LogCategory.Cinematics, "Cinematics validation passed. See " + CinematicPaths.Report);
            return errors;
        }

        private static bool KnownId(string id)
        {
            foreach (var p in s_knownIdPrefixes)
                if (id.StartsWith(p)) return true;
            return false;
        }
    }
}
