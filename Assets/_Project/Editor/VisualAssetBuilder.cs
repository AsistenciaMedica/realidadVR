using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Medical.Interaction;
using UnityEditor;
using UnityEngine;

namespace EmergencyVR.Editor
{
    public sealed class VitalVisualAssetImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/ThirdParty/Rocketbox/", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = assetPath.Contains("/Animations/");
            importer.isReadable = true;
            importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(VisualMaterialsSetup.Textures, StringComparison.Ordinal))
            {
                VisualMaterialsSetup.ConfigureTexture((TextureImporter)assetImporter, assetPath);
                return;
            }
            if (assetPath == VisualMaterialsSetup.ControllerOcclusionTexture)
            {
                VisualMaterialsSetup.ConfigureControllerOcclusion((TextureImporter)assetImporter);
                return;
            }
            // The release roster has its own stricter texture budget and Android settings.
            if (assetPath.StartsWith("Assets/ThirdParty/Rocketbox/Roster/", StringComparison.Ordinal)) return;
            if (!assetPath.StartsWith("Assets/ThirdParty/Rocketbox/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            if (assetPath.Contains("_normal")) importer.textureType = TextureImporterType.NormalMap;
        }
    }

    public static class VisualAssetBuilder
    {
        public const string PatientPath = "Assets/ThirdParty/Rocketbox/Patient.fbx";
        const string Output = "Assets/_Project/Resources/Visual/";
        [MenuItem("Emergency VR/Art/Build realistic patient and hands")]
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(PatientPath);
            if(source==null)throw new InvalidOperationException("Run tools/Fetch-VisualAssets.mjs first.");
            var body=Surface("PatientBody","body");var head=Surface("PatientHead","head");
            var glove=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            glove.SetColor("_BaseColor",new Color(.19f,.43f,.55f));glove.SetFloat("_Smoothness",.3f);
            glove=Save(glove,"Glove.mat");
            BuildPatient(source,body,head);
            BuildHand(source,"L",body,glove);BuildHand(source,"R",body,glove);
            Directory.CreateDirectory("Assets/StreamingAssets/ThirdParty");
            File.Copy("Assets/ThirdParty/Rocketbox/LICENSE.md","Assets/StreamingAssets/ThirdParty/Rocketbox-LICENSE.txt",true);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("VITAL_REALISTIC_ASSETS_BUILT");
        }
        static Material Surface(string name,string part)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/Rocketbox/Textures/m021_"+part+"_color.tga"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/Rocketbox/Textures/m021_"+part+"_normal.tga"));
            material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.5f);material.SetFloat("_Smoothness",.26f);
            return Save(material,name+".mat");
        }
        static T Save<T>(T value,string file) where T:UnityEngine.Object
        {
            var previous=AssetDatabase.LoadAssetAtPath<T>(Output+file);
            if(previous==null){AssetDatabase.CreateAsset(value,Output+file);return value;}
            EditorUtility.CopySerialized(value,previous);UnityEngine.Object.DestroyImmediate(value);return previous;
        }
        static Transform Bone(GameObject model,string name) => model.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Bip01 "+name);
        static Transform Anchor(Transform parent,string name,Vector3 position,Quaternion rotation)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.SetPositionAndRotation(position,rotation);return t;
        }
        static void BuildPatient(GameObject source,Material body,Material head)
        {
            var root=new GameObject("Vital VR articulated patient");
            try
            {
                var model=UnityEngine.Object.Instantiate(source,root.transform);model.name="Human";
                foreach(var animator in model.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);
                var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var selected=renderers.OrderByDescending(r=>r.sharedMesh.vertexCount).First();
                foreach(var renderer in renderers)
                {
                    if(renderer!=selected){renderer.gameObject.SetActive(false);continue;}
                    renderer.gameObject.SetActive(true);renderer.updateWhenOffscreen=true;
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m.name.ToLowerInvariant().Contains("head")?head:body).ToArray();
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
                    var mesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);
                    // Ship only the six facial channels the presentation uses, not the full authoring library.
                    mesh.ClearBlendShapes();
                    var facialChannels=new[]{"AK_09_EyeBlinkLeft","AK_10_EyeBlinkRight","AK_25_JawOpen","AU_04_BrowLowerer","AU_01_InnerBrowRaiser","AU_20_LipStretcher"};
                    var delta=new Vector3[mesh.vertexCount];var deltaNormals=new Vector3[mesh.vertexCount];var deltaTangents=new Vector3[mesh.vertexCount];
                    foreach(var channel in facialChannels)
                    {
                        string name="blendShape1."+channel;int shape=renderer.sharedMesh.GetBlendShapeIndex(name);
                        if(shape<0)throw new InvalidOperationException("Missing facial channel: "+name);
                        for(int f=0;f<renderer.sharedMesh.GetBlendShapeFrameCount(shape);f++)
                        {
                            renderer.sharedMesh.GetBlendShapeFrameVertices(shape,f,delta,deltaNormals,deltaTangents);
                            mesh.AddBlendShapeFrame(name,renderer.sharedMesh.GetBlendShapeFrameWeight(shape,f),delta,deltaNormals,deltaTangents);
                        }
                    }
                    var vertices=mesh.vertices;var breath=new Vector3[vertices.Length];var compress=new Vector3[vertices.Length];
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var p=renderer.transform.TransformPoint(vertices[i]);
                        float front=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.10f,p.z));
                        float thorax=Mathf.Exp(-Mathf.Pow(p.x/.17f,2)-Mathf.Pow((p.y-1.28f)/.22f,2))*front;
                        float sternum=Mathf.Exp(-Mathf.Pow(p.x/.10f,2)-Mathf.Pow((p.y-1.30f)/.15f,2))*front;
                        breath[i]=renderer.transform.InverseTransformVector(Vector3.forward*.011f*thorax);
                        compress[i]=renderer.transform.InverseTransformVector(Vector3.back*.08f*sternum);
                    }
                    mesh.AddBlendShapeFrame("VitalBreath",100,breath,null,null);
                    mesh.AddBlendShapeFrame("VitalCompression",100,compress,null,null);
                    renderer.sharedMesh=Save(mesh,"PatientMesh.asset");
                }
                var frame=new GameObject("Posture frame").transform;frame.SetParent(root.transform,false);
                model.transform.SetParent(frame,false);model.transform.localPosition=new Vector3(0,-1.1f,0);
                frame.localRotation=Quaternion.LookRotation(Vector3.up,Vector3.forward);
                var rig=root.AddComponent<PatientRigAdapter>();rig.provisionalAsset=false;rig.poseRoot=frame;rig.skinColor=Color.white;
                rig.head=Bone(model,"Head");rig.leftEye=Bone(model,"LEye");rig.rightEye=Bone(model,"REye");rig.jaw=Bone(model,"MJaw");
                rig.face=selected;rig.skinRenderers=new Renderer[]{selected};
                rig.blinkLeft="blendShape1.AK_09_EyeBlinkLeft";rig.blinkRight="blendShape1.AK_10_EyeBlinkRight";
                rig.jawOpen="blendShape1.AK_25_JawOpen";rig.pain="blendShape1.AU_04_BrowLowerer";
                rig.fear="blendShape1.AU_01_InnerBrowRaiser";rig.distress="blendShape1.AU_20_LipStretcher";
                var chest=Bone(model,"Spine2");
                rig.chestMotion=Anchor(chest,"Chest surface motion",model.transform.TransformPoint(new Vector3(0,1.3f,.135f)),Quaternion.identity);
                rig.chestMotionAxis=chest.InverseTransformDirection(Vector3.up);
                rig.chestAnchor=Anchor(rig.chestMotion,"Sternum",rig.chestMotion.position,Quaternion.identity);
                rig.aedRightPadAnchor=Anchor(rig.chestMotion,"Right AED pad",model.transform.TransformPoint(new Vector3(.1f,1.42f,.12f)),Quaternion.identity);
                rig.aedLeftPadAnchor=Anchor(rig.chestMotion,"Left AED pad",model.transform.TransformPoint(new Vector3(-.16f,1.23f,.07f)),Quaternion.Euler(0,0,-35));
                rig.upperArmAnchor=Anchor(Bone(model,"L UpperArm"),"Cuff",Bone(model,"L UpperArm").position+Vector3.up*.055f,Quaternion.identity);
                rig.fingerAnchor=Anchor(Bone(model,"L Finger11"),"Finger measurement",Bone(model,"L Finger11").position+Vector3.up*.008f,Quaternion.identity);
                rig.thighAnchor=Anchor(Bone(model,"R Thigh"),"Lateral thigh",model.transform.TransformPoint(new Vector3(.15f,.76f,.06f)),Quaternion.identity);
                rig.woundAnchor=Anchor(Bone(model,"R Forearm"),"Bandage site",Bone(model,"R Forearm").position+Vector3.up*.045f,Quaternion.identity);
                rig.chinAnchor=Anchor(rig.head,"Chin",model.transform.TransformPoint(new Vector3(0,1.60f,.13f)),Quaternion.identity);
                var animated=root.AddComponent<ArticulatedPatient>();animated.skeleton=frame;animated.pelvis=Bone(model,"Pelvis");animated.spine=chest;animated.neck=Bone(model,"Neck");
                animated.upperArms=new[]{Bone(model,"L UpperArm"),Bone(model,"R UpperArm")};
                animated.forearms=new[]{Bone(model,"L Forearm"),Bone(model,"R Forearm")};
                animated.hands=new[]{Bone(model,"L Hand"),Bone(model,"R Hand")};
                animated.thighs=new[]{Bone(model,"L Thigh"),Bone(model,"R Thigh")};
                animated.calves=new[]{Bone(model,"L Calf"),Bone(model,"R Calf")};
                animated.ankles=new[]{Bone(model,"L Foot"),Bone(model,"R Foot")};
                animated.deformingMeshes=new[]{selected};
                rig.upperLegs=animated.thighs;rig.lowerLegs=animated.calves;rig.feet=animated.ankles;
                animated.standingBreath=Clip("m_idle_breathe_01");animated.standingCough=Clip("m_idle_cough_01");
                var volumes=new List<PatientContactVolume>();
                void Volume(string name,Transform bone,Vector3 size) => volumes.Add(new PatientContactVolume(name,bone,Vector3.zero,size));
                Volume("head",rig.head,new Vector3(.26f,.29f,.26f));Volume("torso",chest,new Vector3(.40f,.45f,.30f));
                Volume("pelvis",animated.pelvis,new Vector3(.30f,.35f,.30f));
                foreach(var bone in animated.upperArms.Concat(animated.forearms).Concat(animated.thighs).Concat(animated.calves))Volume(bone.name,bone,new Vector3(.28f,.28f,.28f));
                foreach(var bone in animated.hands)Volume(bone.name,bone,new Vector3(.15f,.15f,.20f));
                rig.contactVolumes=volumes.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,Output+"Patient.prefab");
                Debug.Log("PATIENT_MESH vertices="+selected.sharedMesh.vertexCount+" blendshapes="+selected.sharedMesh.blendShapeCount);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/Rocketbox/Animations/"+name+".fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__"));
        static void BuildHand(GameObject source,string side,Material skin,Material glove)
        {
            var root=new GameObject(side=="L"?"Left hand and forearm":"Right hand and forearm");
            try
            {
                var model=UnityEngine.Object.Instantiate(source,root.transform);model.name="HandSkeleton";
                foreach(var animator in model.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);
                var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var renderer=renderers.OrderByDescending(r=>r.sharedMesh.vertexCount).First();
                foreach(var other in renderers)other.gameObject.SetActive(other==renderer);
                var original=renderer.sharedMesh;var weights=original.boneWeights;
                float Weight(int vertex,bool includeArm)
                {
                    var w=weights[vertex];var indices=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};float total=0;
                    for(int i=0;i<4;i++){var name=renderer.bones[indices[i]].name;if(name.StartsWith("Bip01 "+side+" Finger")||name=="Bip01 "+side+" Hand"||includeArm&&name=="Bip01 "+side+" Forearm")total+=values[i];}
                    return total;
                }
                var armTriangles=new List<int>();var handTriangles=new List<int>();var map=new Dictionary<int,int>();
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var boneWeights=new List<BoneWeight>();
                var originalVertices=original.vertices;var originalNormals=original.normals;var originalUv=original.uv;
                int Vertex(int index){if(map.TryGetValue(index,out int found))return found;int next=vertices.Count;map.Add(index,next);vertices.Add(originalVertices[index]);normals.Add(originalNormals[index]);uv.Add(originalUv[index]);boneWeights.Add(weights[index]);return next;}
                var triangles=original.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    if(Weight(a,true)<.55f||Weight(b,true)<.55f||Weight(c,true)<.55f)continue;
                    var target=(Weight(a,false)+Weight(b,false)+Weight(c,false))/3>.5f?handTriangles:armTriangles;
                    target.Add(Vertex(a));target.Add(Vertex(b));target.Add(Vertex(c));
                }
                if(vertices.Count<100)throw new InvalidOperationException("Hand extraction failed: "+side);
                var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.boneWeights=boneWeights.ToArray();mesh.bindposes=original.bindposes;
                mesh.subMeshCount=2;mesh.SetTriangles(armTriangles,0);mesh.SetTriangles(handTriangles,1);mesh.RecalculateBounds();mesh.RecalculateTangents();
                renderer.sharedMesh=Save(mesh,side+"HandMesh.asset");renderer.sharedMaterials=new[]{skin,glove};renderer.updateWhenOffscreen=true;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
                var wrist=Bone(model,side+" Hand");var middle=Bone(model,side+" Finger2");
                var forward=(middle.position-wrist.position).normalized;
                var across=(Bone(model,side+" Finger4").position-Bone(model,side+" Finger1").position).normalized;
                var normal=Vector3.Cross(forward,across).normalized;if(side=="L")normal=-normal;
                model.transform.rotation=Quaternion.Inverse(Quaternion.LookRotation(forward,normal));model.transform.position=-wrist.position;
                var hand=root.AddComponent<ArticulatedHand>();var joints=new List<Transform>();var axes=new List<Vector3>();var fingers=new List<int>();var segments=new List<int>();
                for(int finger=0;finger<5;finger++)for(int segment=0;segment<3;segment++)
                {
                    var joint=Bone(model,side+" Finger"+finger+(segment==0?"":segment.ToString()));
                    joints.Add(joint);axes.Add(joint.InverseTransformDirection(Vector3.right));fingers.Add(finger);segments.Add(segment);
                }
                hand.fingerJoints=joints.ToArray();hand.curlAxes=axes.ToArray();hand.fingers=fingers.ToArray();hand.segments=segments.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,Output+(side=="L"?"LeftHand":"RightHand")+".prefab");
                Debug.Log("HAND_MESH "+side+" vertices="+vertices.Count);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [MenuItem("Emergency VR/Art/Inspect imported human")]
        public static void Inspect()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PatientPath);
            if (source == null) throw new InvalidOperationException("Patient FBX did not import.");
            var instance = UnityEngine.Object.Instantiate(source);
            var report = new StringBuilder();
            foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                report.AppendLine(node.name + " | pos " + node.position.ToString("F4") + " | rot " + node.eulerAngles.ToString("F2") + " | scale " + node.lossyScale.ToString("F4"));
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                report.AppendLine("MESH " + renderer.name + " vertices=" + renderer.sharedMesh.vertexCount + " bounds=" + renderer.bounds + " materials=" + string.Join(",", renderer.sharedMaterials.Select(m => m == null ? "null" : m.name)));
                report.AppendLine("SHAPES " + string.Join(",", Enumerable.Range(0,renderer.sharedMesh.blendShapeCount).Select(renderer.sharedMesh.GetBlendShapeName)));
            }
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/imported-human.txt", report.ToString());
            UnityEngine.Object.DestroyImmediate(instance);
            Debug.Log("VITAL_ASSET_INSPECTION_OK");
        }
    }
}
