using System;
using System.Collections;
using System.IO;
using System.Linq;
using EmergencyVR.Dialogue;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyVR.Desktop
{
    // Explicit QA entry point in a real desktop player. Normal launches do not install it.
    public sealed class Case01PlayerValidation : MonoBehaviour
    {
        string output;
        TrainingExperience flow;
        DesktopDemoController desktop;
        Case01PatientPresentation body;
        ClinicalHelpController help;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if(!System.Environment.GetCommandLineArgs().Contains("-vital-case01-smoke"))return;
            SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
        }
        static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name=="TrainingRoom")new GameObject("CASE01 desktop acceptance").AddComponent<Case01PlayerValidation>();
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            var args=System.Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-vital-case01-captures");
            output=index>=0&&index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"Case01Captures");
            Directory.CreateDirectory(output);
            var run=Run();Exception failure=null;
            while(true)
            {
                object next=null;bool more=false;
                try{more=run.MoveNext();if(more)next=run.Current;}catch(Exception error){failure=error;}
                if(failure!=null||!more)break;
                yield return next;
            }
            if(failure!=null)
            {
                Debug.LogException(failure);yield return Capture("99-failure");
                File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL\n"+failure);
                Debug.Log("CASE01_PLAYER_SMOKE FAIL "+failure.Message);Application.Quit(1);
            }
            else
            {
                File.WriteAllText(Path.Combine(output,"result.txt"),"PASS\nCASE01 complete desktop route, clinical result and captures.");
                Debug.Log("CASE01_PLAYER_SMOKE PASS");Application.Quit(0);
            }
        }
        IEnumerator Run()
        {
            yield return null;yield return null;yield return null;
            flow=FindFirstObjectByType<TrainingExperience>();desktop=FindFirstObjectByType<DesktopDemoController>();
            Require(flow!=null&&desktop!=null,"Desktop experience must be installed.");
            yield return Capture("00-welcome");
            int index=Array.FindIndex(flow.Review.Catalog.entries,e=>e.medical?.id=="review-hypotension-v2");
            Require(index>=0,"CASE01 must be selectable.");
            flow.Prepare(index);yield return null;yield return Capture("01-briefing");
            flow.BeginTraining();yield return null;yield return null;
            body=flow.Review.GetComponent<Case01PatientPresentation>();help=flow.Review.GetComponent<ClinicalHelpController>();
            Require(body!=null&&body.IsActive,"Daniel's body presentation must be active.");
            var manager=flow.Review.Manager;
            var destination=body.AssistanceAnchor.position;
            desktop.Body.enabled=false;
            desktop.transform.position+=destination-desktop.View.transform.position;
            desktop.View.transform.rotation=Quaternion.LookRotation(body.HeadPosition-desktop.View.transform.position,Vector3.up);
            Physics.SyncTransforms();yield return null;
            yield return Capture("02-seated-interface");
            flow.InterfaceCanvas.enabled=false;yield return Capture("03-seated-body");flow.InterfaceCanvas.enabled=true;
            Require(manager.TrySubmitAction("AssessResponsiveness"),"Response assessment.");
            Require(manager.TrySubmitAction("ObserveBreathing"),"Breathing observation.");
            Require(manager.Dialogue.Ask(DialogueIntent.MAIN_SYMPTOM)!=null,"Main symptom.");
            Require(manager.Dialogue.Ask(DialogueIntent.ONSET)!=null,"Onset question.");
            Require(help.RequestCall(false),"Early simulated call.");
            manager.AdvanceTrainingTime(help.ContactPresentationSeconds+.1);yield return null;
            Require(help.CommunicateLocation(),"Location communication.");
            Require(help.CommunicateSituation(),"Observed situation communication.");
            Require(help.ConfirmOperator(),"Explicit simulated operator acknowledgement.");
            Require(body.BeginAssistance(),"Safe route: "+body.LastValidationFailure);
            body.SetSupportHeld(true);
            float deadline=Time.realtimeSinceStartup+body.assistanceSeconds+15;bool halfway=false;
            while(!body.FinalPositionValidated&&Time.realtimeSinceStartup<deadline)
            {
                if(!halfway&&body.Progress>.45f)
                {
                    halfway=true;flow.InterfaceCanvas.enabled=false;
                    yield return Capture("04-assisted-transition");flow.InterfaceCanvas.enabled=true;
                }
                yield return null;
            }
            Require(body.FinalPositionValidated,"Physical completion: "+body.Instruction+" / "+body.LastValidationFailure);
            flow.InterfaceCanvas.enabled=false;yield return Capture("05-supine-body");flow.InterfaceCanvas.enabled=true;
            manager.AdvanceTrainingTime(61);yield return null;
            Require(manager.TrySubmitAction("ReassessPatient"),"Reassessment with prior observations.");
            manager.Dialogue.Ask(DialogueIntent.CURRENT_STATUS);
            yield return Capture("06-observation");
            Require(help.PerformHandover(),"Handover after help confirmation.");
            flow.FinishTraining();yield return null;yield return Capture("07-debrief");
            Require(manager.MedicalSession.ObjectiveProgress.All(o=>o.result==ObjectiveResult.AchievedIndependently),"All five objectives require evidence.");
            Require(manager.MedicalSession.Observations.Measurements.Count==0,"I0 must not invent vital measurements.");
            string report=flow.Review.ExportResult();File.Copy(report,Path.Combine(output,"attempt.json"),true);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),RuntimeCapture.EncodePng(texture));Destroy(texture);
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
