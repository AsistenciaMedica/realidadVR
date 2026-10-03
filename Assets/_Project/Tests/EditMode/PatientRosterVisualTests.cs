using System.Linq;
using EmergencyVR.Medical;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class PatientRosterVisualTests
    {
        [Test]
        public void EveryReleaseIdentityHasADistinctCompleteSkinnedAppearance()
        {
            var library = MedicalLibraryLoader.Load();
            var scope = ReleaseScope.Load(library);
            var identities = scope.ScenarioIds.Select(id => library.scenarios.Single(s => s.id == id).patientIdentity).ToArray();
            var appearances = identities.Select(p => Resources.Load<PatientAppearance>(p.appearanceResource)).ToArray();
            Assert.That(appearances.All(a => a != null && a.mesh != null), Is.True, "Generate the roster assets before running visual tests.");
            Assert.That(appearances.Select(a => a.mesh).Distinct().Count(), Is.EqualTo(15));
            var rig = Resources.Load<GameObject>("Visual/Patient").GetComponent<PatientRigAdapter>();
            for (int i = 0; i < appearances.Length; i++)
            {
                var appearance = appearances[i];
                Assert.That(appearance.mesh.vertexCount, Is.InRange(2500, 20000), identities[i].id);
                Assert.That(appearance.mesh.bindposes.Length, Is.EqualTo(rig.face.bones.Length), identities[i].id);
                Assert.That(appearance.materials.Length, Is.InRange(2, 3), identities[i].id);
                Assert.That(appearance.materials.Length, Is.EqualTo(appearance.mesh.subMeshCount));
                Assert.That(appearance.materials.All(m => m != null), Is.True);
                Assert.That(appearance.mesh.GetBlendShapeIndex("VitalBreath"), Is.GreaterThanOrEqualTo(0));
                Assert.That(appearance.mesh.GetBlendShapeIndex("VitalCompression"), Is.GreaterThanOrEqualTo(0));
                Assert.That(appearance.patientId, Is.EqualTo(identities[i].id));
                Assert.That(appearance.boneNames, Is.EqualTo(rig.face.bones.Select(b => b.name)));
                Assert.That(appearance.sourceCommit, Is.EqualTo("0943055db6ec570bcef9f2c8b41c9e5467c808f9"));
                foreach (var material in appearance.materials)
                    foreach (var name in new[] { "_BaseMap", "_BumpMap" })
                    {
                        var texture = material.GetTexture(name);
                        if (texture != null) Assert.That(Mathf.Max(texture.width, texture.height), Is.LessThanOrEqualTo(identities[i].id == "Daniel" ? 2048 : 1024));
                    }
            }
        }
    }
}
