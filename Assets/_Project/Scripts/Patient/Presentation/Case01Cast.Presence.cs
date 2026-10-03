using System.Collections.Generic;
using EmergencyVR.Dialogue;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    public sealed partial class Case01Cast
    {
        void LateUpdate()
        {
            var patient = Body;
            if (patient == null || !patient.IsActive || colleaguePresence == null) return;
            var viewer = Camera.main;
            var stage = Phone == null ? PhoneCallStage.Idle : Phone.Stage;
            if (sceneVoice == null)
            {
                var anchor = transform.Find("VITAL VR scene voice");
                if (anchor != null) sceneVoice = anchor.GetComponent<AudioSource>();
            }
            string speaking = sceneVoice != null && sceneVoice.isPlaying && sceneVoice.clip != null ? sceneVoice.clip.name : "";
            float mouth = SupportingActor.SpeechWeight(sceneVoice, speechSamples) / 42f;
            bool companionSpeaking = speaking.StartsWith("CO_");
            bool clinicianSpeaking = speaking.StartsWith("PA_");
            // An attentive companion checks the learner, then returns to Daniel. The head leads;
            // the feet and the actor's world position stay fixed throughout the interaction.
            float cycle = Mathf.Repeat(presenceTime + 1.7f, 11.3f);
            Vector3 focus = patient.HeadPosition;
            if (viewer != null && !patient.IsAssisting && stage != PhoneCallStage.Delegated &&
                (companionSpeaking || cycle > 7.6f)) focus = viewer.transform.position;
            if (stage == PhoneCallStage.EnRoute && Mathf.Repeat(presenceTime, 23) > 21.4f)
                focus = Entrance + Vector3.up * 1.6f;
            float concern = patient.IsAssisting ? .8f : stage == PhoneCallStage.Done ? .18f : .5f;
            colleaguePresence.Present(focus, presenceTime, concern, companionSpeaking ? mouth : 0,
                stage == PhoneCallStage.Delegated ? 0 : .8f, review.Manager.IsPaused);
            if (companionSpeaking) sceneVoice.transform.position = colleaguePresence.HeadPosition;

            foreach (var walker in paramedics)
            {
                bool settled = walker.arrivedAt >= 0;
                Vector3 attention = patient.HeadPosition;
                bool listening = walker.role == 0 && stage == PhoneCallStage.Arrived;
                if (viewer != null && (listening || settled && walker.role == 0 &&
                    Mathf.Repeat(presenceTime + 2, 13) > 10.7f)) attention = viewer.transform.position;
                if (!settled) attention = walker.target + Vector3.up * 1.35f;
                walker.presence.Present(attention, presenceTime, .2f, clinicianSpeaking && walker.role == 0 ? mouth : 0,
                    settled ? listening ? 1 : .35f : 0, review.Manager.IsPaused);
                if (clinicianSpeaking && walker.role == 0) sceneVoice.transform.position = walker.presence.HeadPosition;
            }
        }

        // These imported cast meshes have facial bones, not the patient's generated blendshapes.
        // Capture after mocap and restore before its next evaluation, including bones without curves.
        sealed class ActorPresence
        {
            readonly Transform root, head, chest, leftEye, rightEye, leftTop, leftBottom, rightTop, rightBottom;
            readonly Transform leftBrow, rightBrow, jaw;
            readonly List<Transform> bones = new List<Transform>();
            readonly List<Pose> baseline = new List<Pose>();
            readonly float seed;
            float yaw, pitch;
            bool applied;
            public Vector3 HeadPosition => head == null ? root.position + Vector3.up * 1.6f : head.position;

            public ActorPresence(Transform root, float seed)
            {
                this.root = root; this.seed = seed;
                var hierarchy = root.GetComponentsInChildren<Transform>(true);
                Transform Bone(string name)
                {
                    foreach (var candidate in hierarchy) if (candidate.name == "Bip01 " + name)
                    { bones.Add(candidate); baseline.Add(default); return candidate; }
                    return null;
                }
                head = Bone("Head"); chest = Bone("Spine2");
                leftEye = Bone("LEye"); rightEye = Bone("REye");
                leftTop = Bone("LEyeBlinkTop"); leftBottom = Bone("LEyeBlinkBottom");
                rightTop = Bone("REyeBlinkTop"); rightBottom = Bone("REyeBlinkBottom");
                leftBrow = Bone("LInnerEyebrow"); rightBrow = Bone("RInnerEyebrow"); jaw = Bone("MJaw");
            }

            public void Restore()
            {
                if (!applied) return;
                for (int i = 0; i < bones.Count; i++) if (bones[i] != null)
                { bones[i].localPosition = baseline[i].position; bones[i].localRotation = baseline[i].rotation; }
                applied = false;
            }

            public void Present(Vector3 target, float time, float concern, float speech, float listening, bool paused)
            {
                if (head == null) return;
                for (int i = 0; i < bones.Count; i++) baseline[i] = new Pose(bones[i].localPosition, bones[i].localRotation);
                applied = true;
                var direction = root.InverseTransformDirection(target - head.position);
                float desiredYaw = Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -52, 52);
                float desiredPitch = Mathf.Clamp(Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg, -27, 18);
                float blend = paused ? 0 : 1 - Mathf.Exp(-Time.deltaTime * 5);
                yaw = Mathf.LerpAngle(yaw, desiredYaw, blend); pitch = Mathf.Lerp(pitch, desiredPitch, blend);
                float t = time + seed;
                float nodPhase = Mathf.Repeat(t, 9.7f);
                float nod = nodPhase < 1.4f ? Mathf.Sin(nodPhase / 1.4f * Mathf.PI * 2) * 3.5f * listening : 0;
                if (chest != null)
                {
                    chest.rotation = Quaternion.AngleAxis(Mathf.Sin(t * .72f) * .65f + concern * 1.3f, root.right) * chest.rotation;
                    chest.rotation = Quaternion.AngleAxis(yaw * .12f, root.up) * chest.rotation;
                }
                var headTurn = Quaternion.AngleAxis(yaw * .78f, root.up) * Quaternion.AngleAxis(-pitch + nod, root.right);
                head.rotation = headTurn * head.rotation;
                var eyeTurn = Quaternion.AngleAxis(Mathf.Clamp(desiredYaw - yaw * .9f, -10, 10), root.up);
                if (leftEye != null) leftEye.rotation = eyeTurn * leftEye.rotation;
                if (rightEye != null) rightEye.rotation = eyeTurn * rightEye.rotation;
                float blinkTime = Mathf.Repeat(t, 3.8f + seed * .13f);
                float blink = blinkTime < .17f ? Mathf.Sin(blinkTime / .17f * Mathf.PI) : 0;
                CloseLids(leftTop, leftBottom, blink); CloseLids(rightTop, rightBottom, blink);
                if (leftBrow != null) leftBrow.position += root.up * (.003f * concern);
                if (rightBrow != null) rightBrow.position += root.up * (.0025f * concern);
                if (jaw != null && speech > 0)
                {
                    jaw.rotation = Quaternion.AngleAxis(speech * 7, root.right) * jaw.rotation;
                    jaw.position -= root.up * (.002f * speech);
                }
            }

            static void CloseLids(Transform top, Transform bottom, float weight)
            {
                if (top == null || bottom == null || weight <= 0) return;
                var a = top.position; var b = bottom.position;
                top.position = Vector3.Lerp(a, b, weight * .78f);
                bottom.position = Vector3.Lerp(b, a, weight * .22f);
            }
        }
    }
}
