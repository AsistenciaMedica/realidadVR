using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EmergencyVR.Desktop
{
    /// <summary>Deterministic source identity. Contains only relative paths and hashes, never source text.</summary>
    public static class ValidationSourceFingerprint
    {
        public const string ResourcePath = "Validation/BuildFingerprint";
        public const string ResourceFile = "Assets/_Project/Resources/Validation/BuildFingerprint.json";

        [Serializable] public sealed class Manifest
        {
            public int schemaVersion = 1;
            public string sourceSha256;
            public bool developmentBuild;
            public string method = "SHA256 of ordinal relative paths and SHA256 of UTF8 source text with normalized LF; no timestamps";
            public Entry[] files;
        }
        [Serializable] public sealed class Entry { public string path, sha256; }

        public static Manifest Compute(string projectRoot, bool developmentBuild)
        {
            string root = Path.GetFullPath(projectRoot);
            var paths = new List<string>();
            AddFiles(paths, root, "Assets/_Project/Scripts", "*.cs");
            AddFiles(paths, root, "Assets/_Project/Resources", "*.json");
            AddFiles(paths, root, "Assets/_Project/Settings", "Quest*.asset");
            paths.RemoveAll(p => p.Replace('\\', '/').EndsWith("/" + ResourceFile, StringComparison.Ordinal));
            foreach (string relative in new[] { "ProjectSettings/QualitySettings.asset", "ProjectSettings/GraphicsSettings.asset",
                "ProjectSettings/ProjectVersion.txt", "Packages/manifest.json", "Packages/packages-lock.json" })
            {
                string full = Path.Combine(root, relative);
                if (File.Exists(full)) paths.Add(full);
            }
            var entries = paths.Distinct(StringComparer.Ordinal).Select(path => new Entry {
                path = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/'),
                sha256 = Hash(File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n"))
            }).OrderBy(entry => entry.path, StringComparer.Ordinal).ToArray();
            if (entries.Length == 0) throw new InvalidOperationException("No validation fingerprint inputs found.");
            var canonical = new StringBuilder("VITAL_SOURCE_FINGERPRINT_V1\n");
            foreach (var entry in entries) canonical.Append(entry.path).Append('\0').Append(entry.sha256).Append('\n');
            return new Manifest { sourceSha256 = Hash(canonical.ToString()), developmentBuild = developmentBuild, files = entries };
        }

        static void AddFiles(List<string> paths, string root, string relative, string pattern)
        {
            string directory = Path.Combine(root, relative);
            if (Directory.Exists(directory)) paths.AddRange(Directory.GetFiles(directory, pattern, SearchOption.AllDirectories));
        }

        static string Hash(string value)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        public static Manifest LoadBuilt()
        {
            var resource = Resources.Load<TextAsset>(ResourcePath);
            if (resource == null) throw new InvalidOperationException("Build has no source fingerprint. Rebuild with Case01Build.");
            var manifest = JsonUtility.FromJson<Manifest>(resource.text);
            if (manifest == null || manifest.schemaVersion != 1 || !IsHash(manifest.sourceSha256))
                throw new InvalidOperationException("Build source fingerprint is invalid.");
            return manifest;
        }

        public static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        public static void RequireMatch(string expected, string actual)
        {
            if (!IsHash(expected) || !IsHash(actual) || !string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException("Budget report/build source fingerprint differs from current sources. Rebuild the validation player.");
        }
    }
}
