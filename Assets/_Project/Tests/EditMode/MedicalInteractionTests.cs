using System;
using EmergencyVR.Medical;
using NUnit.Framework;

namespace EmergencyVR.Tests
{
    public sealed class MedicalInteractionTests
    {
        [Test] public void CompressionRequiresDownstrokeAndReleaseAndDoesNotRepeatWhileHeld()
        {
            var r=new CPRSampleEvaluator(new MedicalInteractionSettings());Assert.That(r.Sample(0,0,0,0,true,"WINDOWS"),Is.False);
            Assert.That(r.Sample(.2,.055,0,0,true,"WINDOWS"),Is.False);r.Sample(.3,.055,0,0,true,"WINDOWS");Assert.That(r.Sample(.5,0,0,0,true,"WINDOWS"),Is.True);
            r.Sample(.6,0,0,0,true,"WINDOWS");Assert.That(r.Metrics.compressions,Is.EqualTo(1));Assert.That(r.Metrics.depthInRange,Is.EqualTo(1));
        }
        [Test] public void PoorPlacementAndShallowDepthAreMeasuredWithoutAwardingQuality()
        {
            var r=new CPRSampleEvaluator(new MedicalInteractionSettings());r.Sample(0,.025,.2,70,false,"XR_CONTROLLERS");r.Sample(.5,0,0,0,true,"XR_CONTROLLERS");
            Assert.That(r.Metrics.compressions,Is.EqualTo(1));Assert.That(r.Metrics.depthInRange,Is.Zero);Assert.That(r.Metrics.correctPlacement,Is.Zero);Assert.That(r.Metrics.calibratedMannequin,Is.False);
        }
        [Test] public void CompressionCadenceUsesRealSampleTimesAndSnapshotsAreDetached()
        {
            var r=new CPRSampleEvaluator(new MedicalInteractionSettings());r.Sample(0,.055,0,0,true,"XR_CONTROLLERS");r.Sample(.25,0,0,0,true,"XR_CONTROLLERS");r.Sample(.5,.055,0,0,true,"XR_CONTROLLERS");r.Sample(.8,0,0,0,true,"XR_CONTROLLERS");
            Assert.That(r.Metrics.meanRatePerMinute,Is.EqualTo(60/.55).Within(.001));Assert.That(r.Metrics.rateInRange,Is.EqualTo(1));var copy=r.Metrics;copy.compressions=99;Assert.That(r.Metrics.compressions,Is.EqualTo(2));Assert.Throws<ArgumentException>(()=>r.Sample(.2,0,0,0,true,"XR"));
        }
        [Test] public void AedNeedsOpenPowerPeelAndBothCorrectlyPlacedPads()
        {
            var a=new AEDProcedureState();Assert.That(a.PowerOn(),Is.False);a.Open();a.PowerOn();Assert.That(a.PlacePad(true,true,true),Is.False);a.Peel(true);Assert.That(a.PlacePad(true,false,true),Is.False);Assert.That(a.PlacePad(true,true,false),Is.False);Assert.That(a.PlacePad(true,true,true),Is.True);Assert.That(a.BeginAnalysis(false),Is.False);a.Peel(false);a.PlacePad(false,true,true);Assert.That(a.Phase,Is.EqualTo(AEDPhase.PadsReady));
        }
        [Test] public void AedBlocksContactAndNonShockableRhythm()
        {
            var a=new AEDProcedureState();a.Open();a.PowerOn();a.Peel(true);a.Peel(false);a.PlacePad(true,true,true);a.PlacePad(false,true,true);
            Assert.That(a.BeginAnalysis(true),Is.False);a.BeginAnalysis(false);Assert.That(a.FinishAnalysis(true,true),Is.False);a.FinishAnalysis(true,false);Assert.That(a.Shock(true,true),Is.False);Assert.That(a.Shock(false,false),Is.False);Assert.That(a.Shock(false,true),Is.True);Assert.That(a.Shock(false,true),Is.False);Assert.That(a.Shocks,Is.EqualTo(1));
        }
    }
}
