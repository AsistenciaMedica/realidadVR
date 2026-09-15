using System.Linq;
using EmergencyVR.Core;
using EmergencyVR.Editor;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEditor;

namespace EmergencyVR.Tests
{
    public sealed class ReviewCatalogTests
    {
        ReviewCaseCatalog Catalog => AssetDatabase.LoadAssetAtPath<ReviewCaseCatalog>(ReviewCaseCatalogBuilder.CatalogPath);
        [Test] public void OriginalDemoAndFiveProvisionalCasesHaveValidIndependentSessions()
        {
            Assert.That(Catalog,Is.Not.Null);
            Assert.That(Catalog.entries.Length,Is.EqualTo(6));
            Assert.That(Catalog.entries[0].definition,Is.SameAs(AssetDatabase.LoadAssetAtPath<ClinicalCaseDefinition>(DemoProjectBuilder.CasePath)));
            foreach(var entry in Catalog.entries)
            {
                var session=new CaseSession(entry.definition.ToDomain(),0);
                double time=0;
                foreach(var step in entry.definition.steps) session.Record(step.actionId,++time);
                Assert.That(session.Finish(++time).ScorePercent,Is.EqualTo(100),entry.definition.name);
            }
        }
        [Test] public void PilotContentHasSourcesNoApprovalAndNoGuaranteedRecovery()
        {
            foreach(var entry in Catalog.entries.Skip(1))
            {
                Assert.That(entry.definition.clinicallyApproved,Is.False);
                Assert.That(entry.sourceUrls.All(u=>u.StartsWith("https://")),Is.True);
                Assert.That(entry.sourceUrls.Length,Is.GreaterThan(0));
                Assert.That(entry.definition.steps.All(s=>s.fromState==s.toState),Is.True,"Review scripts must not imply guaranteed recovery.");
                Assert.That(entry.definition.steps.All(s=>s.deadlineSeconds==0),Is.True,"Clinical time cutoffs are not yet approved.");
            }
            var bls=Catalog.entries.Single(e=>e.definition.caseId.Contains("abnormal-breathing"));
            Assert.That(bls.definition.steps.Any(s=>s.actionId=="aed.no-shock"),Is.True);
            Assert.That(bls.definition.steps.Any(s=>s.actionId=="aed.shock"),Is.False);
        }
    }
}
