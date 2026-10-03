using System;
using System.Collections.Generic;
using EmergencyVR.Scenarios;
using UnityEngine;
using UnityEngine.Rendering;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>Civilian witnesses are presentation only. They never submit clinical actions.</summary>
    public sealed class ScenarioSupportingCast : MonoBehaviour
    {
        ReviewCaseSession review;
        readonly List<SupportingActor> actors = new List<SupportingActor>();
        string scenarioId;
        public SupportingActor Witness { get; private set; }
        public int Count => actors.Count;

        public static ScenarioSupportingCast Attach(ReviewCaseSession owner)
        {
            var cast = owner.gameObject.AddComponent<ScenarioSupportingCast>();
            cast.review = owner;
            owner.SelectionChanged += cast.Clear;
            return cast;
        }

        void Update()
        {
            if (review == null || !review.Manager.IsRunning || review.Selected.medical == null)
            { if (actors.Count > 0) Clear(); return; }
            var definition = review.Selected.medical;
            if (scenarioId == definition.id) return;
            Clear(); scenarioId = definition.id;
            string patient = definition.patientIdentity?.id;
            var point = review.Procedures.Visuals.Rig.head.position;
            if (definition.clinicalV2 != null)
            {
                // Daniel already has a gym colleague and, later, two arriving paramedics (four total).
                Add("Mateo", false, new Vector3(-.75f, 0, 2.5f), point);
                return;
            }
            switch (definition.environment)
            {
                case "gym":
                    Witness = Add(patient == "Sara" ? "Lucia" : "Sara", true, new Vector3(1.5f, 0, 3.25f), point);
                    Add(patient == "Mateo" ? "Daniel" : "Mateo", false, new Vector3(-.7f, 0, -.8f), point);
                    break;
                case "mall":
                    Witness = Add(patient == "Rosa" ? "Camila" : "Rosa", true, new Vector3(2.4f, 0, 3.2f), point);
                    Add(patient == "Luis" ? "Javier" : "Luis", false, new Vector3(-1.25f, 0, 2.8f), point);
                    break;
                case "football":
                    Witness = Add(patient == "Pablo" ? "Sergio" : "Pablo", false, new Vector3(2.8f, 0, 2.6f), point);
                    Add(patient == "Sergio" ? "Miguel" : "Sergio", false, new Vector3(-.7f, 0, 3f), point);
                    break;
            }
        }

        SupportingActor Add(string appearance, bool female, Vector3 position, Vector3 look)
        {
            var actor = SupportingActor.Create(transform, appearance, female, position, look);
            actors.Add(actor);
            return actor;
        }

        void Clear()
        {
            foreach (var actor in actors) if (actor != null) { actor.gameObject.SetActive(false); Destroy(actor.gameObject); }
            actors.Clear(); Witness = null; scenarioId = null;
        }
        void OnDestroy() { if (review != null) review.SelectionChanged -= Clear; Clear(); }
    }

    [DefaultExecutionOrder(180)]
    public sealed class SupportingActor : MonoBehaviour
    {
        SkinnedMeshRenderer skin;
        int blinkLeft, blinkRight, mouth;
        float phase;
        readonly float[] speech = new float[128];
        public string AppearanceId { get; private set; }
        public AudioSource Voice { get; private set; }
        public CharacterAnimator Motion { get; private set; }
        public Renderer Skin => skin;

        public static SupportingActor Create(Transform parent, string appearanceId, bool female, Vector3 position, Vector3 look)
        {
            var template = Resources.Load<GameObject>("Visual/Patient");
            var appearance = Resources.Load<PatientAppearance>("Visual/Appearances/" + appearanceId);
            if (template == null || appearance == null) throw new InvalidOperationException("Missing supporting character " + appearanceId);
            var instance = Instantiate(template, position, Quaternion.identity, parent);
            instance.name = "Civilian witness / " + appearanceId;
            var rig = instance.GetComponent<PatientRigAdapter>();
            var body = instance.GetComponent<ArticulatedPatient>();
            body.enabled = false;
            var actor = instance.AddComponent<SupportingActor>();
            actor.AppearanceId = appearanceId;
            actor.skin = rig.face;
            if (appearance.boneNames.Length != actor.skin.bones.Length)
                throw new InvalidOperationException("Supporting character skeleton mismatch: " + appearanceId);
            for (int i = 0; i < appearance.boneNames.Length; i++)
                if (appearance.boneNames[i] != actor.skin.bones[i].name)
                    throw new InvalidOperationException("Supporting character bone order mismatch: " + appearanceId);
            actor.skin.sharedMesh = appearance.mesh;
            actor.skin.sharedMaterials = appearance.materials;
            actor.skin.localBounds = appearance.mesh.bounds;
            actor.skin.lightProbeUsage = LightProbeUsage.BlendProbes;
            actor.skin.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            actor.blinkLeft = appearance.mesh.GetBlendShapeIndex(rig.blinkLeft);
            actor.blinkRight = appearance.mesh.GetBlendShapeIndex(rig.blinkRight);
            actor.mouth = appearance.mesh.GetBlendShapeIndex(rig.jawOpen);
            for (int i = 0; i < appearance.mesh.blendShapeCount; i++) actor.skin.SetBlendShapeWeight(i, 0);
            rig.poseRoot.localRotation = Quaternion.identity;
            rig.poseRoot.localPosition = Vector3.zero;
            var model = rig.poseRoot.GetChild(0);
            model.localPosition = Vector3.zero;
            var direction = look - position; direction.y = 0;
            if (direction.sqrMagnitude > .01f) instance.transform.rotation = Quaternion.LookRotation(direction);
            string clip = female ? "f_idle_breathe_01" : "m_idle_breathe_01";
            CharacterAnimationLibrary.Sample(CharacterAnimationLibrary.Load(clip), model.gameObject, 0);
            actor.Motion = model.gameObject.AddComponent<CharacterAnimator>();
            actor.Motion.Play(clip, 0);
            actor.phase = appearanceId.Length * .43f;
            actor.Voice = CreateVoice(rig.head, "Witness speech");
            CharacterContactShadow.Attach(instance, actor.skin);
            // Keep civilians out of medical searches and interactions; retain only their independent skin and motion.
            foreach (var collider in instance.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            Destroy(body); Destroy(rig);
            var obstacle = instance.AddComponent<CapsuleCollider>();
            obstacle.center = Vector3.up * .9f; obstacle.height = 1.8f; obstacle.radius = .20f;
            return actor;
        }

        public static AudioSource CreateVoice(Transform anchor, string label)
        {
            var source = new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(anchor, false);
            source.playOnAwake = false; source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 1.5f; source.maxDistance = 9;
            source.dopplerLevel = 0; source.volume = .9f;
            return source;
        }

        void LateUpdate()
        {
            phase += Time.deltaTime;
            float time = Mathf.Repeat(phase, 4.3f);
            float blink = time < .18f ? Mathf.Sin(time / .18f * Mathf.PI) * 100 : 0;
            if (blinkLeft >= 0) skin.SetBlendShapeWeight(blinkLeft, blink);
            if (blinkRight >= 0) skin.SetBlendShapeWeight(blinkRight, blink);
            if (mouth >= 0) skin.SetBlendShapeWeight(mouth, SpeechWeight(Voice, speech));
        }

        public static float SpeechWeight(AudioSource source, float[] samples)
        {
            if (source == null || !source.isPlaying) return 0;
            source.GetOutputData(samples, 0);
            float sum = 0; foreach (var value in samples) sum += value * value;
            return Mathf.Clamp(Mathf.Sqrt(sum / samples.Length) * 230, 0, 42);
        }
    }
}
