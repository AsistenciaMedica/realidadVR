using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    // Add this to PatientMale/PatientFemale prefab variants; configure anchors on the actual anatomy.
    // Bones and blendshapes are optional. A missing channel is skipped, never simulated as a real rig.
    public sealed class PatientRigAdapter : MonoBehaviour
    {
        public bool provisionalAsset;
#if VITALVR_ANIMATION
        public Animator animator;
#endif
        public Transform poseRoot, chestMotion, abdomenMotion, head, leftEye, rightEye, leftLid, rightLid, jaw;
        public Transform chestAnchor, aedRightPadAnchor, aedLeftPadAnchor, upperArmAnchor, fingerAnchor,thighAnchor,woundAnchor,chinAnchor;
        public Transform[] upperLegs=System.Array.Empty<Transform>(), lowerLegs=System.Array.Empty<Transform>(), feet=System.Array.Empty<Transform>();
        public PatientControlledRagdoll controlledRagdoll;
        public PatientContactVolume[] contactVolumes=System.Array.Empty<PatientContactVolume>();
        public Renderer[] skinRenderers=System.Array.Empty<Renderer>();
        public SkinnedMeshRenderer face;
        public string blinkLeft="eyeBlinkLeft", blinkRight="eyeBlinkRight", jawOpen="jawOpen", pain="pain", fear="fear", distress="distress";
        public Vector3 chestMotionAxis=Vector3.up;
        public Vector3 headRotationAxes=Vector3.one;
        public Color skinColor=new Color(.59f,.39f,.29f);
        public bool HasHumanoidRig {
            get {
#if VITALVR_ANIMATION
                return animator!=null && animator.avatar!=null && animator.avatar.isValid && animator.isHuman;
#else
                return false;
#endif
            }
        }
        public bool AnimationEnabled {
            get {
#if VITALVR_ANIMATION
                return animator!=null && animator.enabled;
#else
                return false;
#endif
            }
            set {
#if VITALVR_ANIMATION
                if(animator!=null)animator.enabled=value;
#endif
            }
        }
        public bool HasFacialBlendShapes => face!=null && face.sharedMesh!=null && face.sharedMesh.blendShapeCount>0;
        public bool HasContactGeometry => contactVolumes!=null && contactVolumes.Length>0;
        public bool IsPatientContact(Vector3 worldPosition,float margin=.055f)
        {
            if(contactVolumes==null)return false;
            foreach(var volume in contactVolumes)if(volume!=null && volume.Contains(worldPosition,margin))return true;
            return false;
        }

        public void BindHumanoidBones()
        {
#if VITALVR_ANIMATION
            if(!HasHumanoidRig) return;
            if(head==null) head=animator.GetBoneTransform(HumanBodyBones.Head);
            if(leftEye==null) leftEye=animator.GetBoneTransform(HumanBodyBones.LeftEye);
            if(rightEye==null) rightEye=animator.GetBoneTransform(HumanBodyBones.RightEye);
            if(jaw==null) jaw=animator.GetBoneTransform(HumanBodyBones.Jaw);
            // Chest deformation is intentionally an explicit mesh/bone binding; moving arbitrary
            // humanoid chest bones would also drag the neck and shoulders during compressions.
#endif
        }
        public void SetBlendShape(string channel,float weight)
        {
            if(!HasFacialBlendShapes || string.IsNullOrEmpty(channel)) return;
            var index=face.sharedMesh.GetBlendShapeIndex(channel);
            if(index>=0) face.SetBlendShapeWeight(index,Mathf.Clamp(weight,0,100));
        }
    }
}
