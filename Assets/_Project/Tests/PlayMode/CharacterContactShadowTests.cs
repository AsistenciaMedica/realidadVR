using System.Collections;
using EmergencyVR.Patient.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class CharacterContactShadowTests
    {
        [UnityTest]
        public IEnumerator HiddenWelcomePatientNeverLeavesAVisibleContactShadow()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                floor.transform.position = new Vector3(1000, -.05f, 1000);
                floor.transform.localScale = new Vector3(4, .1f, 4);
                body.transform.position = new Vector3(1000, 1, 1000);
                var renderer = body.GetComponent<Renderer>();
                renderer.forceRenderingOff = true; // Intro hides the body before contact.Start creates its quad.
                var contact = CharacterContactShadow.Attach(body, renderer);
                Physics.SyncTransforms();
                yield return null;
                yield return null;
                Assert.That(contact.Visible, Is.False, "A hidden patient must not cast a visible contact in the welcome room.");
                renderer.forceRenderingOff = false;
                yield return null;
                yield return null;
                Assert.That(contact.Visible, Is.True, "Contact must return with the patient during training.");
                renderer.forceRenderingOff = true;
                yield return null;
                yield return null;
                Assert.That(contact.Visible, Is.False, "Returning to welcome must hide an already-created contact too.");
            }
            finally
            {
                Object.Destroy(body);
                Object.Destroy(floor);
            }
            yield return null;
        }
    }
}
