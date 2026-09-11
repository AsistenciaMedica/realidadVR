using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace EmergencyVR.XR
{
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class ResetDroppedObject : MonoBehaviour
    {
        Vector3 initialPosition;
        Quaternion initialRotation;
        Rigidbody body;
        XRGrabInteractable grab;

        void Awake()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
        }

        void FixedUpdate()
        {
            if (body.position.y >= -2f || grab.isSelected) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = initialPosition;
            body.rotation = initialRotation;
        }
    }
}
