using System.Collections.Generic;
using LongShadow.Audio.VO;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>Generated clips and lines for the audio tests (no assets needed).</summary>
    internal static class AudioTestUtil
    {
        private static readonly List<Object> s_created = new List<Object>();

        /// <summary>A silent mono clip of <paramref name="seconds"/> at 48 kHz.</summary>
        public static AudioClip Clip(string name, float seconds = 1f, int channels = 1)
        {
            var c = AudioClip.Create(name, Mathf.Max(1, (int)(seconds * 48000)), channels, 48000, false);
            s_created.Add(c);
            return c;
        }

        /// <summary>A line with one clip per chain (and per voice for barks).</summary>
        public static DialogueLine Line(string id, string speaker, string[] chains, bool radioFilter = false, string[] voices = null,
                                        VOChannel channel = VOChannel.Net, VOPriority priority = VOPriority.Scripted, int beat = 3,
                                        float seconds = 1f, bool critical = false, bool repeatable = false, string subtitle = null)
        {
            voices = voices ?? new[] { "" };
            var clips = new List<DialogueClip>();
            foreach (var ch in chains)
                foreach (var v in voices)
                    clips.Add(new DialogueClip(ch, v, Clip("VO_" + id + "_" + ch + v, seconds), seconds, 0f, new byte[] { 0, 255, 255, 0 }));
            return new DialogueLine(id, speaker, speaker, "text " + id, subtitle, radioFilter)
                .WithRules(beat, priority, channel, critical, repeatable)
                .WithAudio("vo_test", chains, clips, seconds, 0f);
        }

        /// <summary>A line without audio (silent, estimated length).</summary>
        public static DialogueLine Silent(string id, string speaker, float seconds, VOChannel channel = VOChannel.Net,
                                          VOPriority priority = VOPriority.Scripted, bool critical = false, bool repeatable = false,
                                          string subtitle = null)
        {
            return new DialogueLine(id, speaker, speaker, "text " + id, subtitle, false)
                .WithRules(3, priority, channel, critical, repeatable)
                .WithAudio("vo_test", new[] { "DRY" }, new DialogueClip[0], seconds, 0f);
        }

        /// <summary>Destroys everything created by this helper.</summary>
        public static void Cleanup()
        {
            foreach (var o in s_created)
                if (o != null) Object.DestroyImmediate(o);
            s_created.Clear();
        }

        /// <summary>Tracks an object for <see cref="Cleanup"/>.</summary>
        public static T Track<T>(T o) where T : Object
        {
            s_created.Add(o);
            return o;
        }

        /// <summary>A transform at a position (tracked).</summary>
        public static Transform At(string name, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            return go.transform;
        }
    }
}
