using System;
using System.Collections;
using System.IO;
using System.Linq;
using EmergencyVR.Medical;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class MedicalRuntimeTests
    {
        static MedicalLibrary Load() => JsonUtility.FromJson<MedicalLibrary>(File.ReadAllText("Assets/_Project/Resources/MedicalScenarios.json"));
        static IEnumerable Cases() => Load().scenarios.Select(s=>new TestCaseData(s.id).SetName("MedicalPlayable_"+s.id));
        static MedicalScenarioRuntime Runtime(string id,out MedicalScenarioDefinition definition)
        {
            var lib=Load();definition=lib.scenarios.Single(s=>s.id==id);return new MedicalScenarioRuntime(definition,lib,1234);
        }
        static MedicalDebrief Complete(MedicalScenarioRuntime runtime,MedicalScenarioDefinition data)
        {
            double time=0;
            foreach(var id in data.recommendedSequence) { time=Math.Max(time+1,runtime.EarliestTime(id)); Assert.That(runtime.Submit(id,time),Is.EqualTo("Accepted"),data.id+" "+id); }
            return runtime.Finish(time+1);
        }
        [TestCaseSource(nameof(Cases))] public void EveryScenarioCanBeCompletedAndExported(string id)
        {
            var runtime=Runtime(id,out var definition);var result=Complete(runtime,definition);
            Assert.That(result.scorePercent,Is.EqualTo(100));Assert.That(result.criticalErrors,Is.Empty);
            var json=JsonUtility.ToJson(result);var parsed=JsonUtility.FromJson<MedicalDebrief>(json);
            Assert.That(parsed.caseId,Is.EqualTo(id));Assert.That(parsed.actions.Length,Is.GreaterThan(0));Assert.That(parsed.schemaVersion,Is.EqualTo(3));
            Assert.That(parsed.medicalReferences.Length,Is.GreaterThan(0));Assert.That(parsed.medicalValidationStatus,Is.EqualTo("CLIENT_REVIEW"));
        }
        [Test] public void LibraryIsValidHas44CasesAndMigratesAllFivePilots()
        {
            var lib=Load();lib.Validate();Assert.That(lib.scenarios.Length,Is.EqualTo(44));Assert.That(lib.scenarios.Count(s=>s.id.StartsWith("review-")),Is.EqualTo(5));
            Assert.That(lib.scenarios.Select(s=>s.environment).Distinct().Count(),Is.EqualTo(4));
        }
        [Test] public void PatientAndDefinitionAreIsolatedFromExternalMutation()
        {
            var r=Runtime("review-hypoglycaemia-v1",out var d);var p=r.Patient;p.glucose=33;d.initialState.glucose=30;d.actions[0].points=999;
            Assert.That(r.Patient.glucose,Is.LessThan(4));Assert.That(Complete(r,d).scorePercent,Is.EqualTo(100));
        }
        [Test] public void SeedReproducesPatientAndTimelineAcrossTickSizes()
        {
            var l=Load();var d=l.scenarios.Single(s=>s.id=="hypoxia");var a=new MedicalScenarioRuntime(d,l,42);var b=new MedicalScenarioRuntime(d,l,42);
            a.Tick(200);for(int i=0;i<=200;i++)b.Tick(i);
            Assert.That(JsonUtility.ToJson(a.Patient),Is.EqualTo(JsonUtility.ToJson(b.Patient)));
            var other=new MedicalScenarioRuntime(d,l,43);Assert.That(JsonUtility.ToJson(other.Patient),Is.Not.EqualTo(JsonUtility.ToJson(new MedicalScenarioRuntime(d,l,42).Patient)));
        }
        [Test] public void ObservationDoesNotPreventRespiratoryDeterioration()
        {
            var a=Runtime("hypoxia",out var d);a.Tick(200);Assert.That(a.Patient.consciousness,Is.EqualTo("Drowsy"));
            var b=Runtime("hypoxia",out _);b.Submit("CheckSceneSafety",1);b.Submit("CheckResponsiveness",2);b.Submit("CheckBreathing",3);b.Submit("Monitor",4);b.Tick(200);
            Assert.That(b.Patient.consciousness,Is.EqualTo("Drowsy"));
            Assert.That(b.Patient.spo2,Is.EqualTo(a.Patient.spo2));
        }
        [Test] public void OralGlucoseWithoutSwallowingIsCriticalAndCannotChangeGlucose()
        {
            var r=Runtime("glucose-severe",out _);double g=r.Patient.glucose;
            Assert.That(r.Submit("GiveGlucose",1),Is.EqualTo("Unsafe"));Assert.That(r.Patient.glucose,Is.EqualTo(g));Assert.That(r.Finish(2).criticalErrors,Is.Not.Empty);
        }
        [Test] public void NonShockableRhythmCannotBeShocked()
        {
            var r=Runtime("review-abnormal-breathing-v1",out _);Assert.That(r.Submit("DeliverAEDShock",1),Is.EqualTo("Unsafe"));Assert.That(r.Patient.circulation,Is.EqualTo("pulseless"));
        }
        [Test] public void TwoShocksNeedNewAnalysisAndConfiguredCprInterval()
        {
            var r=Runtime("arrest-two-shocks",out var d);
            foreach(var id in d.recommendedSequence.TakeWhile(id=>id!="AnalyzeRhythm2"))r.Submit(id,r.Elapsed+1);
            Assert.That(r.Submit("AnalyzeRhythm2",r.Elapsed+1),Is.EqualTo("OutOfOrder"));
            Assert.That(r.Submit("AnalyzeRhythm2",r.EarliestTime("AnalyzeRhythm2")),Is.EqualTo("Accepted"));
            Assert.That(r.Submit("DeliverAEDShock2",r.Elapsed+1),Is.EqualTo("Accepted"));
        }
        [Test] public void GlucoseRecoveryOccursAfterConfiguredWaitNotImmediately()
        {
            var r=Runtime("review-hypoglycaemia-v1",out var d);foreach(var id in d.recommendedSequence.TakeWhile(id=>id!="RecheckGlucose"))r.Submit(id,r.Elapsed+1);
            var before=r.Patient.glucose;r.Tick(r.Elapsed+500);Assert.That(r.Patient.glucose,Is.EqualTo(before));r.Tick(700);Assert.That(r.Patient.glucose,Is.GreaterThan(before));
        }
        [Test] public void SuccessAndCriticalFailureBranchesDiffer()
        {
            var a=Runtime("arrest-witnessed",out var d);Assert.That(Complete(a,d).outcome,Is.EqualTo("ROSC"));
            var b=Runtime("arrest-witnessed",out _);var result=b.Finish(200);Assert.That(result.outcome,Is.EqualTo("CRITICAL_FAILURE"));Assert.That(result.scorePercent,Is.EqualTo(0));Assert.That(result.finalPatient.circulation,Is.EqualTo("pulseless"));
        }
        [Test] public void DelayedCareDoesNotPromiseRosc()
        {
            var r=Runtime("arrest-witnessed",out var d);double t=100;
            foreach(var id in d.recommendedSequence)r.Submit(id,++t);var result=r.Finish(++t);
            Assert.That(result.outcome,Is.EqualTo("CRITICAL_FAILURE"));Assert.That(result.lateActions,Is.GreaterThan(0));Assert.That(result.scorePercent,Is.LessThanOrEqualTo(49));
        }
        [Test] public void OptionalActionsDoNotCountAsOmissions()
        {
            var r=Runtime("arrest-witnessed",out var d);var result=Complete(r,d);Assert.That(result.omittedActions,Is.Empty);Assert.That(result.physicalCprMeasured,Is.False);
        }
        [Test] public void DuplicateUnknownAndOutOfOrderActionsNeverInflateScores()
        {
            var r=Runtime("stroke",out _);Assert.That(r.Submit("CheckFAST",1),Is.EqualTo("OutOfOrder"));r.Submit("CheckSceneSafety",2);
            Assert.That(r.Submit("CheckSceneSafety",3),Is.EqualTo("Duplicate"));Assert.That(r.Submit("unknown",4),Is.EqualTo("Unknown"));Assert.That(r.Finish(5).scorePercent,Is.InRange(0,100));
        }
        [Test] public void FinishedSessionAndExportSnapshotAreImmutable()
        {
            var r=Runtime("stroke",out var d);var first=Complete(r,d);var score=first.scorePercent;first.finalPatient.spo2=0;first.actions[0].patient.spo2=0;first.sourceUrls[0]="bad";
            Assert.That(r.Submit("LeavePatient",1000),Is.EqualTo("Finished"));var second=r.Finish(1000);Assert.That(second.scorePercent,Is.EqualTo(score));Assert.That(second.finalPatient.spo2,Is.GreaterThan(0));Assert.That(second.sourceUrls[0],Does.StartWith("https://"));
        }
        [Test] public void RejectsInvalidTimeAndBrokenReferences()
        {
            var r=Runtime("stroke",out _);r.Tick(10);Assert.Throws<ArgumentException>(()=>r.Tick(9));Assert.Throws<ArgumentException>(()=>r.Tick(double.NaN));
            var l=Load();l.scenarios[0].actions[0].prerequisites=new[]{"missing"};Assert.Throws<ArgumentException>(()=>l.Validate());
        }
    }
}
