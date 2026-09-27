using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.Scenarios;
using EmergencyVR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    // Optional I1 adapters are exercised with explicit grants and real transforms.
    // These fixtures do not enable instruments in the authored I0 learner case.
    public sealed class Case01EquipmentIntegrationTests
    {
        TrainingExperience flow;
        readonly List<GameObject> fixtures = new List<GameObject>();
        ScenarioManager Manager => flow.Review.Manager;

        [UnitySetUp]
        public IEnumerator LoadCase()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Training/TrainingRoom.unity");
            yield return null; yield return null;
            flow = UnityEngine.Object.FindFirstObjectByType<TrainingExperience>();
            Assert.That(flow, Is.Not.Null);
            int index = Array.FindIndex(flow.Review.Catalog.entries, e => e.medical?.id == "review-hypotension-v2");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            flow.Prepare(index); flow.BeginTraining();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            foreach (var fixture in fixtures) if (fixture != null) UnityEngine.Object.Destroy(fixture);
            fixtures.Clear();
            if (flow != null)
            {
                Manager.SetPaused(false);
                if (Manager.IsRunning) Manager.FinishCase();
            }
            Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
        }

        void ConfigureI1(bool advanced = true, bool measurementGrant = true, bool equipmentGrant = true)
        {
            Manager.FinishCase();
            var library = MedicalLibraryLoader.Load();
            var definition = library.scenarios.Single(s => s.id == "review-hypotension-v2").Copy();
            var profile = definition.clinicalV2.trainingProfiles.Single().Copy();
            profile.id = TrainingProfileId.I1_NonInvasiveEquipment;
            profile.allowedMeasurements.Clear(); profile.allowedEquipment.Clear();
            if (measurementGrant) profile.allowedMeasurements.AddRange(new[] { "BloodPressure", "SpO2" });
            if (equipmentGrant) profile.allowedEquipment.AddRange(new[] { "BloodPressure", "SpO2" });
            definition.clinicalV2.trainingProfiles = new[] { profile };
            definition.clinicalV2.metadata.trainingProfile = "I1_NON_INVASIVE_EQUIPMENT";
            definition.clinicalV2.capabilities.usesAdvancedMeasurements = advanced;
            Manager.ConfigureMedical(definition, library); Manager.StartCase();
        }

        NonInvasiveAcquisitionController Device(bool pressure)
        {
            var fixture = new GameObject("Independent optional instrument test fixture");
            fixtures.Add(fixture);
            fixture.transform.position = new Vector3(12, 1, 12);
            var anatomy = new GameObject("Anatomical target").transform;
            anatomy.SetParent(fixture.transform, false);
            var contact = new GameObject("Device contact").transform;
            contact.SetParent(fixture.transform, false);
            var support = new GameObject("Arm support"); support.transform.SetParent(fixture.transform, false);
            support.transform.localPosition = new Vector3(0, -.08f, 0);
            var supportCollider = support.AddComponent<BoxCollider>();
            supportCollider.size = new Vector3(.4f, .1f, .4f);
            NonInvasiveAcquisitionController device = pressure
                ? (NonInvasiveAcquisitionController)fixture.AddComponent<BloodPressureAcquisitionController>()
                : fixture.AddComponent<PulseOximeterAcquisitionController>();
            device.acquisitionSeconds = .5f;
            device.Initialize(Manager, anatomy, contact, supportCollider);
            Physics.SyncTransforms();
            return device;
        }

        static void PlaceAndStart(NonInvasiveAcquisitionController device)
        {
            Assert.That(device.Open(), Is.False, "A device must be picked up before it can be opened from Idle.");
            Assert.That(device.PickUp(), Is.True);
            Assert.That(device.Open(), Is.True);
            Assert.That(device.Close(), Is.True, device.Quality);
            Assert.That(device.BeginAcquisition(), Is.True);
            Assert.That(device.Result, Is.Null, "Closing a device is not an acquired measurement.");
        }

        [UnityTest]
        public IEnumerator I0AndEachMissingI1PermissionBlockBothInstrumentsWithoutCreatingObservations()
        {
            foreach (bool pressure in new[] { true, false })
            {
                var device = Device(pressure);
                Assert.That(device.Permitted, Is.False, "The authored I0 case does not include instruments.");
                Assert.That(device.PickUp(), Is.False);
                Assert.That(device.Open(), Is.False);
                Assert.That(device.BeginAcquisition(), Is.False);
            }
            foreach (var grants in new[] { new[] { false, true, true }, new[] { true, false, true }, new[] { true, true, false } })
            {
                ConfigureI1(grants[0], grants[1], grants[2]);
                foreach (bool pressure in new[] { true, false })
                {
                    var device = Device(pressure);
                    Assert.That(device.Permitted, Is.False);
                    Assert.That(device.PickUp(), Is.False);
                    Assert.That(device.BeginAcquisition(), Is.False);
                }
                Assert.That(Manager.MedicalSession.Observations.All, Is.Empty);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BloodPressureRequiresContactOrientationAndSupportedArmBeforeAcquiring()
        {
            ConfigureI1();
            var device = Device(true);
            Assert.That(device.PickUp(), Is.True); Assert.That(device.Open(), Is.True);
            device.contact.position += Vector3.right * .2f;
            Assert.That(device.Close(), Is.False);
            Assert.That(device.Quality, Is.EqualTo("InvalidPlacement"));
            Assert.That(device.BeginAcquisition(), Is.False);
            device.contact.position = device.anatomy.position;
            device.contact.rotation = Quaternion.Euler(0, 90, 0);
            Assert.That(device.Open(), Is.True); Assert.That(device.Close(), Is.False);
            Assert.That(device.Quality, Is.EqualTo("InvalidPlacement"));
            device.contact.rotation = device.anatomy.rotation;
            device.armSupport.enabled = false;
            Assert.That(device.Open(), Is.True); Assert.That(device.Close(), Is.False);
            Assert.That(device.Quality, Is.EqualTo("UnsupportedArm"));
            Assert.That(device.Result, Is.Null);
            Assert.That(Manager.MedicalSession.Observations.Measurements, Is.Empty);
            device.armSupport.enabled = true; Physics.SyncTransforms();
            Assert.That(device.Open(), Is.True); Assert.That(device.Close(), Is.True);
            Assert.That(device.BeginAcquisition(), Is.True);
            Assert.That(device.Phase, Is.EqualTo(AcquisitionPhase.Measuring));
            Assert.That(device.Result, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothAcquisitionsProduceDatedHistoricalReadingsOnlyAfterTheClinicalInterval()
        {
            ConfigureI1();
            foreach (bool pressure in new[] { true, false })
            {
                var device = Device(pressure);
                string type = pressure ? "BloodPressure" : "SpO2";
                PlaceAndStart(device);
                device.SampleAcquisition();
                Assert.That(device.Phase, Is.EqualTo(AcquisitionPhase.Measuring));
                Assert.That(Manager.MedicalSession.Observations.LatestMeasurement(type), Is.Null);
                Manager.AdvanceTrainingTime(device.acquisitionSeconds + .1);
                device.SampleAcquisition();
                Assert.That(device.Phase, Is.EqualTo(AcquisitionPhase.Completed));
                var first = device.Result;
                Assert.That(first.valid && first.hasValue, Is.True);
                Assert.That(first.attemptId, Is.EqualTo(Manager.MedicalSession.AttemptId));
                Assert.That(first.simulationTime, Is.EqualTo(Manager.MedicalSession.Elapsed).Within(.01));
                Assert.That(first.type, Is.EqualTo(type));
                Assert.That(first.source, Is.EqualTo(pressure ? "AutomaticBloodPressureMonitor" : "PulseOximeter"));
                if (pressure)
                {
                    Assert.That(first.systolic, Is.EqualTo(85)); Assert.That(first.diastolic, Is.EqualTo(55));
                }
                else Assert.That(first.value, Is.EqualTo(97));
                device.Remove();
                Assert.That(device.Result.observationId, Is.EqualTo(first.observationId));
                Manager.AdvanceTrainingTime(1); device.SampleAcquisition();
                Assert.That(device.Result.simulationTime, Is.EqualTo(first.simulationTime));
                Assert.That(device.Close(), Is.True); Assert.That(device.BeginAcquisition(), Is.True);
                Assert.That(device.Result, Is.Null);
                Manager.AdvanceTrainingTime(device.acquisitionSeconds + .1); device.SampleAcquisition();
                Assert.That(device.Result.observationId, Is.Not.EqualTo(first.observationId));
                Assert.That(device.Result.simulationTime, Is.GreaterThan(first.simulationTime));
                var history = Manager.MedicalSession.Observations.Measurements.Where(m => m.type == type).ToArray();
                Assert.That(history.Length, Is.EqualTo(2));
                Assert.That(history[0].simulationTime, Is.EqualTo(first.simulationTime));
                first.value = -100; first.systolic = -100;
                Assert.That(Manager.MedicalSession.Observations.Measurements.All(m => m.value >= 0 && m.systolic >= 0), Is.True);
            }
            Assert.That(Manager.MedicalSession.ObjectiveProgress.All(o => o.result == ObjectiveResult.NotEvaluated), Is.True,
                "Optional equipment readings alone do not award the I0 case objectives.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator MotionAndRemovalRecordFailedAcquisitionsWithoutInventingVitalValues()
        {
            ConfigureI1();
            var pressure = Device(true);
            PlaceAndStart(pressure);
            // Still inside the placement tolerance, but beyond the stability allowance.
            pressure.contact.position += Vector3.right * .035f;
            Physics.SyncTransforms(); pressure.SampleAcquisition();
            Assert.That(pressure.Quality, Is.EqualTo("Motion"));
            var oxygen = Device(false);
            PlaceAndStart(oxygen); oxygen.Remove();
            Assert.That(oxygen.Quality, Is.EqualTo("ContactLost"));
            Manager.AdvanceTrainingTime(5);
            pressure.SampleAcquisition(); oxygen.SampleAcquisition();
            var failed = Manager.MedicalSession.Observations.Measurements;
            Assert.That(failed.Count, Is.EqualTo(2));
            Assert.That(failed.All(m => !m.valid && !m.hasValue && m.value == 0 && m.systolic == 0 && m.diastolic == 0), Is.True);
            Assert.That(flow.MonitorValue("bp"), Is.EqualTo("Sin lectura"));
            Assert.That(flow.MonitorValue("spo2"), Is.EqualTo("Sin lectura"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseStopsAcquisitionAndResetDiscardsThePreviousAttemptDeadline()
        {
            ConfigureI1();
            var device = Device(false);
            PlaceAndStart(device);
            Manager.SetPaused(true);
            double paused = Manager.MedicalSession.Elapsed;
            float progress = device.Progress;
            Manager.AdvanceTrainingTime(10);
            yield return new WaitForSecondsRealtime(.65f);
            device.SampleAcquisition();
            Assert.That(Manager.MedicalSession.Elapsed, Is.EqualTo(paused));
            Assert.That(device.Progress, Is.EqualTo(progress));
            Assert.That(device.Phase, Is.EqualTo(AcquisitionPhase.Measuring));
            Assert.That(device.Result, Is.Null);
            Assert.That(Manager.MedicalSession.Observations.Measurements, Is.Empty);
            Manager.SetPaused(false);
            string previous = Manager.MedicalSession.AttemptId;
            Manager.FinishCase(); Manager.StartCase();
            device.SampleAcquisition();
            Assert.That(Manager.MedicalSession.AttemptId, Is.Not.EqualTo(previous));
            Assert.That(device.Phase, Is.EqualTo(AcquisitionPhase.Idle));
            Assert.That(device.Result, Is.Null);
            Manager.AdvanceTrainingTime(5); device.SampleAcquisition();
            Assert.That(Manager.MedicalSession.Observations.Measurements, Is.Empty);
            PlaceAndStart(device);
            Manager.AdvanceTrainingTime(device.acquisitionSeconds + .1); device.SampleAcquisition();
            Assert.That(device.Result.attemptId, Is.EqualTo(Manager.MedicalSession.AttemptId));
        }
    }
}
