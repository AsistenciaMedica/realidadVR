using System;
using EmergencyVR.Core;
using UnityEngine;

namespace EmergencyVR.Evaluation
{
    public sealed class EvaluationManager : MonoBehaviour
    {
        public EvaluationResult LatestResult { get; private set; }
        public event Action<EvaluationResult> ResultAvailable;
        public void Clear() { LatestResult = null; }
        public void Publish(EvaluationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            LatestResult = result;
            ResultAvailable?.Invoke(result);
        }
    }
}
