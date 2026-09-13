using System;
using System.IO;
using System.Linq;
using EmergencyVR.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EmergencyVR.Editor.Environment
{
    public static class TrainingRoomPreview
    {
        [MenuItem(TrainingRoomEnvironmentBuilder.Menu + "Capture Training Room Preview")]
        public static void Capture() { CaptureTo("TestResults/training-room-preview.png"); }

        [MenuItem(TrainingRoomEnvironmentBuilder.Menu + "Capture Polished Training Room")]
        public static void CapturePolished() { CaptureTo("TestResults/training-room-polished.png"); }

        static void CaptureTo(string path)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var previous=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath(DemoProjectBuilder.TrainingPath);
            bool opened=!scene.IsValid() || !scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene(DemoProjectBuilder.TrainingPath,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var cameraObject=new GameObject("Temporary environment preview camera");
            var camera=cameraObject.AddComponent<Camera>();
            var target=new RenderTexture(1600,1000,24) {antiAliasing=4};
            var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            var previousTarget=RenderTexture.active;
            try
            {
                camera.enabled=false;
                camera.cameraType=CameraType.Game;
                camera.stereoTargetEye=StereoTargetEyeMask.None;
                camera.transform.position=new Vector3(-2.55f,1.97f,-1.73f);
                camera.transform.LookAt(new Vector3(.6f,1.15f,2.47f));
                camera.fieldOfView=65; camera.nearClipPlane=.04f; camera.farClipPlane=25;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.1f,.15f,.17f);
                var data=camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing=false; data.allowXRRendering=false;
                target.Create();
                var request=new UniversalRenderPipeline.SingleCameraRequest { destination=target };
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                Directory.CreateDirectory("TestResults");
                File.WriteAllBytes(path,pixels.EncodeToPNG());
                var manifest=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GeneratedEnvironment>()).Single();
                var objects=manifest.ownedObjects.Where(o=>o!=null).ToArray();
                var stats=new GeometryStats {
                    renderers=objects.Sum(o=>o.GetComponents<Renderer>().Length),
                    triangles=objects.SelectMany(o=>o.GetComponents<MeshFilter>()).Sum(f=>f.sharedMesh.triangles.Length/3),
                    boxColliders=objects.Sum(o=>o.GetComponents<BoxCollider>().Length)
                };
                File.WriteAllText("TestResults/polished-geometry-stats.json",JsonUtility.ToJson(stats,true));
                Debug.Log("Preview saved: " + path + "\n" + JsonUtility.ToJson(stats));
            }
            finally
            {
                RenderTexture.active=previousTarget;
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(pixels);
                target.Release(); Object.DestroyImmediate(target);
                if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if(opened) EditorSceneManager.CloseScene(scene,true);
            }
        }

        [Serializable] sealed class GeometryStats { public int renderers; public int triangles; public int boxColliders; }
    }
}
