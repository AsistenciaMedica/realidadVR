using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Scenarios;
using UnityEngine;

namespace EmergencyVR.Tests
{
    // Archived procedures still receive regression coverage without entering the product release.
    // Only this scene's runtime catalog is extended; Dispose restores it and releases adapters.
    internal sealed class ArchivedMedicalCaseFixture : IDisposable
    {
        readonly ReviewCaseSession review;
        readonly ReviewCaseEntry[] original;
        readonly MedicalLibrary library;
        readonly List<ClinicalCaseDefinition> adapters = new List<ClinicalCaseDefinition>();

        public ArchivedMedicalCaseFixture(ReviewCaseSession owner)
        {
            review = owner;
            original = owner.Catalog.entries;
            library = MedicalLibraryLoader.Load();
        }

        public int Add(string scenarioId, string appearanceId, string sex = null)
        {
            var existing = Array.FindIndex(review.Catalog.entries, entry => entry.medical?.id == scenarioId);
            if (existing >= 0) return existing;
            var medical = library.scenarios.Single(scenario => scenario.id == scenarioId).Copy();
            if (sex != null) medical.initialState.sex = sex;
            var identity = original.Select(entry => entry.medical?.patientIdentity)
                .First(profile => profile != null && profile.sex == medical.initialState.sex).Copy();
            identity.scenarioId = medical.id;
            identity.id = identity.displayName = appearanceId;
            identity.appearanceResource = "Visual/Appearances/" + appearanceId;
            identity.age = medical.initialState.age;
            identity.patientOpeningLine = medical.initialDialogue ?? "";
            identity.context = medical.incident;
            identity.presentingComplaint = medical.description;
            identity.historyReply = medical.history;
            identity.witnessLines = (string[])medical.variation.witnessStatements.Clone();
            identity.Validate(medical.id);
            medical.patientIdentity = identity;
            medical.initialState.patientId = identity.id;
            medical.initialState.patientName = identity.displayName;
            medical.variation.minAge = medical.variation.maxAge = identity.age;
            medical.variation.sexes = new[] { identity.sex };
            medical.variation.positions = new[] { medical.initialState.position };

            var adapter = ScriptableObject.CreateInstance<ClinicalCaseDefinition>();
            adapters.Add(adapter);
            adapter.caseId = medical.id;
            adapter.displayName = medical.name;
            adapter.description = medical.description;
            adapter.isTechnicalDemo = false;
            foreach (var id in medical.recommendedSequence)
                adapter.steps.Add(new CaseStepData { actionId = id, label = id,
                    fromState = adapter.initialState, toState = adapter.initialState, points = 10 });
            var entry = new ReviewCaseEntry { definition = adapter, medical = medical,
                briefing = medical.description, limitations = "Archived regression fixture",
                sourceUrls = Array.Empty<string>(), reviewedOn = "" };
            review.Catalog.entries = review.Catalog.entries.Concat(new[] { entry }).ToArray();
            return review.Catalog.entries.Length - 1;
        }

        public void Dispose()
        {
            if (review != null)
            {
                review.Manager.SetPaused(false);
                if (review.Manager.IsRunning) review.Manager.FinishCase();
                review.Select(0);
                review.Catalog.entries = original;
            }
            foreach (var adapter in adapters) UnityEngine.Object.Destroy(adapter);
        }
    }
}
