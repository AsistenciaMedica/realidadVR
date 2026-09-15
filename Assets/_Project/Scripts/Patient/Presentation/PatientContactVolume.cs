using System;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    // Oriented presentation volume, not a physics collider or an XR selection target.
    // A replacement character must author these against its actual moving anatomy.
    [Serializable]
    public sealed class PatientContactVolume
    {
        public string region;
        public Transform anchor;
        public Vector3 centre;
        public Vector3 size;
        public PatientContactVolume(string region,Transform anchor,Vector3 centre,Vector3 size)
        {
            this.region=region;this.anchor=anchor;this.centre=centre;this.size=size;
        }
        public bool Contains(Vector3 worldPosition,float margin)
        {
            if(anchor==null || !anchor.gameObject.activeInHierarchy || !Finite(worldPosition) || !Finite(size) ||
                !Finite(centre) || size.x<=0 || size.y<=0 || size.z<=0 || float.IsNaN(margin) || float.IsInfinity(margin)) return false;
            var local=anchor.InverseTransformPoint(worldPosition);var half=size*.5f;
            var nearest=new Vector3(Mathf.Clamp(local.x,centre.x-half.x,centre.x+half.x),
                Mathf.Clamp(local.y,centre.y-half.y,centre.y+half.y),Mathf.Clamp(local.z,centre.z-half.z,centre.z+half.z));
            // Compare in metres after transforming, so a scaled model does not scale the contact margin.
            return (anchor.TransformPoint(nearest)-worldPosition).sqrMagnitude<=Mathf.Max(0,margin)*Mathf.Max(0,margin)+.00000001f;
        }
        static bool Finite(Vector3 vector) => !float.IsNaN(vector.x)&&!float.IsInfinity(vector.x)&&
            !float.IsNaN(vector.y)&&!float.IsInfinity(vector.y)&&!float.IsNaN(vector.z)&&!float.IsInfinity(vector.z);
    }
}
