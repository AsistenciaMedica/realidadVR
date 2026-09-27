using System;
using System.Collections.Generic;
using System.Linq;

namespace EmergencyVR.Medical
{
    public sealed partial class MedicalScenarioRuntime
    {
        ClinicalStateMachine clinicalMachine;
        TrainingProfileDefinition activeProfile;
        string runtimeBuildVersion, attemptId;
        double respiratoryPhase;
        ClinicalPosition clinicalPosition;
        PhysicalPosition effectivePosition, requestedPosition;
        bool positionPending;
        HelpRequestState helpState;
        ObservedPatientDataStore observations;
        readonly List<ClinicalEvent> clinicalEvents=new List<ClinicalEvent>();
        readonly Dictionary<string,LearningObjectiveProgress> objectives=new Dictionary<string,LearningObjectiveProgress>();
        readonly Dictionary<string,List<ClinicalEvent>> outcomeEvidence=new Dictionary<string,List<ClinicalEvent>>();
        readonly HashSet<string> guidedEvidence=new HashSet<string>();
        readonly HashSet<string> clinicalSignals=new HashSet<string>();
        public bool IsPaused { get; private set; }
        public string AttemptId { get { return attemptId; } }
        public ScenarioCapabilities Capabilities { get { return definition.clinicalV2==null?new ScenarioCapabilities():definition.clinicalV2.capabilities.Copy(); } }
        public TrainingProfileDefinition TrainingProfile { get { return activeProfile==null?null:activeProfile.Copy(); } }
        public PatientClinicalState ClinicalState { get { return !HasClinicalFeatures?null:new PatientClinicalState(attemptId,definition.id,clinicalMachine==null?null:clinicalMachine.State,patient,elapsed,clinicalPosition,effectivePosition,requestedPosition,respiratoryPhase); } }
        public ObservedPatientDataStore Observations { get { return observations; } }
        public IReadOnlyList<ClinicalEvent> ClinicalEvents { get { return Array.AsReadOnly(clinicalEvents.Select(x=>x.Copy()).ToArray()); } }
        public IReadOnlyList<LearningObjectiveProgress> ObjectiveProgress { get { return Array.AsReadOnly(objectives.Values.Select(x=>x.Copy()).ToArray()); } }
        public HelpRequestState HelpState { get { return helpState; } }
        public PhysicalPosition RequestedPosition { get { return requestedPosition; } }
        bool HasClinicalFeatures
        {
            get { var c=definition.clinicalV2==null?null:definition.clinicalV2.capabilities;return c!=null&&(c.usesClinicalStateMachine||c.usesObservedPatientData||c.usesPhysicalPositionConfirmation||c.usesIntentDialogue||c.usesAdvancedMeasurements||c.usesObjectiveBasedEvaluation); }
        }
        void InitializeClinicalV2(string buildVersion,TrainingProfileId? profile)
        {
            runtimeBuildVersion=buildVersion??"";attemptId=Guid.NewGuid().ToString("N");observations=new ObservedPatientDataStore(attemptId);
            if(!HasClinicalFeatures) return;
            var v2=definition.clinicalV2;
            activeProfile=profile.HasValue?v2.trainingProfiles.SingleOrDefault(x=>x.id==profile.Value):v2.trainingProfiles.SingleOrDefault(x=>x.ExportId==v2.metadata.trainingProfile);
            if(activeProfile==null) throw new ArgumentException("The selected profile is not configured for this scenario.");
            patient=definition.initialState.Copy();patient.age=v2.patientProfile.age;patient.sex=v2.patientProfile.sex;
            if(v2.capabilities.usesClinicalStateMachine)
            {
                clinicalMachine=new ClinicalStateMachine(v2,patient);clinicalPosition=clinicalMachine.State.position;
                effectivePosition=(PhysicalPosition)clinicalPosition;patient.position=LegacyPosition(clinicalPosition);
            }
            else
            {
                clinicalPosition=patient.position=="seated"?ClinicalPosition.SeatedSupported:patient.position=="standing"?ClinicalPosition.Standing:ClinicalPosition.Supine;
                effectivePosition=(PhysicalPosition)clinicalPosition;
            }
            if(v2.capabilities.usesObjectiveBasedEvaluation)
                foreach(var objective in v2.learningObjectives) objectives[objective.id]=new LearningObjectiveProgress {objectiveId=objective.id,name=objective.name,result=ObjectiveResult.NotEvaluated};
            Emit("ScenarioStarted","Runtime","Started","Valid");
        }
        public void SetPaused(bool paused) { if(!IsFinished) IsPaused=paused; }
        public void Reset()
        {
            var selected=activeProfile==null?(TrainingProfileId?)null:activeProfile.id;
            patient=initial.Copy();elapsed=0;penalties=0;result=null;IsPaused=false;
            accepted.Clear();awarded.Clear();processedEvents.Clear();firedEvents.Clear();log.Clear();critical.Clear();
            clinicalEvents.Clear();objectives.Clear();outcomeEvidence.Clear();guidedEvidence.Clear();clinicalSignals.Clear();
            objectiveGuidance.Clear();
            clinicalMachine=null;activeProfile=null;respiratoryPhase=0;clinicalPosition=ClinicalPosition.Unknown;effectivePosition=PhysicalPosition.Unknown;
            requestedPosition=PhysicalPosition.Unknown;positionPending=false;helpState=HelpRequestState.None;
            InitializeClinicalV2(runtimeBuildVersion,selected);LastFeedback="Nuevo intento iniciado.";
        }
        bool Ready(double time,string suppliedAttempt)
        {
            if(!HasClinicalFeatures||IsFinished||IsPaused||(!string.IsNullOrEmpty(suppliedAttempt)&&suppliedAttempt!=attemptId)) return false;
            Tick(time);return true;
        }
        bool Allows(string action) { return activeProfile!=null&&activeProfile.allowedActions.Contains(action); }
        static string LegacyPosition(ClinicalPosition position) { switch(position) { case ClinicalPosition.SeatedSupported:return "seated";case ClinicalPosition.Supine:return "supine";case ClinicalPosition.Standing:return "standing";default:return "supine"; } }
        void TickClinicalV2(double time)
        {
            EvaluateTransitions();
            var iterations=0;
            while(elapsed<time)
            {
                if(++iterations>1024) throw new InvalidOperationException("Clinical transition cycle exceeded its safety bound.");
                var next=clinicalMachine.NextTimedBoundary(elapsed,time);
                clinicalMachine.Advance(patient,elapsed,next,ref respiratoryPhase);elapsed=next;EvaluateTransitions();
            }
        }
        void EvaluateTransitions()
        {
            if(clinicalMachine==null) return;
            for(var n=0;n<definition.clinicalV2.clinicalStates.Length+1;n++)
            {
                var transition=clinicalMachine.Eligible(elapsed,effectivePosition,clinicalSignals);
                if(transition==null) return;
                var previous=clinicalMachine.State.id;
                clinicalMachine.Enter(clinicalMachine.Find(transition.to),patient,elapsed,transition.optionalTrajectory);
                clinicalSignals.Clear();
                Emit("ClinicalStateChanged","Runtime",transition.to,"Valid",new ClinicalEventMetadata {key="from",value=previous},new ClinicalEventMetadata {key="transitionId",value=transition.id});
                if(positionPending&&!definition.clinicalV2.transitions.Any(x=>x.from==transition.to))
                {
                    Emit("PositionAssistanceCancelled","Runtime","ClinicalStateEnded","Cancelled");
                    positionPending=false;requestedPosition=PhysicalPosition.Unknown;
                }
            }
            throw new InvalidOperationException("Clinical transitions must not form an immediate cycle.");
        }
        ClinicalEvent Emit(string type,string source,string outcome,string quality,params ClinicalEventMetadata[] metadata)
        {
            var v2=definition.clinicalV2;
            var entry=new ClinicalEvent {eventId=attemptId+":"+(clinicalEvents.Count+1),attemptId=attemptId,simulationTime=elapsed,scenarioId=definition.id,
                scenarioVersion=v2.metadata.scenarioVersion,clinicalSpecVersion=v2.metadata.clinicalSpecVersion,trainingProfile=activeProfile.ExportId,buildVersion=runtimeBuildVersion,
                clinicalStateId=clinicalMachine==null?"":clinicalMachine.State.id,eventType=type,source=source??"",result=outcome??"",quality=quality??"",metadata=metadata.Select(x=>x.Copy()).ToArray()};
            clinicalEvents.Add(entry);return entry;
        }
        public bool RequestPosition(PhysicalPosition position,double time,string source="Player",string attemptId=null)
        {
            if(!Ready(time,attemptId)||!Capabilities.usesPhysicalPositionConfirmation||!Allows("AssistPatient")||positionPending||position==PhysicalPosition.Unknown||!Enum.IsDefined(typeof(PhysicalPosition),position)) return false;
            if(clinicalMachine!=null)
            {
                var available=definition.clinicalV2.transitions.Where(x=>x.from==clinicalMachine.State.id).ToArray();
                if(available.Length==0) return false;
                if(position==PhysicalPosition.Standing&&!available.Any(x=>x.conditions.Any(c=>(c.type=="PhysicalPosition"&&c.value==PhysicalPosition.Standing.ToString())||(c.type=="Signal"&&c.value=="UnsafeRiseAttempt")))) return false;
            }
            requestedPosition=position;positionPending=true;
            Emit("PositionAssistanceStarted",source,position.ToString(),"Pending");return true;
        }
        public bool CancelPosition(double time,string reason,string source="Presentation",string attemptId=null)
        {
            if(!Ready(time,attemptId)||!positionPending) return false;
            Emit("PositionAssistanceCancelled",source,reason,"Cancelled");requestedPosition=PhysicalPosition.Unknown;positionPending=false;return true;
        }
        public bool ConfirmPhysicalPosition(PhysicalPosition position,double time,string source="Presentation",bool valid=true,string attemptId=null)
        {
            if(!Ready(time,attemptId)||!Capabilities.usesPhysicalPositionConfirmation||!positionPending||position!=requestedPosition) return false;
            if(!valid) { CancelPosition(time,"PhysicalValidationFailed",source,attemptId);return false; }
            effectivePosition=position;clinicalPosition=(ClinicalPosition)position;patient.position=LegacyPosition(clinicalPosition);
            positionPending=false;requestedPosition=PhysicalPosition.Unknown;
            var entry=Emit("PhysicalPositionConfirmed",source,position.ToString(),"Valid");
            if(position==PhysicalPosition.Supine||position==PhysicalPosition.SeatedSupported) RegisterOutcome("AssistedToSafePosition",entry,false);
            clinicalSignals.Add("PhysicalPositionConfirmed");
            if(position==PhysicalPosition.Standing) clinicalSignals.Add("UnsafeRiseAttempt");
            EvaluateTransitions();clinicalSignals.Clear();return true;
        }
        public DialogueResponseDefinition Ask(DialogueIntent intent,double time,string source="Player",bool guided=false,string attemptId=null)
        {
            if(!Ready(time,attemptId)||!Capabilities.usesIntentDialogue||!Allows("TalkToPatient")) return null;
            var state=ClinicalState;
            var responses=definition.clinicalV2.dialogue.Where(x=>x.intent==intent).SelectMany(x=>x.responses);
            var response=responses.Where(x=>(!x.requiresCanSpeak||state.CanSpeak)&&(x.allowedClinicalStates.Length==0||x.allowedClinicalStates.Contains(state.ClinicalStateId))&&elapsed-(clinicalMachine==null?0:clinicalMachine.EnteredAt)>=x.minStateSeconds)
                .OrderByDescending(x=>x.minStateSeconds).FirstOrDefault();
            if(response==null) return null;
            Emit(intent==DialogueIntent.GREETING?"PatientContactStarted":"AskedSymptom",source,intent.ToString(),"Valid");
            if(response.marksObservation&&Capabilities.usesObservedPatientData)
                AddObservation(new InterviewObservation {intent=intent,responseId=response.id,text=response.text,subtitle=string.IsNullOrEmpty(response.subtitle)?response.text:response.subtitle,type="Interview:"+intent,source=source,quality="Valid",valid=true});
            var entry=Emit("ResponseObserved",source,response.id,"Valid");
            if(guided) guidedEvidence.Add(entry.eventId);
            if(observations.Interviews.Any(x=>x.intent==DialogueIntent.MAIN_SYMPTOM)&&observations.Interviews.Any(x=>x.intent==DialogueIntent.ONSET))
                RegisterOutcome("RelevantInformationGathered",entry,guided||clinicalEvents.Any(x=>x.eventType=="ResponseObserved"&&guidedEvidence.Contains(x.eventId)));
            LastFeedback=response.subtitle??response.text;return response.Copy();
        }
        void AddObservation(ObservationRecord record)
        {
            record.observationId=attemptId+":observation:"+(observations.Count+1);record.attemptId=attemptId;record.simulationTime=elapsed;record.clinicalStateId=clinicalMachine==null?"":clinicalMachine.State.id;
            observations.Add(record);Emit("ObservationAdded",record.source,record.type,record.quality,new ClinicalEventMetadata {key="observationId",value=record.observationId});
        }
        public bool ObservePhysical(string type,double time,string source="Player",bool guided=false,string attemptId=null)
        {
            if(!Ready(time,attemptId)||!Capabilities.usesObservedPatientData) return false;
            string description, outcome, eventType;
            bool hasComparison=observations.Physical.Any(o=>(o.type=="PatientReassessed"||o.type=="Reassessment")&&o.simulationTime<elapsed)||
                (observations.Physical.Any(o=>o.type=="PatientResponsive"&&o.simulationTime<elapsed)&&
                 observations.Physical.Any(o=>o.type=="BreathingNormal"&&o.simulationTime<elapsed));
            switch(type)
            {
                case "PatientResponsive":
                    if(!Allows("AssessResponsiveness")||patient.consciousness=="Unresponsive") return false;
                    description="El paciente responde.";outcome="ResponsivenessAssessed";eventType="ResponsivenessAssessed";break;
                case "BreathingNormal":
                    if(!Allows("ObserveBreathing")||patient.respiration!="normal"||patient.respiratoryRate<=0) return false;
                    description="Respiración espontánea observada.";outcome="BreathingObserved";eventType="BreathingObserved";break;
                case "PatientPale":
                    if(!Allows("AssessResponsiveness")||!patient.flags.Any(x=>x.Equals("pallor",StringComparison.OrdinalIgnoreCase)||x.Equals("pale",StringComparison.OrdinalIgnoreCase))) return false;
                    description="Palidez observada.";outcome="";eventType="PhysicalObservationRecorded";break;
                case "PatientUnstableWhenStanding":
                    if(!Allows("ReassessPatient")||effectivePosition!=PhysicalPosition.Standing) return false;
                    description="Inestabilidad al incorporarse observada.";outcome="";eventType="PhysicalObservationRecorded";break;
                case "Reassessment":case "PatientReassessed":
                    if(!Allows("ReassessPatient")) return false;
                    description=patient.consciousness!="Unresponsive"&&patient.respiratoryRate>0?
                        "El paciente sigue respondiendo y mantiene respiración espontánea.":"Se ha comprobado de nuevo la respuesta y la respiración.";
                    outcome=hasComparison?"PatientReassessed":"";eventType="PatientReassessed";break;
                default:return false;
            }
            AddObservation(new PhysicalObservation {type=type,description=description,source=source,quality="Valid",valid=true});
            var entry=Emit(eventType,source,type,"Valid");if(outcome.Length>0) RegisterOutcome(outcome,entry,guided);return true;
        }
        public bool RecordMeasurement(string type,bool valid,string quality,double time,string source,string attemptId=null)
        {
            if(!Ready(time,attemptId)||!Capabilities.usesAdvancedMeasurements||!Capabilities.usesObservedPatientData||!activeProfile.allowedMeasurements.Contains(type)) return false;
            if(type!="BloodPressure"&&type!="SpO2") return false;
            if(valid&&quality!="Valid") return false;
            var record=new MeasurementObservation {type=type,source=source,quality=valid?"Valid":string.IsNullOrEmpty(quality)?"InvalidPlacement":quality,valid=valid,hasValue=valid,unit=type=="BloodPressure"?"mmHg":"%"};
            if(valid) { if(type=="BloodPressure") { record.systolic=patient.systolic;record.diastolic=patient.diastolic; } else record.value=patient.spo2; }
            AddObservation(record);Emit(valid?"MeasurementCompleted":"MeasurementFailed",source,type,record.quality);return true;
        }
        public bool RequestHelp(double time,bool delegated=false,string source="Player",bool guided=false,string attemptId=null)
        {
            if(!Ready(time,attemptId)||!Allows(delegated?"DelegateHelp":"RequestHelp")||helpState>=HelpRequestState.Confirmed) return false;
            helpState=delegated?HelpRequestState.Delegated:HelpRequestState.Requested;
            var entry=Emit(delegated?"HelpDelegated":"HelpRequested",source,helpState.ToString(),"Pending");if(guided) guidedEvidence.Add(entry.eventId);return true;
        }
        public bool ConfirmHelp(double time,string source="Simulation",string attemptId=null)
        {
            if(!Ready(time,attemptId)||(helpState!=HelpRequestState.Requested&&helpState!=HelpRequestState.Delegated)) return false;
            helpState=HelpRequestState.Confirmed;var entry=Emit("HelpConfirmed",source,"Confirmed","Valid");
            RegisterOutcome("HelpConfirmed",entry,clinicalEvents.Any(x=>(x.eventType=="HelpRequested"||x.eventType=="HelpDelegated")&&guidedEvidence.Contains(x.eventId)));return true;
        }
        public bool SetHandoverAvailable(double time,string source="Simulation",string attemptId=null)
        {
            if(!Ready(time,attemptId)||helpState!=HelpRequestState.Confirmed) return false;
            helpState=HelpRequestState.HandoverAvailable;Emit("HandoverAvailable",source,"Available","Valid");return true;
        }
        public bool CompleteHandover(double time,string source="Player",bool guided=false,string attemptId=null)
        {
            if(!Ready(time,attemptId)||!Allows("PerformHandover")||(helpState!=HelpRequestState.Confirmed&&helpState!=HelpRequestState.HandoverAvailable)) return false;
            Emit("HandoverStarted",source,"Started","Valid");helpState=HelpRequestState.Completed;
            var entry=Emit("HandoverCompleted",source,"Completed","Valid");RegisterOutcome("HandoverCompleted",entry,guided);
            clinicalSignals.Add("HandoverCompleted");EvaluateTransitions();clinicalSignals.Clear();return true;
        }
        public bool ReportClinicalSignal(string signal,double time,string source="Presentation",string attemptId=null)
        {
            if(!Ready(time,attemptId)||clinicalMachine==null||signal=="UnsafeRiseAttempt"||signal=="PhysicalPositionConfirmed"||signal=="HandoverCompleted") return false;
            if(!definition.clinicalV2.transitions.Any(x=>x.from==clinicalMachine.State.id&&x.conditions.Any(c=>c.type=="Signal"&&c.value==signal))) return false;
            Emit("ClinicalSignalReceived",source,signal,"Valid");clinicalSignals.Add(signal);EvaluateTransitions();clinicalSignals.Clear();return true;
        }
        public bool ReportOutcome(string outcome,double time,string source="Interaction",bool guided=false,string attemptId=null)
        {
            if(!Ready(time,attemptId)||!Capabilities.usesObjectiveBasedEvaluation) return false;
            // Physical validators can report equivalent protection outcomes, not assert interview/help completion.
            if(!new[]{"ProtectedFromFall","MaintainedSafeSupportedPosition","AssistedToSafePosition"}.Contains(outcome)||!Allows("AssistPatient")||positionPending||(effectivePosition!=PhysicalPosition.Supine&&effectivePosition!=PhysicalPosition.SeatedSupported)) return false;
            if(!definition.clinicalV2.acceptableAlternatives.Any(x=>x.equivalentOutcomes.Contains(outcome))&&!definition.clinicalV2.learningObjectives.Any(x=>x.requiredOutcomes.Contains(outcome)||x.anyOutcomes.Contains(outcome))) return false;
            var entry=Emit("OutcomeObserved",source,outcome,"Valid");RegisterOutcome(outcome,entry,guided);return true;
        }
        void RegisterOutcome(string outcome,ClinicalEvent evidence,bool guided)
        {
            if(!Capabilities.usesObjectiveBasedEvaluation) return;
            if(!outcomeEvidence.TryGetValue(outcome,out var list)) outcomeEvidence[outcome]=list=new List<ClinicalEvent>();
            list.Add(evidence);if(guided) guidedEvidence.Add(evidence.eventId);
            foreach(var objective in definition.clinicalV2.learningObjectives)
            {
                var progress=objectives[objective.id];
                if(progress.result==ObjectiveResult.AchievedIndependently||progress.result==ObjectiveResult.AchievedWithGuidance) continue;
                var alternatives=definition.clinicalV2.acceptableAlternatives.Where(x=>x.objectiveId==objective.id).SelectMany(x=>x.equivalentOutcomes).ToArray();
                var explicitRequirements=objective.requiredOutcomes.Length+objective.anyOutcomes.Length>0;
                var normal=explicitRequirements&&objective.requiredOutcomes.All(outcomeEvidence.ContainsKey)&&(objective.anyOutcomes.Length==0||objective.anyOutcomes.Any(outcomeEvidence.ContainsKey));
                var equivalent=alternatives.FirstOrDefault(outcomeEvidence.ContainsKey);
                if(!normal&&equivalent==null) continue;
                var names=equivalent!=null?new[]{equivalent}:objective.requiredOutcomes.Concat(objective.anyOutcomes.Where(outcomeEvidence.ContainsKey).Take(1)).ToArray();
                var entries=names.Select(x=>outcomeEvidence[x].First()).ToArray();
                bool assisted=objectiveGuidance.TryGetValue(objective.id,out var guidanceId);
                progress.evidenceEventIds=entries.Select(x=>x.eventId).Concat(assisted?new[]{guidanceId}:Array.Empty<string>()).Distinct().ToArray();
                progress.result=assisted||entries.Any(x=>guidedEvidence.Contains(x.eventId))?ObjectiveResult.AchievedWithGuidance:ObjectiveResult.AchievedIndependently;
                Emit("LearningObjectiveAchieved","Evaluation",objective.id,"Valid",new ClinicalEventMetadata {key="result",value=progress.result.ToString()});
            }
        }
        string SubmitClinicalV2(string id,double time)
        {
            Tick(time);var action=definition.actions.FirstOrDefault(x=>x.id==id);
            if(action==null) return LogClinicalAction(id,null,"Unknown","Acción ajena a este caso.");
            var semantic=string.IsNullOrEmpty(action.semanticAction)?action.action:action.semanticAction;
            // A disabled presentation/observation capability retains its original action path.
            if((semantic=="AssistPatient"&&!Capabilities.usesPhysicalPositionConfirmation)||
               ((semantic=="AssessResponsiveness"||semantic=="ObserveBreathing"||semantic=="ReassessPatient")&&!Capabilities.usesObservedPatientData)||
               (semantic=="TalkToPatient"&&!Capabilities.usesIntentDialogue)) return SubmitLegacy(id,time);
            if(!Allows(semantic)) return LogClinicalAction(id,action,"NotAllowed","Acción fuera del perfil de este entrenamiento.");
            if(accepted.ContainsKey(id)&&action.repeatPolicy==ActionRepeatPolicy.LegacySingleUse) return LogClinicalAction(id,action,"Duplicate","Acción ya registrada.");
            bool success;
            switch(semantic)
            {
                case "TalkToPatient":Emit("PatientContactStarted","Player","Contact","Valid");success=true;break;
                case "AssessResponsiveness":success=ObservePhysical("PatientResponsive",time);break;
                case "ObserveBreathing":success=ObservePhysical("BreathingNormal",time);break;
                case "AssistPatient":success=RequestPosition(PhysicalPosition.Supine,time);break;
                case "RequestHelp":success=RequestHelp(time);break;
                case "DelegateHelp":success=RequestHelp(time,true);break;
                case "ReassessPatient":success=ObservePhysical("PatientReassessed",time);break;
                case "PerformHandover":success=CompleteHandover(time);break;
                default:return LogClinicalAction(id,action,"NotAllowed","La acción no tiene un contrato semántico habilitado.");
            }
            if(!success) return LogClinicalAction(id,action,"Unavailable","La acción no está disponible en este contexto.");
            if(!accepted.ContainsKey(id)) { accepted.Add(id,time);awarded[id]=0; }
            return LogClinicalAction(id,action,"Accepted",action.feedback);
        }
        string LogClinicalAction(string id,ActionRule rule,string disposition,string feedback)
        {
            LastFeedback=feedback;log.Add(new MedicalLogEntry {id=id,actionId=id,elapsedSeconds=elapsed,disposition=disposition,message=feedback,section=rule==null?"Decision Making":library[rule.action].section,patient=patient.Copy()});
            Emit("ActionSubmitted","Player",disposition,disposition=="Accepted"?"Valid":"Unavailable",new ClinicalEventMetadata {key="actionId",value=id});return disposition;
        }
        void AttachClinicalContext(MedicalDebrief report)
        {
            report.schemaVersion=4;report.attemptId=attemptId;report.scenarioVersion=definition.clinicalV2.metadata.scenarioVersion;
            report.clinicalSpecVersion=definition.clinicalV2.metadata.clinicalSpecVersion;report.trainingProfile=activeProfile.ExportId;report.buildVersion=runtimeBuildVersion;
            report.clinicalEvents=clinicalEvents.Select(x=>x.Copy()).ToArray();report.objectives=objectives.Values.Select(x=>x.Copy()).ToArray();
            report.measurements=observations.Measurements.ToArray();report.interviews=observations.Interviews.ToArray();report.physicalObservations=observations.Physical.ToArray();
        }
        MedicalDebrief FinishClinicalV2()
        {
            foreach(var objective in objectives.Values) if(objective.result==ObjectiveResult.NotEvaluated) objective.result=ObjectiveResult.NotDemonstrated;
            Emit("ScenarioFinished","Runtime","TrainingEnded","Valid");
            var v2=definition.clinicalV2;
            result=new MedicalDebrief {schemaVersion=4,caseId=definition.id,caseName=definition.name,createdUtc=DateTime.UtcNow.ToString("O"),seed=seed,
                scenarioVersion=v2.metadata.scenarioVersion,clinicalSpecVersion=v2.metadata.clinicalSpecVersion,trainingProfile=activeProfile.ExportId,buildVersion=runtimeBuildVersion,attemptId=attemptId,
                environment=definition.environment,witness=witness,timingBasis=definition.timingBasis,medicalValidationStatus=definition.medicalValidationStatus,clinicallyApproved=false,objectiveBasedEvaluation=true,
                durationSeconds=elapsed,finalState=clinicalMachine==null?patient.consciousness:clinicalMachine.State.id,outcome="FORMATIVE_REVIEW",branchId="objective-review",
                omittedActions=Array.Empty<string>(),correctActions=accepted.Keys.ToArray(),incorrectActions=log.Where(x=>x.disposition!="Accepted").Select(x=>x.id).ToArray(),criticalErrors=Array.Empty<string>(),
                recommendations=definition.debrief.ToArray(),sourceUrls=references.Select(x=>x.url).ToArray(),medicalReferences=references,
                actions=log.Select(x=>x.Copy()).ToArray(),timeline=log.Select(x=>x.Copy()).ToArray(),initialPatient=initial.Copy(),finalPatient=patient.Copy(),sections=Array.Empty<EvaluationSection>(),
                clinicalEvents=clinicalEvents.Select(x=>x.Copy()).ToArray(),objectives=objectives.Values.Select(x=>x.Copy()).ToArray(),
                measurements=observations.Measurements.ToArray(),interviews=observations.Interviews.ToArray(),physicalObservations=observations.Physical.ToArray()};
            LastFeedback="Entrenamiento finalizado. Revisa las evidencias de los objetivos.";return CopyResult(result);
        }
    }
}
