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
            manager.Changed += Refresh;
            startButton.onClick.AddListener(manager.StartCase);
            transitionButton.onClick.AddListener(manager.CompleteDemoTransition);
            finishButton.onClick.AddListener(manager.FinishCase);
            Refresh();
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
