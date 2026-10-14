using EmergencyVR.Desktop;
using UnityEngine;

namespace EmergencyVR.Medical.Interaction
{
    /// <summary>
    /// Stethoscope tubing drawn as a hanging curve. Resting in the bag it lies coiled next to the chest piece; when
    /// the learner holds it, the tubing runs from the chest piece up to the learner's ears, so in VR and on Windows
    /// it reads as "the stethoscope is on me and I am listening".
    /// </summary>
    public sealed class StethoscopeTubing : MonoBehaviour
    {
        const int Points = 24;
        LineRenderer line;
        MedicalPhysicalTool tool;
        Transform earpieces;
        DesktopDemoController desktop;
        float lookupAt;

        public void Initialize(LineRenderer tubing, Material metal)
        {
            line = tubing; line.positionCount = Points;
            // Two small earpiece tips that follow the end of the tubing when worn.
            earpieces = new GameObject("Earpieces").transform; earpieces.SetParent(transform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere); tip.name = "Earpiece";
                tip.transform.SetParent(earpieces, false); tip.transform.localPosition = new Vector3(side * .07f, 0, 0);
                tip.transform.localScale = Vector3.one * .012f; Destroy(tip.GetComponent<Collider>());
                tip.GetComponent<Renderer>().sharedMaterial = metal;
            }
        }

        void LateUpdate()
        {
            if (line == null) return;
            if (tool == null) tool = GetComponent<MedicalPhysicalTool>();
            if (Time.unscaledTime >= lookupAt) { desktop = FindFirstObjectByType<DesktopDemoController>(); lookupAt = Time.unscaledTime + 1; }
            var start = transform.TransformPoint(new Vector3(0, .02f, .018f));
            var view = desktop != null ? desktop.View : Camera.main;
            bool worn = tool != null && tool.IsHeld && view != null;
            Vector3 end; Vector3 sag;
            if (worn)
            {
                // Ends at the ears: just below and behind the eyes.
                var head = view.transform;
                end = head.position - head.up * .1f - head.forward * .04f;
                sag = Vector3.down * Mathf.Clamp(Vector3.Distance(start, end) * .35f, .05f, .3f);
                earpieces.gameObject.SetActive(true);
                earpieces.SetPositionAndRotation(end, Quaternion.LookRotation(head.forward, head.up));
            }
            else
            {
                // Coiled loop beside the chest piece in the bag.
                end = transform.TransformPoint(new Vector3(-.09f, .006f, .05f));
                sag = Vector3.zero;
                earpieces.gameObject.SetActive(false);
            }
            for (int i = 0; i < Points; i++)
            {
                float t = i / (float)(Points - 1);
                Vector3 p;
                if (worn) p = Vector3.Lerp(start, end, t) + sag * (4 * t * (1 - t));
                else
                {
                    // Flat spiral on the bag surface.
                    float angle = t * Mathf.PI * 3.2f, radius = .035f + .02f * t;
                    p = Vector3.Lerp(start, end, t * .35f) + transform.rotation * new Vector3(Mathf.Cos(angle) * radius - .04f, .004f, Mathf.Sin(angle) * radius);
                }
                line.SetPosition(i, p);
            }
        }
    }
}
