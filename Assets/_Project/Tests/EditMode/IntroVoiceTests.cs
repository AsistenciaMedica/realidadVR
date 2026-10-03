using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class IntroVoiceTests
    {
        [TestCase("WELCOME")]
        [TestCase("POINT")]
        [TestCase("GRAB")]
        [TestCase("TELEPORT")]
        [TestCase("RECENTER")]
        public void IntroOrientationHasUsableSpanishVoiceClip(string id)
        {
            var clip = Resources.Load<AudioClip>("Audio/Intro/" + id);
            Assert.That(clip, Is.Not.Null, "Orientation must not silently ship without the authored voice clip.");
            Assert.That(clip.length, Is.InRange(.5f, 18));
            Assert.That(clip.channels, Is.EqualTo(1));
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
            Assert.That(importer.defaultSampleSettings.sampleRateSetting, Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            // Installed Windows Spanish speech is native 16 kHz PCM. Resampling it to a
            // larger number adds no detail; preserve the source and verify actual signal.
            string path = Path.Combine(Application.dataPath, "..", AssetDatabase.GetAssetPath(clip));
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"));
                reader.ReadUInt32();
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("WAVE"));
                int nativeRate = 0; long samples = 0, audibleSamples = 0;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string chunk = new string(reader.ReadChars(4)); uint bytes = reader.ReadUInt32();
                    long end = reader.BaseStream.Position + bytes;
                    Assert.That(end, Is.LessThanOrEqualTo(reader.BaseStream.Length), "Truncated WAV chunk.");
                    if (chunk == "fmt ")
                    {
                        Assert.That(reader.ReadUInt16(), Is.EqualTo(1), "Expected native PCM, not a renamed compressed file.");
                        Assert.That(reader.ReadUInt16(), Is.EqualTo(1));
                        nativeRate = reader.ReadInt32();
                        reader.ReadUInt32(); reader.ReadUInt16();
                        Assert.That(reader.ReadUInt16(), Is.EqualTo(16));
                    }
                    else if (chunk == "data")
                    {
                        Assert.That(bytes % 2, Is.Zero);
                        samples += bytes / 2;
                        while (reader.BaseStream.Position < end)
                            if (Math.Abs((int)reader.ReadInt16()) > 128) audibleSamples++;
                    }
                    reader.BaseStream.Position = end + (bytes & 1);
                }
                Assert.That(nativeRate, Is.InRange(16000, 48000), "Use native speech quality, without manufactured upsampling.");
                Assert.That(clip.frequency, Is.EqualTo(nativeRate));
                Assert.That(samples, Is.GreaterThan(nativeRate / 2));
                Assert.That(audibleSamples / (double)samples, Is.GreaterThan(.02), "The native recording must contain speech, not silence or a lone impulse.");
            }
        }
    }
}
