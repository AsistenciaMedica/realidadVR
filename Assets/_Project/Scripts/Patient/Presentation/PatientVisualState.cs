using System;
using EmergencyVR.Medical;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    public enum PatientBreathingMode { Normal, Fast, Slow, Shallow, Labored, Agonal, Absent }
    public enum PatientConsciousness { Alert, Confused, Drowsy, Unresponsive }
    public enum PatientExpression { Neutral, Pain, Fear, Confused, Distress, Unresponsive, Recovering }
    public enum PatientPosture { Supine, Recovery, Seated, Standing }

    // Presentation-only values. No diagnosis, treatment, recovery or clinical score is inferred here.
    public readonly struct PatientVisualState
    {
        public readonly PatientBreathingMode Breathing;
        public readonly PatientConsciousness Consciousness;
        public readonly PatientExpression Expression;
        public readonly PatientPosture Posture;
        public readonly float RespiratoryRate, Pallor, Cyanosis, Flushing;
        public readonly bool Seizure, Bleeding;
        public PatientVisualState(PatientBreathingMode breathing, PatientConsciousness consciousness,
            PatientExpression expression, PatientPosture posture, float rate, float pallor, float cyanosis,
            float flushing, bool seizure, bool bleeding)
        {
            Breathing=breathing; Consciousness=consciousness; Expression=expression; Posture=posture;
            RespiratoryRate=rate; Pallor=pallor; Cyanosis=cyanosis; Flushing=flushing; Seizure=seizure; Bleeding=bleeding;
        }
        public static PatientVisualState FromSnapshot(PatientSnapshot state)
        {
            if(state==null) throw new ArgumentNullException(nameof(state));
            bool Flag(string value) => Array.Exists(state.flags??Array.Empty<string>(), f=>string.Equals(f,value,StringComparison.OrdinalIgnoreCase));
            var breathing=state.respiration switch {
                "fast"=>PatientBreathingMode.Fast, "slow"=>PatientBreathingMode.Slow,
                "shallow"=>PatientBreathingMode.Shallow, "labored"=>PatientBreathingMode.Labored,
                "agonal"=>PatientBreathingMode.Agonal, "absent"=>PatientBreathingMode.Absent,
                _=>PatientBreathingMode.Normal };
            // Optional explicit presentation flags support the existing medical schema without changing it.
            if(breathing!=PatientBreathingMode.Absent && breathing!=PatientBreathingMode.Agonal)
            {
                if(Flag("labored breathing")) breathing=PatientBreathingMode.Labored;
                else if(Flag("shallow breathing")) breathing=PatientBreathingMode.Shallow;
            }
            var consciousness=state.consciousness switch {
                "Confused"=>PatientConsciousness.Confused,"Drowsy"=>PatientConsciousness.Drowsy,
                "Unresponsive"=>PatientConsciousness.Unresponsive,_=>PatientConsciousness.Alert };
            var expression=consciousness==PatientConsciousness.Unresponsive ? PatientExpression.Unresponsive :
                state.pain>0 || Flag("pain") ? PatientExpression.Pain :
                consciousness==PatientConsciousness.Confused ? PatientExpression.Confused :
                breathing==PatientBreathingMode.Labored || Flag("distress") ? PatientExpression.Distress :
                Flag("recovering") ? PatientExpression.Recovering : Flag("fear") ? PatientExpression.Fear : PatientExpression.Neutral;
            var posture=state.position=="standing" ? PatientPosture.Standing : state.position=="recovery" ? PatientPosture.Recovery :
                state.position=="seated" || state.position=="sitting" ? PatientPosture.Seated : PatientPosture.Supine;
            var rate=double.IsNaN(state.respiratoryRate)||double.IsInfinity(state.respiratoryRate) ? 0 : Mathf.Clamp((float)state.respiratoryRate,0,65);
            return new PatientVisualState(breathing,consciousness,expression,posture,rate,
                Flag("pallor")?.16f:0,Flag("cyanosis")?.13f:0,Flag("flushing")?.12f:0,Flag("seizure"),Flag("bleeding"));
        }
    }

    public static class PatientBreathingAnimator
    {
        // Metres of visual excursion, not diagnostic measurements or CPR depth thresholds.
        public static float Excursion(PatientBreathingMode mode, float rate, float seconds)
        {
            if(mode==PatientBreathingMode.Absent || rate<=0) return 0;
            var phase=Mathf.Repeat(seconds*rate/60f,1f);
            var amplitude=mode switch { PatientBreathingMode.Shallow=>.002f, PatientBreathingMode.Labored=>.011f,
                PatientBreathingMode.Fast=>.007f, PatientBreathingMode.Slow=>.005f,PatientBreathingMode.Agonal=>.009f,_=>.006f };
            if(mode==PatientBreathingMode.Agonal)
            {
                // Sparse, unequal gasps. Never reuse the regular breathing curve for agonal motion.
                var period=Mathf.FloorToInt(seconds*rate/60f);
                if(period%4==1 || period%4==3) return 0;
                return amplitude*Mathf.Pow(Mathf.Max(0,Mathf.Sin(phase*Mathf.PI*2)),5)*(period%4==2?.55f:1f);
            }
            return amplitude*(1-Mathf.Cos(phase*Mathf.PI*2))*.5f;
        }
        public static float Awareness(PatientConsciousness state) => state switch {
            PatientConsciousness.Alert=>1,PatientConsciousness.Confused=>.5f,PatientConsciousness.Drowsy=>.15f,_=>0 };
    }
}
