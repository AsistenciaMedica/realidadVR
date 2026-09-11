using EmergencyVR.Scenarios;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EmergencyVR.XR
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class PatientInteraction : MonoBehaviour
    {
        [SerializeField] ScenarioManager scenarioManager;
        [SerializeField] string actionId = "demo.inspect";
        XRSimpleInteractable interactable;

        public void Configure(ScenarioManager manager) { scenarioManager = manager; }

        void OnEnable()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);
        }

        void OnDisable()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(OnSelected);
        }

        void OnSelected(SelectEnterEventArgs args)
        {
            if (scenarioManager != null) scenarioManager.SubmitAction(actionId);
        }
    }
}
