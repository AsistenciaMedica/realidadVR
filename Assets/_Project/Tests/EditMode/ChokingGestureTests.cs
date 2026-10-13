using EmergencyVR.Medical.Interaction;
using NUnit.Framework;

namespace EmergencyVR.Tests
{
    public sealed class ChokingGestureTests
    {
        [Test]
        public void RequiresApproachMovementAndReturnRatherThanStaticButtonHolding()
        {
            var gesture = new ChokingGestureEvaluator();
            Assert.That(gesture.Sample(0, .05, true), Is.False, "Starting already pressed is not a stroke.");
            Assert.That(gesture.Sample(.1, .05, true), Is.False);
            Assert.That(gesture.Sample(.2, 0, true), Is.False);
            Assert.That(gesture.Sample(.3, .05, true), Is.False);
            Assert.That(gesture.Sample(.5, .05, true), Is.False);
            Assert.That(gesture.Sample(.6, 0, true), Is.True);
            Assert.That(gesture.Sample(.7, 0, true), Is.False, "Returning once must count only once.");
        }
        [Test]
        public void BadPlacementAndPauseCancellationCannotCompleteAnArmedStroke()
        {
            var gesture = new ChokingGestureEvaluator();
            gesture.Sample(0, 0, true); gesture.Sample(.1, .05, true);
            Assert.That(gesture.Sample(.2, 0, false), Is.False);
            Assert.That(gesture.Sample(.3, 0, true), Is.False);
            gesture.Sample(.4, .05, true); gesture.Cancel();
            Assert.That(gesture.Sample(.5, 0, true), Is.False);
            Assert.That(gesture.Sample(.6, .05, true), Is.False);
            Assert.That(gesture.Sample(.7, 0, true), Is.True);
        }
        [Test]
        public void RejectsJitterOvertravelStaleSamplesAndInvalidValues()
        {
            var gesture = new ChokingGestureEvaluator();
            gesture.Sample(0, 0, true); gesture.Sample(.1, .02, true);
            Assert.That(gesture.Sample(.2, 0, true), Is.False);
            gesture.Sample(.3, .1, true);
            Assert.That(gesture.Sample(.4, 0, true), Is.False);
            gesture.Sample(.5, .05, true);
            Assert.That(gesture.Sample(4, 0, true), Is.False);
            gesture.Sample(5, 0, true); gesture.Sample(5.1, .05, true);
            Assert.That(gesture.Sample(4.9, 0, true), Is.False);
            gesture.Sample(6, 0, true); gesture.Sample(6.1, .05, true);
            Assert.That(gesture.Sample(6.2, double.NaN, true), Is.False);
            Assert.That(gesture.Sample(6.3, 0, true), Is.False);
        }
    }
}
