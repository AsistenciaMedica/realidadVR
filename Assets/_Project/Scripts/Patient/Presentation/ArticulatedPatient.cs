using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    // Drives the imported skin and skeleton from the existing medical presentation state.
    [DefaultExecutionOrder(90)]
    public sealed class ArticulatedPatient : MonoBehaviour
    {
        public Transform skeleton, pelvis, spine, neck;
        public Transform[] upperArms, forearms, hands, thighs, calves, ankles;
        public SkinnedMeshRenderer[] deformingMeshes;
        public string breathingShape = "VitalBreath", compressionShape = "VitalCompression";
        public AnimationClip standingBreath, standingCough;
        PatientVisualController visual;
        PatientRigAdapter rig;
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        readonly Dictionary<Transform, Vector3> positions = new Dictionary<Transform, Vector3>();
        Vector3 rootPosition;
        Quaternion rootRotation;
        float phase, response, cough;
        public bool HasImportedSkin => deformingMeshes != null && deformingMeshes.Length > 0;

        void Start()
        {
            visual = GetComponentInParent<PatientVisualController>();
            rig = GetComponent<PatientRigAdapter>();
            rootPosition = skeleton.localPosition; rootRotation = skeleton.localRotation;
            foreach (var bone in skeleton.GetComponentsInChildren<Transform>())
                if(bone!=skeleton){rest[bone]=bone.localRotation;positions[bone]=bone.localPosition;}
        }
        public void React(string action)
        {
            if (action == "CheckResponsiveness") response = 1;
            if (action == "EncourageCough" || action == "AssessCough") cough = 1;
        }
        public void ResetReactions(){response=0;cough=0;}
        void LateUpdate()
        {
            if (visual == null || rig == null) return;
            phase += Time.deltaTime;
            response = Mathf.MoveTowards(response, 0, Time.deltaTime * .65f);
            cough = Mathf.MoveTowards(cough, 0, Time.deltaTime * .5f);
            var state = visual.VisualState;
            var posture = visual.EffectivePosture;
            foreach(var pair in rest)pair.Key.localRotation=pair.Value;
            foreach(var pair in positions)pair.Key.localPosition=pair.Value;
            bool useClip=posture==PatientPosture.Standing&&state.Consciousness!=PatientConsciousness.Unresponsive&&!state.Seizure;
            var clip=cough>0?standingCough:standingBreath;
            if(useClip&&clip!=null)clip.SampleAnimation(skeleton.GetChild(0).gameObject,Mathf.Repeat(phase,clip.length));
            float blend = 1 - Mathf.Exp(-Time.deltaTime * 5);
            var rotation = posture == PatientPosture.Recovery ? Quaternion.Euler(0,0,65) :
                posture == PatientPosture.Seated ? Quaternion.Euler(-65,0,0) :
                posture == PatientPosture.Standing ? Quaternion.Euler(-90,0,0) : Quaternion.identity;
            skeleton.localRotation = Quaternion.Slerp(skeleton.localRotation, rotation * rootRotation, blend);
            skeleton.localPosition = Vector3.Lerp(skeleton.localPosition, rootPosition + Vector3.up *
                (posture == PatientPosture.Standing ? .9f : posture == PatientPosture.Seated ? .49f : posture == PatientPosture.Recovery ? .09f : 0), blend);

            var frame = skeleton;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                var armDirection = new Vector3(sign * .13f, -.99f, 0).normalized;
                var forearmDirection = new Vector3(sign * .03f, -.99f, .035f).normalized;
                bool responsive = state.Consciousness != PatientConsciousness.Unresponsive;
                if (responsive && (state.Expression == PatientExpression.Pain || state.Expression == PatientExpression.Distress) && side == 0)
                { armDirection = new Vector3(.42f,-.8f,.28f); forearmDirection = new Vector3(-.8f,.4f,.36f); }
                if (responsive && response > 0) forearmDirection += new Vector3(0,.3f,.65f) * Mathf.Sin(response * Mathf.PI);
                if (responsive && cough > 0 && !useClip)
                {
                    armDirection = new Vector3(sign*.24f,-.75f,.45f);
                    forearmDirection = new Vector3(-sign*.38f,.65f,.25f);
                }
                float seizure = state.Seizure ? Mathf.Sin(phase * 17 + side * 2) : 0;
                if(!useClip)
                {
                    Aim(upperArms[side], forearms[side], frame.TransformDirection(armDirection + Vector3.forward * seizure * .25f));
                    Aim(forearms[side], hands[side], frame.TransformDirection(forearmDirection + Vector3.right * seizure * .22f));
                }
                Vector3 thighDirection = Vector3.down, calfDirection = Vector3.down;
                if (posture == PatientPosture.Seated) { thighDirection = new Vector3(0,-.45f,.9f); calfDirection = new Vector3(0,-.95f,-.3f); }
                if (posture == PatientPosture.Recovery && side == 0) { thighDirection = new Vector3(.25f,-.65f,.7f); calfDirection = new Vector3(0,-.7f,-.65f); }
                if(!useClip)
                {
                    Aim(thighs[side], calves[side], frame.TransformDirection(thighDirection + Vector3.right * seizure * .1f));
                    Aim(calves[side], ankles[side], frame.TransformDirection(calfDirection));
                }
            }
            if(cough>0 && !useClip && state.Consciousness!=PatientConsciousness.Unresponsive)
                spine.rotation=Quaternion.AngleAxis(Mathf.Sin(phase*16)*cough*4,frame.right)*spine.rotation;
            foreach (var renderer in deformingMeshes)
            {
                Set(renderer, breathingShape, visual.BreathingExcursionMetres / .011f * 100);
                Set(renderer, compressionShape, visual.CompressionDepthMetres / .08f * 100);
            }
        }
        static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null || direction.sqrMagnitude < .00001f) return;
            bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction.normalized) * bone.rotation;
        }
        static void Set(SkinnedMeshRenderer renderer, string shape, float value)
        {
            if (renderer == null) return;
            int index = renderer.sharedMesh.GetBlendShapeIndex(shape);
            if (index >= 0) renderer.SetBlendShapeWeight(index, Mathf.Clamp(value,0,100));
        }
    }
}
