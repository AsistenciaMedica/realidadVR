using System;
using System.IO;
using EmergencyVR.Desktop;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class ValidationSourceFingerprintTests
    {
        string temporary;

        [SetUp] public void CreateInputs()
        {
            temporary = Path.Combine(Path.GetTempPath(), "vital-fingerprint-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(temporary, "Assets/_Project/Scripts"));
            Directory.CreateDirectory(Path.Combine(temporary, "Assets/_Project/Resources/Validation"));
            File.WriteAllText(Path.Combine(temporary, "Assets/_Project/Scripts/Example.cs"), "class Example {}\r\n");
        }

        [TearDown] public void CleanInputs()
        {
            string permitted = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (temporary != null && Path.GetFullPath(temporary).StartsWith(permitted, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(temporary).StartsWith("vital-fingerprint-", StringComparison.Ordinal) && Directory.Exists(temporary))
                Directory.Delete(temporary, true);
        }

        [Test] public void SameSourcesAndLineEndingsHaveTheSameIdentityWithoutTimestampsOrBuildPath()
        {
            var before = ValidationSourceFingerprint.Compute(temporary, true);
            File.WriteAllText(Path.Combine(temporary, "Assets/_Project/Scripts/Example.cs"), "class Example {}\n");
            File.WriteAllText(Path.Combine(temporary, ValidationSourceFingerprint.ResourceFile), JsonUtility.ToJson(before));
            var after = ValidationSourceFingerprint.Compute(temporary, true);
            Assert.That(after.sourceSha256, Is.EqualTo(before.sourceSha256));
            Assert.That(after.files, Has.Length.EqualTo(1), "Generated fingerprint must never hash itself.");
        }

        [Test] public void ChangedSourceRejectsEvidenceFromThePreviousBuild()
        {
            var before = ValidationSourceFingerprint.Compute(temporary, true);
            File.AppendAllText(Path.Combine(temporary, "Assets/_Project/Scripts/Example.cs"), "// actual source change\n");
            var after = ValidationSourceFingerprint.Compute(temporary, true);
            Assert.Throws<InvalidOperationException>(() => ValidationSourceFingerprint.RequireMatch(after.sourceSha256, before.sourceSha256));
            Assert.DoesNotThrow(() => ValidationSourceFingerprint.RequireMatch(after.sourceSha256, after.sourceSha256));
        }

        [Test] public void ChangedResourceConfigurationRejectsThePreviousFingerprint()
        {
            var before = ValidationSourceFingerprint.Compute(temporary, true);
            File.WriteAllText(Path.Combine(temporary, "Assets/_Project/Resources/ReleaseScope.json"), "{\"releaseId\":\"changed\"}");
            var after = ValidationSourceFingerprint.Compute(temporary, true);
            Assert.Throws<InvalidOperationException>(() => ValidationSourceFingerprint.RequireMatch(after.sourceSha256, before.sourceSha256));
        }
    }
}
