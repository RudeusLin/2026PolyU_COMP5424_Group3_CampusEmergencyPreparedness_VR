using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CampusEmergency.Player.XR
{
    public sealed class CampusSnapTurn : LocomotionProvider
    {
        [SerializeField] private CampusXRInput input;
        [SerializeField] private CampusTeleportController teleport;
        [SerializeField] private float degrees = 30f;
        private bool armed;
        public void Configure(CampusXRInput source, CampusTeleportController target)
        { input = source; teleport = target; }
        private void Update()
        {
            if (!input.RightTracked || !input.HasInteractionFocus) { armed = false; return; }
            var axis = input.TurnAxis;
            if (axis.magnitude < .2f) armed = true;
            if (input.TeleportHeld || !armed || Mathf.Abs(axis.x) < .7f || Mathf.Abs(axis.y) > .5f) return;
            if (BeginLocomotion())
            {
                system.xrOrigin.RotateAroundCameraUsingOriginUp(Mathf.Sign(axis.x) * degrees);
                EndLocomotion();
                armed = false;
            }
        }
        private void OnDisable() => armed = false;
    }
}
