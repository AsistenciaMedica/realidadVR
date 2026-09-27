using UnityEngine;
using UnityEngine.UI;

namespace EmergencyVR.UI
{
    // Shared presentation tokens. Product branding never changes scenario or interaction identifiers.
    public static class VitalBrand
    {
        public const string ProductName="VITAL VR";
        public const string Tagline="Simulación y entrenamiento clínico inmersivo";
        public static readonly Color Navy=new Color32(3,17,35,255);
        public static readonly Color Surface=new Color32(11,32,55,255);
        public static readonly Color Red=new Color32(237,25,57,255);
        public static readonly Color Action=new Color32(200,16,46,255);
        public static readonly Color Cyan=new Color32(8,173,214,255);
        public static readonly Color White=new Color32(247,249,252,255);
        public static readonly Color Muted=new Color32(173,190,208,255);
        public static readonly Color Line=new Color32(41,66,92,255);

        public static Texture2D LoadLockup() => Resources.Load<Texture2D>("Branding/VitalVRLockup");
        public static Texture2D LoadMark() => Resources.Load<Texture2D>("Branding/VitalVRMark");

        public static RawImage AddLockup(Transform parent,Vector2 position,Vector2 available)
        {
            var texture=LoadLockup();if(texture==null)return null;
            var go=new GameObject("VITAL VR brand",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);
            var image=go.GetComponent<RawImage>();image.texture=texture;image.color=Color.white;image.raycastTarget=false;
            var rect=go.GetComponent<RectTransform>();rect.anchoredPosition=position;
            float ratio=Mathf.Min(available.x/texture.width,available.y/texture.height);
            rect.sizeDelta=new Vector2(texture.width*ratio,texture.height*ratio);return image;
        }

        public static void StyleButton(Button button,bool primary=false)
        {
            if(button==null)return;
            var image=button.targetGraphic as Image;if(image!=null)image.color=Color.white;
            var colors=button.colors;
            colors.normalColor=primary?Action:Surface;
            colors.highlightedColor=primary?Color.Lerp(Action,Red,.2f):Color.Lerp(Surface,Line,.8f);
            colors.pressedColor=primary?Color.Lerp(Action,Navy,.25f):Line;
            colors.selectedColor=primary?Color.Lerp(Action,Red,.2f):Color.Lerp(Surface,Cyan,.24f);
            colors.disabledColor=Color.Lerp(Surface,Navy,.4f);
            colors.colorMultiplier=1;colors.fadeDuration=.12f;button.colors=colors;
            foreach(var text in button.GetComponentsInChildren<Text>())text.color=White;
        }

        public static void RefreshButtonTone(Button button)
        {
            if(button==null)return;
            foreach(var text in button.GetComponentsInChildren<Text>())text.color=button.interactable?White:Muted;
        }
    }
}
