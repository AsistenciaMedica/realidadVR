using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EmergencyVR.Environment
{
    // Serialized ownership manifest; no runtime behaviour or dependency on clinical/XR systems.
    [DisallowMultipleComponent]
    public sealed class GeneratedEnvironment : MonoBehaviour
    {
        public const string GeneratorId = "EmergencyVR.TrainingRoom.Environment.v1";
        [HideInInspector] public string generatorId = GeneratorId;
        [HideInInspector] public List<GameObject> ownedObjects = new List<GameObject>();
        [HideInInspector] public List<PreservedObject> preservedObjects = new List<PreservedObject>();
        [HideInInspector] public AmbientMode previousAmbientMode;
        [HideInInspector] public Color previousAmbientLight;
        [HideInInspector] public List<PreservedPresentation> presentation = new List<PreservedPresentation>();
        [HideInInspector] public List<PreservedMaterials> materialOverrides = new List<PreservedMaterials>();
        [HideInInspector] public Light existingRoomLight;
        [HideInInspector] public float previousLightIntensity;
    }

    [Serializable]
    public sealed class PreservedObject
    {
        public GameObject target;
        public bool activeSelf;
    }

    [Serializable]
    public sealed class PreservedPresentation
    {
        public Transform target;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [Serializable]
    public sealed class PreservedMaterials
    {
        public Renderer target;
        public Material[] materials;
    }
}
