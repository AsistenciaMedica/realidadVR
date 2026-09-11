using System;
using System.Collections.Generic;

namespace EmergencyVR.Core
{
    /// <summary>One attempt, independent of scenes, devices and Unity's clock.</summary>
    public sealed class CaseSession
    {
        readonly CaseSpecification specification;
        readonly double startedAt;
        readonly List<ActionRecord> actions = new List<ActionRecord>();
        readonly HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);
        double lastTimestamp;
        int nextStep;
        int points;
        int errors;
        int lateActions;

        public PatientState State { get; private set; }
        public bool IsFinished { get { return Result != null; } }
        public EvaluationResult Result { get; private set; }
        public int ActionCount { get { return actions.Count; } }
        public StepDefinition NextStep
        {
            get { return nextStep < specification.Steps.Count ? specification.Steps[nextStep] : null; }
        }

        public CaseSession(CaseSpecification specification, double now)
        {
            if (specification == null) throw new ArgumentNullException("specification");
            ValidateNumber(now);
            this.specification = specification;
            startedAt = lastTimestamp = now;
            State = specification.InitialState;
        }

        public ActionRecord Record(string actionId, double now)
        {
            if (IsFinished) throw new InvalidOperationException("The case is already finished.");
            if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("Action ID is required.");
            ValidateTimestamp(now);
            var elapsed = now - startedAt;
            ActionDisposition disposition;
            var step = NextStep;
            if (step != null && string.Equals(step.ActionId, actionId, StringComparison.Ordinal))
            {
                if (State != step.FromState) throw new InvalidOperationException("Invalid session state.");
                var late = step.DeadlineSeconds > 0 && elapsed > step.DeadlineSeconds;
                disposition = late ? ActionDisposition.Late : ActionDisposition.Accepted;
                if (late) lateActions++;
                else points += step.Points;
                completed.Add(actionId);
                State = step.ToState;
                nextStep++;
            }
            else
            {
                disposition = ActionDisposition.Unknown;
                if (completed.Contains(actionId)) disposition = ActionDisposition.Duplicate;
                else
                    foreach (var candidate in specification.Steps)
                        if (candidate.ActionId == actionId) disposition = ActionDisposition.OutOfOrder;
                errors++;
            }
            lastTimestamp = now;
            var record = new ActionRecord(actionId, elapsed, disposition);
            actions.Add(record);
            return record;
        }

        public EvaluationResult Finish(double now)
        {
            // Repeated finish returns the same immutable result; no double scoring.
            if (IsFinished) return Result;
            ValidateTimestamp(now);
            var omitted = new List<string>();
            for (var i = nextStep; i < specification.Steps.Count; i++)
                omitted.Add(specification.Steps[i].ActionId);
            var earned = Math.Max(0.0, points - (double)errors * specification.ErrorPenalty);
            var percent = Math.Min(100.0, 100.0 * earned / specification.MaxPoints);
            Result = new EvaluationResult(specification.Id, percent, now - startedAt,
                errors, lateActions, State, actions, omitted);
            lastTimestamp = now;
            return Result;
        }

        void ValidateTimestamp(double now)
        {
            ValidateNumber(now);
            if (now < lastTimestamp) throw new ArgumentOutOfRangeException("now", "Clock must be monotonic.");
        }

        static void ValidateNumber(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0)
                throw new ArgumentOutOfRangeException("now");
        }
    }
}
