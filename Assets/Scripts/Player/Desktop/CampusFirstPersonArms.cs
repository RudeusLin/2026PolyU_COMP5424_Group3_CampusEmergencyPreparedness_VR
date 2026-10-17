using UnityEngine;
using UnityEngine.Rendering;

namespace CampusEmergency.Prototype
{
    // Reuse the student's meshes and materials; hands never own movement colliders.
    public sealed class CampusFirstPersonArms : MonoBehaviour
    {
        private Transform rig, left, right;
        private float phase, movement, running, airborne, landing, previousVerticalSpeed;
        private bool wasGrounded = true;
        public Transform HoldPoint { get; private set; }

        public void Initialize(CampusStudentAvatar avatar)
        {
            rig = new GameObject("FirstPersonArms").transform;
            rig.SetParent(transform, false);
            Transform body = avatar.transform.Find("BodyRoot");
            left = Instantiate(body.Find("LeftArm/LeftForearm").gameObject, rig).transform;
            right = Instantiate(body.Find("RightArm/RightForearm").gameObject, rig).transform;
            left.name = "LeftHandView";
            right.name = "RightHandView";
            BendWrist(left);
            BendWrist(right);
            HoldPoint = right.Find("Wrist/HandHoldPoint");
            foreach (Renderer surface in rig.GetComponentsInChildren<Renderer>())
            {
                surface.enabled = true;
                surface.lightmapIndex = -1;
                surface.shadowCastingMode = ShadowCastingMode.Off;
                surface.lightProbeUsage = LightProbeUsage.BlendProbes;
#if UNITY_EDITOR
                // receiveGI is only available in the Unity Editor API.
                if (surface is MeshRenderer mesh) mesh.receiveGI = ReceiveGI.LightProbes;
#endif
            }
            ResetPose();
        }

        private static void BendWrist(Transform forearm)
        {
            Transform wrist = new GameObject("Wrist").transform;
            wrist.SetParent(forearm, false);
            wrist.localPosition = new Vector3(0, -.225f, .018f);
            foreach (string name in new[] { "Hand", "Thumb", "HandDetails", "HandHoldPoint" })
                forearm.Find(name).SetParent(wrist, true);
            wrist.localRotation = Quaternion.Euler(-25, 0, 0);
        }

        public void SetVisible(bool visible)
        {
            if (rig != null) rig.gameObject.SetActive(visible);
        }

        public void ResetPose()
        {
            phase = movement = running = airborne = landing = previousVerticalSpeed = 0;
            wasGrounded = true;
            Animate(Vector3.zero, true, false, 1);
        }

        public void Animate(Vector3 velocity, bool grounded, bool holdingTowel, float deltaTime)
        {
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            movement = Mathf.MoveTowards(movement, Mathf.Clamp01(speed / 2.6f), deltaTime * 8);
            running = Mathf.MoveTowards(running, Mathf.Clamp01((speed - 2.6f) / 1.6f), deltaTime * 6);
            airborne = Mathf.MoveTowards(airborne, grounded ? 0 : 1, deltaTime * 10);
            if (grounded && !wasGrounded) landing = Mathf.Clamp01(-previousVerticalSpeed / 7);
            landing = Mathf.MoveTowards(landing, 0, deltaTime * 5);
            previousVerticalSpeed = velocity.y;
            wasGrounded = grounded;
            phase += speed * deltaTime * 4.8f;
            float swing = Mathf.Sin(phase) * movement;
            float bob = Mathf.Abs(Mathf.Cos(phase)) * .009f * movement - landing * .025f;
            left.localPosition = new Vector3(-.24f, -.25f + bob + running * .06f, .42f + swing * .018f);
            right.localPosition = new Vector3(.24f, -.25f + bob + running * .06f, .42f - swing * .018f);
            left.localRotation = Quaternion.Euler(-108 + running * 5 + swing * 4 - airborne * 4, -7, -4);
            right.localRotation = Quaternion.Euler(-108 + running * 5 - swing * 4 - airborne * 4, 7, 4);
            if (holdingTowel)
            {
                right.localPosition = new Vector3(.25f, -.24f + bob, .46f);
                right.localRotation = Quaternion.Euler(-103, 0, 0);
            }
            // Retract the hands near a surface while preserving their apparent size.
            float reach = 1;
            if (Physics.SphereCast(transform.position, .08f, transform.forward, out RaycastHit hit,
                .8f, ~0, QueryTriggerInteraction.Ignore) && !(hit.collider is CharacterController))
                reach = Mathf.Clamp((hit.distance - .05f) / .8f, .18f, 1);
            rig.localScale = Vector3.one * reach;
        }
    }
}
