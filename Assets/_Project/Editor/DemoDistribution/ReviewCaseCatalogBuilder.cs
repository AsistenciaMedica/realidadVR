using System.Collections.Generic;
using System.IO;
using EmergencyVR.Core;
using EmergencyVR.Scenarios;
using UnityEditor;
using UnityEngine;

namespace EmergencyVR.Editor
{
    public static class ReviewCaseCatalogBuilder
    {
        public const string CatalogPath="Assets/_Project/Resources/ReviewCaseCatalog.asset";
        const string Cases="Assets/_Project/ScriptableObjects/Cases/Review";
        const string Bls="https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/adult-basic-life-support-guidelines";
        const string FirstAid="https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/first-aid-guidelines";

        [MenuItem("Emergency VR/Demo/Prepare review cases")]
        public static void Generate()
        {
            Directory.CreateDirectory(Cases); Directory.CreateDirectory("Assets/_Project/Resources"); AssetDatabase.Refresh();
            CreateEnvironmentPrefabLibrary();
            var catalog=AssetDatabase.LoadAssetAtPath<ReviewCaseCatalog>(CatalogPath);
            if(catalog!=null) { Debug.Log("Review catalog already exists; author edits preserved."); return; }
            var entries=new List<ReviewCaseEntry>();
            entries.Add(new ReviewCaseEntry { definition=AssetDatabase.LoadAssetAtPath<ClinicalCaseDefinition>(DemoProjectBuilder.CasePath),
                briefing="Prueba técnica original de interacción y puntuación.",limitations="Transiciones arbitrarias; no es un protocolo médico.",sourceUrls=new string[0],reviewedOn="2026-09-13" });
            entries.Add(Case("fainting","Desvanecimiento",PatientState.Conscious,
                "Adulto que acaba de recuperar la respuesta tras un desvanecimiento. Respira normalmente, sin trauma ni signos de alarma en este guion.",
                new[]{"https://www.nhs.uk/symptoms/fainting/"},
                "scene.safe|Comprobar seguridad", "demo.inspect|Valorar respuesta y respiración normal", "position.supine|Registrar decúbito y piernas elevadas (sin lesión)",
                "reassess|Reevaluar respuesta y signos de alarma", "handover|Solicitar valoración médica y comunicar hallazgos"));
            entries.Add(Case("unconscious-breathing","Inconsciente con respiración",PatientState.UnconsciousBreathing,
                "Adulto que no responde y mantiene respiración normal; no hay trauma en este guion. No se simula recuperación automática.",new[]{Bls,FirstAid},
                "scene.safe|Comprobar seguridad", "demo.inspect|Comprobar respuesta", "help.call|Activar emergencias y pedir ayuda", "breathing.normal|Confirmar respiración normal",
                "position.recovery|Registrar posición lateral (sin trauma)", "monitor.continuous|Vigilar respiración hasta relevo"));
            entries.Add(Case("hypoglycaemia","Hipoglucemia consciente",PatientState.Conscious,
                "Adulto con diabetes, consciente y capaz de tragar. Glucemia ficticia: 3,2 mmol/L. Tras la espera simulada: 4,5 mmol/L y mejoría referida.",
                new[]{"https://www.nhs.uk/conditions/low-blood-sugar-hypoglycaemia/"},
                "scene.safe|Comprobar seguridad", "demo.inspect|Valorar consciencia y deglución segura", "measure.glucose|Consultar glucemia ficticia: 3,2 mmol/L",
                "glucose.oral|Registrar azúcar de absorción rápida (consciente)", "glucose.recheck|Simular espera de 10–15 min y nueva medición", "handover|Registrar reevaluación y seguimiento"));
            entries.Add(Case("hypotension","Hipotensión sintomática",PatientState.Conscious,
                "Adulto consciente con mareo persistente. TA ficticia: 85/55 mmHg. No se deduce la causa ni se indican fármacos o fluidos.",
                new[]{"https://www.nhs.uk/conditions/low-blood-pressure-hypotension/","https://www.resus.org.uk/library/abcde-approach"},
                "scene.safe|Comprobar seguridad", "demo.inspect|Valorar respuesta y síntomas", "measure.bp|Consultar TA ficticia: 85/55 mmHg",
                "measure.spo2|Consultar SpO2 ficticia: 97 %", "help.call|Solicitar evaluación por síntomas persistentes", "reassess|Reevaluar y comunicar hallazgos"));
            entries.Add(Case("abnormal-breathing","Sin respiración normal · BLS",PatientState.UnconsciousNotBreathing,
                "Adulto que no responde y no respira normalmente. Rama de reanimador lego: sospechar parada cardíaca. No equivale a parada respiratoria aislada con pulso confirmado. DEA del guion: descarga NO indicada.",
                new[]{Bls}, "scene.safe|Comprobar seguridad", "demo.inspect|Comprobar respuesta", "help.call|Activar emergencias y solicitar DEA",
                "breathing.abnormal|Reconocer ausencia de respiración normal", "cpr.start|Registrar inicio de RCP (sin medir compresiones)",
                "aed.analyse|Registrar conexión y análisis del DEA", "aed.no-shock|Seguir indicación: NO descargar; continuar RCP", "handover|Continuar atención hasta relevo"));
            catalog=ScriptableObject.CreateInstance<ReviewCaseCatalog>(); catalog.entries=entries.ToArray();
            AssetDatabase.CreateAsset(catalog,CatalogPath); AssetDatabase.SaveAssets();
            Debug.Log("Six review entries created: technical demo plus five provisional guided cases.");
        }

        static void CreateEnvironmentPrefabLibrary()
        {
            const string path="Assets/_Project/Resources/EnvironmentPrefabLibrary.asset";
            var library=AssetDatabase.LoadAssetAtPath<EmergencyVR.Environment.Presentation.EnvironmentPrefabLibrary>(path);
            if(library!=null)return;
            library=ScriptableObject.CreateInstance<EmergencyVR.Environment.Presentation.EnvironmentPrefabLibrary>();
            var entries=new List<EmergencyVR.Environment.Presentation.EnvironmentPrefabLibrary.Entry>();
            foreach(var item in new[]{"Furniture/MedicalCart","MedicalEquipment/DefibrillatorPlaceholder","Furniture/MedicalCabinet","Furniture/Stool","Furniture/SideTable","MedicalEquipment/Supplies","MedicalEquipment/PatientMonitor","MedicalEquipment/OxygenTank"})
            {
                var assetPath=item.Replace("MedicalEquipment/","Medical/");
                if(item=="MedicalEquipment/Supplies")assetPath="Props/Supplies";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Prefabs/Generated/"+assetPath+".prefab");
                if(prefab==null)throw new System.InvalidOperationException("Missing reusable environment prefab: "+assetPath);
                entries.Add(new EmergencyVR.Environment.Presentation.EnvironmentPrefabLibrary.Entry {path=item,prefab=prefab});
            }
            library.entries=entries.ToArray();AssetDatabase.CreateAsset(library,path);AssetDatabase.SaveAssets();
        }

        static ReviewCaseEntry Case(string id,string title,PatientState state,string briefing,string[] sources,params string[] actions)
        {
            var path=Cases+"/"+id+".asset";
            var definition=AssetDatabase.LoadAssetAtPath<ClinicalCaseDefinition>(path);
            if(definition==null)
            {
                definition=ScriptableObject.CreateInstance<ClinicalCaseDefinition>();
                definition.caseId="review-"+id+"-v1"; definition.displayName=title;
                definition.description=briefing; definition.isTechnicalDemo=false; definition.clinicallyApproved=false;
                definition.initialState=state;
                foreach(var item in actions)
                {
                    var split=item.Split('|');
                    definition.steps.Add(new CaseStepData {actionId=split[0],label=split[1],fromState=state,toState=state,points=10,deadlineSeconds=0});
                }
                definition.ToDomain(); AssetDatabase.CreateAsset(definition,path);
            }
            return new ReviewCaseEntry {definition=definition,briefing=briefing,sourceUrls=sources,reviewedOn="2026-09-13",
                limitations="Piloto adulto pendiente de validación clínica/local. Acciones declarativas, secuencia y puntos provisionales; sin garantía de recuperación ni mediciones físicas. No administrar nada por boca si no hay deglución segura."};
        }
    }
}
