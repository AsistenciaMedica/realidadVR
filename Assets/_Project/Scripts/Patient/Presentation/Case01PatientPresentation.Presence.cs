using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    // Autonomous acting remains a visual layer. It never advances a procedure, invents
    // patient speech, changes physiology, or moves the supported pelvis and feet.
    public sealed partial class Case01PatientPresentation
    {
        bool learnerWasNearby;
        float acknowledgementAt = -1;

        void PresentAcknowledgement()
        {
            if (acknowledgementAt < 0) return;
            float t = performanceTime - acknowledgementAt;
            if (t >= 1.45f) { acknowledgementAt = -1; return; }
            // One small acknowledgement, followed by rest; not a repeated nodding loop.
            float nod = Mathf.Sin(Mathf.Clamp01(t / 1.45f) * Mathf.PI) * 5;
            rig.head.rotation = Quaternion.AngleAxis(nod, Right) * rig.head.rotation;
        }

        void PresentSupportedTorsoMotion()
        {
            // A discrete shoulder adjustment rather than constant whole-body swaying.
            float t = Mathf.Repeat(performanceTime + .25f, 13.7f);
            float settle = Smooth(t / .9f) * Smooth((3.1f - t) / 1.2f);
            if (settle <= 0 || body.spine == null) return;
            body.spine.rotation = Quaternion.AngleAxis(2.7f * settle, Forward) * body.spine.rotation;
            rig.head.rotation = Quaternion.AngleAxis(-1.3f * settle, Forward) * rig.head.rotation;
        }

        void PoseSettlingGesture(float amount)
        {
            var resting = SeatedHandTarget(1);
            var target = resting + Forward * (.055f * amount) + Vector3.up * (.018f * amount);
            SolveLimb(body.upperArms[1], body.forearms[1], body.hands[1], target,
                body.upperArms[1].position + Right * .26f - Vector3.up * .32f + Forward * .16f);
            OrientRightHand(Forward - Vector3.up * .12f, Vector3.down, 1);
        }

        void PoseSpeakingHand(float amount, float time)
        {
            var target = body.pelvis.position + Vector3.up * .24f + Forward * .32f + Right * .19f;
            target += Vector3.up * (Mathf.Sin(time * 3.4f) * .018f);
            SolveLimb(body.upperArms[1], body.forearms[1], body.hands[1], Vector3.Lerp(SeatedHandTarget(1), target, amount),
                body.upperArms[1].position + Right * .28f - Vector3.up * .30f + Forward * .19f);
            OrientRightHand(Forward - Right * .25f, Vector3.up + Forward * .4f, amount);
        }

        void OrientRightHand(Vector3 fingers, Vector3 palmNormal, float weight)
        {
            var middle = middleFingers[1]; var index = indexFingers[1]; var little = littleFingers[1];
            if (middle == null || index == null || little == null) return;
            var hand = body.hands[1];
            var direction = middle.position - hand.position;
            var normal = Vector3.Cross(index.position - hand.position, little.position - hand.position);
            if (direction.sqrMagnitude < .00001f || normal.sqrMagnitude < .00000001f) return;
            var current = Quaternion.LookRotation(direction, normal);
            // The right hand's index-to-little winding points away from its anatomical palm.
            var desired = Quaternion.LookRotation(fingers.normalized, -palmNormal.normalized);
            hand.rotation = Quaternion.Slerp(hand.rotation, desired * Quaternion.Inverse(current) * hand.rotation, weight);
        }
    }
}
