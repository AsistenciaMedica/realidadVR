using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EmergencyVR.Core
{
    public enum ActionDisposition { Accepted, Late, OutOfOrder, Duplicate, Unknown }

    public sealed class ActionRecord
    {
        public string ActionId { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public ActionDisposition Disposition { get; private set; }

        public ActionRecord(string actionId, double elapsedSeconds, ActionDisposition disposition)
        {
            ActionId = actionId;
            ElapsedSeconds = elapsedSeconds;
            Disposition = disposition;
        }
    }

    public sealed class EvaluationResult
    {
        public string CaseId { get; private set; }
        public double ScorePercent { get; private set; }
        public double DurationSeconds { get; private set; }
        public int Errors { get; private set; }
        public int LateActions { get; private set; }
        public bool AllStepsCompleted { get; private set; }
        public PatientState FinalState { get; private set; }
        public ReadOnlyCollection<ActionRecord> Actions { get; private set; }
        public ReadOnlyCollection<string> OmittedActions { get; private set; }

        internal EvaluationResult(string caseId, double scorePercent, double durationSeconds,
            int errors, int lateActions, PatientState finalState,
            IEnumerable<ActionRecord> actions, IEnumerable<string> omittedActions)
        {
            CaseId = caseId;
            ScorePercent = scorePercent;
            DurationSeconds = durationSeconds;
            Errors = errors;
            LateActions = lateActions;
            FinalState = finalState;
            Actions = new List<ActionRecord>(actions).AsReadOnly();
            OmittedActions = new List<string>(omittedActions).AsReadOnly();
            AllStepsCompleted = OmittedActions.Count == 0;
        }
    }
}
