using UnityEngine;
namespace CampusEmergency.Player
{
    public sealed partial class LanguageSelectionUI
    {
        private Vector3 fixedPlayerPosition, fixedCanvasPosition;
        private Quaternion fixedPlayerRotation, fixedCanvasRotation;
        private void CaptureFixedPanelPose()
        {
            if (!xrMode || canvas == null || targetCamera == null) return;
            fixedPlayerPosition = transform.position;
            fixedPlayerRotation = transform.rotation;
            var forward = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .01f) forward = transform.forward;
            forward.Normalize();
            // Same 1.65m centre and 1.45m distance as startup; ignore the phone/head pitch.
            fixedCanvasPosition = fixedPlayerPosition + Vector3.up * 1.65f + forward * 1.45f;
            fixedCanvasRotation = Quaternion.LookRotation(forward, Vector3.up);
            canvas.transform.SetParent(transform, true);
            LockFixedPanelPose();
        }
        private void LockFixedPanelPose()
        {
            transform.SetPositionAndRotation(fixedPlayerPosition, fixedPlayerRotation);
            if (canvas != null) canvas.transform.SetPositionAndRotation(fixedCanvasPosition, fixedCanvasRotation);
        }
    }
}
