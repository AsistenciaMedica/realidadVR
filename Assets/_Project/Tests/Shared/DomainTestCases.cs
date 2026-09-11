using System;
using System.Collections.Generic;
using EmergencyVR.Core;

namespace EmergencyVR.Tests
{
    public sealed class NamedTest
    {
        public readonly string Name;
        public readonly Action Run;
        public NamedTest(string name, Action run) { Name = name; Run = run; }
        public override string ToString() { return Name; }
    }

    // Same tests run under NUnit in Unity and the dependency-free Windows runner.
    public static class DomainTestCases
    {
        static CaseSpecification Definition(double deadline = 0, int penalty = 10)
        {
            return new CaseSpecification("demo", "Demo", PatientState.UnconsciousBreathing,
                new[] {
                    new StepDefinition("inspect", "Inspect", PatientState.UnconsciousBreathing, PatientState.Recovering, 50, deadline),
                    new StepDefinition("confirm", "Confirm", PatientState.Recovering, PatientState.Recovered, 50, 0)
                }, penalty);
        }

        static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception("Expected " + expected + "; actual " + actual);
        }

        static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("Expected " + typeof(T).Name);
        }

        public static IEnumerable<NamedTest> All()
        {
            yield return new NamedTest("Initial state comes from case data", () => {
                var session = new CaseSession(Definition(), 10);
                Equal(PatientState.UnconsciousBreathing, session.State);
                Equal(false, session.IsFinished);
                Equal("inspect", session.NextStep.ActionId);
            });
            yield return new NamedTest("Ordered actions transition and score 100", () => {
                var session = new CaseSession(Definition(), 10);
                Equal(ActionDisposition.Accepted, session.Record("inspect", 12).Disposition);
                Equal(PatientState.Recovering, session.State);
                session.Record("confirm", 15);
                Equal(PatientState.Recovered, session.State);
                Equal<StepDefinition>(null, session.NextStep);
                var result = session.Finish(20);
                Equal(100.0, result.ScorePercent);
                Equal(10.0, result.DurationSeconds);
                Equal(true, result.AllStepsCompleted);
                Equal(0, result.Errors);
                Equal(0, result.OmittedActions.Count);
            });
            yield return new NamedTest("Out of order action does not change patient", () => {
                var session = new CaseSession(Definition(), 0);
                Equal(ActionDisposition.OutOfOrder, session.Record("confirm", 1).Disposition);
                Equal(PatientState.UnconsciousBreathing, session.State);
                session.Record("inspect", 2);
                session.Record("confirm", 3);
                Equal(90.0, session.Finish(4).ScorePercent);
                Equal(1, session.Result.Errors);
            });
            yield return new NamedTest("Duplicate action cannot award more points", () => {
                var session = new CaseSession(Definition(), 0);
                session.Record("inspect", 1);
                Equal(ActionDisposition.Duplicate, session.Record("inspect", 2).Disposition);
                Equal(PatientState.Recovering, session.State);
                session.Record("confirm", 3);
                Equal(90.0, session.Finish(4).ScorePercent);
            });
            yield return new NamedTest("Unknown action remains auditable", () => {
                var session = new CaseSession(Definition(), 2);
                Equal(ActionDisposition.Unknown, session.Record("unknown", 4).Disposition);
                var result = session.Finish(6);
                Equal(1, result.Actions.Count);
                Equal("unknown", result.Actions[0].ActionId);
                Equal(2.0, result.Actions[0].ElapsedSeconds);
                Equal(2, result.OmittedActions.Count);
            });
            yield return new NamedTest("Early finish identifies omitted steps", () => {
                var session = new CaseSession(Definition(), 0);
                session.Record("inspect", 1);
                var result = session.Finish(2);
                Equal(50.0, result.ScorePercent);
                Equal("confirm", result.OmittedActions[0]);
                Equal(false, result.AllStepsCompleted);
                Equal(PatientState.Recovering, result.FinalState);
            });
            yield return new NamedTest("No actions yields zero and all omissions", () => {
                var result = new CaseSession(Definition(), 0).Finish(0);
                Equal(0.0, result.ScorePercent);
                Equal(2, result.OmittedActions.Count);
            });
            yield return new NamedTest("Finish is idempotent", () => {
                var session = new CaseSession(Definition(), 0);
                var first = session.Finish(1);
                Equal(true, object.ReferenceEquals(first, session.Finish(5)));
                Equal(1.0, session.Result.DurationSeconds);
            });
            yield return new NamedTest("Finished sessions reject later actions", () => {
                var session = new CaseSession(Definition(), 0);
                session.Finish(1);
                Throws<InvalidOperationException>(() => session.Record("inspect", 2));
                Equal(0, session.ActionCount);
            });
            yield return new NamedTest("Late step advances state without timing points", () => {
                var session = new CaseSession(Definition(5), 100);
                Equal(ActionDisposition.Late, session.Record("inspect", 106).Disposition);
                Equal(PatientState.Recovering, session.State);
                session.Record("confirm", 107);
                var result = session.Finish(108);
                Equal(50.0, result.ScorePercent);
                Equal(1, result.LateActions);
                Equal(0, result.Errors);
                Equal(true, result.AllStepsCompleted);
            });
            yield return new NamedTest("Exact deadline is accepted relative to start", () => {
                var session = new CaseSession(Definition(5), 100);
                Equal(ActionDisposition.Accepted, session.Record("inspect", 105).Disposition);
            });
            yield return new NamedTest("Zero deadline disables timing limit", () => {
                var session = new CaseSession(Definition(), 0);
                Equal(ActionDisposition.Accepted, session.Record("inspect", 10000).Disposition);
            });
            yield return new NamedTest("Score cannot become negative", () => {
                var session = new CaseSession(Definition(0, int.MaxValue), 0);
                session.Record("unknown", 1);
                session.Record("unknown", 2);
                Equal(0.0, session.Finish(3).ScorePercent);
            });
            yield return new NamedTest("Repeated actions never inflate score", () => {
                var session = new CaseSession(Definition(0, 0), 0);
                session.Record("inspect", 1);
                session.Record("confirm", 2);
                for (var i = 0; i < 20; i++) session.Record("confirm", 3 + i);
                var result = session.Finish(23);
                Equal(100.0, result.ScorePercent);
                Equal(20, result.Errors);
            });
            yield return new NamedTest("Backward clock rejects record without mutation", () => {
                var session = new CaseSession(Definition(), 10);
                Throws<ArgumentOutOfRangeException>(() => session.Record("inspect", 9));
                Equal(0, session.ActionCount);
                Equal(PatientState.UnconsciousBreathing, session.State);
                session.Record("inspect", 11);
                Throws<ArgumentOutOfRangeException>(() => session.Finish(10));
                Equal(false, session.IsFinished);
            });
            yield return new NamedTest("Clock rejects nonfinite and negative values", () => {
                foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1.0 })
                {
                    Throws<ArgumentOutOfRangeException>(() => new CaseSession(Definition(), value));
                    var session = new CaseSession(Definition(), 0);
                    Throws<ArgumentOutOfRangeException>(() => session.Record("inspect", value));
                    Throws<ArgumentOutOfRangeException>(() => session.Finish(value));
                    Equal(0, session.ActionCount);
                }
            });
            yield return new NamedTest("Empty action rejected without mutation", () => {
                var session = new CaseSession(Definition(), 0);
                Throws<ArgumentException>(() => session.Record(" ", 1));
                Throws<ArgumentException>(() => session.Record(null, 1));
                Equal(0, session.ActionCount);
            });
            yield return new NamedTest("Definition validates IDs and steps", () => {
                Throws<ArgumentException>(() => new CaseSpecification("", "Demo", PatientState.Normal, Definition().Steps, 0));
                Throws<ArgumentException>(() => new CaseSpecification("demo", " ", PatientState.Normal, Definition().Steps, 0));
                Throws<ArgumentException>(() => new CaseSpecification("demo", "Demo", PatientState.Normal, new StepDefinition[0], 0));
                Throws<ArgumentNullException>(() => new CaseSpecification("demo", "Demo", PatientState.Normal, null, 0));
                Throws<ArgumentException>(() => new CaseSpecification("demo", "Demo", PatientState.Normal, new StepDefinition[] { null }, 0));
            });
            yield return new NamedTest("Duplicate step IDs rejected", () => {
                var steps = new[] {
                    new StepDefinition("same", "One", PatientState.Normal, PatientState.Conscious, 1, 0),
                    new StepDefinition("same", "Two", PatientState.Conscious, PatientState.Recovered, 1, 0)
                };
                Throws<ArgumentException>(() => new CaseSpecification("demo", "Demo", PatientState.Normal, steps, 0));
            });
            yield return new NamedTest("Unreachable state chain rejected", () => {
                Throws<ArgumentException>(() => new CaseSpecification("demo", "Demo", PatientState.Normal, Definition().Steps, 0));
                var steps = new[] {
                    new StepDefinition("one", "One", PatientState.Normal, PatientState.Conscious, 1, 0),
                    new StepDefinition("two", "Two", PatientState.Recovered, PatientState.Recovered, 1, 0)
                };
                Throws<ArgumentException>(() => new CaseSpecification("demo", "Demo", PatientState.Normal, steps, 0));
            });
            yield return new NamedTest("Invalid scoring and timing values rejected", () => {
                Throws<ArgumentOutOfRangeException>(() => Definition(0, -1));
                foreach (var value in new[] { -1.0, double.NaN, double.PositiveInfinity })
                    Throws<ArgumentOutOfRangeException>(() => Definition(value));
                Throws<ArgumentOutOfRangeException>(() => new StepDefinition("a", "A", PatientState.Normal, PatientState.Normal, 0, 0));
                Throws<ArgumentException>(() => new StepDefinition(" ", "A", PatientState.Normal, PatientState.Normal, 1, 0));
                Throws<ArgumentException>(() => new StepDefinition("a", "", PatientState.Normal, PatientState.Normal, 1, 0));
            });
            yield return new NamedTest("Unknown enum states rejected", () => {
                Throws<ArgumentException>(() => new StepDefinition("a", "A", (PatientState)999, PatientState.Normal, 1, 0));
                Throws<ArgumentException>(() => new CaseSpecification("demo", "Demo", (PatientState)999, Definition().Steps, 0));
            });
            yield return new NamedTest("Point total overflow is rejected", () => {
                var steps = new[] {
                    new StepDefinition("one", "One", PatientState.Normal, PatientState.Normal, int.MaxValue, 0),
                    new StepDefinition("two", "Two", PatientState.Normal, PatientState.Normal, 1, 0)
                };
                Throws<OverflowException>(() => new CaseSpecification("demo", "Demo", PatientState.Normal, steps, 0));
            });
            yield return new NamedTest("Definitions snapshot mutable input collections", () => {
                var steps = new List<StepDefinition>(Definition().Steps);
                var definition = new CaseSpecification("demo", "Demo", PatientState.UnconsciousBreathing, steps, 0);
                steps.Clear();
                Equal(2, definition.Steps.Count);
                Throws<NotSupportedException>(() => ((IList<StepDefinition>)definition.Steps).Clear());
            });
            yield return new NamedTest("Result collections cannot be modified", () => {
                var result = new CaseSession(Definition(), 0).Finish(1);
                Throws<NotSupportedException>(() => ((IList<ActionRecord>)result.Actions).Clear());
                Throws<NotSupportedException>(() => ((IList<string>)result.OmittedActions).Clear());
            });
            yield return new NamedTest("Separate attempts never share state or score", () => {
                var definition = Definition();
                var first = new CaseSession(definition, 0);
                first.Record("inspect", 1);
                var second = new CaseSession(definition, 2);
                Equal(PatientState.UnconsciousBreathing, second.State);
                Equal(0, second.ActionCount);
                Equal(0.0, second.Finish(3).ScorePercent);
                Equal(50.0, first.Finish(4).ScorePercent);
            });
            yield return new NamedTest("Null case cannot start a session", () => {
                Throws<ArgumentNullException>(() => new CaseSession(null, 0));
            });
        }
    }
}
