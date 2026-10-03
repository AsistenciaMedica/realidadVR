using System;
using System.IO;
using System.Linq;
using EmergencyVR.Core;
using EmergencyVR.Evaluation;
using EmergencyVR.Patient;
using UnityEngine;
using UnityEngine.SceneManagement;
using EmergencyVR.Medical;
using System.Collections.Generic;

namespace EmergencyVR.Scenarios
{
    // Shared by desktop and VR presentation. CaseSession still owns all transitions and evaluation.
    public sealed class ReviewCaseSession : MonoBehaviour
    {
        public ReviewCaseCatalog Catalog { get; private set; }
        public ReleaseScope Scope { get; private set; }
        public int SelectedIndex { get; private set; }
        public ReviewCaseEntry Selected => Catalog.entries[SelectedIndex];
        public ScenarioManager Manager { get; private set; }
        public EmergencyVR.Medical.Interaction.MedicalProcedureRig Procedures { get; private set; }
        public string ExportMessage { get; private set; } = "";
        ScenarioDefinition runtimeScenario;
        PatientController patient;
        EvaluationManager evaluation;
        MedicalLibrary medicalLibrary;
        readonly List<UnityEngine.Object> generated=new List<UnityEngine.Object>();
        public string[] ActionIds => Selected.medical==null?Selected.definition.steps.Select(s=>s.actionId).ToArray():Selected.medical.actions.Select(a=>a.id).ToArray();
        public string ActionLabel(string id) => Selected.medical==null?Selected.definition.steps.First(s=>s.actionId==id).label:medicalLibrary.actions.First(a=>a.id==Selected.medical.actions.First(r=>r.id==id).action).label;
        public bool HasResult => Manager.MedicalResult!=null||evaluation.LatestResult!=null;
        public double Score => Manager.MedicalResult!=null?Manager.MedicalResult.scorePercent:evaluation.LatestResult?.ScorePercent??0;
        public string PatientReadout()
        {
            if(Selected.medical?.clinicalV2?.capabilities.usesObservedPatientData==true)
                return EmergencyVR.UI.ClinicalObservationText.Format(Manager.MedicalSession?.Observations);
            var p=Manager.MedicalSession?.Patient??Selected.medical?.initialState;
            if(p==null) return "Demo técnica original";
            var noPerfusion=p.circulation=="pulseless";
            return $"{p.age} años · {p.sex} · {p.consciousness} · {p.respiration}\n"+
                (noPerfusion?"Sin pulso · TA/SpO2 sin lectura fiable":$"FC {p.heartRate:0} · TA {p.systolic:0}/{p.diastolic:0} · SpO2 {p.spo2:0}%")+
                $"\nGlucemia {p.glucose:0.0} mmol/L · FR {p.respiratoryRate:0} · {p.temperature:0.0} °C · Dolor {p.pain:0}/10\n{p.dialogue}\n"+string.Join(", ",p.flags);
        }
        public event Action SelectionChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if(scene.name != "TrainingRoom") return;
            var manager=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ScenarioManager>()).SingleOrDefault();
            var catalog=Resources.Load<ReviewCaseCatalog>("ReviewCaseCatalog");
            if(manager==null || catalog==null || catalog.entries.Length==0) return;
            var session=manager.gameObject.AddComponent<ReviewCaseSession>();
            session.Initialize(catalog,manager);
            EmergencyVR.UI.TrainingExperience.Attach(session);
        }

        public void Initialize(ReviewCaseCatalog catalog,ScenarioManager manager)
        {
            Catalog=catalog; Manager=manager;
            patient=FindFirstObjectByType<PatientController>();
            evaluation=FindFirstObjectByType<EvaluationManager>();
            runtimeScenario=Instantiate(manager.Scenario);
            medicalLibrary=MedicalLibraryLoader.Load();
            Scope=ReleaseScope.Load(medicalLibrary);
            Catalog=Instantiate(catalog); generated.Add(Catalog);
            // The technical exercise remains reachable from Help; only release cases enter product selection.
            var entries=catalog.entries.Where(e=>e.definition!=null&&e.definition.isTechnicalDemo).ToList();
            foreach(var scenarioId in Scope.ScenarioIds)
            {
                var medical=medicalLibrary.scenarios.Single(s=>s.id==scenarioId);
                // Runtime adapters preserve legacy asset IDs without modifying the original authored pilots.
                var legacy=ScriptableObject.CreateInstance<ClinicalCaseDefinition>(); generated.Add(legacy);
                legacy.caseId=medical.id; legacy.displayName=medical.name; legacy.description=medical.description; legacy.isTechnicalDemo=false;
                foreach(var id in medical.recommendedSequence) legacy.steps.Add(new CaseStepData {actionId=id,label=medicalLibrary.actions.First(a=>a.id==medical.actions.First(r=>r.id==id).action).label,fromState=legacy.initialState,toState=legacy.initialState,points=10});
                var entry=new ReviewCaseEntry {definition=legacy,medical=medical,briefing=medical.description+"\n"+medical.incident,limitations="PENDING MEDICAL VALIDATION",sourceUrls=medicalLibrary.references.Where(r=>medical.references.Contains(r.id)).Select(r=>r.url).ToArray(),reviewedOn="2026-09-13"};
                entries.Add(entry);
            }
            Catalog.entries=entries.ToArray();
            // Resolve after filtering: archived cases must never shift the initial technical selection.
            SelectedIndex=Math.Max(0,Array.FindIndex(Catalog.entries,e=>e.definition==manager.Scenario.defaultCase));
            Procedures=EmergencyVR.Medical.Interaction.MedicalProcedureRig.Attach(this);
            var help=gameObject.AddComponent<EmergencyVR.Dialogue.ClinicalHelpController>();
            help.Initialize(Manager);
            gameObject.AddComponent<EmergencyVR.Dialogue.PhoneCallController>().Initialize(this);
            gameObject.AddComponent<EmergencyVR.Patient.Presentation.Case01Cast>().Initialize(this);
            var body=gameObject.AddComponent<EmergencyVR.Patient.Presentation.Case01PatientPresentation>();
            body.Initialize(this);
            var variations=gameObject.AddComponent<Case01VariationController>();
            variations.Initialize(this);
            EmergencyVR.Patient.Presentation.PatientAppearanceController.Attach(this);
            EmergencyVR.Patient.Presentation.PatientSceneStaging.Attach(this);
        }

        public bool Select(int index)
        {
            if(Manager.IsRunning || index<0 || index>=Catalog.entries.Length) return false;
            var entry=Catalog.entries[index];
            if(entry.definition==null) return false;
            if(entry.medical==null) entry.definition.ToDomain();
            runtimeScenario.defaultCase=entry.definition;
            Manager.Configure(runtimeScenario,patient,evaluation);
            if(entry.medical!=null) Manager.ConfigureMedical(entry.medical,medicalLibrary);
            evaluation.Clear(); SelectedIndex=index; ExportMessage="";
            EmergencyVR.Environment.ScenarioEnvironmentPresenter.Apply(entry.medical?.environment,patient);
            if(entry.medical!=null) patient.PresentMedical(entry.medical.initialState);
            else patient.Present(entry.definition.initialState);
            SelectionChanged?.Invoke();
            return true;
        }

        public void Submit(string id) { Manager.SubmitAction(id); }

        public string ExportResult()
        {
            if(Manager.MedicalResult!=null && Selected.medical?.clinicalV2?.capabilities.usesObjectiveBasedEvaluation==true)
                return SaveReport(JsonUtility.ToJson(ClinicalV2Report.From(Manager.MedicalResult),true));
            if(Manager.MedicalResult!=null) return SaveReport(JsonUtility.ToJson(Manager.MedicalResult,true));
            if(evaluation.LatestResult==null) return ExportMessage="Finaliza un intento antes de exportar.";
            var result=evaluation.LatestResult;
            var report=new ReviewReport {
                caseId=result.CaseId, caseName=Selected.definition.displayName,
                clinicallyApproved=Selected.definition.clinicallyApproved, technicalDemo=Selected.definition.isTechnicalDemo,
                createdUtc=DateTime.UtcNow.ToString("O"), scorePercent=result.ScorePercent,
                durationSeconds=result.DurationSeconds, errors=result.Errors, lateActions=result.LateActions,
                finalState=result.FinalState.ToString(), omittedActions=result.OmittedActions.ToArray(),
                actions=result.Actions.Select(a=>new ReviewAction {id=a.ActionId,elapsedSeconds=a.ElapsedSeconds,disposition=a.Disposition.ToString()}).ToArray(),
                sourceUrls=Selected.sourceUrls
            };
            return SaveReport(JsonUtility.ToJson(report,true));
        }
        string SaveReport(string json)
        {
            string directory=Path.Combine(Application.persistentDataPath,"ReviewResults");
            Directory.CreateDirectory(directory);
            var path=Path.Combine(directory,"attempt-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");
            File.WriteAllText(path,json);
            ExportMessage="Resultado guardado: "+path;
            Debug.Log(ExportMessage);
            return path;
        }

        void OnDestroy() { if(Procedures!=null) Destroy(Procedures.gameObject); if(runtimeScenario!=null) Destroy(runtimeScenario); foreach(var item in generated) if(item!=null) Destroy(item); }
    }

    [Serializable] public sealed class ReviewReport
    {
        public string caseId,caseName,createdUtc,finalState;
        public bool clinicallyApproved,technicalDemo;
        public double scorePercent,durationSeconds;
        public int errors,lateActions;
        public string[] omittedActions,sourceUrls;
        public ReviewAction[] actions;
    }
    [Serializable] public sealed class ReviewAction { public string id,disposition; public double elapsedSeconds; }
}
