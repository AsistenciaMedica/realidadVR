using System;

namespace EmergencyVR.Medical
{
    [Serializable] public sealed class MedicalInteractionSettings
    {
        public string medicalValidationStatus="CLIENT_REVIEW";
        public string referenceUrl="https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/adult-basic-life-support-guidelines";
        public double minimumDepth=.05,maximumDepth=.06,minimumRate=100,maximumRate=120;
        public double recoilThreshold=.005,cycleThreshold=.015,maximumHandError=.09,maximumHandAngle=40;
        public double padSnapDistance=.12,padSnapAngle=45,readingDelaySeconds=2;
        public string calibrationNote="Depth is virtual controller travel, not instrumented mannequin depth. Hand tolerances and acquisition delay are interaction settings; medical validation pending.";
        public void Validate()
        {
            PatientSnapshot.Bounds(minimumDepth,.001,.1);PatientSnapshot.Bounds(maximumDepth,minimumDepth,.1);
            PatientSnapshot.Bounds(minimumRate,1,300);PatientSnapshot.Bounds(maximumRate,minimumRate,300);
            PatientSnapshot.Bounds(recoilThreshold,0,cycleThreshold);PatientSnapshot.Bounds(cycleThreshold,.001,minimumDepth);
            PatientSnapshot.Bounds(maximumHandError,.001,.3);PatientSnapshot.Bounds(maximumHandAngle,0,90);
            PatientSnapshot.Bounds(padSnapDistance,.01,.3);PatientSnapshot.Bounds(padSnapAngle,0,90);PatientSnapshot.Bounds(readingDelaySeconds,.1,30);
        }
    }
    [Serializable] public sealed class CPRMetrics
    {
        public int compressions,depthInRange,rateInRange,correctPlacement,fullReleases;
        public double meanDepthMeters,meanRatePerMinute,longestPauseSeconds;
        public string inputSource="NONE";
        public bool calibratedMannequin=false;
        public CPRMetrics Copy() => (CPRMetrics)MemberwiseClone();
    }
    public sealed class CPRSampleEvaluator
    {
        readonly MedicalInteractionSettings settings;
        readonly CPRMetrics metrics=new CPRMetrics();
        double lastTime=-1,lastRelease=-1,peak,depthTotal,rateTotal;
        int intervals;
        bool cycle,placement=true;
        public CPRMetrics Metrics => metrics.Copy();
        // A UI pause interrupts an unfinished gesture without recording a clinical compression.
        public void CancelPendingCycle() { cycle=false; peak=0; placement=true; }
        public CPRSampleEvaluator(MedicalInteractionSettings settings) {settings.Validate();this.settings=new MedicalInteractionSettings {minimumDepth=settings.minimumDepth,maximumDepth=settings.maximumDepth,minimumRate=settings.minimumRate,maximumRate=settings.maximumRate,recoilThreshold=settings.recoilThreshold,cycleThreshold=settings.cycleThreshold,maximumHandError=settings.maximumHandError,maximumHandAngle=settings.maximumHandAngle};}
        public bool Sample(double time,double depth,double handError,double handAngle,bool bothHands,string source)
        {
            if(double.IsNaN(time)||double.IsInfinity(time)||time<lastTime)throw new ArgumentException("Invalid CPR sample time.");
            PatientSnapshot.Bounds(depth,0,.15);PatientSnapshot.Bounds(handError,0,10);PatientSnapshot.Bounds(handAngle,0,180);lastTime=time;
            metrics.inputSource=source;
            if(lastRelease>=0) metrics.longestPauseSeconds=Math.Max(metrics.longestPauseSeconds,time-lastRelease);
            if(depth>=settings.cycleThreshold && !cycle) {cycle=true;peak=depth;placement=true;}
            if(!cycle)return false;
            peak=Math.Max(peak,depth);placement&=bothHands&&handError<=settings.maximumHandError&&handAngle<=settings.maximumHandAngle;
            if(depth>settings.recoilThreshold)return false;
            cycle=false;metrics.compressions++;metrics.fullReleases++;depthTotal+=peak;metrics.meanDepthMeters=depthTotal/metrics.compressions;
            if(peak>=settings.minimumDepth&&peak<=settings.maximumDepth)metrics.depthInRange++;
            if(placement)metrics.correctPlacement++;
            if(lastRelease>=0&&time>lastRelease){double rate=60/(time-lastRelease);rateTotal+=rate;intervals++;metrics.meanRatePerMinute=rateTotal/intervals;if(rate>=settings.minimumRate&&rate<=settings.maximumRate)metrics.rateInRange++;}
            lastRelease=time;peak=0;return true;
        }
    }
    public enum AEDPhase { Closed, Open, Powered, PadsReady, Analysing, ShockAdvised, ContinueCPR }
    public sealed class AEDProcedureState
    {
        public AEDPhase Phase {get;private set;}
        public bool RightPad {get;private set;}
        public bool LeftPad {get;private set;}
        public bool RightPeeled {get;private set;}
        public bool LeftPeeled {get;private set;}
        public int Shocks {get;private set;}
        public bool Open() {if(Phase!=AEDPhase.Closed)return false;Phase=AEDPhase.Open;return true;}
        public bool PowerOn() {if(Phase!=AEDPhase.Open)return false;Phase=AEDPhase.Powered;return true;}
        public bool Peel(bool right) {if(Phase!=AEDPhase.Powered&&Phase!=AEDPhase.PadsReady)return false;if(right)RightPeeled=true;else LeftPeeled=true;return true;}
        public bool PlacePad(bool right,bool inZone,bool orientationValid)
        {
            if(!inZone||!orientationValid||Phase!=AEDPhase.Powered||!(right?RightPeeled:LeftPeeled))return false;
            if(right)RightPad=true;else LeftPad=true;if(RightPad&&LeftPad)Phase=AEDPhase.PadsReady;return true;
        }
        public bool BeginAnalysis(bool touching) {if(touching||!RightPad||!LeftPad||(Phase!=AEDPhase.PadsReady&&Phase!=AEDPhase.ContinueCPR))return false;Phase=AEDPhase.Analysing;return true;}
        public bool FinishAnalysis(bool shockAdvised,bool touching) {if(Phase!=AEDPhase.Analysing||touching)return false;Phase=shockAdvised?AEDPhase.ShockAdvised:AEDPhase.ContinueCPR;return true;}
        public bool Shock(bool touching,bool currentlyShockable) {if(Phase!=AEDPhase.ShockAdvised||touching||!currentlyShockable)return false;Shocks++;Phase=AEDPhase.ContinueCPR;return true;}
    }
    [Serializable] public sealed class ProcedureMetrics
    {
        public string mode="TRAINING";
        public CPRMetrics cpr=new CPRMetrics();
        public bool rightPadPlaced,leftPadPlaced;
        public int shocks,measurements,unsafeDeviceAttempts;
        public string limitation="Virtual interaction metrics only; human model, hand fidelity and clinical calibration require review.";
    }
}
