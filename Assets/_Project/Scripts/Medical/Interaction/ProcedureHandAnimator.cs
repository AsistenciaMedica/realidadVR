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
        public bool HasArticulatedHands => left != null && right != null;
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
            return prefab==null?null:Instantiate(prefab,transform).GetComponent<ArticulatedHand>();
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
            if(desktop==null){Track(left,XRNode.LeftHand);Track(right,XRNode.RightHand);return;}
            var camera=desktop.View.transform;
            bool compress=rig.CPR.HasManualContact;
            if(compress)action=null;
            var held=desktop.HeldTool;
            for(int side=0;side<2;side++)
            {
                var hand=side==0?left:right;
                hand.gameObject.SetActive(true);
                var position=camera.TransformPoint(new Vector3(side==0?-.22f:.22f,-.29f,.48f));
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
                else if(held!=null && side==1)
                {
                    position=held.transform.TransformPoint(new Vector3(.025f,-.025f,-.06f));
                    rotation=held.transform.rotation;
                    pose=held.Kind==MedicalToolKind.RightPad||held.Kind==MedicalToolKind.LeftPad||held.Kind==MedicalToolKind.Oximeter?MedicalHandPose.Pinch:MedicalHandPose.Grip;
                }
                else if(action!=null && actionTime<1.8f)
                {
                    var target=Target(action,side);
                    if(target!=null && Vector3.Distance(camera.position,target.position)<2.2f)
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
                float rate=compress?35:18;
                hand.transform.SetPositionAndRotation(Vector3.Lerp(hand.transform.position,position,1-Mathf.Exp(-Time.deltaTime*rate)),
                    Quaternion.Slerp(hand.transform.rotation,rotation,1-Mathf.Exp(-Time.deltaTime*rate)));
                hand.SetPose(pose);
            }
        }
        Transform Target(string name,int side)
        {
            if(name.Contains("Airway")||name.Contains("Breathing"))return rig.Visuals.ChinAnchor;
            if(name.Contains("Glucose")||name.Contains("SpO2"))return rig.Visuals.FingerAnchor;
            if(name.Contains("BloodPressure"))return rig.Visuals.UpperArmAnchor;
            if(name.Contains("Bandage")||name=="ApplyPressure"||name=="ControlBleeding")return rig.Visuals.WoundAnchor;
            if(name.Contains("Epinephrine"))return rig.Visuals.ThighAnchor;
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
            if(rig==null)return;
            if(rig.Manager!=null)rig.Manager.ActionAccepted-=OnAction;
            if(rig.Review!=null)rig.Review.SelectionChanged-=ResetPose;
        }
    }
}
