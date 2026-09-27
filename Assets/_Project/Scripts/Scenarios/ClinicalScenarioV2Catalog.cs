using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Medical;
using UnityEngine;

namespace EmergencyVR.Scenarios
{
    /// <summary>Composes optional clinical definitions with the intact legacy library. No runtime state lives here.</summary>
    public static class ClinicalScenarioV2Catalog
    {
        const string RegistryResource = "ClinicalScenarioV2Registry";

        public static void LoadInto(MedicalLibrary library)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            var registry = Resources.Load<ClinicalScenarioV2Registry>(RegistryResource);
            if (registry == null) throw new InvalidOperationException("Missing clinical V2 registry.");
            if (registry.entries == null) throw new InvalidOperationException("Missing clinical V2 registry entries.");
            var scenarios = library.scenarios.ToList();
            var actions = library.actions.ToList();
            var registered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in registry.entries)
            {
                if (entry == null) throw new InvalidOperationException("Null clinical V2 registration.");
                var definition = ReadDefinition(entry.definition);
                if (!registered.Add(definition.metadata.scenarioId)) throw new InvalidOperationException("Duplicate clinical V2 registration.");
                var existing = scenarios.FirstOrDefault(x => x.id == definition.metadata.scenarioId);
                if (existing != null)
                {
                    if (existing.clinicalV2 == null || JsonUtility.ToJson(existing.clinicalV2) != JsonUtility.ToJson(definition))
                        throw new InvalidOperationException("A clinical V2 registration would replace an existing case.");
                    continue;
                }
                var scenario = ToMedicalDefinition(definition);
                foreach (var semantic in scenario.actions.Select(x => x.action).Distinct())
                    if (!actions.Any(x => x.id == semantic)) actions.Add(new MedicalAction
                    {
                        id = semantic, label = ActionLabel(semantic), section = "Clinical interaction",
                        description = "Acción semántica autorizada por el perfil configurado del escenario."
                    });
                scenarios.Add(scenario);
            }
            var composed = new MedicalLibrary { schemaVersion = library.schemaVersion, actions = actions.ToArray(),
                references = library.references, scenarios = scenarios.ToArray() };
            composed.Validate();
            library.actions = composed.actions;
            library.scenarios = composed.scenarios;
        }

        public static ClinicalScenarioV2Definition ReadDefinition(TextAsset source)
        {
            if (source == null) throw new InvalidOperationException("A clinical V2 registration has no JSON TextAsset.");
            var definition = JsonUtility.FromJson<ClinicalScenarioV2Definition>(source.text);
            if (definition?.metadata == null) throw new InvalidOperationException("Invalid clinical V2 JSON.");
            definition.Validate(definition.metadata.scenarioId);
            return definition;
        }

        public static ScriptableObject FindPresentationAssets(string scenarioId)
        {
            var registry = Resources.Load<ClinicalScenarioV2Registry>(RegistryResource);
            if (registry?.entries == null) return null;
            foreach (var entry in registry.entries)
                if (entry != null && ReadDefinition(entry.definition).metadata.scenarioId == scenarioId) return entry.presentationAssets;
            return null;
        }

        public static MedicalScenarioDefinition ToMedicalDefinition(ClinicalScenarioV2Definition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate(definition.metadata.scenarioId);
            var metadata = definition.metadata;
            if (string.IsNullOrWhiteSpace(metadata.displayName) || string.IsNullOrWhiteSpace(metadata.environment) ||
                metadata.referenceIds == null || metadata.referenceIds.Length == 0)
                throw new ArgumentException("Clinical catalog metadata is incomplete.");
            var profile = definition.trainingProfiles.Single(x => x.ExportId == metadata.trainingProfile);
            var initial = definition.clinicalStates.Single(x => x.id == metadata.initialClinicalStateId);
            var patient = new PatientSnapshot
            {
                age = definition.patientProfile.age, sex = definition.patientProfile.sex,
                consciousness = initial.consciousness == ConsciousnessState.Alert ? "Conscious" : initial.consciousness.ToString(),
                position = initial.position == ClinicalPosition.SeatedSupported ? "seated" : initial.position == ClinicalPosition.Standing ? "standing" : "supine",
                canSwallow = initial.consciousness != ConsciousnessState.Unresponsive,
                flags = (string[])initial.symptoms.Clone(), dialogue = ""
            };
            foreach (var curve in initial.vitalTrajectories)
                switch (curve.vital)
                {
                    case "HeartRate": patient.heartRate = curve.startValue; break;
                    case "Systolic": patient.systolic = curve.startValue; break;
                    case "Diastolic": patient.diastolic = curve.startValue; break;
                    case "RespiratoryRate": patient.respiratoryRate = curve.startValue; break;
                    case "SpO2": patient.spo2 = curve.startValue; break;
                }
            var rules = profile.allowedActions.Distinct().Select(semantic => new ActionRule
            {
                id = semantic, action = semantic, semanticAction = semantic, kind = "optional", points = 0, penalty = 0,
                repeatPolicy = semantic == "AssessResponsiveness" || semantic == "ObserveBreathing"
                    ? ActionRepeatPolicy.RepeatableWithNewObservation : ActionRepeatPolicy.RepeatableNoAdditionalCredit,
                feedback = "Interacción registrada."
            }).ToArray();
            return new MedicalScenarioDefinition
            {
                id = metadata.scenarioId, name = metadata.displayName, version = metadata.scenarioVersion,
                category = metadata.category, difficulty = metadata.difficulty, environment = metadata.environment,
                description = metadata.description, history = metadata.history ?? "", incident = metadata.incident,
                initialDialogue = "", medicalValidationStatus = "CLIENT_REVIEW",
                timingBasis = "Parámetros configurables de prototipo formativo. CLINICAL_REVIEW_REQUIRED.",
                initialState = patient, clinicalV2 = definition.Copy(), references = (string[])metadata.referenceIds.Clone(),
                symptoms = Array.Empty<string>(), visibleSigns = Array.Empty<string>(),
                variation = new ScenarioVariation { minAge = patient.age, maxAge = patient.age, sexes = new[] { patient.sex },
                    positions = new[] { patient.position }, heartRateSpread = 0, spo2Spread = 0, glucoseSpread = 0, timelineJitterSeconds = 0,
                    witnessStatements = new[] { "La información del testigo requiere una interacción específica." } },
                actions = rules, recommendedSequence = rules.Select(x => x.id).ToArray(),
                // The existing catalog schema requires an outcome adapter. V2 evaluates objectives in its runtime branch.
                outcomes = new[] { new OutcomeRule { id = "clinical-handover", outcome = "REQUIRES_ADVANCED_CARE",
                    feedback = "Revisar los objetivos y la información obtenida durante el intento." } },
                errorPenalty = 0, criticalScoreCap = 100,
                debrief = new[] { "Prototipo formativo pendiente de revisión clínica; sin resultado de competencia ni porcentaje validado." }
            };
        }

        static string ActionLabel(string semantic)
        {
            switch (semantic)
            {
                case "TalkToPatient": return "Hablar con el paciente";
                case "AssessResponsiveness": return "Comprobar respuesta";
                case "ObserveBreathing": return "Observar respiración";
                case "AssistPatient": return "Solicitar ayuda a la posición segura";
                case "RequestHelp": return "Solicitar ayuda";
                case "DelegateHelp": return "Delegar la petición de ayuda";
                case "ReassessPatient": return "Reevaluar al paciente";
                case "PerformHandover": return "Comunicar el relevo";
                default: return semantic;
            }
        }
    }
}
