using System;
using System.Collections.Generic;
using System.Linq;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class MedicalLogEntry
    {
        public string id, actionId, disposition, message, section;
        public double elapsedSeconds;
        public PatientSnapshot patient;
        public MedicalLogEntry Copy() { var x=(MedicalLogEntry)MemberwiseClone(); x.patient=patient.Copy(); return x; }
    }
    [Serializable] public sealed class EvaluationSection
    {
        public string name;
        public double scorePercent;
        public int earnedPoints, possiblePoints;
        public bool measured;
    }
    [Serializable] public sealed class MedicalDebrief
    {
        // Original JSON keys retained; schemaVersion identifies additive medical fields.
        public int schemaVersion=3, seed, errors, lateActions;
        public string caseId, caseName, createdUtc, finalState, outcome, branchId, medicalValidationStatus, scenarioVersion, environment, witness, timingBasis;
        public bool clinicallyApproved, technicalDemo, physicalCprMeasured;
        public double scorePercent, durationSeconds, timeToCpr=-1, timeToAed=-1;
        public string[] omittedActions, correctActions, incorrectActions, criticalErrors, recommendations, sourceUrls;
        public MedicalReference[] medicalReferences;
        public MedicalLogEntry[] actions, timeline;
        public PatientSnapshot initialPatient, finalPatient;
        public EvaluationSection[] sections;
        public ProcedureMetrics procedures;
        public bool objectiveBasedEvaluation;
        public string attemptId, clinicalSpecVersion, trainingProfile, buildVersion;
        public ClinicalEvent[] clinicalEvents=Array.Empty<ClinicalEvent>();
        public LearningObjectiveProgress[] objectives=Array.Empty<LearningObjectiveProgress>();
        public MeasurementObservation[] measurements=Array.Empty<MeasurementObservation>();
        public InterviewObservation[] interviews=Array.Empty<InterviewObservation>();
        public PhysicalObservation[] physicalObservations=Array.Empty<PhysicalObservation>();
    }
    // Pure C#: no scene, Unity clock, input or platform dependencies. One instance per attempt.
    public sealed partial class MedicalScenarioRuntime
    {
        readonly MedicalScenarioDefinition definition;
        readonly Dictionary<string,MedicalAction> library;
        readonly MedicalReference[] references;
        readonly Dictionary<string,double> accepted=new Dictionary<string,double>();
        readonly Dictionary<string,int> awarded=new Dictionary<string,int>();
        readonly HashSet<string> processedEvents=new HashSet<string>(), firedEvents=new HashSet<string>();
        readonly List<MedicalLogEntry> log=new List<MedicalLogEntry>();
        readonly List<string> critical=new List<string>();
        readonly Dictionary<string,double> eventDelays=new Dictionary<string,double>();
        PatientSnapshot initial;
        readonly int seed;
        readonly string witness;
        PatientSnapshot patient;
        double elapsed;
        int penalties;
        MedicalDebrief result;
        public bool IsFinished { get { return result!=null; } }
        public double Elapsed { get { return elapsed; } }
        public PatientSnapshot Patient { get { return patient.Copy(); } }
        public string LastFeedback { get; private set; }="Caso iniciado. Valores de simulación.";
        public string[] Completed { get { return accepted.Keys.ToArray(); } }
        public string[] ActionIds { get { return definition.actions.Select(a=>a.id).ToArray(); } }
        public MedicalScenarioRuntime(MedicalScenarioDefinition data,MedicalLibrary catalog,int seed,string buildVersion="",TrainingProfileId? profile=null)
        {
            data.Validate(catalog); definition=data.Copy(); this.seed=seed;
            library=catalog.actions.ToDictionary(a=>a.id,a=>new MedicalAction {id=a.id,label=a.label,section=a.section,description=a.description});
            references=catalog.references.Where(r=>data.references.Contains(r.id)).Select(r=>new MedicalReference {id=r.id,organization=r.organization,title=r.title,year=r.year,url=r.url,accessedAt=r.accessedAt}).ToArray();
            var random=new SeedRandom(seed); patient=definition.initialState.Copy(); var v=definition.variation;
            patient.age=v.minAge+(int)(random.Next()*(v.maxAge-v.minAge+1)); patient.sex=v.sexes[(int)(random.Next()*v.sexes.Length)];
            patient.position=v.positions[(int)(random.Next()*v.positions.Length)]; witness=v.witnessStatements[(int)(random.Next()*v.witnessStatements.Length)];
            new PatientEffect {heartRateDelta=patient.heartRate==0?0:random.Spread(v.heartRateSpread),spo2Delta=patient.spo2==0?0:random.Spread(v.spo2Spread),glucoseDelta=random.Spread(v.glucoseSpread)}.Apply(patient);
            patient.dialogue=definition.initialDialogue;
            InitializeClinicalV2(buildVersion,profile); initial=patient.Copy();
            foreach(var e in definition.timeline) eventDelays[e.id]=Math.Max(0,e.afterSeconds+(e.kind=="deterioration"?random.Spread(v.timelineJitterSeconds):0));
        }
        public double EarliestTime(string id)
        {
            var a=definition.actions.Single(x=>x.id==id);
            if(string.IsNullOrEmpty(a.anchorAction)) return a.earliestSeconds;
            return accepted.TryGetValue(a.anchorAction,out var t)?t+a.earliestSeconds:double.PositiveInfinity;
        }
        public void Tick(double time)
        {
            if(IsFinished || IsPaused) return;
            if(double.IsNaN(time)||double.IsInfinity(time)||time<elapsed) throw new ArgumentException("Clock must be finite and monotonic.");
            if(clinicalMachine!=null) { TickClinicalV2(time); return; }
            if(processedEvents.Count==definition.timeline.Length) { elapsed=time;return; }
            // Sort absolute due times, so a large tick has the same result as many small ticks.
            var due=definition.timeline.Where(e=>!processedEvents.Contains(e.id) && (string.IsNullOrEmpty(e.anchorAction)||accepted.ContainsKey(e.anchorAction)))
                .Select(e=>new {entry=e,time=eventDelays[e.id]+(string.IsNullOrEmpty(e.anchorAction)?0:accepted[e.anchorAction])}).Where(e=>e.time<=time).OrderBy(e=>e.time).ThenBy(e=>e.entry.id,StringComparer.Ordinal).ToArray();
            foreach(var item in due)
            {
                var e=item.entry; processedEvents.Add(e.id);
                if(e.requiresActions.All(accepted.ContainsKey)&&!e.unlessActions.Any(accepted.ContainsKey))
                {
                    e.effect.Apply(patient); firedEvents.Add(e.id); LastFeedback=e.message;
                    log.Add(new MedicalLogEntry {id=e.id,actionId="",elapsedSeconds=item.time,disposition=e.kind,message=e.message,section="Timeline",patient=patient.Copy()});
                }
            }
            elapsed=time;
        }
        public string Submit(string id,double time)
        {
            if(IsFinished) return "Finished";
            if(IsPaused) return "Paused";
            if(HasClinicalFeatures) return SubmitClinicalV2(id,time);
            return SubmitLegacy(id,time);
        }
        string SubmitLegacy(string id,double time)
        {
            Tick(time);
            var a=definition.actions.FirstOrDefault(x=>x.id==id);
            string disposition, message;
            if(a==null) { disposition="Unknown"; message="Acción ajena a este caso."; penalties+=definition.errorPenalty; }
            else if(accepted.ContainsKey(id) && a.repeatPolicy==ActionRepeatPolicy.LegacySingleUse) { disposition="Duplicate"; message="Acción ya registrada; no suma puntos."; penalties+=definition.errorPenalty; }
            else if(a.kind=="dangerous"||a.kind=="incorrect")
            {
                disposition=a.kind=="dangerous"?"Dangerous":"Incorrect"; message=a.feedback;
                penalties+=a.penalty; if(a.kind=="dangerous") AddCritical(a.id+": "+message); a.effect.Apply(patient);
            }
            else if(!Guard(a.guard))
            {
                disposition="Unsafe"; message="Condición de seguridad incumplida: "+a.guard+". "+a.feedback;
                penalties+=a.penalty; AddCritical(a.id+": "+message);
            }
            else if(a.prerequisites.Any(x=>!accepted.ContainsKey(x)) || time<EarliestTime(id))
            {
                disposition="OutOfOrder"; message="Faltan requisitos o aún no se cumple la espera configurada."; penalties+=definition.errorPenalty;
            }
            else if(accepted.ContainsKey(id))
            {
                // Observe the current state again, after Tick and the original safety/time checks.
                // Keep first completion/credit and never replay a treatment effect or reset an anchored event.
                disposition="Accepted"; message=a.feedback+" Nueva observación registrada; sin puntos adicionales.";
            }
            else
            {
                var origin=string.IsNullOrEmpty(a.anchorAction)?0:accepted[a.anchorAction];
                bool late=a.deadlineSeconds>0 && time-origin>a.deadlineSeconds;
                disposition=late?"Late":"Accepted"; message=a.feedback;
                accepted.Add(id,time); awarded[id]=late?0:a.points; a.effect.Apply(patient);
                if(late && a.critical) AddCritical(id+": fuera de la ventana educativa configurada ("+Math.Round(time-origin)+" s).");
            }
            LastFeedback=message;
            log.Add(new MedicalLogEntry {id=id,actionId=id,elapsedSeconds=time,disposition=disposition,message=message,section=a==null?"Decision Making":library[a.action].section,patient=patient.Copy()});
            // Process events anchored at this action with zero delay.
            Tick(time); return disposition;
        }
        bool Guard(string guard)
        {
            switch(guard)
            {
                case "swallow": return patient.canSwallow && patient.consciousness!="Unresponsive" && patient.consciousness!="Drowsy";
                case "normalBreathing": return patient.respiration=="normal" && !patient.flags.Contains("trauma");
                case "arrest": return patient.consciousness=="Unresponsive" && (patient.respiration=="absent"||patient.respiration=="agonal");
                case "shock": return patient.shockAdvised && patient.circulation=="pulseless";
                case "noShock": return !patient.shockAdvised;
                case "noTrauma": return !patient.flags.Contains("trauma");
                case "seizureStopped": return !patient.flags.Contains("seizure");
                case "conscious": return patient.consciousness=="Conscious" || patient.consciousness=="Confused";
                default: return true;
            }
        }
        void AddCritical(string value) { if(!critical.Contains(value)) critical.Add(value); }
        public MedicalDebrief Finish(double time)
        {
            if(IsFinished) return CopyResult(result);
            if(IsPaused) return null;
            Tick(time);
            if(Capabilities.usesObjectiveBasedEvaluation) return FinishClinicalV2();
            var required=definition.actions.Where(a=>a.kind=="required").ToArray();
            var missing=required.Where(a=>!accepted.ContainsKey(a.id)).ToArray();
            foreach(var a in missing.Where(a=>a.critical)) AddCritical(a.id+": omisión crítica — "+library[a.action].label);
            var branch=definition.outcomes.FirstOrDefault(o=>o.requiresActions.All(accepted.ContainsKey) && o.missingActions.All(x=>!accepted.ContainsKey(x)) && o.requiresEvents.All(firedEvents.Contains) && (!o.noCriticalErrors||critical.Count==0) && time>=o.minimumSeconds && (o.maximumSeconds==0||time<=o.maximumSeconds));
            string outcome=branch==null?"REQUIRES_ADVANCED_CARE":branch.outcome;
            if(critical.Count>0) outcome="CRITICAL_FAILURE";
            if(branch!=null && critical.Count==0) branch.effect.Apply(patient);
            int possible=required.Sum(a=>a.points), earned=required.Where(a=>awarded.ContainsKey(a.id)).Sum(a=>awarded[a.id]);
            double score=possible==0?0:Math.Max(0,Math.Min(100,100.0*(earned-penalties)/possible));
            if(critical.Count>0) score=Math.Min(score,definition.criticalScoreCap);
            var sections=new[]{"Initial Assessment","Airway","Breathing","Circulation","CPR","AED","Measurements","Decision Making","Timing"}.Select(name=>
            {
                var rules=required.Where(a=>library[a.action].section==name).ToArray();
                int max=rules.Sum(a=>a.points), got=rules.Where(a=>awarded.ContainsKey(a.id)).Sum(a=>awarded[a.id]);
                if(name=="Timing") { rules=required.Where(a=>a.deadlineSeconds>0).ToArray(); max=rules.Length; got=rules.Count(a=>awarded.ContainsKey(a.id)&&awarded[a.id]>0); }
                return new EvaluationSection {name=name,possiblePoints=max,earnedPoints=got,scorePercent=max==0?0:100.0*got/max,measured=max>0};
            }).ToArray();
            result=new MedicalDebrief {caseId=definition.id,caseName=definition.name,createdUtc=DateTime.UtcNow.ToString("O"),seed=seed,scenarioVersion=definition.version,environment=definition.environment,witness=witness,timingBasis=definition.timingBasis,
                medicalValidationStatus=definition.medicalValidationStatus,clinicallyApproved=definition.medicalValidationStatus=="APPROVED",technicalDemo=false,physicalCprMeasured=false,
                scorePercent=score,durationSeconds=time,finalState=patient.consciousness,outcome=outcome,branchId=critical.Count>0?"critical-errors":branch==null?"incomplete":branch.id,
                omittedActions=missing.Select(a=>a.id).ToArray(),correctActions=accepted.Keys.ToArray(),incorrectActions=log.Where(l=>l.section!="Timeline"&&l.disposition!="Accepted"&&l.disposition!="Late").Select(l=>l.id).ToArray(),
                criticalErrors=critical.ToArray(),errors=log.Count(l=>l.section!="Timeline"&&l.disposition!="Accepted"&&l.disposition!="Late"),lateActions=log.Count(l=>l.disposition=="Late"),
                actions=log.Where(l=>l.section!="Timeline").Select(l=>l.Copy()).ToArray(),timeline=log.Select(l=>l.Copy()).ToArray(),initialPatient=initial.Copy(),finalPatient=patient.Copy(),sections=sections,
                recommendations=definition.debrief.Concat(new[]{branch==null?"Revisar acciones omitidas y solicitar relevo.":branch.feedback,"RCP: se evalúa secuencia declarada; profundidad, ritmo y retroceso no medidos.",definition.timingBasis}).ToArray(),medicalReferences=references,sourceUrls=references.Select(r=>r.url).ToArray(),
                timeToCpr=FirstTime("StartCPR"),timeToAed=FirstTime("AttachAEDPads")};
            LastFeedback="Resultado simulado: "+outcome+" · "+score.ToString("0")+"/100";
            if(HasClinicalFeatures) AttachClinicalContext(result);
            return CopyResult(result);
        }
        double FirstTime(string action) { var times=definition.actions.Where(a=>a.action==action&&accepted.ContainsKey(a.id)).Select(a=>accepted[a.id]).ToArray(); return times.Length==0?-1:times.Min(); }
        static MedicalDebrief CopyResult(MedicalDebrief r)
        {
            // JSON-facing DTO is detached, including nested arrays and patient snapshots.
            var copy=new MedicalDebrief();
            copy.schemaVersion=r.schemaVersion; copy.seed=r.seed; copy.errors=r.errors; copy.lateActions=r.lateActions;
            copy.caseId=r.caseId; copy.caseName=r.caseName; copy.createdUtc=r.createdUtc; copy.finalState=r.finalState; copy.outcome=r.outcome; copy.branchId=r.branchId; copy.medicalValidationStatus=r.medicalValidationStatus; copy.scenarioVersion=r.scenarioVersion; copy.environment=r.environment; copy.witness=r.witness; copy.timingBasis=r.timingBasis;
            copy.clinicallyApproved=r.clinicallyApproved; copy.technicalDemo=r.technicalDemo; copy.physicalCprMeasured=r.physicalCprMeasured; copy.scorePercent=r.scorePercent; copy.durationSeconds=r.durationSeconds; copy.timeToCpr=r.timeToCpr; copy.timeToAed=r.timeToAed;
            copy.omittedActions=(string[])r.omittedActions.Clone(); copy.correctActions=(string[])r.correctActions.Clone(); copy.incorrectActions=(string[])r.incorrectActions.Clone(); copy.criticalErrors=(string[])r.criticalErrors.Clone(); copy.recommendations=(string[])r.recommendations.Clone(); copy.sourceUrls=(string[])r.sourceUrls.Clone();
            copy.actions=r.actions.Select(a=>a.Copy()).ToArray(); copy.timeline=r.timeline.Select(a=>a.Copy()).ToArray(); copy.initialPatient=r.initialPatient.Copy(); copy.finalPatient=r.finalPatient.Copy();
            copy.sections=r.sections.Select(s=>new EvaluationSection {name=s.name,scorePercent=s.scorePercent,earnedPoints=s.earnedPoints,possiblePoints=s.possiblePoints,measured=s.measured}).ToArray();
            copy.medicalReferences=r.medicalReferences.Select(s=>new MedicalReference {id=s.id,organization=s.organization,title=s.title,year=s.year,url=s.url,accessedAt=s.accessedAt}).ToArray();
            copy.objectiveBasedEvaluation=r.objectiveBasedEvaluation;copy.attemptId=r.attemptId;copy.clinicalSpecVersion=r.clinicalSpecVersion;copy.trainingProfile=r.trainingProfile;copy.buildVersion=r.buildVersion;
            copy.clinicalEvents=r.clinicalEvents.Select(x=>x.Copy()).ToArray();copy.objectives=r.objectives.Select(x=>x.Copy()).ToArray();
            copy.measurements=r.measurements.Select(x=>(MeasurementObservation)x.Copy()).ToArray();copy.interviews=r.interviews.Select(x=>(InterviewObservation)x.Copy()).ToArray();copy.physicalObservations=r.physicalObservations.Select(x=>(PhysicalObservation)x.Copy()).ToArray();return copy;
        }
        sealed class SeedRandom
        {
            uint state;
            public SeedRandom(int seed) { state=unchecked((uint)seed); if(state==0) state=0x9e3779b9; }
            public double Next() { state^=state<<13; state^=state>>17; state^=state<<5; return state/4294967296.0; }
            public double Spread(double span) { return (Next()*2-1)*span; }
        }
    }
}
