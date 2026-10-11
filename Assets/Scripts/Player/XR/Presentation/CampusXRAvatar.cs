using UnityEngine;
using Unity.XR.CoreUtils;
using CampusEmergency.Prototype;

namespace CampusEmergency.Player.XR
{
    /// <summary>Reuses the original model without changing geometry. Hidden locally to prevent head/body clipping.</summary>
    public sealed class CampusXRAvatar : MonoBehaviour
    {
        [SerializeField] private XROrigin origin;
        [SerializeField] private CampusStudentAvatar avatar;
        public void Configure(XROrigin o, CampusStudentAvatar a) { origin = o; avatar = a; }
        private void LateUpdate()
        {
            if (origin == null || avatar == null) return;
            Vector3 feet = origin.Camera.transform.position;
            feet.y = origin.transform.position.y;
            avatar.transform.position = feet;
            Vector3 forward = Vector3.ProjectOnPlane(origin.Camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > .001f) avatar.transform.rotation = Quaternion.LookRotation(forward);
            avatar.SetVisible(false);
            // Teleport displacement must never be interpreted as walking velocity.
            avatar.Animate(Vector3.zero, true, Time.deltaTime);
        }
    }
}
