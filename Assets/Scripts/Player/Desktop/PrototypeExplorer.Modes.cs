using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed partial class PrototypeExplorer
    {
        private void EnterFloor(Transform spawn)
        {
            ResetWashroomInteractions();
            currentSpawn = spawn;
            controller.enabled = false;
            transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            pitch = 0f;
            verticalSpeed = 0f;
            groundedGrace = jumpBuffer = 0;
            horizontalVelocity = Vector3.zero;
            if (avatar != null) avatar.ResetPose();
            if (firstPersonArms != null) firstPersonArms.ResetPose();
            walkCamera.transform.localRotation = Quaternion.identity;
            controller.enabled = true;
            walking = true;
            reachedAssembly = false;
            walkCamera.enabled = true;
            overviewCamera.enabled = false;
            roof.SetActive(true);
            UpdateWalkCamera();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            notice = "Follow EXIT signs to a stairwell. Press E to open the stair doors.";
            noticeUntil = Time.time + 5f;
        }

        private void SetOverview()
        {
            walking = false;
            focused = null;
            controller.enabled = false;
            walkCamera.enabled = false;
            overviewCamera.enabled = true;
            if (avatar != null) avatar.SetVisible(true);
            if (firstPersonArms != null) firstPersonArms.SetVisible(false);
            ReleaseCursor();
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }
    }
}
