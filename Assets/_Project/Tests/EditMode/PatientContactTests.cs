using EmergencyVR.Patient.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace EmergencyVR.Tests
{
    public sealed class PatientContactTests
    {
        GameObject patient;
        PatientRigAdapter rig;
        [SetUp] public void SetUp()
        {
            patient=new GameObject("Contact anatomy test");rig=patient.AddComponent<PatientRigAdapter>();
            rig.poseRoot=Child(patient.transform,Vector3.zero);rig.chestMotion=Child(rig.poseRoot,Vector3.zero);
            rig.head=Child(rig.poseRoot,new Vector3(0,.012f,.66f));
            rig.upperLegs=new[]{Child(rig.poseRoot,new Vector3(-.09f,-.03f,-.29f)),Child(rig.poseRoot,new Vector3(.09f,-.03f,-.29f))};
            rig.lowerLegs=new[]{Child(rig.upperLegs[0],new Vector3(-.008f,-.01f,-.345f)),Child(rig.upperLegs[1],new Vector3(.008f,-.01f,-.345f))};
            PatientAnatomicalProxy.ConfigureContactVolumes(rig);
        }
        static Transform Child(Transform parent,Vector3 position){var t=new GameObject("Anatomical pivot").transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        [TearDown] public void TearDown(){Object.DestroyImmediate(patient);}

        [TestCase(0f,.05f,.66f)]
        [TestCase(-.30f,0f,-.20f)]
        [TestCase(.30f,0f,-.20f)]
        [TestCase(-.10f,0f,-.78f)]
        [TestCase(.10f,0f,-.94f)]
        public void ContactOutsideSternumIsDetected(float x,float y,float z)
        {
            Assert.That(rig.contactVolumes.Length,Is.EqualTo(11));
            Assert.That(rig.IsPatientContact(new Vector3(x,y,z),.01f),Is.True,"DEA contact guard must include head, arms, legs and feet.");
            Assert.That(patient.GetComponentsInChildren<Collider>(),Is.Empty,"Contact volumes must not change XR collider registration.");
        }
        [Test] public void MovingAndRollingThePatientMovesContactGeometry()
        {
            patient.transform.SetPositionAndRotation(new Vector3(2,.17f,3),Quaternion.Euler(0,31,0));
            rig.poseRoot.localRotation=Quaternion.Euler(0,0,65);
            Assert.That(rig.IsPatientContact(rig.head.TransformPoint(new Vector3(0,.1f,0)),.01f),Is.True);
            var oldFoot=rig.lowerLegs[0].TransformPoint(new Vector3(0,0,-.24f));
            rig.lowerLegs[0].localRotation=Quaternion.Euler(-80,0,0);
            var movedFoot=rig.lowerLegs[0].TransformPoint(new Vector3(0,0,-.24f));
            Assert.That(Vector3.Distance(oldFoot,movedFoot),Is.GreaterThan(.25f));
            Assert.That(rig.IsPatientContact(movedFoot,.01f),Is.True);
            Assert.That(rig.IsPatientContact(new Vector3(-3,2,-3),.055f),Is.False);
        }
        [Test] public void MarginRemainsInWorldMetresAndRejectsInvalidPoints()
        {
            rig.head.localScale=new Vector3(2,1,1);
            var volume=new PatientContactVolume("head test",rig.head,Vector3.zero,new Vector3(.2f,.2f,.2f));
            Assert.That(volume.Contains(rig.head.TransformPoint(new Vector3(.12f,0,0)),.05f),Is.True);
            Assert.That(volume.Contains(rig.head.TransformPoint(new Vector3(.135f,0,0)),.05f),Is.False);
            Assert.That(volume.Contains(new Vector3(float.NaN,0,0),.05f),Is.False);
            Assert.That(rig.IsPatientContact(new Vector3(0,1.5f,0),.055f),Is.False,"Hands well away from the anatomy must allow the DEA guard to clear.");
            rig.head.gameObject.SetActive(false);
            Assert.That(volume.Contains(rig.head.position,.05f),Is.False);
        }
    }
}
