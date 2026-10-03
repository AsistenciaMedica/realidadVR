using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace EmergencyVR.Desktop
{
    public sealed class QuestQualityControl : MonoBehaviour
    {
        const string Preference = "VitalVR.QuestSmoothQuality";
        public static bool Smooth => PlayerPrefs.GetInt(Preference, 0) == 1;
        public const float HighRenderScale = 1f, SmoothRenderScale = .85f, FixedFoveationLevel = 1f;
        // Shared with the Android manifest policy: changing to gaze must retain eye-tracking declarations.
        public const XRDisplaySubsystem.FoveatedRenderingFlags FoveationFlags = 0;
        static QuestQualityControl instance;
        UniversalRenderPipelineAsset original, pipeline;
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        float nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartOnQuest()
        {
            if (Application.platform == RuntimePlatform.Android) Ensure();
        }

        public static void SetSmooth(bool smooth)
        {
            PlayerPrefs.SetInt(Preference, smooth ? 1 : 0);
            PlayerPrefs.Save();
            Ensure();
            instance?.Apply();
        }

        static void Ensure()
        {
            if (instance != null || (!QuestLookSimulation.Enabled && Application.platform != RuntimePlatform.Android)) return;
            instance = new GameObject("Quest quality control").AddComponent<QuestQualityControl>();
            DontDestroyOnLoad(instance.gameObject);
            instance.Initialize();
        }

        void Initialize()
        {
            original = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (original == null) return;
            pipeline = Instantiate(original);
            pipeline.name = "QuestURP (runtime)";
            QualitySettings.renderPipeline = pipeline;
            SceneManager.sceneLoaded += SceneLoaded;
            Apply();
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        void Apply()
        {
            if (pipeline != null) { pipeline.msaaSampleCount = 4; pipeline.renderScale = Smooth ? SmoothRenderScale : HighRenderScale; }
            foreach (var group in FindObjectsByType<QuestDistantAudience>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                group.SetVisible(!Smooth);
            ApplyFoveation();
        }

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1;
            // Display starts asynchronously and may be recreated after device sleep.
            ApplyFoveation();
        }

        void ApplyFoveation()
        {
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
            {
                if (!display.running) continue;
                display.foveatedRenderingFlags = FoveationFlags; // Fixed foveation; never move the focal region with gaze.
                display.foveatedRenderingLevel = FixedFoveationLevel;
            }
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = original;
            if (pipeline != null) Destroy(pipeline);
            if (instance == this) instance = null;
        }
    }

}
