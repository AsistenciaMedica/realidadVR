using System;
using System.IO;
using System.Linq;
using System.Xml;
using EmergencyVR.Desktop;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace EmergencyVR.Editor
{
    // OpenXR 1.16 lists eye-tracked and fixed foveation in the same feature. Its Meta hook
    // consequently declares eye tracking required when Quest Pro is among the supported devices.
    // This application only uses fixed foveation, which does not require eye data or permission.
    public sealed class QuestFixedFoveationManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        const string ToolsNamespace = "http://schemas.android.com/tools";
        public int callbackOrder => 10000; // XR Management generates xrmanifest.androidlib at order 1.

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var feature = settings == null ? null : settings.GetFeature<FoveatedRenderingFeature>();
            bool featureEnabled = feature != null && feature.enabled;
            bool srpFoveation = settings != null && settings.foveatedRenderingApi == OpenXRSettings.BackendFovationApi.SRPFoveation;
            if (!UsesFixedFoveation(featureEnabled, srpFoveation, QuestQualityControl.FoveationFlags, QuestQualityControl.FixedFoveationLevel)) return;

            // Unity passes the unityLibrary module. launcher has the highest manifest priority.
            // Do not edit PackageCache or narrow MetaQuestFeature.targetDevices to hide this issue.
            var gradleRoot = Directory.GetParent(Path.GetFullPath(path));
            var manifestPath = gradleRoot == null ? null : Path.Combine(gradleRoot.FullName, "launcher", "src", "main", "AndroidManifest.xml");
            if (manifestPath == null || !File.Exists(manifestPath))
                throw new BuildFailedException("Fixed foveation: generated launcher manifest was not found; cannot verify the eye-tracking merge policy.");
            var manifest = new XmlDocument { PreserveWhitespace = true };
            manifest.Load(manifestPath);
            if (Apply(manifest, featureEnabled, srpFoveation, QuestQualityControl.FoveationFlags, QuestQualityControl.FixedFoveationLevel))
                manifest.Save(manifestPath);
            Debug.Log("Fixed foveation manifest: remove eye-tracking feature/permissions at merge; supported Quest devices remain unchanged. Inspect the merged APK manifest before distribution.");
        }

        public static bool UsesFixedFoveation(bool featureEnabled, bool srpFoveation,
            XRDisplaySubsystem.FoveatedRenderingFlags flags, float level)
        {
            return featureEnabled && srpFoveation && level > 0 &&
                (flags & XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed) == 0;
        }

        // Pure XML entry point allows testing without a build or mutation of project settings.
        public static bool Apply(XmlDocument document, bool featureEnabled, bool srpFoveation,
            XRDisplaySubsystem.FoveatedRenderingFlags flags, float level)
        {
            if (!UsesFixedFoveation(featureEnabled, srpFoveation, flags, level)) return false;
            var root = document.DocumentElement;
            if (root == null || root.Name != "manifest") throw new ArgumentException("Expected an Android manifest document.");
            string before = document.OuterXml;
            if (root.GetNamespaceOfPrefix("tools") != ToolsNamespace) root.SetAttribute("xmlns:tools", ToolsNamespace);
            RemoveAtMerge(document, "uses-feature", "oculus.software.eye_tracking");
            RemoveAtMerge(document, "uses-permission", "com.oculus.permission.EYE_TRACKING");
            RemoveAtMerge(document, "uses-permission", "android.permission.EYE_TRACKING_FINE");
            return document.OuterXml != before;
        }

        static void RemoveAtMerge(XmlDocument document, string tag, string name)
        {
            var root = document.DocumentElement;
            var matches = root.ChildNodes.OfType<XmlElement>()
                .Where(node => node.Name == tag && node.GetAttribute("name", AndroidNamespace) == name).ToArray();
            var element = matches.FirstOrDefault();
            if (element == null)
            {
                element = document.CreateElement(tag);
                var attribute = document.CreateAttribute("android", "name", AndroidNamespace);
                attribute.Value = name;
                element.SetAttributeNode(attribute);
                root.AppendChild(element);
            }
            foreach (var duplicate in matches.Skip(1)) root.RemoveChild(duplicate);
            var marker = document.CreateAttribute("tools", "node", ToolsNamespace);
            marker.Value = "remove";
            element.SetAttributeNode(marker);
        }
    }
}
