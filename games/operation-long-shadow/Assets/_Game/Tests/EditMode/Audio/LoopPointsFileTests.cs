using System.IO;
using LongShadow.Audio.Editor;
using LongShadow.Core.Editor;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>WP06_SFX LOOP_POINTS.json: loop bodies and the music grid.</summary>
    public class LoopPointsFileTests
    {
        private const string Sample = @"{
 ""Assets/_Game/Audio/Music/MUS_01_SESSION.wav"": {""clip"": ""MUS_01_SESSION"", ""sr"": 48000, ""loop_start_sample"": 2241536, ""loop_end_sample"": 3681277, ""loop_seconds"": 29.9946, ""bpm_grid"": 72.005, ""bar_seconds"": 3.3331, ""loop_bars"": 9, ""has_intro"": true},
 ""Assets/_Game/Audio/Ambience/AMB_CICADAS_01.wav"": {""clip"": ""AMB_CICADAS_01"", ""sr"": 48000, ""loop_start_sample"": 2048, ""loop_end_sample"": 962048, ""loop_seconds"": 20.0},
 ""Assets/_Game/Audio/SFX/SetPieces/AMB_CASSETTE_TUNE.wav"": {""clip"": ""AMB_CASSETTE_TUNE"", ""sr"": 48000, ""loop_start_sample"": 2048, ""loop_end_sample"": 711168, ""bpm_grid"": 130.0, ""loop_bars"": 8}
}";

        [Test]
        public void ParsesRows()
        {
            var f = LoopPointsFile.Parse(Sample);
            Assert.That(f.Count, Is.EqualTo(3));
            Assert.That(f.TryGet("Assets/_Game/Audio/Music/MUS_01_SESSION.wav", out var m), Is.True);
            Assert.That(m.HasIntro, Is.True);
            Assert.That(m.StartSeconds, Is.EqualTo(2241536f / 48000f).Within(1e-4f));
            Assert.That(m.Bar, Is.EqualTo(3.3331f).Within(1e-5f));
            Assert.That(f.TryGet("Assets/Elsewhere/AMB_CICADAS_01.wav", out var c), Is.True, "falls back to the clip name");
            Assert.That(c.EndSeconds - c.StartSeconds, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(f.TryGet("Assets/_Game/Audio/SFX/SetPieces/AMB_CASSETTE_TUNE.wav", out var t), Is.True);
            Assert.That(t.Bar, Is.EqualTo(240f / 130f).Within(1e-4f), "bar from the tempo in 4/4");
            Assert.That(f.TryGet("nope.wav", out _), Is.False);
        }

        [Test]
        public void DeliveredFileParsesWhenPresent()
        {
            var a = Path.Combine(BuilderUtil.ProjectRoot, LoopPointsFile.DefaultPath);
            var b = Path.Combine(BuilderUtil.ProjectRoot, "RawAssets/audio/ready", LoopPointsFile.DefaultPath);
            var path = File.Exists(a) ? a : File.Exists(b) ? b : null;
            if (path == null) Assert.Ignore("LOOP_POINTS.json has not been delivered yet.");
            var f = LoopPointsFile.Load(path);
            Assert.That(f.Count, Is.GreaterThan(0));
            Assert.That(f.TryGet("Assets/_Game/Audio/Music/MUS_02_MARKET_LO.wav", out var lo) && lo.Valid, Is.True);
            Assert.That(f.TryGet("Assets/_Game/Audio/Music/MUS_02_MARKET_HI.wav", out var hi) && hi.Valid, Is.True);
            Assert.That(hi.EndSeconds - hi.StartSeconds, Is.EqualTo(lo.EndSeconds - lo.StartSeconds).Within(0.01f), "LO/HI share a loop length");
        }
    }
}
