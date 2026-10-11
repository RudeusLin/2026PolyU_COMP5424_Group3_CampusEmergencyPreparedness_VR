using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;

namespace CampusEmergency.Player.XR
{
    /// <summary>Right-hand straight-ray targeting and click input. XRI owns origin movement and room-scale offset compensation.</summary>
    [DefaultExecutionOrder(-10)]
    public sealed class CampusTeleportController : MonoBehaviour
    {
        [SerializeField] private CampusXRInput input;
        [SerializeField] private XROrigin origin;
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private TeleportationProvider provider;
        [SerializeField] private CampusTeleportValidator validator;
        [SerializeField] private CampusTeleportVisual visual;
        [SerializeField] private CampusXRFeedback feedback;
        [SerializeField] private float rayDistance = 12f;
        private readonly Vector3[] points = new Vector3[2];
        private bool armed;
        private float lastTeleport = -10f;
        public bool IsAiming { get; private set; }
        public CampusTeleportValidator Validator => validator;

        public void Configure(CampusXRInput i, XROrigin o, Transform ray, TeleportationProvider p,
            CampusTeleportValidator v, CampusTeleportVisual display, CampusXRFeedback message)
        { input = i; origin = o; rayOrigin = ray; provider = p; validator = v; visual = display; feedback = message; }

        private void Update() => ProcessInput(input.HasInteractionFocus);
        private void ProcessInput(bool hasFocus)
        {
            var extinguisher = GetComponent<CampusXRExtinguisherController>();
            bool holding = extinguisher != null && extinguisher.Held != null;
            if (!input.RightTracked || !hasFocus || (input.CancelTeleport && !holding) || input.InteractionHeld
                || (extinguisher != null && extinguisher.BlocksTeleport))
            { Cancel(); armed = false; return; }
            bool held = input.TeleportHeld;
            if (!held) armed = true;
            IsAiming = true;
            bool valid = Trace(out var hit, out int count, out string reason);
            visual.Show(points, count, valid, hit.point);
            feedback.SetAim(valid ? "RIGHT TRIGGER: TELEPORT / A: INTERACT" : reason + " / A: INTERACT");
            // Require a release before every click, including after focus/tracking loss.
            if (!held || !armed) return;
            armed = false;
            if (!valid || Time.unscaledTime - lastTeleport < .25f
                || provider.system == null || provider.system.busy) return;
            if (provider.QueueTeleportRequest(new TeleportRequest {
                destinationPosition = hit.point + Vector3.up * .035f,
                destinationRotation = Quaternion.identity,
                matchOrientation = MatchOrientation.WorldSpaceUp }))
                lastTeleport = Time.unscaledTime;
        }

        public bool Trace(out RaycastHit hit, out int count, out string reason)
        {
            hit = default;
            count = 1;
            Vector3 start = rayOrigin.position;
            points[0] = start;
            // A tracked controller may physically pass through virtual geometry.
            // Reject starts inside geometry and obstructed head-to-hand segments.
            Vector3 eye = origin.Camera.transform.position;
            Vector3 handOffset = start - eye;
            bool obstructed = Physics.CheckSphere(eye, .08f, validator.EnvironmentMask, QueryTriggerInteraction.Ignore)
                || Physics.CheckSphere(start, .025f, validator.EnvironmentMask, QueryTriggerInteraction.Ignore);
            if (handOffset.sqrMagnitude > .0001f && Physics.Raycast(eye, handOffset.normalized, out var between,
                handOffset.magnitude, validator.EnvironmentMask, QueryTriggerInteraction.Ignore))
            { hit = between; obstructed = true; }
            if (obstructed)
            {
                points[1] = start + rayOrigin.forward * .1f;
                count = 2;
                reason = "Move your head and controller clear of the wall";
                return false;
            }
            count = 2;
            points[1] = start + rayOrigin.forward * rayDistance;
            if (Physics.Raycast(start, rayOrigin.forward, out hit, rayDistance,
                validator.EnvironmentMask, QueryTriggerInteraction.Ignore))
            {
                points[1] = hit.point;
                Vector3 feet = origin.Camera.transform.position;
                feet.y = origin.transform.position.y;
                return validator.IsValid(hit, feet, origin.CameraInOriginSpaceHeight, out reason);
            }
            reason = "Point the RIGHT controller at a floor within 12m";
            return false;
        }

        public void Cancel()
        {
            IsAiming = false;
            if (visual != null) visual.Hide();
            if (feedback != null) feedback.SetAim(string.Empty);
        }
        private void OnDisable() { Cancel(); armed = false; }
    }
}
