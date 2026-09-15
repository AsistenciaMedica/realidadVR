using System.Text;
using EmergencyVR.Evaluation;
using EmergencyVR.Scenarios;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    public sealed class TrainingPanel : MonoBehaviour
    {
        [SerializeField] ScenarioManager manager;
        [SerializeField] EvaluationManager evaluation;
        [SerializeField] Text status;
        [SerializeField] Button startButton;
        [SerializeField] Button transitionButton;
        [SerializeField] Button finishButton;

        public void Configure(ScenarioManager scenarioManager, EvaluationManager evaluationManager,
            Text statusText, Button start, Button transition, Button finish)
        {
            manager = scenarioManager;
            evaluation = evaluationManager;
            status = statusText;
            startButton = start;
            transitionButton = transition;
            finishButton = finish;
        }

        void Start()
        {
            ApplyBrand();
            manager.Changed += Refresh;
            startButton.onClick.AddListener(manager.StartCase);
            transitionButton.onClick.AddListener(manager.CompleteDemoTransition);
            finishButton.onClick.AddListener(manager.FinishCase);
            Refresh();
        }

        void ApplyBrand()
        {
            var background=transform.Find("Background")?.GetComponent<Image>();
            if(background!=null)background.color=VitalBrand.Navy;
            foreach(Transform child in transform){var text=child.GetComponent<Text>();if(text!=null)text.color=VitalBrand.White;}
            var title=transform.Find("Title")?.GetComponent<Text>();
            if(title!=null)
            {
                title.text="ENTRENAMIENTO";title.fontSize=28;title.fontStyle=FontStyle.Bold;title.alignment=TextAnchor.MiddleRight;
                title.rectTransform.anchoredPosition=new Vector2(200,270);title.rectTransform.sizeDelta=new Vector2(530,60);
                var logo=VitalBrand.AddLockup(transform,new Vector2(-300,285),new Vector2(315,46));
                if(logo==null){title.text="<i>Vital <color=#ED1939>VR</color></i> · ENTRENAMIENTO";title.supportRichText=true;title.rectTransform.anchoredPosition=new Vector2(0,270);title.rectTransform.sizeDelta=new Vector2(930,60);}
                else
                {
                    var tag=new GameObject("Vital VR tagline",typeof(RectTransform),typeof(Text));tag.transform.SetParent(transform,false);
                    var copy=tag.GetComponent<Text>();copy.font=title.font;copy.text=VitalBrand.Tagline;copy.fontSize=15;
                    copy.color=VitalBrand.Muted;copy.alignment=TextAnchor.MiddleCenter;copy.raycastTarget=false;
                    copy.rectTransform.anchoredPosition=new Vector2(-300,245);copy.rectTransform.sizeDelta=new Vector2(330,21);
                }
            }
            var notice=transform.Find("Notice")?.GetComponent<Text>();if(notice!=null)notice.color=VitalBrand.Muted;
            var controls=transform.Find("Controls")?.GetComponent<Text>();if(controls!=null)controls.color=VitalBrand.Muted;
            VitalBrand.StyleButton(startButton,true);VitalBrand.StyleButton(transitionButton);VitalBrand.StyleButton(finishButton);
        }

        void OnDestroy()
        {
            if (manager == null) return;
            manager.Changed -= Refresh;
            if (startButton != null) startButton.onClick.RemoveListener(manager.StartCase);
            if (transitionButton != null) transitionButton.onClick.RemoveListener(manager.CompleteDemoTransition);
            if (finishButton != null) finishButton.onClick.RemoveListener(manager.FinishCase);
        }

        void Refresh()
        {
            startButton.interactable = !manager.IsRunning;
            transitionButton.interactable = manager.IsRunning;
            finishButton.interactable = manager.IsRunning;
            VitalBrand.RefreshButtonTone(startButton);VitalBrand.RefreshButtonTone(transitionButton);VitalBrand.RefreshButtonTone(finishButton);
            if(manager.MedicalDefinition!=null)
            {
                transitionButton.interactable=false;
                VitalBrand.RefreshButtonTone(transitionButton);
                var medical=manager.MedicalResult;
                status.text="Vital VR · "+manager.MedicalDefinition.name+"\n"+manager.Feedback+
                    (medical==null?"\nUsa las acciones del selector. PENDING MEDICAL VALIDATION":$"\n{medical.scorePercent:0} / 100 · {medical.outcome}\nErrores críticos: {medical.criticalErrors.Length}. Ver debrief en el selector.");
                return;
            }
            var text = new StringBuilder(manager.Feedback);
            var session = manager.Session;
            if (session != null)
            {
                text.Append("\nEstado: ").Append(session.State);
                text.Append(" | Acciones: ").Append(session.ActionCount);
                if (manager.IsRunning && session.NextStep != null)
                    text.Append("\nSiguiente: ").Append(session.NextStep.Label);
                else if (manager.IsRunning) text.Append("\nPasos completos. Pulsa Finalizar.");
            }
            var result = evaluation.LatestResult;
            if (result != null)
            {
                text.Append("\nResultado técnico: ").Append(result.ScorePercent.ToString("0")).Append(" / 100");
                text.Append(" | ").Append(result.DurationSeconds.ToString("0.0")).Append(" s");
                text.Append("\nErrores: ").Append(result.Errors).Append(" | Tardías: ").Append(result.LateActions);
                text.Append(" | Omitidas: ").Append(result.OmittedActions.Count);
                text.Append(result.AllStepsCompleted ? "\nSecuencia completa." : "\nSecuencia incompleta.");
            }
            status.text = text.ToString();
        }
    }
}
