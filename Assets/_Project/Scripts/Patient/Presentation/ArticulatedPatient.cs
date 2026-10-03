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
        Mesh seatedMesh, seatedSample;
        Vector3 seatedCorrection, seatedOrigin;
        Quaternion seatedOrientation;
        bool seatedCalibrated;
        readonly RaycastHit[] seatedFloorHits = new RaycastHit[32];
        Transform leftMiddleFinger, leftIndexFinger, leftLittleFinger;
        Transform rightMiddleFinger, rightIndexFinger, rightLittleFinger;
        public bool HasImportedSkin => deformingMeshes != null && deformingMeshes.Length > 0;

        void Start()
        {
            visual = GetComponentInParent<PatientVisualController>();
            rig = GetComponent<PatientRigAdapter>();
            rootPosition = skeleton.localPosition; rootRotation = skeleton.localRotation;
            foreach (var bone in skeleton.GetComponentsInChildren<Transform>())
            {
                if(bone!=skeleton){rest[bone]=bone.localRotation;positions[bone]=bone.localPosition;}
                if (bone.name == "Bip01 L Finger2") leftMiddleFinger = bone;
                if (bone.name == "Bip01 L Finger1") leftIndexFinger = bone;
                if (bone.name == "Bip01 L Finger4") leftLittleFinger = bone;
                if (bone.name == "Bip01 R Finger2") rightMiddleFinger = bone;
                if (bone.name == "Bip01 R Finger1") rightIndexFinger = bone;
                if (bone.name == "Bip01 R Finger4") rightLittleFinger = bone;
            }
        }
        public void React(string action)
        {
            if (action == "CheckResponsiveness") response = 1;
            if (action == "EncourageCough" || action == "AssessCough") cough = 1;
        }
        public void ResetReactions(){response=0;cough=0;}
        void LateUpdate()
        {
            if (visual == null || rig == null || visual.ExternalBodyPresentation) return;
            phase += Time.deltaTime;
            response = Mathf.MoveTowards(response, 0, Time.deltaTime * .65f);
            cough = Mathf.MoveTowards(cough, 0, Time.deltaTime * .5f);
            var state = visual.VisualState;
            var posture = visual.EffectivePosture;
            foreach(var pair in rest)pair.Key.localRotation=pair.Value;
            foreach(var pair in positions)pair.Key.localPosition=pair.Value;
            bool useClip=posture==PatientPosture.Standing&&state.Consciousness!=PatientConsciousness.Unresponsive&&!state.Seizure;
            var clip=cough>0?standingCough:standingBreath;
            if(useClip&&clip!=null)CharacterAnimationLibrary.Sample(clip,skeleton.GetChild(0).gameObject,Mathf.Repeat(phase,clip.length));
            float blend = 1 - Mathf.Exp(-Time.deltaTime * 5);
            var rotation = posture == PatientPosture.Recovery ? Quaternion.Euler(0,0,65) :
                posture == PatientPosture.Seated ? Quaternion.Euler(-85,0,0) :
                posture == PatientPosture.Standing ? Quaternion.Euler(-90,0,0) : Quaternion.identity;
            if (posture != PatientPosture.Seated) seatedCalibrated = false;
            bool calibrateSeat = posture == PatientPosture.Seated && (!seatedCalibrated || seatedMesh != rig.face.sharedMesh ||
                (seatedOrigin - transform.position).sqrMagnitude > .000001f || Quaternion.Angle(seatedOrientation, transform.rotation) > .1f);
            var previousPosition = skeleton.localPosition;
            var previousRotation = skeleton.localRotation;
            var targetRotation = rotation * rootRotation;
            var targetPosition = rootPosition + Vector3.up *
                (posture == PatientPosture.Standing ? .9f : posture == PatientPosture.Seated ? .49f : posture == PatientPosture.Recovery ? .09f : 0);
            if (posture == PatientPosture.Seated && !calibrateSeat) targetPosition += seatedCorrection;
            // Measure the final pose once, then return to normal interpolation in this same frame.
            skeleton.localRotation = calibrateSeat ? targetRotation : Quaternion.Slerp(previousRotation, targetRotation, blend);
            skeleton.localPosition = calibrateSeat ? targetPosition : Vector3.Lerp(previousPosition, targetPosition, blend);

            var frame = skeleton;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                var armDirection = new Vector3(sign * .13f, -.99f, 0).normalized;
                var forearmDirection = new Vector3(sign * .03f, -.99f, .035f).normalized;
                bool responsive = state.Consciousness != PatientConsciousness.Unresponsive;
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
                if (posture == PatientPosture.Seated)
                {
                    // Keep the validated leg/sole directions while bringing the unsupported trunk upright.
                    var legs = Quaternion.Inverse(targetRotation) * Quaternion.Euler(-65, 0, 0) * rootRotation;
                    thighDirection = legs * new Vector3(0, -.45f, .9f);
                    calfDirection = legs * new Vector3(0, -.95f, -.3f);
                }
                if (posture == PatientPosture.Recovery && side == 0) { thighDirection = new Vector3(.25f,-.65f,.7f); calfDirection = new Vector3(0,-.7f,-.65f); }
                if(!useClip)
                {
                    Aim(thighs[side], calves[side], frame.TransformDirection(thighDirection + Vector3.right * seizure * .1f));
                    Aim(calves[side], ankles[side], frame.TransformDirection(calfDirection));
                }
            }
            if(cough>0 && !useClip && state.Consciousness!=PatientConsciousness.Unresponsive)
                spine.rotation=Quaternion.AngleAxis(Mathf.Sin(phase*16)*cough*4,frame.right)*spine.rotation;
            if (posture == PatientPosture.Seated && state.Consciousness != PatientConsciousness.Unresponsive && !state.Seizure &&
                visual.CompressionDepthMetres <= .001f && cough <= .01f && response <= .01f)
            {
                PoseRestingHand(0);
                PoseRestingHand(1);
                // Preserve the existing symptom gesture; only free hands rest on the thighs.
                if (state.Expression == PatientExpression.Pain || state.Expression == PatientExpression.Distress)
                    PoseSymptomHand(state.Expression == PatientExpression.Pain);
            }
            if (calibrateSeat)
            {
                CalibrateSeatedHeight();
                skeleton.localRotation = Quaternion.Slerp(previousRotation, targetRotation, blend);
                skeleton.localPosition = Vector3.Lerp(previousPosition, targetPosition + seatedCorrection, blend);
            }
            foreach (var renderer in deformingMeshes)
            {
                Set(renderer, breathingShape, visual.BreathingExcursionMetres / .011f * 100);
                Set(renderer, compressionShape, visual.CompressionDepthMetres / .08f * 100);
            }
        }
        void PoseRestingHand(int side)
        {
            var front = skeleton.TransformDirection(Vector3.forward).normalized;
            var up = skeleton.TransformDirection(Vector3.up).normalized;
            var lateral = skeleton.TransformDirection(side == 0 ? Vector3.left : Vector3.right).normalized;
            var target = Vector3.Lerp(thighs[side].position, calves[side].position, .65f) + up * .095f;
            var pole = upperArms[side].position + lateral * .14f - up * .28f + front * .18f;
            SolveTwoBone(upperArms[side], forearms[side], hands[side], target, pole);
            var middle = side == 0 ? leftMiddleFinger : rightMiddleFinger;
            var index = side == 0 ? leftIndexFinger : rightIndexFinger;
            var little = side == 0 ? leftLittleFinger : rightLittleFinger;
            if (middle == null || index == null || little == null) return;
            var fingers = middle.position - hands[side].position;
            var normal = Vector3.Cross(index.position - hands[side].position, little.position - hands[side].position);
            if (fingers.sqrMagnitude < .00001f || normal.sqrMagnitude < .00000001f) return;
            var current = Quaternion.LookRotation(fingers, normal);
            var desired = Quaternion.LookRotation((calves[side].position - thighs[side].position).normalized, -up);
            hands[side].rotation = desired * Quaternion.Inverse(current) * hands[side].rotation;
        }
        void PoseSymptomHand(bool chestDiscomfort)
        {
            if (rig.chestAnchor == null || hands == null || hands.Length < 1) return;
            var front = skeleton.TransformDirection(Vector3.forward).normalized;
            var up = skeleton.TransformDirection(Vector3.up).normalized;
            var left = skeleton.TransformDirection(Vector3.left).normalized;
            // Keep the wrist in front of the torso, with a low lateral elbow instead of crossed fixed directions.
            var target = rig.chestAnchor.position + left * .08f - up * (chestDiscomfort ? .085f : .15f) + front * .075f;
            var pole = upperArms[0].position + left * .32f - up * .28f + front * .22f;
            SolveTwoBone(upperArms[0], forearms[0], hands[0], target, pole);
            if (leftMiddleFinger == null || leftIndexFinger == null || leftLittleFinger == null) return;
            var fingers = leftMiddleFinger.position - hands[0].position;
            var palmNormal = Vector3.Cross(leftIndexFinger.position - hands[0].position, leftLittleFinger.position - hands[0].position);
            if (fingers.sqrMagnitude < .00001f || palmNormal.sqrMagnitude < .00000001f) return;
            var current = Quaternion.LookRotation(fingers, palmNormal);
            var desired = Quaternion.LookRotation((up - left * .45f).normalized, -front);
            hands[0].rotation = desired * Quaternion.Inverse(current) * hands[0].rotation;
        }
        // Analytic two-bone solve, using the same geometric construction as the authored Case01 pose.
        static void SolveTwoBone(Transform upper, Transform middle, Transform end, Vector3 target, Vector3 pole)
        {
            float a = Vector3.Distance(upper.position, middle.position), b = Vector3.Distance(middle.position, end.position);
            var delta = target - upper.position;
            if (delta.sqrMagnitude < .00001f || a < .001f || b < .001f) return;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            var direction = delta.normalized;
            var bend = Vector3.ProjectOnPlane(pole - upper.position, direction).normalized;
            if (bend.sqrMagnitude < .001f) bend = Vector3.Cross(direction, Vector3.right).normalized;
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            var joint = upper.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(middle.position - upper.position, joint - upper.position) * upper.rotation;
            middle.rotation = Quaternion.FromToRotation(end.position - middle.position, upper.position + direction * distance - middle.position) * middle.rotation;
        }
        void CalibrateSeatedHeight()
        {
            seatedCalibrated = true; seatedMesh = rig.face.sharedMesh;
            seatedOrigin = transform.position; seatedOrientation = transform.rotation; seatedCorrection = Vector3.zero;
            if (seatedMesh == null || ankles == null || ankles.Length < 2 || ankles[0] == null || ankles[1] == null) return;
            Physics.SyncTransforms();
            int count = Physics.RaycastNonAlloc(pelvis.position, Vector3.down, seatedFloorHits, 2f, ~0, QueryTriggerInteraction.Ignore);
            if (count == seatedFloorHits.Length) return;
            float floor = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = seatedFloorHits[i];
                if (hit.normal.y < .95f || hit.collider.transform.IsChildOf(visual.transform) || hit.point.y >= pelvis.position.y - .3f) continue;
                floor = Mathf.Max(floor, hit.point.y);
            }
            if (float.IsNegativeInfinity(floor)) return;
            if (seatedSample == null) seatedSample = new Mesh { name = "Seated shoe height calibration" };
            rig.face.BakeMesh(seatedSample);
            var vertices = seatedSample.vertices;
            var weights = seatedMesh.boneWeights;
            if (weights.Length != vertices.Length) return;
            var footBones = new bool[rig.face.bones.Length];
            for (int i = 0; i < footBones.Length; i++)
            {
                var bone = rig.face.bones[i];
                footBones[i] = bone != null && (bone == ankles[0] || bone.IsChildOf(ankles[0]) || bone == ankles[1] || bone.IsChildOf(ankles[1]));
            }
            float sole = float.PositiveInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                var weight = weights[i];
                float influence = (footBones[weight.boneIndex0] ? weight.weight0 : 0) + (footBones[weight.boneIndex1] ? weight.weight1 : 0) +
                    (footBones[weight.boneIndex2] ? weight.weight2 : 0) + (footBones[weight.boneIndex3] ? weight.weight3 : 0);
                if (influence > .35f) sole = Mathf.Min(sole, rig.face.transform.TransformPoint(vertices[i]).y);
            }
            if (!float.IsPositiveInfinity(sole))
                seatedCorrection = skeleton.parent.InverseTransformVector(Vector3.up * (floor + .005f - sole));
        }
        void OnDestroy() { if (seatedSample != null) Destroy(seatedSample); }
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
