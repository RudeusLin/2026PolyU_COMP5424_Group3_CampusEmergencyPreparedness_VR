using UnityEngine;

namespace CampusEmergency.Prototype
{
    // The visible student is a rig only; the parent CharacterController owns collisions.
    public sealed class CampusStudentAvatar : MonoBehaviour
    {
        private Transform body, leftLeg, rightLeg, leftShin, rightShin;
        private Transform leftArm, rightArm, leftForearm, rightForearm;
        private Renderer[] surfaces;
        private float phase, blend, runBlend, airBlend, landing;
        private float previousVerticalSpeed;
        private bool wasGrounded = true;
        private bool? currentVisibility;
        private Vector3 bodyRestPosition;
        public Transform RightHandHoldPoint { get; private set; }

        private void Awake() => InitializeRig();

        public void InitializeRig()
        {
            Transform rig = transform.Find("BodyRoot");
            if (body == null) bodyRestPosition = rig.localPosition;
            body = rig;
            leftLeg = body.Find("LeftLeg");
            rightLeg = body.Find("RightLeg");
            leftShin = leftLeg.Find("LeftShin");
            rightShin = rightLeg.Find("RightShin");
            leftArm = body.Find("LeftArm");
            rightArm = body.Find("RightArm");
            leftForearm = leftArm.Find("LeftForearm");
            rightForearm = rightArm.Find("RightForearm");
            AddHandDetails(leftForearm, -1);
            AddHandDetails(rightForearm, 1);
            RightHandHoldPoint = rightForearm.Find("HandHoldPoint");
            surfaces = GetComponentsInChildren<Renderer>();
            currentVisibility = null;
        }

        public void SetVisible(bool visible)
        {
            if (surfaces == null) InitializeRig();
            if (currentVisibility == visible) return;
            foreach (Renderer surface in surfaces) surface.enabled = visible;
            currentVisibility = visible;
        }

        public void Animate(Vector3 localVelocity, bool grounded, float deltaTime, bool holdingTowel = false)
        {
            if (body == null) InitializeRig();
            float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            blend = Mathf.MoveTowards(blend, grounded ? Mathf.Clamp01(speed / 2.6f) : 0, deltaTime * 8);
            runBlend = Mathf.MoveTowards(runBlend, Mathf.Clamp01((speed - 2.6f) / 1.6f), deltaTime * 6);
            airBlend = Mathf.MoveTowards(airBlend, grounded ? 0 : 1, deltaTime * 10);
            if (grounded && !wasGrounded) landing = Mathf.Clamp01(-previousVerticalSpeed / 7f);
            landing = Mathf.MoveTowards(landing, 0, deltaTime * 5);
            wasGrounded = grounded;
            previousVerticalSpeed = localVelocity.y;
            phase += speed * deltaTime * Mathf.Lerp(4.8f, 4.1f, runBlend) * (localVelocity.z < 0 ? -1 : 1);
            float swing = Mathf.Sin(phase) * Mathf.Lerp(25, 42, runBlend) * blend;
            float tuck = localVelocity.y > 0 ? 24 : 12;
            float side = Mathf.Clamp(localVelocity.x / 4.2f, -1, 1);
            leftLeg.localRotation = Quaternion.Euler(Mathf.Lerp(swing, -tuck, airBlend), 0, -side * 5 * blend);
            rightLeg.localRotation = Quaternion.Euler(Mathf.Lerp(-swing, -tuck * .6f, airBlend), 0, -side * 5 * blend);
            leftShin.localRotation = Quaternion.Euler(Mathf.Lerp(Mathf.Max(0, swing) * 1.25f, 42, airBlend) + landing * 18, 0, 0);
            rightShin.localRotation = Quaternion.Euler(Mathf.Lerp(Mathf.Max(0, -swing) * 1.25f, 30, airBlend) + landing * 18, 0, 0);
            leftArm.localRotation = Quaternion.Euler(Mathf.Lerp(-swing * .8f, -18, airBlend), 0, -4 - airBlend * 9);
            rightArm.localRotation = Quaternion.Euler(holdingTowel ? -25 : Mathf.Lerp(swing * .8f, -18, airBlend), 0, 4 + airBlend * 9);
            float elbow = Mathf.Lerp(-8, -60, runBlend) - Mathf.Abs(swing) * .25f;
            leftForearm.localRotation = Quaternion.Euler(Mathf.Lerp(elbow, -55, airBlend), 0, 0);
            rightForearm.localRotation = Quaternion.Euler(holdingTowel ? -65 : Mathf.Lerp(elbow, -55, airBlend), 0, 0);
            body.localRotation = Quaternion.Euler(runBlend * 8 * blend, Mathf.Sin(phase) * 3 * blend, -side * 4 * blend);
            body.localPosition = bodyRestPosition + Vector3.up *
                (Mathf.Abs(Mathf.Sin(phase)) * Mathf.Lerp(.014f, .025f, runBlend) * blend - landing * .045f);
        }

        public void ResetPose()
        {
            phase = blend = runBlend = airBlend = landing = previousVerticalSpeed = 0;
            wasGrounded = true;
            Animate(Vector3.zero, true, 1);
        }

        private static void AddHandDetails(Transform forearm, int side)
        {
            if (forearm.Find("HandDetails") != null) return;
            Transform palm = forearm.Find("Hand");
            palm.localPosition = new Vector3(0, -.27f, .022f);
            palm.localScale = new Vector3(.075f, .1f, .06f);
            GameObject details = new GameObject("HandDetails");
            details.transform.SetParent(forearm, false);
            Mesh mesh = palm.GetComponent<MeshFilter>().sharedMesh;
            Material skin = palm.GetComponent<Renderer>().sharedMaterial;
            for (int finger = 0; finger < 4; finger++)
            {
                float x = (finger - 1.5f) * .018f;
                float length = finger == 0 || finger == 3 ? .035f : .043f;
                AddPart("Finger" + finger, details.transform, mesh, skin,
                    new Vector3(x, -.295f - length / 2, .032f), new Vector3(.017f, length, .023f));
                AddPart("Fingertip" + finger, details.transform, mesh, skin,
                    new Vector3(x, -.291f - length, .032f), new Vector3(.017f, .025f, .023f));
            }
            AddPart("ThumbTip", details.transform, mesh, skin,
                new Vector3(-side * .044f, -.296f, .046f), new Vector3(.022f, .026f, .023f));
            Transform hold = new GameObject("HandHoldPoint").transform;
            hold.SetParent(forearm, false);
            hold.localPosition = new Vector3(0, -.29f, .055f);
            hold.localRotation = Quaternion.Euler(90, 0, 0);
        }

        private static void AddPart(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveGI = ReceiveGI.LightProbes;
        }
    }
}
