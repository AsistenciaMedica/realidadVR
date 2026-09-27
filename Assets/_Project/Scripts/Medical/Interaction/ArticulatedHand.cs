using UnityEngine;

namespace EmergencyVR.Medical.Interaction
{
    public enum MedicalHandPose { Relaxed, Grip, Pinch, Point, Compression, Support }

    public sealed class ArticulatedHand : MonoBehaviour
    {
        public Transform[] fingerJoints;
        public Vector3[] curlAxes;
        public int[] fingers, segments;
        Quaternion[] rests;
        public MedicalHandPose Pose { get; private set; }
        public float Curl { get; private set; }
        void Awake()
        {
            rests = new Quaternion[fingerJoints.Length];
            for (int i=0;i<rests.Length;i++) rests[i]=fingerJoints[i].localRotation;
        }
        public void SetPose(MedicalHandPose pose, float amount = 1)
        {
            Pose=pose;
            Curl=Mathf.Lerp(Curl, Mathf.Clamp01(amount), 1-Mathf.Exp(-Time.deltaTime*16));
            for(int i=0;i<fingerJoints.Length;i++)
            {
                float degrees=pose switch { MedicalHandPose.Grip=>72,MedicalHandPose.Pinch=>fingers[i]<=1?43:20,
                    MedicalHandPose.Point=>fingers[i]==1?0:66,MedicalHandPose.Compression=>5,MedicalHandPose.Support=>18,_=>12 };
                if(fingers[i]==0) degrees*=.5f;
                if(segments[i]==2) degrees*=.65f;
                var target=rests[i]*Quaternion.AngleAxis(degrees*Curl,curlAxes[i]);
                fingerJoints[i].localRotation=Quaternion.Slerp(fingerJoints[i].localRotation,target,1-Mathf.Exp(-Time.deltaTime*18));
            }
        }
    }
}
