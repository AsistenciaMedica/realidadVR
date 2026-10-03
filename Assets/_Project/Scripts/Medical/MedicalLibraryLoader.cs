using System;
using UnityEngine;

namespace EmergencyVR.Medical
{
    public static class MedicalLibraryLoader
    {
        public static MedicalLibrary Load()
        {
            var data=Resources.Load<TextAsset>("MedicalScenarios");
            if(data==null) throw new InvalidOperationException("Missing MedicalScenarios.json. Existing technical demo remains available.");
            var library=JsonUtility.FromJson<MedicalLibrary>(data.text);
            library.Validate();
            EmergencyVR.Scenarios.ClinicalScenarioV2Catalog.LoadInto(library);
            EmergencyVR.Scenarios.PatientRoster.Load().Apply(library, EmergencyVR.Scenarios.ReleaseScope.Load(library));
            library.Validate();
            return library;
        }
    }
}
