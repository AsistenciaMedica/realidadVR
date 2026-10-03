using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EmergencyVR.UI
{
    // A camera-rendered Windows image is evidence of presentation, never headset validation.
    [Serializable]
    public sealed class CaptureFrameMetadata
    {
        public int schemaVersion = 1, width, height, qualityLevel;
        public string capturedUtc, verification, capturePurpose, interactionMethod, qualityName, renderPipeline, canvasMode;
        public bool headsetTested, stereoscopic, desktopPath, canvasVisible, hdr, postProcessing;
        public float verticalFieldOfView, aspect, canvasWidthMeters, canvasDistanceMeters;
        public Vector3 cameraPosition, cameraEulerAngles;

        public static CaptureFrameMetadata Read(Camera camera, Canvas canvas, int width, int height,
            bool questLook, bool desktopPath, string purpose)
        {
            var additional = camera.GetComponent<UniversalAdditionalCameraData>();
            var rect = canvas.transform as RectTransform;
            int level = QualitySettings.GetQualityLevel();
            return new CaptureFrameMetadata
            {
                capturedUtc = DateTime.UtcNow.ToString("O"), width = width, height = height,
                verification = questLook ? "Verificado en simulación de apariencia Quest en Windows. Sin visor." : "Captura de presentación en Windows. Sin visor.",
                capturePurpose = purpose,
                interactionMethod = "Semantic UI events for capture orchestration; this is not an XR trigger walkthrough.",
                headsetTested = false, stereoscopic = false, desktopPath = desktopPath,
                qualityLevel = level, qualityName = QualitySettings.names[level],
                renderPipeline = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name,
                canvasMode = canvas.renderMode.ToString(), canvasVisible = canvas.enabled && canvas.gameObject.activeInHierarchy,
                hdr = camera.allowHDR, postProcessing = additional != null && additional.renderPostProcessing,
                verticalFieldOfView = camera.fieldOfView, aspect = (float)width / height,
                cameraPosition = camera.transform.position, cameraEulerAngles = camera.transform.eulerAngles,
                canvasWidthMeters = canvas.renderMode == RenderMode.WorldSpace && rect != null ? rect.rect.width * Mathf.Abs(rect.lossyScale.x) : 0,
                canvasDistanceMeters = Vector3.Distance(camera.transform.position, canvas.transform.position)
            };
        }
    }
}
