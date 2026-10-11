using UnityEngine;
using Unity.XR.CoreUtils;

namespace CampusEmergency.Player.XR
{
    /// <summary>Small session state: initial tracked-head alignment and assembly-point feedback.</summary>
    public sealed class CampusXRSession : MonoBehaviour
    {
        [SerializeField] private XROrigin origin;
        [SerializeField] private CampusXRInput input;
        [SerializeField] private Transform spawn;
        [SerializeField] private Transform assembly;
        [SerializeField] private CampusXRFeedback feedback;
        private bool positioned, reachedAssembly;
        public void Configure(XROrigin o, CampusXRInput i, Transform start, Transform goal, CampusXRFeedback f)
        { origin = o; input = i; spawn = start; assembly = goal; feedback = f; }
        private void Update()
        {
            if (!input.HeadTracked) return;
            if (!positioned)
            {
                origin.MatchOriginUpCameraForward(Vector3.up, spawn.forward);
                origin.MoveCameraToWorldLocation(spawn.position + Vector3.up * origin.CameraInOriginSpaceHeight);
                positioned = true;
                feedback.Notify("Follow EXIT signs. Aim at the floor; green marks a clear destination.");
            }
            Vector3 delta = origin.Camera.transform.position - assembly.position;
            delta.y = 0;
            if (!reachedAssembly && origin.transform.position.y < 1f && delta.magnitude < 2.4f)
            { reachedAssembly = true; feedback.Notify("ASSEMBLY POINT REACHED", 8f); }
        }
    }
}
