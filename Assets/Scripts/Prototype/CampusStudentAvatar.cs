using UnityEngine;

namespace CampusEmergency.Prototype
{
    // The visible student is a rig only; the parent CharacterController owns collisions.
    public sealed class CampusStudentAvatar : MonoBehaviour
    {
        private Transform body, leftLeg, rightLeg, leftShin, rightShin;
        private Transform leftArm, rightArm, leftForearm, rightForearm;
        private Renderer[] surfaces;
        private float phase, blend;
        private bool? currentVisibility;
        private Vector3 bodyRestPosition;

        private void Awake() => InitializeRig();

        public void InitializeRig()
        {
            body = transform.Find("BodyRoot");
            bodyRestPosition = body.localPosition;
            leftLeg = body.Find("LeftLeg");
            rightLeg = body.Find("RightLeg");
            leftShin = leftLeg.Find("LeftShin");
            rightShin = rightLeg.Find("RightShin");
            leftArm = body.Find("LeftArm");
            rightArm = body.Find("RightArm");
            leftForearm = leftArm.Find("LeftForearm");
            rightForearm = rightArm.Find("RightForearm");
            surfaces = GetComponentsInChildren<Renderer>();
        }

        public void SetVisible(bool visible)
        {
            if (surfaces == null) InitializeRig();
            if (currentVisibility == visible) return;
            foreach (Renderer surface in surfaces) surface.enabled = visible;
            currentVisibility = visible;
        }

        public void Animate(Vector3 localVelocity, bool grounded, float deltaTime)
        {
            if (body == null) InitializeRig();
            float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            blend = Mathf.MoveTowards(blend, grounded ? Mathf.Clamp01(speed / 2.6f) : 0, deltaTime * 8);
            phase += speed * deltaTime * 4.8f * (localVelocity.z < 0 ? -1 : 1);
            float swing = Mathf.Sin(phase) * 25 * blend;
            leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            leftShin.localRotation = Quaternion.Euler(Mathf.Max(0, -swing) * 1.1f, 0, 0);
            rightShin.localRotation = Quaternion.Euler(Mathf.Max(0, swing) * 1.1f, 0, 0);
            leftArm.localRotation = Quaternion.Euler(-swing * .65f, 0, -4);
            rightArm.localRotation = Quaternion.Euler(swing * .65f, 0, 4);
            leftForearm.localRotation = Quaternion.Euler(-8 - Mathf.Abs(swing) * .2f, 0, 0);
            rightForearm.localRotation = leftForearm.localRotation;
            body.localPosition = bodyRestPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * .014f * blend);
        }
    }
}
