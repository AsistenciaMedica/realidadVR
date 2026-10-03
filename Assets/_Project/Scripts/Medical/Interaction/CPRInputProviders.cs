using EmergencyVR.Desktop;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace EmergencyVR.Medical.Interaction
{
    [DefaultExecutionOrder(-50)]
    public sealed class WindowsCPRInput : MonoBehaviour
    {
        CPRInteractionController controller;DesktopDemoController desktop;float startY,placement,lookupAt;bool dragging;
        public void Initialize(CPRInteractionController c){controller=c;}
        void Update()
        {
            if(desktop==null && Time.unscaledTime>=lookupAt){desktop=FindFirstObjectByType<DesktopDemoController>();lookupAt=Time.unscaledTime+.5f;}
            if(desktop==null||desktop.WorldInputBlocked||Keyboard.current==null||Mouse.current==null){if(dragging)controller.ReleaseContact();dragging=false;return;}
            var mouse=Mouse.current;
            if(Keyboard.current.cKey.wasPressedThisFrame)
            {
                bool close=Vector3.Distance(desktop.View.transform.position,controller.ChestRestPosition)<2.2f;
                controller.SetWindowsEngaged(!controller.WindowsEngaged&&close&&controller.CanCompress&&!desktop.HasHeldTool);
            }
            if(!controller.WindowsEngaged){dragging=false;return;}
            if(desktop.PointerOverInterface||desktop.HasHeldTool){if(dragging)controller.ReleaseContact();dragging=false;return;}
            if(!controller.CanCompress){controller.SetWindowsEngaged(false);return;}
            if(Vector3.Distance(desktop.View.transform.position,controller.ChestRestPosition)>2.2f){controller.SetWindowsEngaged(false);dragging=false;return;}
            if(mouse.leftButton.wasPressedThisFrame)
            {
                var ray=desktop.View.ScreenPointToRay(mouse.position.ReadValue());var rest=controller.ChestRestPosition;
                var plane=new Plane(controller.ChestAnchor.up,rest);dragging=false;
                if(plane.Raycast(ray,out var distance)&&distance<=2.2f){placement=Vector3.Distance(ray.GetPoint(distance),rest);dragging=placement<.25f;startY=mouse.position.y.ReadValue();}
            }
            if(dragging&&mouse.leftButton.isPressed)controller.Feed(Mathf.Max(0,(startY-mouse.position.y.ReadValue())*.0005f),placement,0,true,"WINDOWS_POINTER");
            if(dragging&&mouse.leftButton.wasReleasedThisFrame){dragging=false;controller.ReleaseContact();}
        }
        void OnDisable(){if(dragging&&controller!=null)controller.ReleaseContact();dragging=false;}
    }
    [DefaultExecutionOrder(-50)]
    public sealed class VRCPRInput : MonoBehaviour
    {
        CPRInteractionController controller;MedicalProcedureRig rig;XROrigin origin;bool engaged,leftContact,rightContact;DesktopDemoController desktop;float lookupAt,nextLeftContact,nextRightContact;
        UnityEngine.XR.InputDevice left,right;
        [Tooltip("Virtual grip-pose offset from the sternum. This must be calibrated for the controller/hand visual, not used as an instrumented depth measurement.")]
        public float stackedGripOffset=.04f;
        public void Initialize(CPRInteractionController c){controller=c;rig=c.GetComponent<MedicalProcedureRig>();}
        void Update()
        {
            if(desktop==null&&Time.unscaledTime>=lookupAt){desktop=FindFirstObjectByType<DesktopDemoController>();lookupAt=Time.unscaledTime+.5f;}
            if(desktop!=null){ClearContact();return;}
            if(origin==null)origin=FindFirstObjectByType<XROrigin>();if(origin==null){ClearContact();return;}
            if(!left.isValid)left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if(!right.isValid)right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            var space=origin.CameraFloorOffsetObject!=null?origin.CameraFloorOffsetObject.transform:origin.transform;
            bool simulated=QuestLookSimulation.TryGetControllerPose(XRNode.LeftHand,out var lp,out var lr,out var leftTracked);
            QuestLookSimulation.TryGetControllerPose(XRNode.RightHand,out var rp,out var rr,out var rightTracked);
            bool hasRotations=simulated;
            bool gripping;
            if(simulated)
            {
                QuestLookSimulation.TryGetGripTrigger(XRNode.LeftHand,out var lg,out _);
                QuestLookSimulation.TryGetGripTrigger(XRNode.RightHand,out var rg,out _);
                gripping=lg>.5f&&rg>.5f;
            }
            else
            {
                bool leftPosition=left.TryGetFeatureValue(CommonUsages.devicePosition,out lp);
                bool rightPosition=right.TryGetFeatureValue(CommonUsages.devicePosition,out rp);
                leftTracked=leftPosition&&left.TryGetFeatureValue(CommonUsages.isTracked,out var lt)&&lt;
                rightTracked=rightPosition&&right.TryGetFeatureValue(CommonUsages.isTracked,out var rt)&&rt;
                lp=space.TransformPoint(lp);rp=space.TransformPoint(rp);
                hasRotations=left.TryGetFeatureValue(CommonUsages.deviceRotation,out lr)&right.TryGetFeatureValue(CommonUsages.deviceRotation,out rr);
                lr=space.rotation*lr;rr=space.rotation*rr;
                gripping=left.TryGetFeatureValue(CommonUsages.gripButton,out var lg)&&lg&&right.TryGetFeatureValue(CommonUsages.gripButton,out var rg)&&rg;
            }
            var anchor=controller.ChestAnchor;var rest=controller.ChestRestPosition;var normal=anchor.up;
            // Contact detection is independent of grip buttons and remains live during AED analysis/shock.
            // Explicit anatomy volumes include head, arms and legs, not just the CPR contact region.
            bool touchingLeft=leftTracked&&controller.IsPatientContact(lp),touchingRight=rightTracked&&controller.IsPatientContact(rp);
            controller.SetProximityContact(touchingLeft||touchingRight);
            if(rig!=null&&rig.Manager.AcceptsInput)
            {
                if(touchingLeft&&!leftContact&&Time.unscaledTime>=nextLeftContact){MedicalHaptics.Pulse(XRNode.LeftHand,.10f,.025f,"PatientContact");nextLeftContact=Time.unscaledTime+.2f;}
                if(touchingRight&&!rightContact&&Time.unscaledTime>=nextRightContact){MedicalHaptics.Pulse(XRNode.RightHand,.10f,.025f,"PatientContact");nextRightContact=Time.unscaledTime+.2f;}
            }
            leftContact=touchingLeft;rightContact=touchingRight;
            bool tracked=leftTracked&&rightTracked;
            if(!tracked||!gripping||!controller.CanCompress){if(engaged)controller.ReleaseContact();engaged=false;return;}
            var middle=(lp+rp)*.5f;float error=Vector3.ProjectOnPlane(middle-rest,normal).magnitude;
            float elevation=Vector3.Dot(middle-rest,normal);
            if(error>.25f||elevation>.12f||elevation<-.12f){if(engaged)controller.ReleaseContact();engaged=false;return;}
            if(!engaged){if(error>.15f||elevation>stackedGripOffset+.025f)return;engaged=true;}
            // Depth references the undeformed chest; moving the chest cannot inflate the next sample.
            float depth=Mathf.Clamp(stackedGripOffset-elevation,0,.09f),angle=180;
            if(hasRotations)
                angle=Mathf.Max(Vector3.Angle(lr*Vector3.up,normal),Vector3.Angle(rr*Vector3.up,normal));
            controller.Feed(depth,error,angle,Vector3.Distance(lp,rp)<.16f,"XR_CONTROLLERS");
        }
        void ClearContact(){if(controller==null)return;if(engaged)controller.ReleaseContact();engaged=false;leftContact=rightContact=false;controller.SetProximityContact(false);}
        void OnDisable(){ClearContact();}
    }
    public static class MedicalHaptics
    {
        public readonly struct Request
        {
            public readonly XRNode Hand;
            public readonly float Amplitude,Seconds;
            public readonly string Reason;
            public readonly bool Simulated;
            public Request(XRNode hand,float amplitude,float seconds,string reason,bool simulated)
            {Hand=hand;Amplitude=amplitude;Seconds=seconds;Reason=reason;Simulated=simulated;}
        }
        // Records a requested output, not a claim that a physical controller vibrated.
        public static event System.Action<Request> Requested;
        public static void Pulse(float amplitude,float seconds,string reason="Procedure")
        {
            Pulse(XRNode.LeftHand,amplitude,seconds,reason);Pulse(XRNode.RightHand,amplitude,seconds,reason);
        }
        public static void Pulse(XRNode node,float amplitude,float seconds,string reason)
        {
            if(node!=XRNode.LeftHand&&node!=XRNode.RightHand||float.IsNaN(amplitude)||float.IsNaN(seconds)||float.IsInfinity(amplitude)||float.IsInfinity(seconds)||amplitude<=0||seconds<=0)return;
            amplitude=Mathf.Clamp01(amplitude);seconds=Mathf.Min(seconds,.25f);
            bool simulated=QuestLookSimulation.Enabled;
            Requested?.Invoke(new Request(node,amplitude,seconds,reason,simulated));
            if(simulated)return;
            var device=InputDevices.GetDeviceAtXRNode(node);
            if(device.isValid&&device.TryGetHapticCapabilities(out var capabilities)&&capabilities.supportsImpulse)
                device.SendHapticImpulse(0,amplitude,seconds);
        }
    }
}
