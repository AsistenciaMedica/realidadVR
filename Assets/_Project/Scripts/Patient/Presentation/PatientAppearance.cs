using System;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    /// <summary>
    /// A clothed skin for the shared patient skeleton: a Rocketbox mesh remapped to the patient's bone order.
    /// Cases swap it in and restore the default skin afterwards; physiology and rig stay untouched.
    /// </summary>
    public sealed class PatientAppearance : ScriptableObject
    {
        public Mesh mesh;
        public Material[] materials = Array.Empty<Material>();
        public string patientId, sourceModel, sourceCommit;
        public string[] boneNames = Array.Empty<string>();
    }
}
