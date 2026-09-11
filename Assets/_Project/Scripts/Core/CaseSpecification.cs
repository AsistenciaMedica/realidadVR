using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EmergencyVR.Core
{
    public sealed class StepDefinition
    {
        public string ActionId { get; private set; }
        public string Label { get; private set; }
        public PatientState FromState { get; private set; }
        public PatientState ToState { get; private set; }
        public int Points { get; private set; }
        // Deadline relative to case start. Zero means no time limit.
        public double DeadlineSeconds { get; private set; }

        public StepDefinition(string actionId, string label, PatientState fromState,
            PatientState toState, int points, double deadlineSeconds)
        {
            if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("Action ID is required.");
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Step label is required.");
            if (!Enum.IsDefined(typeof(PatientState), fromState) ||
                !Enum.IsDefined(typeof(PatientState), toState)) throw new ArgumentException("Unknown patient state.");
            if (points <= 0) throw new ArgumentOutOfRangeException("points");
            if (double.IsNaN(deadlineSeconds) || double.IsInfinity(deadlineSeconds) || deadlineSeconds < 0)
                throw new ArgumentOutOfRangeException("deadlineSeconds");
            ActionId = actionId;
            Label = label;
            FromState = fromState;
            ToState = toState;
            Points = points;
            DeadlineSeconds = deadlineSeconds;
        }
    }

    public sealed class CaseSpecification
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public PatientState InitialState { get; private set; }
        public ReadOnlyCollection<StepDefinition> Steps { get; private set; }
        public int ErrorPenalty { get; private set; }
        public int MaxPoints { get; private set; }

        public CaseSpecification(string id, string displayName, PatientState initialState,
            IEnumerable<StepDefinition> steps, int errorPenalty)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Case ID is required.");
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Case name is required.");
            if (!Enum.IsDefined(typeof(PatientState), initialState)) throw new ArgumentException("Unknown initial state.");
            if (steps == null) throw new ArgumentNullException("steps");
            if (errorPenalty < 0) throw new ArgumentOutOfRangeException("errorPenalty");
            var copy = new List<StepDefinition>(steps);
            if (copy.Count == 0) throw new ArgumentException("A case needs at least one step.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var state = initialState;
            var total = 0;
            foreach (var step in copy)
            {
                if (step == null) throw new ArgumentException("Null step.");
                if (!ids.Add(step.ActionId)) throw new ArgumentException("Each step needs a unique action ID.");
                if (step.FromState != state) throw new ArgumentException("Unreachable step: " + step.ActionId);
                state = step.ToState;
                total = checked(total + step.Points);
            }
            Id = id;
            DisplayName = displayName;
            InitialState = initialState;
            Steps = copy.AsReadOnly();
            ErrorPenalty = errorPenalty;
            MaxPoints = total;
        }
    }
}
