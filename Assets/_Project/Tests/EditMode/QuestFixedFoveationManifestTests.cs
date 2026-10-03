using System.Xml;
using EmergencyVR.Desktop;
using EmergencyVR.Editor;
using NUnit.Framework;
using UnityEngine.XR;

namespace EmergencyVR.Tests
{
    public sealed class QuestFixedFoveationManifestTests
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        const string ToolsNamespace = "http://schemas.android.com/tools";
        const string Fixture = "<manifest xmlns:android='http://schemas.android.com/apk/res/android' package='com.vitalvr.training'>" +
            "<uses-feature android:name='android.hardware.vr.headtracking' android:required='true' android:version='1'/>" +
            "<uses-feature android:name='oculus.software.eye_tracking' android:required='true'/>" +
            "<uses-permission android:name='com.oculus.permission.EYE_TRACKING'/>" +
            "<uses-permission android:name='com.oculus.permission.EYE_TRACKING'/>" +
            "<uses-permission android:name='android.permission.INTERNET'/>" +
            "<application><meta-data android:name='com.oculus.supportedDevices' android:value='quest2|questpro|quest3|quest3s'/>" +
            "<activity android:name='com.unity3d.player.UnityPlayerGameActivity' android:exported='true'/></application></manifest>";

        [Test]
        public void FixedFoveationRemovesOnlyEyeRequirementsAtMergeAndIsIdempotent()
        {
            var document = Load();
            string applicationBefore = document.SelectSingleNode("/manifest/application").OuterXml;
            string headtrackingBefore = document.SelectSingleNode("/manifest/uses-feature[1]").OuterXml;
            Assert.That(QuestFixedFoveationManifest.Apply(document, true, true, QuestQualityControl.FoveationFlags, QuestQualityControl.FixedFoveationLevel), Is.True);
            var namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("android", AndroidNamespace);
            namespaces.AddNamespace("tools", ToolsNamespace);
            Assert.That(document.SelectNodes("/manifest/*[@tools:node='remove']", namespaces).Count, Is.EqualTo(3));
            Assert.That(document.SelectNodes("/manifest/uses-permission[@android:name='com.oculus.permission.EYE_TRACKING']", namespaces).Count, Is.EqualTo(1));
            Assert.That(document.SelectSingleNode("/manifest/uses-feature[@android:name='oculus.software.eye_tracking']/@tools:node", namespaces).Value, Is.EqualTo("remove"));
            Assert.That(document.SelectSingleNode("/manifest/uses-permission[@android:name='android.permission.EYE_TRACKING_FINE']/@tools:node", namespaces).Value, Is.EqualTo("remove"));
            Assert.That(document.SelectSingleNode("/manifest/application").OuterXml, Is.EqualTo(applicationBefore), "Supported devices and launch activity must not change.");
            Assert.That(document.SelectSingleNode("/manifest/uses-feature[1]").OuterXml, Is.EqualTo(headtrackingBefore));
            Assert.That(document.SelectSingleNode("/manifest/uses-permission[@android:name='android.permission.INTERNET']/@tools:node", namespaces), Is.Null);
            string once = document.OuterXml;
            Assert.That(QuestFixedFoveationManifest.Apply(document, true, true, QuestQualityControl.FoveationFlags, QuestQualityControl.FixedFoveationLevel), Is.False);
            Assert.That(document.OuterXml, Is.EqualTo(once));
        }

        [TestCase(false, true, 0, 1f)]
        [TestCase(true, false, 0, 1f)]
        [TestCase(true, true, (int)XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed, 1f)]
        [TestCase(true, true, 0, 0f)]
        public void OtherRenderingPoliciesLeaveTheManifestUntouched(bool enabled, bool srp, int flags, float level)
        {
            var document = Load();
            string before = document.OuterXml;
            Assert.That(QuestFixedFoveationManifest.Apply(document, enabled, srp, (XRDisplaySubsystem.FoveatedRenderingFlags)flags, level), Is.False);
            Assert.That(document.OuterXml, Is.EqualTo(before));
        }

        static XmlDocument Load()
        {
            var document = new XmlDocument();
            document.LoadXml(Fixture);
            return document;
        }
    }
}
