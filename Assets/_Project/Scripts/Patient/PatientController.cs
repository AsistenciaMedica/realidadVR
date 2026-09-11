using System;
using EmergencyVR.Core;
using UnityEngine;
using UnityEngine.Events;

namespace EmergencyVR.Patient
{
    [Serializable] public sealed class PatientStateEvent : UnityEvent<PatientState> { }

    // Presentation adapter only: clinical transitions belong to CaseSession.
    public sealed class PatientController : MonoBehaviour
    {
        [SerializeField] Renderer body;
        [SerializeField] PatientStateEvent onStateChanged;
        MaterialPropertyBlock properties;
        public PatientState State { get; private set; } = PatientState.Normal;
        public PatientStateEvent OnStateChanged { get { return onStateChanged; } }

        void Awake()
        {
            if (onStateChanged == null) onStateChanged = new PatientStateEvent();
            properties = new MaterialPropertyBlock();
        }

        public void Configure(Renderer bodyRenderer) { body = bodyRenderer; }

        public void Present(PatientState state)
        {
            State = state;
            if (body != null)
            {
                // Debug representation, not a medical appearance model.
                var color = state == PatientState.Recovered ? new Color(0.1f, 0.7f, 0.4f) :
                    state == PatientState.Recovering ? new Color(0.9f, 0.65f, 0.2f) :
                    new Color(0.2f, 0.55f, 0.85f);
                body.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", color);
                body.SetPropertyBlock(properties);
            }
            onStateChanged.Invoke(state);
        }
    }
}
