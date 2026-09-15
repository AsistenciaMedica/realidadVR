using EmergencyVR.Scenarios;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    // Uses the existing world-space Canvas/raycaster. No second XR input system.
    public sealed class ReviewCasePanel : MonoBehaviour
    {
        ReviewCaseSession review;
        Text title,briefing,feedback;
        Button previous,next;
        Button[] actions;
        Text[] actionLabels;
        int actionPage, detailPage;
        bool debrief;

        public static void Attach(ReviewCaseSession session)
        {
            var existing=Object.FindFirstObjectByType<TrainingPanel>();
            if(existing==null) return;
            var root=new GameObject("Review cases",typeof(RectTransform),typeof(Image));
            root.transform.SetParent(existing.transform,false);
            var rect=root.GetComponent<RectTransform>(); rect.anchoredPosition=new Vector2(1060,0); rect.sizeDelta=new Vector2(1000,920);
            root.GetComponent<Image>().color=VitalBrand.Navy;
            root.AddComponent<ReviewCasePanel>().Build(session);
        }

        void Build(ReviewCaseSession session)
        {
            review=session;
            title=Label("Title",new Vector2(0,415),new Vector2(950,60),28);
            if(VitalBrand.AddLockup(transform,new Vector2(-350,422),new Vector2(240,44))!=null)
            {
                title.rectTransform.anchoredPosition=new Vector2(125,415);title.rectTransform.sizeDelta=new Vector2(690,60);
                title.alignment=TextAnchor.MiddleRight;title.fontSize=24;
                var tagline=Label("Vital VR tagline",new Vector2(-350,387),new Vector2(245,18),12);
                tagline.text=VitalBrand.Tagline;tagline.color=VitalBrand.Muted;
            }
            previous=Button("Caso anterior",new Vector2(-250,345),new Vector2(470,65),()=>review.Select((review.SelectedIndex+review.Catalog.entries.Length-1)%review.Catalog.entries.Length));
            next=Button("Caso siguiente",new Vector2(250,345),new Vector2(470,65),()=>review.Select((review.SelectedIndex+1)%review.Catalog.entries.Length));
            briefing=Label("Briefing",new Vector2(0,190),new Vector2(930,225),21);
            actions=new Button[8]; actionLabels=new Text[8];
            for(int i=0;i<actions.Length;i++)
            {
                int index=i;
                actions[i]=Button("Acción "+i,new Vector2(i%2==0?-240:240,15-(i/2)*83),new Vector2(465,76),()=> { var ids=review.ActionIds;int k=actionPage*8+index;if(k<ids.Length)review.Submit(ids[k]); });
                actionLabels[i]=actions[i].GetComponentInChildren<Text>(); actionLabels[i].fontSize=22;
            }
            Button("Más acciones",new Vector2(-330,-315),new Vector2(300,55),()=>{actionPage=(actionPage+1)%Mathf.CeilToInt(review.ActionIds.Length/8f);Refresh();});
            Button("+30s simulados",new Vector2(0,-315),new Vector2(300,55),()=>review.Manager.AdvanceTrainingTime(30));
            Button("Detalle / debrief",new Vector2(330,-315),new Vector2(300,55),()=>{debrief=true;detailPage++;Refresh();});
            Button("Exportar JSON",new Vector2(-330,-382),new Vector2(300,55),()=>{review.ExportResult();Refresh();});
            Button("Seed +1",new Vector2(0,-382),new Vector2(300,55),()=>{if(!review.Manager.IsRunning)review.Manager.MedicalSeed++;Refresh();});
            Button("Entrenar / evaluar",new Vector2(330,-382),new Vector2(300,55),()=>{if(!review.Manager.IsRunning)review.Procedures.TrainingMode=!review.Procedures.TrainingMode;Refresh();});
            feedback=Label("ReviewStatus",new Vector2(0,-432),new Vector2(930,48),18);
            feedback.color=VitalBrand.Muted;
            review.SelectionChanged+=SelectionChanged; review.Manager.Changed+=Refresh; Refresh();
        }
        void SelectionChanged() { actionPage=0;detailPage=0;debrief=false;Refresh(); }

        void Refresh()
        {
            title.text="Vital VR · "+(review.SelectedIndex+1)+"/"+review.Catalog.entries.Length+" · Seed "+review.Manager.MedicalSeed;
            briefing.text=review.Selected.definition.displayName+"\n"+(review.Procedures.TrainingMode?review.PatientReadout():"Evaluación: observar paciente y consultar instrumentos.");
            if(debrief)
            {
                var result=review.Manager.MedicalResult;
                var pages=result==null?new[]{review.Selected.briefing,review.PatientReadout(),"Fuentes:\n"+string.Join("\n",review.Selected.sourceUrls)}:
                    new[]{result.caseName+"\n"+result.scorePercent.ToString("0")+"/100 · "+result.outcome,"ERRORES CRÍTICOS\n"+string.Join("\n",result.criticalErrors),"Omitidas:\n"+string.Join(", ",result.omittedActions)}
                    .Concat(result.sections.Select(s=>s.name+": "+(s.measured?s.scorePercent.ToString("0")+"/100":"No evaluado")))
                    .Concat(result.timeline.Select(e=>e.elapsedSeconds.ToString("0")+" s · "+e.id+"\n"+e.disposition+"\n"+e.message)).Concat(result.recommendations).Concat(result.sourceUrls).ToArray();
                briefing.text=pages[detailPage%pages.Length];
            }
            previous.interactable=next.interactable=!review.Manager.IsRunning;
            VitalBrand.RefreshButtonTone(previous);VitalBrand.RefreshButtonTone(next);
            var ids=review.ActionIds;
            actionPage=Mathf.Min(actionPage,Mathf.CeilToInt(ids.Length/8f)-1);
            for(int i=0;i<actions.Length;i++)
            {
                int k=actionPage*8+i; bool exists=k<ids.Length;
                actions[i].gameObject.SetActive(exists);
                if(exists)
                {
                    bool physical=review.Procedures.IsPhysicalAction(ids[k]);
                    actionLabels[i].text=review.ActionLabel(ids[k])+(physical?" · usar equipo":""); actions[i].interactable=review.Manager.IsRunning&&!physical;
                    VitalBrand.RefreshButtonTone(actions[i]);
                }
            }
            feedback.text="PENDING MEDICAL VALIDATION · "+review.Manager.Feedback;
        }

        Text Label(string name,Vector2 position,Vector2 size,int fontSize,Transform parent=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent??transform,false);
            var rt=go.GetComponent<RectTransform>(); rt.anchoredPosition=position; rt.sizeDelta=size;
            var text=go.GetComponent<Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=fontSize;
            text.alignment=TextAnchor.MiddleCenter; text.color=VitalBrand.White; text.raycastTarget=false; return text;
        }
        Button Button(string label,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction callback)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(transform,false);
            var rt=go.GetComponent<RectTransform>(); rt.anchoredPosition=position; rt.sizeDelta=size;
            var button=go.GetComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(callback);
            Label("Label",Vector2.zero,size-new Vector2(18,8),25,go.transform).text=label;VitalBrand.StyleButton(button);return button;
        }
        void OnDestroy() { if(review!=null) { review.SelectionChanged-=SelectionChanged; if(review.Manager!=null) review.Manager.Changed-=Refresh; } }
    }
}
