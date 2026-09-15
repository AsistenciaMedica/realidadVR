using System;
using System.Collections;
using System.IO;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Medical.Interaction;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Desktop
{
    // Executable smoke harness: exercises the same physical components used by desktop/XR.
    public static class ProcedureValidation
    {
        public static IEnumerator Run(ReviewCaseSession review,Camera camera=null,string screenshots=null)
        {
            int index=Array.FindIndex(review.Catalog.entries,e=>e.medical?.id=="arrest-witnessed");
            Require(review.Select(index),"Physical case selection");
            review.Manager.StartCase(); var rig=review.Procedures;
            review.Submit("CheckSceneSafety");review.Submit("CheckResponsiveness");
            Tool(MedicalToolKind.Phone).Use();review.Submit("CheckBreathing");
            rig.CPR.Feed(.055f,0,0,true,"AUTOMATED_COMPONENT_TEST");
            yield return null;
            Capture("vital-cpr",new Vector3(2.75f,1.55f,1.25f),rig.Visuals.ChestAnchor.position);
            rig.CPR.ReleaseContact();
            Require(rig.CPR.Metrics.compressions==1,"Manual compression/release");
            Require(review.Manager.MedicalSession.Completed.Contains("StartCPR"),"Physical CPR reached medical engine");
            var aed=Tool(MedicalToolKind.AED);aed.OnGrabbed();
            aed.transform.SetPositionAndRotation(new Vector3(rig.Visuals.ChestAnchor.position.x+.62f,.11f,rig.Visuals.ChestAnchor.position.z-.3f),Quaternion.identity);
            aed.Use();aed.Use();
            foreach(bool right in new[]{true,false})
            {
                var pad=Tool(right?MedicalToolKind.RightPad:MedicalToolKind.LeftPad);
                pad.OnGrabbed();pad.Use();
                var anchor=right?rig.Visuals.AedRightPadAnchor:rig.Visuals.AedLeftPadAnchor;
                pad.transform.SetPositionAndRotation(anchor.position,anchor.rotation);
                Require(pad.OnReleased(),"Pad physical placement");
            }
            yield return new WaitForSeconds(.25f);
            Capture("vital-aed",new Vector3(2.8f,1.4f,2.9f),rig.Visuals.ChestAnchor.position);
            rig.CPR.Feed(.01f,0,0,true,"AUTOMATED_COMPONENT_TEST");
            aed.Use();Require(rig.AED.State.Phase==AEDPhase.PadsReady,"AED must reject contact");
            rig.CPR.ReleaseContact();
            rig.CPR.SetProximityContact(rig.Visuals.IsPatientContact(rig.Visuals.Rig.head.position));
            aed.Use();Require(rig.AED.State.Phase==AEDPhase.PadsReady,"AED must also reject head contact without a compression");
            rig.CPR.SetProximityContact(false);aed.Use();
            Require(rig.AED.State.Phase==AEDPhase.Analysing,"AED analysis started");
            yield return new WaitForSeconds((float)rig.Settings.readingDelaySeconds+.15f);
            Require(rig.AED.State.Phase==AEDPhase.ShockAdvised,"Shockable analysis result");
            aed.Use();Require(rig.AED.State.Shocks==1,"Indicated simulated shock");
            rig.CPR.Feed(.055f,0,0,true,"AUTOMATED_COMPONENT_TEST");rig.CPR.ReleaseContact();
            review.Submit("Handover");review.Manager.FinishCase();
            Require(review.Manager.MedicalResult.procedures?.shocks==1,"Procedure metrics exported");
            Require(review.Manager.MedicalResult.procedures.cpr.compressions>=2,"Compression metrics exported");
            review.ExportResult();

            MedicalPhysicalTool Tool(MedicalToolKind kind)=>UnityEngine.Object.FindObjectsByType<MedicalPhysicalTool>(FindObjectsSortMode.None).Single(t=>t.Kind==kind);
            void Capture(string name,Vector3 position,Vector3 target)
            {
                if(camera==null||string.IsNullOrEmpty(screenshots))return;
                camera.transform.position=position;camera.transform.LookAt(target);RuntimeCapture.Save(camera,Path.Combine(screenshots,name+".png"));
            }
        }
        static void Require(bool condition,string detail){if(!condition)throw new InvalidOperationException("Procedure smoke failed: "+detail);}
    }
}
