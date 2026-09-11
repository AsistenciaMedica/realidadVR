using System;
using System.Collections.Generic;
using EmergencyVR.Core;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    [Serializable]
    public sealed class CaseStepData
    {
        public string actionId;
        public string label;
        public PatientState fromState;
        public PatientState toState;
        [Min(1)] public int points = 50;
        [Min(0)] public float deadlineSeconds;

        public StepDefinition ToDomain()
        {
            return new StepDefinition(actionId, label, fromState, toState, points, deadlineSeconds);
        }
    }

    [CreateAssetMenu(menuName = "Emergency VR/Clinical Case", fileName = "NewCase")]
    public sealed class ClinicalCaseDefinition : ScriptableObject
    {
        public string caseId = "new-case";
        public string displayName = "Nuevo caso";
        [TextArea] public string description;
        public bool isTechnicalDemo = true;
        public bool clinicallyApproved;
        public PatientState initialState = PatientState.UnconsciousBreathing;
        [Min(0)] public int errorPenalty = 10;
        public List<CaseStepData> steps = new List<CaseStepData>();

        public CaseSpecification ToDomain()
        {
            var definitions = new List<StepDefinition>();
            if (steps == null) throw new InvalidOperationException("Missing case steps.");
            foreach (var step in steps)
            {
                if (step == null) throw new InvalidOperationException("Null case step.");
                definitions.Add(step.ToDomain());
            }
            return new CaseSpecification(caseId, displayName, initialState, definitions, errorPenalty);
        }
    }
}
