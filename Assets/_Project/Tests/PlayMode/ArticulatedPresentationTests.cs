using System.Collections;
using System.Linq;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.Scenarios;
using EmergencyVR.Desktop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class ArticulatedPresentationTests
    {
        [UnityTest] public IEnumerator DesktopCompressionHandsApproachFromTheOperator()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom");yield return null;
            var desktop=DesktopDemoController.Create();
            var review=Object.FindFirstObjectByType<ReviewCaseSession>();
            review.Select(System.Array.FindIndex(review.Catalog.entries,e=>e.medical?.id=="arrest-witnessed"));
            review.Manager.StartCase();
            yield return new WaitForSeconds(1.1f);
            var rig=review.Procedures;var chest=rig.Visuals.ChestAnchor;
            desktop.View.transform.position=chest.position+new Vector3(.85f,.75f,0);
            desktop.View.transform.LookAt(chest);
            rig.CPR.Feed(.055f,0,0,true,"AUTOMATED_COMPONENT_TEST");
            yield return new WaitForSeconds(.3f);
            var direction=Vector3.ProjectOnPlane(chest.position-desktop.View.transform.position,chest.up).normalized;
            foreach(var hand in rig.Hands.GetComponentsInChildren<ArticulatedHand>())
            {
                Assert.That(hand.Pose,Is.EqualTo(MedicalHandPose.Compression));
                Assert.That(Vector3.Distance(hand.transform.position,chest.position),Is.LessThan(.15f));
                Assert.That(Vector3.Dot(hand.transform.forward,direction),Is.GreaterThan(.8f));
            }
            Assert.That(rig.Hands.GetComponentsInChildren<ArticulatedHand>().Length,Is.EqualTo(2));
            rig.CPR.ReleaseContact();review.Manager.FinishCase();
        }
        [UnityTest] public IEnumerator FingersDeformAndPostureMovesTheSkeleton()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom");yield return null;
            var review=Object.FindFirstObjectByType<ReviewCaseSession>();
            var hand=Object.Instantiate(Resources.Load<GameObject>("Visual/LeftHand")).GetComponent<ArticulatedHand>();
            var skin=hand.GetComponentInChildren<SkinnedMeshRenderer>();
            var before=new Mesh();var after=new Mesh();
            skin.BakeMesh(before);var rest=hand.fingerJoints[4].localRotation;
            // Animation is time based; a hidden welcome world can render 30 frames in a few milliseconds.
            float gripUntil=Time.time+.6f;
            while(Time.time<gripUntil){hand.SetPose(MedicalHandPose.Grip);yield return null;}
            skin.BakeMesh(after);
            Assert.That(Quaternion.Angle(rest,hand.fingerJoints[4].localRotation),Is.GreaterThan(20));
            Assert.That(before.vertices.Zip(after.vertices,(a,b)=>Vector3.Distance(a,b)).Max(),Is.GreaterThan(.005f));
            Object.Destroy(before);Object.Destroy(after);Object.Destroy(hand.gameObject);
            var visual=review.Procedures.Visuals;
            visual.SetStartingPose(PatientPosture.Supine);
            yield return new WaitForSeconds(.6f);
            var previous=visual.Rig.poseRoot.rotation;
            visual.SetStartingPose(PatientPosture.Recovery);
            yield return new WaitForSeconds(.6f);
            Assert.That(Quaternion.Angle(previous,visual.Rig.poseRoot.rotation),Is.GreaterThan(40));
            visual.ClearStartingPose();
        }
        [UnityTest] public IEnumerator ImportedPatientAndHandsAreSharedAcrossReleaseEnvironments()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom");yield return null;
            var review=Object.FindFirstObjectByType<ReviewCaseSession>();
            var visual=review.Procedures.Visuals;
            Assert.That(visual.NeedsHumanAsset,Is.False);
            Assert.That(review.Procedures.Hands.HasArticulatedHands,Is.True);
            foreach(var environment in review.Scope.environments.Select(e=>e.id))
            {
                Assert.That(review.Select(System.Array.FindIndex(review.Catalog.entries,e=>e.medical?.environment==environment)),Is.True);
                yield return null;
                Assert.That(review.Procedures.Visuals,Is.SameAs(visual));
                Assert.That(visual.Rig.face.enabled&&visual.Rig.face.gameObject.activeInHierarchy,Is.True);
                Assert.That(visual.IsPatientContact(visual.Rig.head.position),Is.True);
            }
            visual.SetCompressionDepth(.055f);yield return null;yield return null;
            int index=visual.Rig.face.sharedMesh.GetBlendShapeIndex("VitalCompression");
            Assert.That(visual.Rig.face.GetBlendShapeWeight(index),Is.InRange(68,70));
            visual.SetCompressionDepth(0);yield return null;
            Assert.That(visual.Rig.face.GetBlendShapeWeight(index),Is.Zero);
        }
        [UnityTest] public IEnumerator RejectedAndDuplicateActionsDoNotTriggerPresentation()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRoom");yield return null;
            var review=Object.FindFirstObjectByType<ReviewCaseSession>();
            review.Select(System.Array.FindIndex(review.Catalog.entries,e=>e.medical?.id=="arrest-witnessed"));
            int count=0;review.Manager.ActionAccepted+=_=>count++;
            review.Manager.StartCase();
            review.Submit("does-not-exist");Assert.That(count,Is.Zero);
            review.Submit("CheckSceneSafety");Assert.That(count,Is.EqualTo(1));
            review.Submit("CheckSceneSafety");Assert.That(count,Is.EqualTo(1));
            review.Manager.FinishCase();review.Submit("CheckResponsiveness");Assert.That(count,Is.EqualTo(1));
        }
    }
}
