using System;
using System.Linq;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class MedicalReference
    {
        public string id, organization, title, year, url, accessedAt;
    }
    [Serializable] public sealed class MedicalAction
    {
        public string id, label, section, description;
    }
    [Serializable] public sealed class MedicalLibrary
    {
        public int schemaVersion;
        public MedicalAction[] actions;
        public MedicalReference[] references;
        public MedicalScenarioDefinition[] scenarios;
        public void Validate()
        {
            if(schemaVersion!=3 || actions==null || references==null || scenarios==null || scenarios.Length==0) throw new ArgumentException("Invalid medical library version/content.");
            if(actions.Select(a=>a.id).Distinct().Count()!=actions.Length || scenarios.Select(s=>s.id).Distinct().Count()!=scenarios.Length || references.Select(r=>r.id).Distinct().Count()!=references.Length) throw new ArgumentException("Duplicate library ID.");
            foreach(var r in references) if(string.IsNullOrWhiteSpace(r.organization) || string.IsNullOrWhiteSpace(r.title) || string.IsNullOrWhiteSpace(r.year) || !Uri.IsWellFormedUriString(r.url,UriKind.Absolute) || !r.url.StartsWith("https://") || !DateTime.TryParse(r.accessedAt,out _)) throw new ArgumentException("Invalid medical reference.");
            foreach(var s in scenarios) s.Validate(this);
        }
    }
    [Serializable] public sealed class PatientSnapshot
    {
        public string consciousness="Conscious", respiration="normal", circulation="normal", sex="female", appearance="", dialogue="", position="supine";
        public int age=40;
        public double heartRate=78, systolic=120, diastolic=80, spo2=97, glucose=5, respiratoryRate=16, temperature=37, pain;
        public bool canSwallow=true, shockAdvised;
        public string[] flags=Array.Empty<string>();
        public PatientSnapshot Copy() { var x=(PatientSnapshot)MemberwiseClone(); x.flags=(string[])flags.Clone(); return x; }
        public void Validate()
        {
            if(!new[]{"Conscious","Confused","Drowsy","Unresponsive"}.Contains(consciousness) || !new[]{"normal","fast","slow","agonal","absent"}.Contains(respiration) || !new[]{"normal","tachycardia","bradycardia","hypotension","hypertension","pulseless"}.Contains(circulation)) throw new ArgumentException("Invalid patient state.");
            if(age<18 || age>100 || flags==null) throw new ArgumentException("Adult scenarios only; flags required.");
            Bounds(heartRate,0,250); Bounds(systolic,0,260); Bounds(diastolic,0,180); Bounds(spo2,0,100); Bounds(glucose,0,40); Bounds(respiratoryRate,0,65); Bounds(temperature,25,43); Bounds(pain,0,10);
            if(diastolic>systolic || (consciousness=="Unresponsive" && canSwallow)) throw new ArgumentException("Inconsistent patient data.");
        }
        internal static void Bounds(double n,double low,double high) { if(double.IsNaN(n)||double.IsInfinity(n)||n<low||n>high) throw new ArgumentException("Invalid numeric range."); }
    }
    [Serializable] public sealed class PatientEffect
    {
        public string consciousness, respiration, circulation, dialogue, position;
        public string[] addFlags=Array.Empty<string>(), removeFlags=Array.Empty<string>();
        public double spo2Delta, heartRateDelta, glucoseDelta, systolicDelta, diastolicDelta, respiratoryRateDelta, temperatureDelta, painDelta;
        public bool setSwallow, canSwallow, setShockAdvised, shockAdvised;
        public PatientEffect Copy() { var x=(PatientEffect)MemberwiseClone(); x.addFlags=(string[])addFlags.Clone(); x.removeFlags=(string[])removeFlags.Clone(); return x; }
        public void Apply(PatientSnapshot p)
        {
            if(!string.IsNullOrEmpty(consciousness)) p.consciousness=consciousness;
            if(!string.IsNullOrEmpty(respiration)) p.respiration=respiration;
            if(!string.IsNullOrEmpty(circulation)) p.circulation=circulation;
            if(!string.IsNullOrEmpty(dialogue)) p.dialogue=dialogue;
            if(!string.IsNullOrEmpty(position)) p.position=position;
            if(setSwallow) p.canSwallow=canSwallow;
            if(p.consciousness=="Unresponsive") p.canSwallow=false;
            if(setShockAdvised) p.shockAdvised=shockAdvised;
            p.spo2=Clamp(p.spo2+spo2Delta,0,100); p.heartRate=Clamp(p.heartRate+heartRateDelta,0,250);
            p.glucose=Clamp(p.glucose+glucoseDelta,0,40); p.systolic=Clamp(p.systolic+systolicDelta,0,260); p.diastolic=Clamp(p.diastolic+diastolicDelta,0,p.systolic);
            p.respiratoryRate=Clamp(p.respiratoryRate+respiratoryRateDelta,0,65); p.temperature=Clamp(p.temperature+temperatureDelta,25,43); p.pain=Clamp(p.pain+painDelta,0,10);
            p.flags=p.flags.Except(removeFlags).Union(addFlags).ToArray(); p.Validate();
        }
        static double Clamp(double n,double min,double max) { return Math.Max(min,Math.Min(max,n)); }
    }
    [Serializable] public sealed class ActionRule
    {
        public string id, action, kind="required", feedback="Acción registrada.", guard="", anchorAction="";
        public string semanticAction="";
        public ActionRepeatPolicy repeatPolicy=ActionRepeatPolicy.LegacySingleUse;
        public string[] prerequisites=Array.Empty<string>();
        public int points=10, penalty=10;
        public bool critical;
        public double earliestSeconds, deadlineSeconds;
        public PatientEffect effect=new PatientEffect();
        public ActionRule Copy() { var x=(ActionRule)MemberwiseClone(); x.prerequisites=(string[])prerequisites.Clone(); x.effect=effect.Copy(); return x; }
    }
    [Serializable] public sealed class PatientTimelineEvent
    {
        public string id, kind, message, anchorAction="";
        public double afterSeconds;
        public string[] requiresActions=Array.Empty<string>(), unlessActions=Array.Empty<string>();
        public PatientEffect effect=new PatientEffect();
        public PatientTimelineEvent Copy() { var x=(PatientTimelineEvent)MemberwiseClone(); x.requiresActions=(string[])requiresActions.Clone(); x.unlessActions=(string[])unlessActions.Clone(); x.effect=effect.Copy(); return x; }
    }
    [Serializable] public sealed class OutcomeRule
    {
        public string id, outcome, feedback;
        public string[] requiresActions=Array.Empty<string>(), missingActions=Array.Empty<string>(), requiresEvents=Array.Empty<string>();
        public double minimumSeconds, maximumSeconds;
        public bool noCriticalErrors;
        public PatientEffect effect=new PatientEffect();
        public OutcomeRule Copy() { var x=(OutcomeRule)MemberwiseClone(); x.requiresActions=(string[])requiresActions.Clone(); x.missingActions=(string[])missingActions.Clone(); x.requiresEvents=(string[])requiresEvents.Clone(); x.effect=effect.Copy(); return x; }
    }
    [Serializable] public sealed class ScenarioVariation
    {
        public int minAge=18,maxAge=80;
        public double heartRateSpread=3, spo2Spread=1, glucoseSpread=.1, timelineJitterSeconds=5;
        public string[] sexes=new[]{"female","male"}, witnessStatements=new[]{"No dispongo de más información."}, positions=new[]{"supine"};
        public ScenarioVariation Copy() { var x=(ScenarioVariation)MemberwiseClone(); x.sexes=(string[])sexes.Clone(); x.witnessStatements=(string[])witnessStatements.Clone(); x.positions=(string[])positions.Clone(); return x; }
    }
    [Serializable] public sealed class MedicalScenarioDefinition
    {
        public string id, name, category, difficulty, environment, description, history, incident, severity, initialDialogue;
        public string medicalValidationStatus="CLIENT_REVIEW", version="1", timingBasis="Parámetros de guion educativo; pendientes de validación médica.";
        public string availability="AVAILABLE";
        public string[] symptoms=Array.Empty<string>(), visibleSigns=Array.Empty<string>(), recommendedSequence=Array.Empty<string>(), references=Array.Empty<string>(), debrief=Array.Empty<string>();
        public PatientSnapshot initialState=new PatientSnapshot();
        public ScenarioVariation variation=new ScenarioVariation();
        public ActionRule[] actions=Array.Empty<ActionRule>();
        public PatientTimelineEvent[] timeline=Array.Empty<PatientTimelineEvent>();
        public OutcomeRule[] outcomes=Array.Empty<OutcomeRule>();
        public int errorPenalty=5, criticalScoreCap=49;
        // Composed from the separately serialized clinical JSON registry. Unity's inline
        // serializer otherwise materializes this omitted reference for every legacy case.
        [NonSerialized] public ClinicalScenarioV2Definition clinicalV2;
        public MedicalScenarioDefinition Copy()
        {
            var x=(MedicalScenarioDefinition)MemberwiseClone(); x.initialState=initialState.Copy(); x.variation=variation.Copy();
            x.symptoms=(string[])symptoms.Clone(); x.visibleSigns=(string[])visibleSigns.Clone(); x.recommendedSequence=(string[])recommendedSequence.Clone(); x.references=(string[])references.Clone(); x.debrief=(string[])debrief.Clone();
            x.actions=actions.Select(a=>a.Copy()).ToArray(); x.timeline=timeline.Select(e=>e.Copy()).ToArray(); x.outcomes=outcomes.Select(o=>o.Copy()).ToArray(); x.clinicalV2=clinicalV2==null?null:clinicalV2.Copy(); return x;
        }
        public void Validate(MedicalLibrary library)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(name)||actions==null||actions.Length==0||outcomes==null||outcomes.Length==0||timeline==null) throw new ArgumentException("Incomplete scenario.");
            if(!new[]{"DRAFT","REFERENCE_REVIEWED","CLIENT_REVIEW","APPROVED","CLINICAL_REVIEW_REQUIRED"}.Contains(medicalValidationStatus) || !new[]{"gym","mall","dental","football"}.Contains(environment)) throw new ArgumentException("Invalid validation/environment.");
            if(clinicalV2!=null) clinicalV2.Validate(id);
            initialState.Validate();
            if(errorPenalty<0||criticalScoreCap<0||criticalScoreCap>100||references.Length==0||references.Any(r=>!library.references.Any(x=>x.id==r))) throw new ArgumentException("Invalid scoring or references.");
            var ids=actions.Select(a=>a.id).ToArray();
            if(ids.Distinct().Count()!=ids.Length||ids.Any(string.IsNullOrWhiteSpace)||timeline.Select(e=>e.id).Distinct().Count()!=timeline.Length) throw new ArgumentException("Duplicate or empty IDs.");
            Action<string[]> known = list=> { if(list==null||list.Any(x=>!ids.Contains(x))) throw new ArgumentException("Unknown action reference."); };
            foreach(var a in actions)
            {
                if(!library.actions.Any(x=>x.id==a.action)||!new[]{"required","optional","incorrect","dangerous"}.Contains(a.kind)||a.points<0||a.penalty<0||a.effect==null) throw new ArgumentException("Invalid action rule.");
                if(!Enum.IsDefined(typeof(ActionRepeatPolicy),a.repeatPolicy)) throw new ArgumentException("Unknown action repeat policy.");
                if(!new[]{"","swallow","normalBreathing","arrest","shock","noShock","noTrauma","seizureStopped","conscious"}.Contains(a.guard)) throw new ArgumentException("Unknown guard.");
                known(a.prerequisites); if(a.prerequisites.Contains(a.id)) throw new ArgumentException("Self prerequisite.");
                if(!string.IsNullOrEmpty(a.anchorAction)) known(new[]{a.anchorAction});
                PatientSnapshot.Bounds(a.earliestSeconds,0,86400); PatientSnapshot.Bounds(a.deadlineSeconds,0,86400);
            }
            known(recommendedSequence);
            if(recommendedSequence.Distinct().Count()!=recommendedSequence.Length||actions.Any(a=>a.kind=="required"&&!recommendedSequence.Contains(a.id))) throw new ArgumentException("Recommended sequence incomplete.");
            var reached=new System.Collections.Generic.HashSet<string>();
            foreach(var id in recommendedSequence) { var a=actions.Single(x=>x.id==id); if(a.prerequisites.Any(x=>!reached.Contains(x))) throw new ArgumentException("Unreachable action sequence."); reached.Add(id); }
            foreach(var e in timeline) { known(e.requiresActions); known(e.unlessActions); if(!string.IsNullOrEmpty(e.anchorAction)) known(new[]{e.anchorAction}); PatientSnapshot.Bounds(e.afterSeconds,0,86400); }
            foreach(var o in outcomes) { known(o.requiresActions); known(o.missingActions); if(o.requiresEvents.Any(x=>!timeline.Any(e=>e.id==x))||!new[]{"FULL_RECOVERY","PARTIAL_RECOVERY","VERBAL_RESPONSE","UNCONSCIOUS_STABLE","ROSC","REQUIRES_ADVANCED_CARE","CRITICAL_FAILURE"}.Contains(o.outcome)) throw new ArgumentException("Invalid outcome."); }
            PatientSnapshot.Bounds(variation.minAge,18,100); PatientSnapshot.Bounds(variation.maxAge,variation.minAge,100);
            PatientSnapshot.Bounds(variation.heartRateSpread,0,20); PatientSnapshot.Bounds(variation.spo2Spread,0,5); PatientSnapshot.Bounds(variation.glucoseSpread,0,1); PatientSnapshot.Bounds(variation.timelineJitterSeconds,0,60);
            if(variation.sexes.Length==0||variation.positions.Length==0||variation.witnessStatements.Length==0) throw new ArgumentException("Empty variation options.");
        }
    }
}
