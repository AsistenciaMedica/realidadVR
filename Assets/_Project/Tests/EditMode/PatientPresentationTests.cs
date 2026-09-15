using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class PatientPresentationTests
    {
        [TestCase("normal",PatientBreathingMode.Normal)]
        [TestCase("fast",PatientBreathingMode.Fast)]
        [TestCase("slow",PatientBreathingMode.Slow)]
        [TestCase("agonal",PatientBreathingMode.Agonal)]
        [TestCase("absent",PatientBreathingMode.Absent)]
        public void RespirationUsesSnapshotInsteadOfConsciousness(string respiration,PatientBreathingMode expected)
        {
            var snapshot=new PatientSnapshot { respiration=respiration,consciousness="Unresponsive",canSwallow=false };
            Assert.That(PatientVisualState.FromSnapshot(snapshot).Breathing,Is.EqualTo(expected));
        }
        [Test] public void ExplicitLaboredAndShallowFlagsNeverOverrideAbsentOrAgonalBreathing()
        {
            var snapshot=new PatientSnapshot { flags=new[]{"labored breathing"} };
            Assert.That(PatientVisualState.FromSnapshot(snapshot).Breathing,Is.EqualTo(PatientBreathingMode.Labored));
            snapshot.flags=new[]{"shallow breathing"};
            Assert.That(PatientVisualState.FromSnapshot(snapshot).Breathing,Is.EqualTo(PatientBreathingMode.Shallow));
            snapshot.respiration="absent";
            Assert.That(PatientVisualState.FromSnapshot(snapshot).Breathing,Is.EqualTo(PatientBreathingMode.Absent));
            snapshot.respiration="agonal";
            Assert.That(PatientVisualState.FromSnapshot(snapshot).Breathing,Is.EqualTo(PatientBreathingMode.Agonal));
        }
        [Test] public void AbsentAndZeroRateNeverProduceBreathingExcursion()
        {
            for(float t=0;t<20;t+=.11f)
            {
                Assert.That(PatientBreathingAnimator.Excursion(PatientBreathingMode.Absent,16,t),Is.Zero);
                Assert.That(PatientBreathingAnimator.Excursion(PatientBreathingMode.Normal,0,t),Is.Zero);
                Assert.That(PatientBreathingAnimator.Excursion(PatientBreathingMode.Agonal,8,t),Is.InRange(0,.0091f));
            }
            Assert.That(PatientBreathingAnimator.Excursion(PatientBreathingMode.Agonal,12,6),Is.Zero,"An agonal pause cannot look like normal rhythmic breathing.");
        }
        [TestCase("Conscious",PatientConsciousness.Alert,1f)]
        [TestCase("Confused",PatientConsciousness.Confused,.5f)]
        [TestCase("Drowsy",PatientConsciousness.Drowsy,.15f)]
        [TestCase("Unresponsive",PatientConsciousness.Unresponsive,0f)]
        public void AwarenessControlsHeadTracking(string consciousness,PatientConsciousness expected,float amount)
        {
            var mapped=PatientVisualState.FromSnapshot(new PatientSnapshot { consciousness=consciousness });
            Assert.That(mapped.Consciousness,Is.EqualTo(expected));
            Assert.That(PatientBreathingAnimator.Awareness(mapped.Consciousness),Is.EqualTo(amount));
        }
        [Test] public void PresentationDoesNotInventClinicalSignsOrModifySnapshot()
        {
            var snapshot=new PatientSnapshot { spo2=65,circulation="hypotension",position="recovery",flags=new[]{"pain"} };
            string before=JsonUtility.ToJson(snapshot);
            var visual=PatientVisualState.FromSnapshot(snapshot);
            Assert.That(visual.Cyanosis,Is.Zero);Assert.That(visual.Pallor,Is.Zero);
            Assert.That(visual.Expression,Is.EqualTo(PatientExpression.Pain));
            Assert.That(visual.Posture,Is.EqualTo(PatientPosture.Recovery));
            Assert.That(JsonUtility.ToJson(snapshot),Is.EqualTo(before));
        }
        [Test] public void StartingPoseEndsOnAuthoredLossOfConsciousnessWithoutEditingMedicalState()
        {
            var go=new GameObject("Presentation test");
            try
            {
                var visual=go.AddComponent<PatientVisualController>();
                visual.SetState(new PatientSnapshot());visual.SetStartingPose(PatientPosture.Standing);
                Assert.That(visual.EffectivePosture,Is.EqualTo(PatientPosture.Standing));
                var collapsed=new PatientSnapshot { consciousness="Unresponsive",canSwallow=false,position="supine" };
                visual.SetState(collapsed);
                Assert.That(visual.EffectivePosture,Is.EqualTo(PatientPosture.Supine));
                Assert.That(collapsed.consciousness,Is.EqualTo("Unresponsive"));
                Assert.That(PatientVisualState.FromSnapshot(new PatientSnapshot{position="standing"}).Posture,Is.EqualTo(PatientPosture.Standing));
            }
            finally {Object.DestroyImmediate(go);}
        }
        [Test] public void MissingHumanRagdollCannotStartPhysics()
        {
            var go=new GameObject("Unconfigured patient rig");
            try
            {
                var rig=go.AddComponent<PatientRigAdapter>();rig.provisionalAsset=true;
                var ragdoll=go.AddComponent<PatientControlledRagdoll>();ragdoll.rig=rig;
                Assert.That(ragdoll.BeginFall(),Is.False);
                Assert.That(ragdoll.Phase,Is.EqualTo(PatientRagdollPhase.Animation));
                rig.provisionalAsset=false;
                Assert.That(ragdoll.BeginFall(),Is.False,"A humanoid label alone cannot replace missing colliders and connected bodies.");
            }
            finally {Object.DestroyImmediate(go);}
        }
    }
}
