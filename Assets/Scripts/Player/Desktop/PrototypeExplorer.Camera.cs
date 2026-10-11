using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed partial class PrototypeExplorer
    {
        public void ToggleCameraView()
        {
            thirdPerson = !thirdPerson;
            if (HeldTowel != null) HeldTowel.RefreshHoldPose();
            if (walking) UpdateWalkCamera();
        }

        private void UpdateWalkCamera()
        {
            if (firstPersonArms != null) firstPersonArms.SetVisible(walking && !thirdPerson);
            if (!thirdPerson)
            {
                walkCamera.transform.localPosition = new Vector3(0, 1.65f, 0);
                if (avatar != null) avatar.SetVisible(false);
                return;
            }
            Vector3 pivot = transform.position + Vector3.up * 1.5f;
            Vector3 offset = transform.rotation * Quaternion.Euler(pitch, 0, 0) * new Vector3(.32f, .15f, -2.6f);
            float distance = offset.magnitude;
            if (Physics.SphereCast(pivot, .14f, offset.normalized, out RaycastHit hit, distance,
                ~0, QueryTriggerInteraction.Ignore) && hit.collider != controller)
                distance = Mathf.Max(.1f, hit.distance - .08f);
            walkCamera.transform.position = pivot + offset.normalized * distance;
            if (avatar != null) avatar.SetVisible(distance > .55f);
        }

        private void UpdateOverview()
        {
            if (Input.GetMouseButton(1))
            {
                orbitYaw += Input.GetAxis("Mouse X") * 3f;
                orbitPitch = Mathf.Clamp(orbitPitch - Input.GetAxis("Mouse Y") * 2f, 10f, 75f);
            }
            orbitDistance = Mathf.Clamp(orbitDistance - Input.mouseScrollDelta.y * 3f, 25f, 85f);
            Vector3 target = new Vector3(0f, 6f, -3f);
            overviewCamera.transform.position = target + Quaternion.Euler(orbitPitch, orbitYaw, 0f) * Vector3.back * orbitDistance;
            overviewCamera.transform.LookAt(target);
        }
    }
}
