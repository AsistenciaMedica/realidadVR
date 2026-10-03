using EmergencyVR.Desktop;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class CaptureEvidenceTests
    {
        [Test] public void QuestLookMetadataRecordsWorldSpaceGeometryWithoutFlatteningTheCanvas()
        {
            var cameraObject = new GameObject("Evidence camera", typeof(Camera));
            var canvasObject = new GameObject("Evidence interface", typeof(RectTransform), typeof(Canvas));
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(1, 1.62f, -2);
                camera.fieldOfView = QuestLookSimulation.FieldOfView;
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)canvas.transform;
                rect.sizeDelta = new Vector2(1440, 900);
                rect.localScale = Vector3.one * .00165f;
                rect.position = camera.transform.position + Vector3.forward * 2.2f;
                var metadata = CaptureFrameMetadata.Read(camera, canvas,
                    QuestLookSimulation.CaptureWidth, QuestLookSimulation.CaptureHeight, true, false, "learner-view");
                Assert.That(metadata.width, Is.EqualTo(2064));
                Assert.That(metadata.height, Is.EqualTo(2208));
                Assert.That(metadata.verticalFieldOfView, Is.EqualTo(100));
                Assert.That(metadata.aspect, Is.EqualTo(2064f / 2208f).Within(.0001));
                Assert.That(metadata.canvasMode, Is.EqualTo("WorldSpace"));
                Assert.That(metadata.canvasWidthMeters, Is.EqualTo(2.376f).Within(.001));
                Assert.That(metadata.canvasDistanceMeters, Is.EqualTo(2.2f).Within(.001));
                Assert.That(metadata.verification, Does.Contain("simulación").And.Contain("Sin visor"));
                Assert.That(metadata.headsetTested, Is.False);
                Assert.That(metadata.stereoscopic, Is.False);
                Assert.That(metadata.desktopPath, Is.False);
                Assert.That(metadata.interactionMethod, Does.Contain("not an XR trigger walkthrough"));
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(rect.localScale.x, Is.EqualTo(.00165f));
                Assert.That(camera.fieldOfView, Is.EqualTo(100));
            }
            finally { Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(cameraObject); }
        }

        [Test] public void ActorPortraitMetadataDisclosesHiddenInterfaceAndNeverClaimsHeadsetValidation()
        {
            var cameraObject = new GameObject("Portrait camera", typeof(Camera));
            var canvasObject = new GameObject("Portrait interface", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace; canvas.enabled = false;
                var metadata = CaptureFrameMetadata.Read(cameraObject.GetComponent<Camera>(), canvas, 2064, 2208,
                    true, false, "actor-seat-contact-inspection-no-interface");
                var restored = JsonUtility.FromJson<CaptureFrameMetadata>(JsonUtility.ToJson(metadata));
                Assert.That(restored.canvasVisible, Is.False);
                Assert.That(restored.capturePurpose, Is.EqualTo("actor-seat-contact-inspection-no-interface"));
                Assert.That(restored.headsetTested, Is.False);
                Assert.That(restored.stereoscopic, Is.False);
                Assert.That(restored.cameraPosition, Is.EqualTo(cameraObject.transform.position));
            }
            finally { Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(cameraObject); }
        }
    }
}
