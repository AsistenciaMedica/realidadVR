using UnityEngine;

namespace EmergencyVR.Desktop
{
    // Optional distant spectators only; never attach to the witness or patient.
    public sealed class QuestDistantAudience : MonoBehaviour
    {
        void Start() => SetVisible(!QuestQualityControl.Smooth);
        public void SetVisible(bool visible)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
        }
    }
}
