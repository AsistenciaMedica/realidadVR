using EmergencyVR.Desktop;
using System.Collections.Generic;
using EmergencyVR.Patient.Presentation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace EmergencyVR.Medical.Interaction
{
    [DefaultExecutionOrder(150)]
    public sealed class ProcedureHandAnimator : MonoBehaviour
    {
        MedicalProcedureRig rig;
        ArticulatedHand left, right;
        DesktopDemoController desktop;
        XROrigin origin;
        readonly Dictionary<XRNode,Renderer[]> controllerModels=new Dictionary<XRNode,Renderer[]>();
        readonly Dictionary<Renderer,bool> originalRendering=new Dictionary<Renderer,bool>();
        string action;
        float actionTime, lookupTime;
        // Desktop first person: virtual shoulders relative to the camera and an adult shoulder-to-wrist reach.
        static readonly Vector3 LeftShoulder=new Vector3(-.19f,-.24f,.02f), RightShoulder=new Vector3(.19f,-.24f,.02f);
        const float ArmReach=.68f;
        public bool HasArticulatedHands => left != null && right != null;
        /// <summary>A prop the right hand holds up in view, such as the learner's phone during a call.</summary>
        public Transform HeldProp {get;set;}
        public void Initialize(MedicalProcedureRig owner)
        {
            rig=owner;
            left=Create("LeftHand");right=Create("RightHand");
            rig.Manager.ActionAccepted+=OnAction;
            rig.Review.SelectionChanged+=ResetPose;
        }
        ArticulatedHand Create(string name)
        {
            var prefab=Resources.Load<GameObject>("Visual/"+name);
            var hand=prefab==null?null:Instantiate(prefab,transform).GetComponent<ArticulatedHand>();
            if(hand!=null)AddSleeve(hand.transform);
            return hand;
        }
        Material sleeveMaterial;
        Mesh sleeveMesh;
        // The extracted forearm ends in a ragged cut; a uniform sleeve from mid-forearm hides it in every pose.
        void AddSleeve(Transform hand)
        {
            if(sleeveMesh==null)
            {
                sleeveMesh=Tube(.052f,.066f,-.2f,-.5f,20);
                sleeveMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Responder sleeve"};
                sleeveMaterial.SetColor("_BaseColor",new Color(.05f,.10f,.16f));
                sleeveMaterial.SetFloat("_Smoothness",.12f);
            }
            // The forearm submesh becomes a fitted sleeve; the looser cuff hides the extraction's ragged edge.
            var skinned=hand.GetComponentInChildren<SkinnedMeshRenderer>();
            if(skinned!=null&&skinned.sharedMaterials.Length>1)
            {
                var materials=skinned.sharedMaterials;materials[0]=sleeveMaterial;skinned.sharedMaterials=materials;
            }
            var sleeve=new GameObject("Sleeve",typeof(MeshFilter),typeof(MeshRenderer));
            sleeve.transform.SetParent(hand,false);
            sleeve.GetComponent<MeshFilter>().sharedMesh=sleeveMesh;
            sleeve.GetComponent<MeshRenderer>().sharedMaterial=sleeveMaterial;
        }
        // Open tapered tube along -Z (hand local space: +Z is wrist→fingers), with a rolled cuff edge.
        static Mesh Tube(float wristRadius,float elbowRadius,float from,float to,int segments)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            float[] z={from+.008f,from,to};float[] r={wristRadius*.92f,wristRadius,elbowRadius};
            for(int ring=0;ring<z.Length;ring++)
                for(int i=0;i<=segments;i++)
                {
                    float a=i*Mathf.PI*2/segments;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.86f,0);
                    vertices.Add(d*r[ring]+Vector3.forward*z[ring]);normals.Add(d.normalized);
                }
            for(int ring=0;ring<z.Length-1;ring++)
                for(int i=0;i<segments;i++)
                {
                    int a=ring*(segments+1)+i,b=a+1,c=a+segments+1,e=c+1;
                    triangles.AddRange(new[]{a,c,b,b,c,e,a,b,c,b,e,c});
                }
            var mesh=new Mesh{name="Responder sleeve"};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            return mesh;
        }
        void OnAction(string name)
        {
            if(name=="StartCPR"||name=="ChestCompression"||name=="ContinueCPR")return;
            action=name;actionTime=0;
            rig.Visuals.Rig.GetComponent<ArticulatedPatient>()?.React(name);
        }
        public void ResetPose()
        {
            action=null;actionTime=0;
            if(rig!=null && rig.Visuals!=null && rig.Visuals.Rig!=null)
                rig.Visuals.Rig.GetComponent<ArticulatedPatient>()?.ResetReactions();
        }
        void LateUpdate()
        {
            if(!HasArticulatedHands)return;
            if(Time.unscaledTime>=lookupTime)
            {
                desktop=FindFirstObjectByType<DesktopDemoController>();
                origin=desktop==null?FindFirstObjectByType<XROrigin>():null;
                if(origin!=null && controllerModels.Count==0)
                    foreach(var node in origin.GetComponentsInChildren<Transform>(true))
                        if(node.name=="Left Controller Visual"||node.name=="Right Controller Visual")
                        {
                            var models=node.GetComponentsInChildren<Renderer>(true);
                            controllerModels[node.name.StartsWith("Left")?XRNode.LeftHand:XRNode.RightHand]=models;
                            foreach(var model in models)originalRendering[model]=model.forceRenderingOff;
                        }
                lookupTime=Time.unscaledTime+1;
            }
            actionTime+=Time.deltaTime;
            if(!rig.Manager.IsRunning)ResetPose();
            if(desktop==null)
            {
                Track(left,XRNode.LeftHand);Track(right,XRNode.RightHand);
                if(HeldProp!=null&&right.gameObject.activeSelf)
                    HeldProp.SetPositionAndRotation(right.transform.TransformPoint(new Vector3(0,.03f,.07f)),right.transform.rotation*Quaternion.Euler(-90,0,0));
                return;
            }
            var camera=desktop.View.transform;
            bool compress=rig.CPR.HasManualContact;
            if(compress)action=null;
            var held=desktop.HeldTool;
            for(int side=0;side<2;side++)
            {
                var hand=side==0?left:right;
                hand.gameObject.SetActive(true);
                // At rest the hands wait just below the view; they rise into frame only to act.
                var position=camera.TransformPoint(new Vector3(side==0?-.24f:.24f,-.62f,.22f));
                var rotation=camera.rotation*Quaternion.Euler(12,side==0?12:-12,side==0?-12:12);
                var pose=MedicalHandPose.Relaxed;
                if(compress)
                {
                    var anchor=rig.Visuals.ChestAnchor;
                    var forward=Vector3.ProjectOnPlane(anchor.position-camera.position,anchor.up).normalized;
                    if(forward.sqrMagnitude<.01f)forward=anchor.forward;
                    rotation=Quaternion.LookRotation(forward,anchor.up)*Quaternion.Euler(0,side==0?0:20,0);
                    position=rig.CPR.ChestRestPosition-anchor.up*rig.Visuals.CompressionDepthMetres+
                        anchor.up*(side==0?.025f:.05f)-(rotation*Vector3.forward)*.06f;
                    pose=MedicalHandPose.Compression;
                }
                else if(HeldProp!=null && side==1)
                {
                    // Phone held low in the right of view, screen toward the learner, fingers wrapped behind it.
                    HeldProp.SetPositionAndRotation(camera.TransformPoint(new Vector3(.12f,-.13f,.34f)),camera.rotation*Quaternion.Euler(-12,180,-6));
                    position=HeldProp.position-HeldProp.up*.03f+camera.forward*.035f;
                    rotation=Quaternion.LookRotation(HeldProp.up,camera.forward);
                    pose=MedicalHandPose.Grip;
                }
                else if(held!=null && side==1)
                {
                    position=held.transform.TransformPoint(new Vector3(.025f,-.025f,-.06f));
                    rotation=held.transform.rotation;
                    pose=held.Kind==MedicalToolKind.RightPad||held.Kind==MedicalToolKind.LeftPad||held.Kind==MedicalToolKind.Oximeter?MedicalHandPose.Pinch:MedicalHandPose.Grip;
                }
                else if(Assisting(out var shoulderAnchor,out var backAnchor))
                {
                    // Supporting Daniel's descent: one hand on the near shoulder, the other on the upper back.
                    var target=side==0?shoulderAnchor:backAnchor;
                    if(Vector3.Distance(camera.TransformPoint(side==0?LeftShoulder:RightShoulder),target)<=ArmReach)
                    {
                        position=target;
                        rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(target-camera.position,Vector3.up).normalized+Vector3.down*.4f,Vector3.up);
                        pose=MedicalHandPose.Support;
                    }
                }
                else if(action!=null && actionTime<1.8f)
                {
                    var target=action=="AssessResponsiveness"?NearShoulder(camera,side):Target(action,side);
                    var shoulder=camera.TransformPoint(side==0?LeftShoulder:RightShoulder);
                    // Only reach what an arm can physically touch; otherwise the hands stay at rest.
                    if(target!=null && Vector3.Distance(shoulder,target.position)<=ArmReach)
                    {
                        float reach=Mathf.SmoothStep(0,1,Mathf.Clamp01(actionTime/.35f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((1.8f-actionTime)/.45f));
                        var approach=Vector3.ProjectOnPlane(target.position-camera.position,target.up).normalized;
                        var contactRotation=approach.sqrMagnitude>.01f?Quaternion.LookRotation(approach,target.up):target.rotation;
                        var offset=target.up*.03f-(contactRotation*Vector3.forward)*.065f+(contactRotation*Vector3.right)*(side==0?-.025f:.025f);
                        position=Vector3.Lerp(position,target.position+offset,reach);
                        rotation=Quaternion.Slerp(rotation,contactRotation,reach);
                        pose=action.Contains("Pulse")||action.Contains("Airway")?MedicalHandPose.Point:MedicalHandPose.Support;
                    }
                }
                if(!compress)rotation=AlongForearm(position,rotation,camera.TransformPoint(side==0?LeftShoulder:RightShoulder));
                float rate=compress?35:18;
                hand.transform.SetPositionAndRotation(Vector3.Lerp(hand.transform.position,position,1-Mathf.Exp(-Time.deltaTime*rate)),
                    Quaternion.Slerp(hand.transform.rotation,rotation,1-Mathf.Exp(-Time.deltaTime*rate)));
                hand.SetPose(pose);
            }
        }
        // Touch the patient's shoulder nearest the learner, with the learner's hand on that side only.
        Transform nearShoulder;
        Transform NearShoulder(Transform camera,int side)
        {
            var anchor=rig.Visuals.UpperArmAnchor;var chest=rig.Visuals.ChestAnchor;
            if(anchor==null||chest==null)return side==0?anchor:null;
            if(nearShoulder==null)nearShoulder=new GameObject("Near shoulder contact").transform;
            var mirrored=new Vector3(2*chest.position.x-anchor.position.x,anchor.position.y,2*chest.position.z-anchor.position.z);
            var point=Vector3.Distance(camera.position,mirrored)<Vector3.Distance(camera.position,anchor.position)?mirrored:anchor.position;
            nearShoulder.SetPositionAndRotation(point,anchor.rotation);
            bool leftCloser=Vector3.Distance(camera.TransformPoint(LeftShoulder),point)<=Vector3.Distance(camera.TransformPoint(RightShoulder),point);
            return (side==0)==leftCloser?nearShoulder:null;
        }
        bool Assisting(out Vector3 shoulder,out Vector3 back)
        {
            shoulder=back=default;
            var body=rig.Review==null?null:rig.Review.GetComponent<Case01PatientPresentation>();
            if(body==null||!body.IsAssisting||rig.Visuals==null||rig.Visuals.UpperArmAnchor==null)return false;
            shoulder=rig.Visuals.UpperArmAnchor.position+Vector3.up*.03f;
            // Daniel faces -Z on the bench, so his upper back is on the +Z side of the spine.
            back=Vector3.Lerp(body.PelvisPosition,body.HeadPosition,.62f)+Vector3.forward*.14f;
            return true;
        }
        // The hand root's +Z runs wrist→fingers and its forearm extends along -Z, so aiming +Z away from the
        // shoulder keeps the forearm on the shoulder–wrist line instead of pointing into the camera.
        static Quaternion AlongForearm(Vector3 wrist,Quaternion desired,Vector3 shoulder)
        {
            var arm=wrist-shoulder;
            if(arm.sqrMagnitude<.0004f)return desired;
            var palm=Vector3.ProjectOnPlane(desired*Vector3.up,arm);
            if(palm.sqrMagnitude<.0001f)palm=Vector3.ProjectOnPlane(Vector3.up,arm);
            if(palm.sqrMagnitude<.0001f)return desired;
            return Quaternion.LookRotation(arm.normalized,palm.normalized);
        }
        Transform Target(string name,int side)
        {
            // Observing breathing in someone who talks is visual only; the chin is for airway manoeuvres.
            if(name=="ObserveBreathing")return null;
            if(name.Contains("Airway")||name.Contains("Breathing"))return rig.Visuals.ChinAnchor;
            if(name.Contains("Glucose")||name.Contains("SpO2"))return rig.Visuals.FingerAnchor;
            if(name.Contains("BloodPressure"))return rig.Visuals.UpperArmAnchor;
            if(name.Contains("Bandage")||name=="ApplyPressure"||name=="ControlBleeding")return rig.Visuals.WoundAnchor;
            if(name.Contains("Epinephrine"))return rig.Visuals.ThighAnchor;
            // A conscious patient is approached with one gentle hand on the shoulder, not both hands on the chest.
            if(name=="AssessResponsiveness")return side==0?rig.Visuals.UpperArmAnchor:null;
            if(name.Contains("Responsiveness"))return side==0?rig.Visuals.UpperArmAnchor:rig.Visuals.ChestAnchor;
            if(name.Contains("Recovery")||name.Contains("Position"))return side==0?rig.Visuals.UpperArmAnchor:rig.Visuals.ThighAnchor;
            if(name.Contains("Pulse"))return rig.Visuals.ChinAnchor;
            if(name=="CheckTemperature")return side==1?rig.Visuals.Rig.head:null;
            if(name=="ProtectFromInjury")return side==0?rig.Visuals.UpperArmAnchor:rig.Visuals.Rig.head;
            return null;
        }
        void Track(ArticulatedHand hand,XRNode node)
        {
            var device=InputDevices.GetDeviceAtXRNode(node);
            bool tracked=origin!=null&&device.TryGetFeatureValue(CommonUsages.isTracked,out var value)&&value;
            bool hasPose=device.TryGetFeatureValue(CommonUsages.devicePosition,out var position)&device.TryGetFeatureValue(CommonUsages.deviceRotation,out var rotation);
            tracked&=hasPose;
            if(controllerModels.TryGetValue(node,out var models))
                foreach(var model in models)if(model!=null)model.forceRenderingOff=tracked||originalRendering[model];
            hand.gameObject.SetActive(tracked);
            if(!tracked)return;
            var frame=origin.CameraFloorOffsetObject!=null?origin.CameraFloorOffsetObject.transform:origin.transform;
            hand.transform.SetPositionAndRotation(frame.TransformPoint(position),frame.rotation*rotation);
            device.TryGetFeatureValue(CommonUsages.grip,out float grip);
            device.TryGetFeatureValue(CommonUsages.trigger,out float trigger);
            hand.SetPose(rig.CPR.HasManualContact?MedicalHandPose.Compression:grip>.15f?MedicalHandPose.Grip:trigger>.15f?MedicalHandPose.Pinch:MedicalHandPose.Relaxed,Mathf.Max(.2f,Mathf.Max(grip,trigger)));
        }
        void OnDisable()
        {
            foreach(var pair in originalRendering)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;
            if(left!=null)left.gameObject.SetActive(false);
            if(right!=null)right.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            if(sleeveMesh!=null)Destroy(sleeveMesh);
            if(sleeveMaterial!=null)Destroy(sleeveMaterial);
            if(rig==null)return;
            if(rig.Manager!=null)rig.Manager.ActionAccepted-=OnAction;
            if(rig.Review!=null)rig.Review.SelectionChanged-=ResetPose;
        }
    }
}
