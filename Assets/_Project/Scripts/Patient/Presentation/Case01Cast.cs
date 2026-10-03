using System.Collections.Generic;
using EmergencyVR.Dialogue;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>
    /// CASE 01 supporting cast (Rocketbox, MIT): a gym colleague beside Daniel who can phone 112 when asked,
    /// and two paramedics who walk in from the entrance when the simulated ambulance arrives.
    /// Presentation only: every clinical event still comes from the runtime and PhoneCallController.
    /// </summary>
    public sealed class Case01Cast : MonoBehaviour
    {
        const float WalkSpeed = 1.15f;
        static readonly Vector3 Entrance = new Vector3(0, 0, -2.75f);

        sealed class Walker
        {
            public Transform body; public CharacterAnimator motion;
            public Vector3 target; public Vector3 look; public string arrive;
        }

        ReviewCaseSession review;
        Case01PatientPresentation Body => review == null ? null : review.GetComponent<Case01PatientPresentation>();
        PhoneCallController Phone => review == null ? null : review.GetComponent<PhoneCallController>();
        CharacterAnimator colleague;
        readonly List<Walker> paramedics = new List<Walker>();
        string attemptId;

        public void Initialize(ReviewCaseSession owner) { review = owner; }

        void Update()
        {
            var body = Body;
            bool active = body != null && body.IsActive && review.Manager.IsRunning;
            string attempt = review?.Manager?.MedicalSession?.ClinicalState?.AttemptId;
            if (!active || attempt != attemptId) { Clear(); attemptId = active ? attempt : null; }
            if (!active) return;

            if (colleague == null)
            {
                colleague = Spawn("GymStaff", new Vector3(1.45f, 0, 1.25f));
                if (colleague != null) Face(colleague.transform, body.HeadPosition);
            }
            var phone = Phone;
            if (colleague != null)
                colleague.Play(phone != null && phone.Stage == PhoneCallStage.Delegated ? "f_cell_phone_talk_02" : "f_idle_breathe_01");

            if (phone != null && (phone.Stage == PhoneCallStage.Arrived || phone.Stage == PhoneCallStage.Done) && paramedics.Count == 0)
            {
                // Both kneel on Daniel's free side (+X), at hip and chest, leaving the learner's side (-X) open.
                var pelvis = body.PelvisPosition; var head = body.HeadPosition;
                var chest = Vector3.Lerp(pelvis, head, .6f);
                Arrive(new Vector3(pelvis.x + .72f, 0, pelvis.z - .25f), pelvis, Entrance + new Vector3(.3f, 0, 0));
                Arrive(new Vector3(chest.x + .72f, 0, chest.z + .2f), head, Entrance + new Vector3(-.3f, 0, -.4f));
            }
            foreach (var walker in paramedics) Step(walker);
        }

        void Arrive(Vector3 target, Vector3 look, Vector3 start)
        {
            var motion = Spawn("Paramedic", start);
            if (motion == null) return;
            paramedics.Add(new Walker { body = motion.transform, motion = motion, target = target, look = look, arrive = "m_crouch_idle" });
        }

        static void Step(Walker walker)
        {
            var to = walker.target - walker.body.position; to.y = 0;
            if (to.magnitude > .05f)
            {
                walker.motion.Play("m_walk_neutral_01", .25f);
                walker.body.rotation = Quaternion.Slerp(walker.body.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-Time.deltaTime * 6));
                walker.body.position += to.normalized * Mathf.Min(to.magnitude, WalkSpeed * Time.deltaTime);
                return;
            }
            walker.motion.Play(walker.arrive, .5f);
            Face(walker.body, walker.look, 4);
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
            colleague = null;
            foreach (var walker in paramedics) if (walker.body != null) Destroy(walker.body.gameObject);
            paramedics.Clear();
        }
    }
}
