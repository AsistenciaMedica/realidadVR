using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EmergencyVR.Desktop
{
    /// <summary>
    /// Desktop-only image quality: switches to the "Desktop" URP quality level (soft shadows, HDR, SSAO) and adds
    /// a restrained filmic grade. Quest never runs this; its pipeline and budget stay as authored.
    /// </summary>
    public static class DesktopAtmosphere
    {
        public const string QualityName = "Desktop";

        public static void Apply(Camera view)
        {
            if (QuestLookSimulation.Enabled) return;
            int level = Array.IndexOf(QualitySettings.names, QualityName);
            if (level >= 0 && QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
            if (view == null) return;
            var data = view.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            view.allowHDR = true;

            var go = new GameObject("VITAL desktop grade", typeof(Volume));
            go.transform.SetParent(view.transform.root, false);
            var volume = go.GetComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "VITAL desktop grade";
            volume.sharedProfile = profile;
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.45f); color.contrast.Override(8); color.saturation.Override(-6);
            var white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(4);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f); bloom.intensity.Override(.35f); bloom.scatter.Override(.62f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(.2f); vignette.smoothness.Override(.45f);
        }
    }
}
