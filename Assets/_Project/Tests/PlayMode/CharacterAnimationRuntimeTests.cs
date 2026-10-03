using System.Collections;
using System.Linq;
using EmergencyVR.Patient.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EmergencyVR.Tests
{
    public sealed class CharacterAnimationRuntimeTests
    {
        [UnityTest]
        public IEnumerator GenericSamplingBindsTheTargetAndActuallyMovesBonesWithoutChangingSharedClips()
        {
            var patient = Object.Instantiate(Resources.Load<GameObject>("Visual/Patient"));
            var body = patient.GetComponent<ArticulatedPatient>();
            body.enabled = false;
            var target = CopyTransforms(body.skeleton.GetChild(0), null).gameObject;
            try
            {
                Assert.That(target.GetComponent<Animator>(), Is.Null, "Match the bare hierarchy used by Daniel's performance sampler.");
                var bones = target.GetComponentsInChildren<Transform>();
                foreach (var name in new[] { "m_idle_breathe_01", "f_idle_breathe_01", "m_idle_cough_01", "m_sit_chair_idle_nervous_01", "m_sit_chair_idle_touch_face" })
                {
                    var clip = CharacterAnimationLibrary.Load(name);
                    Assert.That(clip, Is.Not.Null, name);
                    Assert.That(clip.legacy, Is.False, "Shared source clips must remain compatible with the character Playables mixer.");
                    CharacterAnimationLibrary.Sample(clip, target, 0);
                    var initialRotations = bones.Select(b => b.localRotation).ToArray();
                    var initialPositions = bones.Select(b => b.localPosition).ToArray();
                    float maxAngle = 0, maxDistance = 0;
                    foreach (float phase in new[] { .2f, .4f, .6f, .8f })
                    {
                        CharacterAnimationLibrary.Sample(clip, target, clip.length * phase);
                        for (int i = 0; i < bones.Length; i++)
                        {
                            maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(initialRotations[i], bones[i].localRotation));
                            maxDistance = Mathf.Max(maxDistance, Vector3.Distance(initialPositions[i], bones[i].localPosition));
                        }
                    }
                    Assert.That(maxAngle > .01f || maxDistance > .0001f, Is.True, name + " must change a bone, not merely avoid a Player warning.");
                    Assert.That(clip.legacy, Is.False);
                }
                var animator = target.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(target.GetComponents<Animator>().Length, Is.EqualTo(1), "Sampling many clips reuses one binding.");
                Assert.That(animator.runtimeAnimatorController, Is.Null);
                Assert.That(animator.applyRootMotion, Is.False);
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(target); Object.Destroy(patient); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedGenericClipsStillAnimateTheSupportingCastThroughPlayables()
        {
            var person = Object.Instantiate(Resources.Load<GameObject>("Visual/Characters/Paramedic"));
            try
            {
                var animator = person.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                var clip = CharacterAnimationLibrary.Load("m_walk_neutral_01");
                CharacterAnimationLibrary.Sample(clip, person, 0);
                Assert.That(person.GetComponent<Animator>(), Is.SameAs(animator), "An existing Playables binding must be preserved.");
                var bones = person.GetComponentsInChildren<Transform>();
                var motion = person.AddComponent<CharacterAnimator>();
                motion.Play("m_walk_neutral_01");
                yield return null;
                var before = bones.Select(b => b.localRotation).ToArray();
                yield return new WaitForSeconds(.3f);
                Assert.That(bones.Select((b, i) => Quaternion.Angle(before[i], b.localRotation)).Max(), Is.GreaterThan(.1f));
                Assert.That(clip.legacy, Is.False);
                Assert.That(motion.Playing, Is.EqualTo("m_walk_neutral_01"));
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(person); }
            yield return null;
        }

        static Transform CopyTransforms(Transform source, Transform parent)
        {
            var target = new GameObject(source.name).transform;
            target.SetParent(parent, false);
            target.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            target.localScale = source.localScale;
            foreach (Transform child in source) CopyTransforms(child, target);
            return target;
        }
    }
}
