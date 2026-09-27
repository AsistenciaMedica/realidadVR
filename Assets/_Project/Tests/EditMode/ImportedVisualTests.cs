using System.Linq;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.Medical.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class ImportedVisualTests
    {
        [Test] public void PatientHasLicensedTexturedSkinFacialChannelsAndDeformableThorax()
        {
            var prefab=Resources.Load<GameObject>("Visual/Patient");
            Assert.That(prefab,Is.Not.Null);
            var rig=prefab.GetComponent<PatientRigAdapter>();
            Assert.That(rig.provisionalAsset,Is.False);
            Assert.That(rig.face.sharedMesh.vertexCount,Is.GreaterThan(3000));
            Assert.That(rig.face.sharedMesh.blendShapeCount,Is.EqualTo(8));
            foreach(var material in rig.face.sharedMaterials)
            {
                Assert.That(material.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.GetTexture("_BaseMap"),Is.Not.Null);
                Assert.That(material.GetTexture("_BumpMap"),Is.Not.Null);
            }
            foreach(var channel in new[]{rig.blinkLeft,rig.blinkRight,rig.jawOpen,"VitalBreath","VitalCompression"})
                Assert.That(rig.face.sharedMesh.GetBlendShapeIndex(channel),Is.GreaterThanOrEqualTo(0),channel);
            Assert.That(rig.chestAnchor,Is.Not.Null);Assert.That(rig.aedLeftPadAnchor,Is.Not.Null);
            Assert.That(prefab.GetComponent<ArticulatedPatient>().standingBreath,Is.Not.Null);
        }
        [TestCase("LeftHand")][TestCase("RightHand")]
        public void HandsHaveFifteenArticulatedFingerJointsAndCompactSkin(string name)
        {
            var prefab=Resources.Load<GameObject>("Visual/"+name);
            Assert.That(prefab,Is.Not.Null);
            var hand=prefab.GetComponent<ArticulatedHand>();
            Assert.That(hand.fingerJoints.Length,Is.EqualTo(15));
            Assert.That(hand.fingerJoints.Distinct().Count(),Is.EqualTo(15));
            Assert.That(hand.curlAxes.All(axis=>axis.sqrMagnitude>.9f),Is.True);
            var skin=prefab.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
            Assert.That(skin.sharedMesh.vertexCount,Is.InRange(100,2000));
            Assert.That(skin.sharedMesh.subMeshCount,Is.EqualTo(2));
            Assert.That(skin.sharedMesh.boneWeights.Length,Is.EqualTo(skin.sharedMesh.vertexCount));
        }
    }
}
