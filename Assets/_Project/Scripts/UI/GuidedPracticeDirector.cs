using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.UI
{
    /// <summary>
    /// Guided practice for clinical v2 cases: one explicit step at a time, a world-space arrow and floor path
    /// to where the learner must go, and the UI control to use. Steps complete only from evidence recorded by
    /// the clinical runtime, never from the guide itself. Evaluation mode shows nothing.
    /// </summary>
    public sealed class GuidedPracticeDirector : MonoBehaviour
    {
        public enum Target { None, Patient, PatientChest, Phone, TransitionArea }

        public sealed class Step
        {
            public string Id, Title, Detail;
            public Target Target;
            public string[] Controls;
        }

        const float ArrivalRadius = 1.35f;
        const int Chevrons = 14;
        const float ChevronSpacing = .45f;

        ReviewCaseSession review;
        Camera viewer;
        Transform root, arrow, ring;
        readonly List<Transform> chevrons = new List<Transform>();
        Material glow;
        Mesh arrowMesh, chevronMesh, ringMesh;
        bool approached;
        string attemptId;

        public Step Current { get; private set; }
        public int StepNumber { get; private set; }
        public int StepCount { get; private set; }
        public bool Active => Current != null;

        public void Initialize(ReviewCaseSession session, Camera camera)
        {
            review = session; viewer = camera;
            BuildVisuals();
        }

        Case01PatientPresentation Body => review == null ? null : review.GetComponent<Case01PatientPresentation>();
        ClinicalHelpController Help => review == null ? null : review.GetComponent<ClinicalHelpController>();

        void Update()
        {
            Current = Evaluate();
            Vector3 point = default;
            bool show = Current != null && Current.Target != Target.None && TryTarget(Current.Target, out point);
            // Beside the patient the arrow would cover his face; the floor ring alone marks the spot.
            bool near = show && viewer != null && Flat(viewer.transform.position - point) < ArrivalRadius &&
                (Current.Target == Target.Patient || Current.Target == Target.PatientChest);
            root.gameObject.SetActive(show);
            if (show) { Present(); arrow.gameObject.SetActive(!near); }
        }

        /// <summary>Returns the first incomplete step in guided practice, or null when guidance must stay hidden.</summary>
        Step Evaluate()
        {
            var manager = review == null ? null : review.Manager;
            var runtime = manager == null ? null : manager.MedicalSession;
            if (runtime == null || !manager.IsRunning || manager.IsPaused || !review.Procedures.TrainingMode ||
                !runtime.Capabilities.usesObservedPatientData || Body == null || !Body.IsActive)
            { StepCount = 0; return null; }
            var clinical = runtime.ClinicalState;
            if (clinical == null) return null;
            if (attemptId != clinical.AttemptId) { attemptId = clinical.AttemptId; approached = false; }

            var observations = runtime.Observations;
            var interviews = observations.Interviews;
            var physical = observations.Physical;
            bool Asked(DialogueIntent intent) => interviews.Any(x => x.intent == intent);
            bool Observed(string type) => physical.Any(x => x.type == type && x.valid);
            bool Achieved(string objective) => runtime.ObjectiveProgress.Any(x => x.objectiveId == objective &&
                (x.result == ObjectiveResult.AchievedIndependently || x.result == ObjectiveResult.AchievedWithGuidance));
            bool lying = clinical.EffectivePhysicalPosition == PhysicalPosition.Supine || Achieved("OBJ_02");
            if (!approached && TryTarget(Target.Patient, out var patientPoint) && viewer != null && Flat(viewer.transform.position - patientPoint) < ArrivalRadius)
                approached = true;
            var help = Help;
            var helpStage = help == null ? ClinicalHelpStage.Idle : help.Status;
            var phone = review.GetComponent<PhoneCallController>();
            var phoneStage = phone == null ? (helpStage == ClinicalHelpStage.Idle ? PhoneCallStage.Idle : PhoneCallStage.Talking) : phone.Stage;

            var steps = new List<(Step step, bool done)>
            {
                (new Step { Id = "approach", Title = "Acércate a Daniel", Target = Target.Patient,
                    Detail = "Sigue las flechas del suelo. Comprueba que el espacio alrededor es seguro: la cinta está parada y no hay pesas en el paso." },
                    approached || interviews.Count > 0 || physical.Count > 0),
                (new Step { Id = "greet", Title = "Preséntate y habla con él", Target = Target.Patient,
                    Detail = "Dile quién eres y que vas a ayudarle. Mírale y pulsa E (o mantén M y dilo en voz alta: «Hola, vengo a ayudarte»).",
                    Controls = new[] { "Intent GREETING", "Previous dialogue", "Open dialogue" } },
                    Asked(DialogueIntent.GREETING)),
                (new Step { Id = "responsive", Title = "Comprueba si responde", Target = Target.Patient,
                    Detail = "Una persona que contesta con coherencia está consciente. Mírale y pulsa G: apoyas una mano en su hombro y le hablas.",
                    Controls = new[] { "Action AssessResponsiveness", "Open actions" } },
                    Observed("PatientResponsive")),
                (new Step { Id = "breathing", Title = "Observa cómo respira", Target = Target.PatientChest,
                    Detail = "Mira el pecho y el abdomen, sin tocarle: ritmo regular, sin esfuerzo. Pulsa B mientras le miras.",
                    Controls = new[] { "Action ObserveBreathing", "Open actions" } },
                    Observed("BreathingNormal")),
                (new Step { Id = "history", Title = "Pregunta qué le pasa y cuándo empezó", Target = Target.Patient,
                    Detail = "Saber si empezó durante o después del ejercicio cambia el riesgo. Pulsa E y pregunta «¿Qué te pasa?» y «¿Cuándo empezó?» (o dilo con M).",
                    Controls = new[] { Asked(DialogueIntent.MAIN_SYMPTOM) ? "Intent ONSET" : "Intent MAIN_SYMPTOM", "Previous dialogue", "Open dialogue" } },
                    Asked(DialogueIntent.MAIN_SYMPTOM) && Asked(DialogueIntent.ONSET)),
                (new Step { Id = "red-flags", Title = "Busca señales de alarma", Target = Target.Patient,
                    Detail = "Pregunta si ha perdido el conocimiento, si le duele el pecho y si le falta el aire. Cualquier «sí» obliga a pedir ayuda ya.",
                    Controls = new[] { RedFlagControl(Asked), "Next dialogue", "Open dialogue" } },
                    Asked(DialogueIntent.LOSS_OF_CONSCIOUSNESS) && Asked(DialogueIntent.CHEST_PAIN) && Asked(DialogueIntent.BREATHING_DIFFICULTY)),
                (new Step { Id = "call", Title = "Llama al 112 desde tu móvil", Target = Target.Patient,
                    Detail = "No hace falta medir nada antes de pedir ayuda, y llamar desde tu móvil te permite quedarte con él. Pulsa T y marca 1-1-2.",
                    Controls = phone?.GuideControls() ?? new[] { "Open help conversation" } },
                    phoneStage != PhoneCallStage.Idle),
                (new Step { Id = "call-talk", Title = "Responde a la operadora del 112", Target = Target.Patient,
                    Detail = CallDetail(phone),
                    Controls = phone?.GuideControls() ?? new[] { "Open help conversation" } },
                    helpStage == ClinicalHelpStage.Confirmed || helpStage == ClinicalHelpStage.HandoverCompleted),
                (new Step { Id = "consent", Title = "Pide permiso para ayudarle", Target = Target.Patient,
                    Detail = "Explícale que vas a ayudarle a tumbarse para que no se caiga. Pulsa E y pregunta «¿Puedo ayudarte?».",
                    Controls = new[] { "Intent CONSENT_HELP", "Next dialogue", "Open dialogue" } },
                    Asked(DialogueIntent.CONSENT_HELP) || lying),
                (new Step { Id = "lie-down", Title = "Ayúdale a tumbarse en el suelo", Target = Target.TransitionArea,
                    Detail = "Colócate a su lado (sigue la flecha) y mantén pulsada la tecla F hasta el 100 %. Bájale despacio, sin tirar de los brazos; si sueltas F, el movimiento se detiene.",
                    Controls = new[] { "Action AssistPatient" } },
                    lying),
                (new Step { Id = "reassess", Title = "Vuelve a valorarle", Target = Target.Patient,
                    Detail = "Pregunta «¿Cómo te encuentras ahora?» (E) y reevalúa con H. Compara con cómo estaba al principio.",
                    Controls = new[] { Asked(DialogueIntent.CURRENT_STATUS) ? "Action ReassessPatient" : "Intent CURRENT_STATUS",
                        Asked(DialogueIntent.CURRENT_STATUS) ? "Open actions" : "Next dialogue", "Open dialogue" } },
                    Achieved("OBJ_04")),
                (new Step { Id = "handover", Title = phoneStage == PhoneCallStage.EnRoute ? "Espera a la ambulancia junto a Daniel" : "Entrega el relevo al equipo de emergencias",
                    Target = Target.Patient,
                    Detail = phoneStage == PhoneCallStage.EnRoute ? "Llega en " + Mathf.CeilToInt((float)phone.SecondsToArrival) + " s. No le dejes solo y vigila si empeora." :
                        "Resume cuándo empezó, cómo ha evolucionado y lo que has hecho. Solo datos que hayas comprobado.",
                    Controls = phone?.GuideControls() ?? new[] { "Communicate handover", "Open help conversation" } },
                    helpStage == ClinicalHelpStage.HandoverCompleted),
            };
            StepCount = steps.Count;
            // Safety interrupts the sequence: a rise attempt must be stopped before anything else.
            if (Body.CanPreventRise)
            {
                StepNumber = steps.FindIndex(x => !x.done) + 1;
                return new Step { Id = "prevent-rise", Title = "¡Daniel intenta levantarse!", Target = Target.Patient,
                    Detail = "Levantarse puede hacer que vuelva el mareo y se caiga. Pídele que espere y mantenga el apoyo.",
                    Controls = new[] { "Prevent rise attempt" } };
            }
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].done) continue;
                StepNumber = i + 1;
                return steps[i].step;
            }
            StepNumber = steps.Count;
            return new Step { Id = "finish", Title = "Práctica completada", Target = Target.None,
                Detail = "Has recorrido todos los pasos. Finaliza para revisar tus decisiones y tiempos.",
                Controls = new[] { "Review completed practice", "Finish training" } };
        }

        static string CallDetail(PhoneCallController phone)
        {
            if (phone == null || phone.Current == null) return "Espera a que contesten y responde con lo que has comprobado.";
            if (phone.Current.Prerequisite?.Invoke() != null) return "Aún no lo has comprobado. Hazlo ahora sin colgar y después responde.";
            return "Contesta solo con información que hayas comprobado. La respuesta recomendada está iluminada.";
        }

        static string RedFlagControl(System.Func<DialogueIntent, bool> asked)
        {
            if (!asked(DialogueIntent.LOSS_OF_CONSCIOUSNESS)) return "Intent LOSS_OF_CONSCIOUSNESS";
            if (!asked(DialogueIntent.CHEST_PAIN)) return "Intent CHEST_PAIN";
            return "Intent BREATHING_DIFFICULTY";
        }

        bool TryTarget(Target target, out Vector3 point)
        {
            point = default;
            var body = Body;
            if (body == null || !body.IsActive) return false;
            switch (target)
            {
                case Target.Patient: point = body.HeadPosition; return true;
                case Target.PatientChest: point = Vector3.Lerp(body.PelvisPosition, body.HeadPosition, .6f); return true;
                case Target.TransitionArea: point = body.TransitionAreaCenter; return true;
                case Target.Phone:
                    if (body.PhoneAnchor == null) return false;
                    point = body.PhoneAnchor.position; return true;
                default: return false;
            }
        }

        void Present()
        {
            TryTarget(Current.Target, out var target);
            float t = Time.unscaledTime;
            var floorTarget = new Vector3(target.x, .012f, target.z);
            arrow.position = target + Vector3.up * (.42f + Mathf.Sin(t * 3.2f) * .06f);
            arrow.rotation = Quaternion.Euler(0, t * 90, 0);
            ring.position = floorTarget;
            float pulse = 1 + Mathf.Sin(t * 4) * .08f;
            ring.localScale = new Vector3(pulse, 1, pulse);

            var from = viewer == null ? floorTarget : new Vector3(viewer.transform.position.x, .012f, viewer.transform.position.z);
            var delta = floorTarget - from;
            float distance = delta.magnitude;
            bool far = distance > ArrivalRadius;
            var direction = distance > .001f ? delta / distance : Vector3.forward;
            float offset = t * .9f % ChevronSpacing;
            for (int i = 0; i < chevrons.Count; i++)
            {
                float along = .6f + offset + i * ChevronSpacing;
                bool visible = far && along < distance - .55f;
                chevrons[i].gameObject.SetActive(visible);
                if (!visible) continue;
                chevrons[i].position = from + direction * along;
                chevrons[i].rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
            var color = Color.Lerp(new Color(.28f, .85f, .73f), new Color(.55f, 1, .9f), .5f + Mathf.Sin(t * 4) * .5f);
            glow.SetColor("_BaseColor", color);
            if (glow.HasProperty("_EmissionColor")) glow.SetColor("_EmissionColor", color * 1.4f);
        }

        void BuildVisuals()
        {
            root = new GameObject("Guided practice markers").transform;
            root.SetParent(transform, false);
            // A saved unlit material survives player shader stripping; the runtime copy is animated.
            var saved = Resources.Load<Material>("Visual/Materials/GuideGlow");
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
            glow = saved != null ? new Material(saved) : new Material(shader);
            glow.name = "Guided practice glow";
            arrowMesh = ArrowMesh();
            chevronMesh = ChevronMesh();
            ringMesh = RingMesh(.42f, .5f, 48);
            arrow = Part("Target arrow", arrowMesh, root);
            ring = Part("Target ring", ringMesh, root);
            for (int i = 0; i < Chevrons; i++) chevrons.Add(Part("Path chevron", chevronMesh, root));
            root.gameObject.SetActive(false);
        }

        Transform Part(string name, Mesh mesh, Transform parent)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        // A downward-pointing 3D arrow (square shaft + pyramid head), tip at the local origin.
        static Mesh ArrowMesh()
        {
            var v = new List<Vector3>(); var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int s = v.Count; v.AddRange(new[] { a, b, c, d }); tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); }
            void Tri(Vector3 a, Vector3 b, Vector3 c) { int s = v.Count; v.AddRange(new[] { a, b, c }); tris.AddRange(new[] { s, s + 1, s + 2 }); }
            float h = .09f, head = .16f, shaft = .035f, top = .42f;
            var corners = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) };
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % 4];
                Tri(Vector3.zero, a * h + Vector3.up * head, b * h + Vector3.up * head);
                Quad(a * shaft + Vector3.up * head, a * shaft + Vector3.up * top, b * shaft + Vector3.up * top, b * shaft + Vector3.up * head);
            }
            Quad(corners[3] * h + Vector3.up * head, corners[2] * h + Vector3.up * head, corners[1] * h + Vector3.up * head, corners[0] * h + Vector3.up * head);
            Quad(corners[0] * shaft + Vector3.up * top, corners[1] * shaft + Vector3.up * top, corners[2] * shaft + Vector3.up * top, corners[3] * shaft + Vector3.up * top);
            return Build("Guide arrow", v, tris);
        }

        // Flat chevron on the floor pointing along +Z.
        static Mesh ChevronMesh()
        {
            var v = new List<Vector3>
            {
                new Vector3(-.16f, 0, -.07f), new Vector3(0, 0, .09f), new Vector3(.16f, 0, -.07f),
                new Vector3(.16f, 0, -.15f), new Vector3(0, 0, .01f), new Vector3(-.16f, 0, -.15f)
            };
            var tris = new List<int> { 0, 1, 4, 0, 4, 5, 1, 2, 3, 1, 3, 4 };
            return Build("Guide chevron", v, tris);
        }

        static Mesh RingMesh(float inner, float outer, int segments)
        {
            var v = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * inner); v.Add(d * outer);
                if (i == segments) break;
                int s = i * 2;
                tris.AddRange(new[] { s, s + 1, s + 3, s, s + 3, s + 2 });
            }
            return Build("Guide ring", v, tris);
        }

        static Mesh Build(string name, List<Vector3> vertices, List<int> triangles)
        {
            // Double-sided so the markers read from any viewpoint without a custom shader.
            int count = triangles.Count;
            for (int i = 0; i < count; i += 3) triangles.AddRange(new[] { triangles[i], triangles[i + 2], triangles[i + 1] });
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void OnDestroy()
        {
            if (glow != null) Destroy(glow);
            if (arrowMesh != null) Destroy(arrowMesh);
            if (chevronMesh != null) Destroy(chevronMesh);
            if (ringMesh != null) Destroy(ringMesh);
        }
    }
}
