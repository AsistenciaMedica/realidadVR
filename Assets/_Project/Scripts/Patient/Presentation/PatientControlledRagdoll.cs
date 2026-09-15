using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    public enum PatientRagdollPhase { Animation, Falling, Settled, BlendingToPose }

    // Optional replacement-asset adapter. Never creates loose physics limbs for the procedural proxy.
    // Inspector bodies must belong to one authored, connected ragdoll with convex/simple colliders.
    public sealed class PatientControlledRagdoll : MonoBehaviour
    {
        public PatientRigAdapter rig;
        public Rigidbody[] bodies=System.Array.Empty<Rigidbody>();
        public Joint[] joints=System.Array.Empty<Joint>();
        [Min(.1f)] public float maximumSpeed=3,maximumAngularSpeed=6,maximumFallSeconds=5;
        [Min(.1f)] public float settleDuration=.45f;
        public PatientRagdollPhase Phase {get;private set;}
        public bool OwnsPose => Phase!=PatientRagdollPhase.Animation;
        float elapsed,stillTime,blendDuration;
        bool animatorWasEnabled;
        BodyState[] original;
        Vector3[] blendFromPositions;
        Quaternion[] blendFromRotations;
        struct BodyState
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
            public bool kinematic,gravity;
            public CollisionDetectionMode collision;
            public RigidbodyInterpolation interpolation;
            public float maximumAngularVelocity;
        }
        public bool IsConfigured
        {
            get
            {
                if(rig==null || rig.provisionalAsset || bodies==null || bodies.Length<3 || joints==null || joints.Length<bodies.Length-1) return false;
                var connected=new HashSet<Rigidbody>();
                foreach(var body in bodies)
                {
                    if(body==null || !body.transform.IsChildOf(rig.transform) || !connected.Add(body)) return false;
                    var colliders=body.GetComponents<Collider>();
                    if(colliders.Length==0) return false;
                    foreach(var collider in colliders)
                        if(!collider.enabled || collider.isTrigger || collider is MeshCollider mesh && !mesh.convex) return false;
                }
                foreach(var joint in joints)
                    if(joint==null || !connected.Contains(joint.GetComponent<Rigidbody>()) || !connected.Contains(joint.connectedBody)) return false;
                var reached=new HashSet<Rigidbody>{bodies[0]};
                for(int pass=0;pass<bodies.Length;pass++) foreach(var joint in joints)
                {
                    var a=joint.GetComponent<Rigidbody>();var b=joint.connectedBody;
                    if(reached.Contains(a)) reached.Add(b);if(reached.Contains(b)) reached.Add(a);
                }
                return reached.Count==bodies.Length;
            }
        }
        public bool BeginFall()
        {
            if(Phase!=PatientRagdollPhase.Animation || !IsConfigured) return false;
            original=new BodyState[bodies.Length];blendFromPositions=new Vector3[bodies.Length];blendFromRotations=new Quaternion[bodies.Length];
            animatorWasEnabled=rig.AnimationEnabled;rig.AnimationEnabled=false;
            for(int i=0;i<bodies.Length;i++)
            {
                var body=bodies[i];original[i]=new BodyState { localPosition=body.transform.localPosition,localRotation=body.transform.localRotation,
                    kinematic=body.isKinematic,gravity=body.useGravity,collision=body.collisionDetectionMode,interpolation=body.interpolation,maximumAngularVelocity=body.maxAngularVelocity };
                body.isKinematic=false;body.useGravity=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                body.interpolation=RigidbodyInterpolation.Interpolate;body.maxAngularVelocity=Mathf.Max(.1f,maximumAngularSpeed);
                body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            }
            elapsed=0;stillTime=0;Phase=PatientRagdollPhase.Falling;return true;
        }
        void FixedUpdate()
        {
            if(Phase!=PatientRagdollPhase.Falling) return;
            elapsed+=Time.fixedDeltaTime;bool still=true;
            foreach(var body in bodies)
            {
                if(body==null) { StopAtCurrentPose();return; }
                body.linearVelocity=Vector3.ClampMagnitude(body.linearVelocity,Mathf.Max(.1f,maximumSpeed));
                body.angularVelocity=Vector3.ClampMagnitude(body.angularVelocity,Mathf.Max(.1f,maximumAngularSpeed));
                if(body.linearVelocity.sqrMagnitude>.0064f || body.angularVelocity.sqrMagnitude>.04f) still=false;
            }
            stillTime=still?stillTime+Time.fixedDeltaTime:0;
            if(stillTime>=settleDuration || elapsed>=maximumFallSeconds) StopAtCurrentPose();
        }
        public void StopAtCurrentPose()
        {
            if(original==null) return;
            foreach(var body in bodies) if(body!=null)
            {
                if(!body.isKinematic) {body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
                body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;
            }
            Phase=PatientRagdollPhase.Settled;
        }
        // Explicit presentation command only; no clinical recovery is inferred from settling.
        public bool ReturnToAuthoredPose(float duration=1)
        {
            if(original==null || Phase==PatientRagdollPhase.Animation) return false;
            StopAtCurrentPose();
            for(int i=0;i<bodies.Length;i++)
            {
                if(bodies[i]==null) return false;
                blendFromPositions[i]=bodies[i].transform.localPosition;blendFromRotations[i]=bodies[i].transform.localRotation;
            }
            elapsed=0;blendDuration=Mathf.Max(.1f,duration);Phase=PatientRagdollPhase.BlendingToPose;return true;
        }
        void LateUpdate()
        {
            if(Phase!=PatientRagdollPhase.BlendingToPose)return;
            elapsed+=Time.deltaTime;float t=Mathf.SmoothStep(0,1,elapsed/blendDuration);
            for(int i=0;i<bodies.Length;i++) if(bodies[i]!=null)
            {
                bodies[i].transform.localPosition=Vector3.Lerp(blendFromPositions[i],original[i].localPosition,t);
                bodies[i].transform.localRotation=Quaternion.Slerp(blendFromRotations[i],original[i].localRotation,t);
            }
            if(elapsed>=blendDuration)Restore();
        }
        void Restore()
        {
            if(original==null)return;
            for(int i=0;i<bodies.Length && i<original.Length;i++) if(bodies[i]!=null)
            {
                var body=bodies[i];body.isKinematic=original[i].kinematic;body.useGravity=original[i].gravity;
                body.collisionDetectionMode=original[i].collision;body.interpolation=original[i].interpolation;body.maxAngularVelocity=original[i].maximumAngularVelocity;
            }
            if(rig!=null)rig.AnimationEnabled=animatorWasEnabled;
            original=null;Phase=PatientRagdollPhase.Animation;
        }
        void OnDisable() { if(Phase==PatientRagdollPhase.Falling)StopAtCurrentPose();Restore(); }
    }
}
