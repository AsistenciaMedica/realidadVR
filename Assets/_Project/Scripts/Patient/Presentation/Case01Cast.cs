using System.Collections.Generic;
using EmergencyVR.Dialogue;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>
    /// CASE 01 supporting cast (Rocketbox, MIT): a companion beside Daniel who can phone 112 when asked,
    /// and two paramedics who walk in from the entrance when the simulated ambulance arrives.
    /// Presentation only: every clinical event still comes from the runtime and PhoneCallController.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed partial class Case01Cast : MonoBehaviour
    {
        const float WalkSpeed = 1.15f;
        static readonly Vector3 Entrance = new Vector3(0, 0, -2.75f);

        sealed class Walker
        {
            public Transform body; public CharacterAnimator motion;
            public Vector3 target; public Vector3 look;
            public ActorPresence presence;
            public float arrivedAt = -1;
            public int role;
            public CapsuleCollider collider;
        }

        ReviewCaseSession review;
        Case01PatientPresentation Body => review == null ? null : review.GetComponent<Case01PatientPresentation>();
        PhoneCallController Phone => review == null ? null : review.GetComponent<PhoneCallController>();
        CharacterAnimator colleague;
        ActorPresence colleaguePresence;
        readonly List<Walker> paramedics = new List<Walker>();
        string attemptId;
        float presenceTime, stageChangedAt;
        PhoneCallStage lastStage;
        AudioSource sceneVoice;
        readonly float[] speechSamples = new float[128];

        public void Initialize(ReviewCaseSession owner) { review = owner; }

        void Update()
        {
            // Undo only our previous frame's additive offsets before the Animator evaluates its new pose.
            colleaguePresence?.Restore();
            foreach (var walker in paramedics) walker.presence.Restore();
            var body = Body;
            bool active = body != null && body.IsActive && review.Manager.IsRunning;
            string attempt = review?.Manager?.MedicalSession?.ClinicalState?.AttemptId;
            if (!active || attempt != attemptId) { Clear(); attemptId = active ? attempt : null; }
            if (!active) return;
            if (!review.Manager.IsPaused) presenceTime += Time.deltaTime;

            if (colleague == null)
            {
                colleague = Spawn("GymStaff", new Vector3(1.45f, 0, 1.25f));
                if (colleague != null)
                {
                    colleague.gameObject.name = review.Selected.medical.environment == "gym" ? "Gym companion" :
                        review.Selected.medical.environment == "football" ? "Training companion" : "Passer-by companion";
                    Face(colleague.transform, body.HeadPosition);
                    colleaguePresence = new ActorPresence(colleague.transform, 1.7f);
                }
            }
            var phone = Phone;
            var stage = phone == null ? PhoneCallStage.Idle : phone.Stage;
            if (stage != lastStage) { stageChangedAt = presenceTime; lastStage = stage; }
            if (colleague != null)
            {
                bool reassuring = !body.IsAssisting && stage != PhoneCallStage.Delegated &&
                    ((stage == PhoneCallStage.EnRoute && presenceTime - stageChangedAt < 3.8f) ||
                    (stage == PhoneCallStage.Idle && Mathf.Repeat(presenceTime - 2.2f, 21.7f) < 5.4f));
                colleague.Play(stage == PhoneCallStage.Delegated ? "f_cell_phone_talk_02" :
                    reassuring ? "f_gestic_talk_neutral_01" : "f_idle_breathe_01", .85f, reassuring ? .16f : .08f);
            }

            if (phone != null && (phone.Stage == PhoneCallStage.Arrived || phone.Stage == PhoneCallStage.Done) && paramedics.Count == 0)
            {
                // Both kneel on Daniel's free side (+X), at hip and chest, leaving the learner's side (-X) open.
                var pelvis = body.PelvisPosition; var head = body.HeadPosition;
                var chest = Vector3.Lerp(pelvis, head, .6f);
                Arrive(new Vector3(pelvis.x + .72f, 0, pelvis.z - .25f), pelvis, Entrance + new Vector3(.3f, 0, 0));
                Arrive(new Vector3(chest.x + .72f, 0, chest.z + .2f), head, Entrance + new Vector3(-.3f, 0, -.4f));
            }
            foreach (var walker in paramedics) Step(walker, body, stage);
        }

        void Arrive(Vector3 target, Vector3 look, Vector3 start)
        {
            var motion = Spawn("Paramedic", start);
            if (motion == null) return;
            paramedics.Add(new Walker { body = motion.transform, motion = motion, target = target, look = look,
                role = paramedics.Count, presence = new ActorPresence(motion.transform, 3.4f + paramedics.Count * 2.9f),
                collider = motion.GetComponent<CapsuleCollider>() });
        }

        void Step(Walker walker, Case01PatientPresentation patient, PhoneCallStage stage)
        {
            var to = walker.target - walker.body.position; to.y = 0;
            if (to.magnitude > .05f)
            {
                walker.motion.Play("m_walk_neutral_01", .25f);
                walker.body.rotation = Quaternion.Slerp(walker.body.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-Time.deltaTime * 6));
                walker.body.position += to.normalized * Mathf.Min(to.magnitude, WalkSpeed * Time.deltaTime);
                return;
            }
            if (walker.arrivedAt < 0) walker.arrivedAt = presenceTime;
            // The first clinician receives the learner's handover; the second attends to Daniel.
            // Kneeling only makes sense beside a patient on the floor, not beside a seated face.
            bool receivingHandover = walker.role == 0 && stage == PhoneCallStage.Arrived;
            bool kneeling = !receivingHandover && patient.HeadPosition.y < 1.05f;
            walker.look = receivingHandover && Camera.main != null ? Camera.main.transform.position :
                walker.role == 0 ? patient.HeadPosition : Vector3.Lerp(patient.PelvisPosition, patient.HeadPosition, .7f);
            bool greeting = receivingHandover && presenceTime - walker.arrivedAt < 3.1f;
            walker.motion.Play(kneeling ? "m_crouch_idle" : greeting ? "m_gestic_talk_neutral_01" :
                walker.role == 0 ? "m_idle_breathe_01" : "m_idle_breathe_02", .7f);
            // Finish the arrival turn once; later attention is handled by head and torso, not sliding feet.
            if (presenceTime - walker.arrivedAt < 1.2f) Face(walker.body, patient.HeadPosition, 2.5f);
            if (walker.collider != null)
            {
                walker.collider.height = kneeling ? 1.2f : 1.8f;
                walker.collider.center = Vector3.up * (walker.collider.height * .5f);
            }
        }

        static void Face(Transform body, Vector3 point, float rate = 0)
        {
            var direction = point - body.position; direction.y = 0;
            if (direction.sqrMagnitude < .0001f) return;
            var rotation = Quaternion.LookRotation(direction);
            body.rotation = rate <= 0 ? rotation : Quaternion.Slerp(body.rotation, rotation, 1 - Mathf.Exp(-Time.deltaTime * rate));
        }

        CharacterAnimator Spawn(string character, Vector3 position)
        {
            var prefab = Resources.Load<GameObject>("Visual/Characters/" + character);
            if (prefab == null) return null;
            var instance = Instantiate(prefab, position, Quaternion.identity, transform);
            var collider = instance.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * .9f; collider.height = 1.8f; collider.radius = .22f;
            CharacterContactShadow.Attach(instance, instance.GetComponentInChildren<SkinnedMeshRenderer>());
            return instance.AddComponent<CharacterAnimator>();
        }

        void Clear()
        {
            if (colleague != null) Destroy(colleague.gameObject);
            colleague = null; colleaguePresence = null;
            foreach (var walker in paramedics) if (walker.body != null) Destroy(walker.body.gameObject);
            paramedics.Clear();
            presenceTime = stageChangedAt = 0; lastStage = PhoneCallStage.Idle; sceneVoice = null;
        }

        void OnDisable() { Clear(); }
    }
}
