using System;
using System.Collections;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    // One renderer and one physical patient. Assets are loaded only when the selected identity changes.
    public sealed class PatientAppearanceController : MonoBehaviour
    {
        ReviewCaseSession review;
        SkinnedMeshRenderer skin;
        Mesh originalMesh;
        Material[] originalMaterials;
        Bounds originalBounds;
        ArticulatedPatient body;
        AnimationClip originalBreath;
        PatientAppearance activeAppearance;
        bool unloading;
        public string ActivePatientId { get; private set; } = "";
        public int AppearanceChanges { get; private set; }

        public static PatientAppearanceController Attach(ReviewCaseSession owner)
        {
            var controller = owner.gameObject.AddComponent<PatientAppearanceController>();
            controller.review = owner;
            var rig = owner.Procedures.Visuals.Rig;
            controller.skin = rig.face;
            controller.originalMesh = rig.face.sharedMesh;
            controller.originalMaterials = rig.face.sharedMaterials;
            controller.originalBounds = rig.face.localBounds;
            controller.body = rig.GetComponent<ArticulatedPatient>();
            if (controller.body != null) controller.originalBreath = controller.body.standingBreath;
            // Subscribe after Case01PatientPresentation: its mesh restoration must happen first.
            owner.SelectionChanged += controller.Selected;
            controller.Selected();
            return controller;
        }

        void Selected()
        {
            var identity = review.Selected.medical?.patientIdentity;
            string next = identity?.id ?? "";
            if (next == ActivePatientId && (next == "" || activeAppearance != null)) return;
            activeAppearance = null;
            if (identity == null)
            {
                skin.sharedMesh = originalMesh; skin.sharedMaterials = originalMaterials;
                skin.localBounds = originalBounds;
                if (body != null) body.standingBreath = originalBreath;
            }
            else
            {
                var appearance = Resources.Load<PatientAppearance>(identity.appearanceResource);
                if (appearance == null || appearance.mesh == null || appearance.materials.Length != appearance.mesh.subMeshCount)
                    throw new InvalidOperationException("Missing or invalid patient appearance: " + identity.appearanceResource);
                if (appearance.boneNames.Length > 0)
                {
                    if (appearance.boneNames.Length != skin.bones.Length) throw new InvalidOperationException("Patient appearance skeleton size mismatch: " + next);
                    for (int i = 0; i < skin.bones.Length; i++)
                        if (skin.bones[i].name != appearance.boneNames[i]) throw new InvalidOperationException("Patient appearance bone order mismatch: " + next);
                }
                activeAppearance = appearance;
                skin.sharedMesh = appearance.mesh; skin.sharedMaterials = appearance.materials;
                skin.localBounds = appearance.mesh.bounds;
                if (body != null) body.standingBreath = CharacterAnimationLibrary.Load(identity.sex == "female" ? "f_idle_breathe_01" : "m_idle_breathe_01") ?? originalBreath;
            }
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, 0);
            review.Procedures.Visuals.RefreshAppearance();
            ActivePatientId = next;
            AppearanceChanges++;
            // No roster-wide asset cache: retain the active skin and release unreferenced predecessors off the training loop.
            if (isActiveAndEnabled && !unloading) StartCoroutine(ReleaseUnused());
        }

        IEnumerator ReleaseUnused()
        {
            unloading = true;
            yield return null;
            // BeginTraining selects and starts in the same frame. Sweep only between attempts,
            // so a global resource scan cannot interrupt an active clinical interaction.
            while (review != null && review.Manager.IsRunning) yield return null;
            yield return Resources.UnloadUnusedAssets();
            unloading = false;
        }

        void OnDestroy() { if (review != null) review.SelectionChanged -= Selected; }
    }
}
