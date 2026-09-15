using UnityEngine;

namespace EmergencyVR.Medical.Interaction
{
    // Fit generated TextMesh readouts inside their physical screen, including changing AED prompts.
    public sealed class MedicalDeviceTextFit : MonoBehaviour
    {
        public Vector2 Size;
        Renderer textRenderer;
        void LateUpdate()
        {
            if(textRenderer==null)textRenderer=GetComponent<Renderer>();
            if(textRenderer==null)return;
            var bounds=textRenderer.localBounds.size;
            if(bounds.x<=0||bounds.y<=0)return;
            float scale=Mathf.Min(1,Size.x/bounds.x,Size.y/bounds.y);
            transform.localScale=Vector3.one*scale;
        }
        public static void Fit(TextMesh text,float width,float height)
        {
            text.gameObject.AddComponent<MedicalDeviceTextFit>().Size=new Vector2(width,height);
        }
    }
}
