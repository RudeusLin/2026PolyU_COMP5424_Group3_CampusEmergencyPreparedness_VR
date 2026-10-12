using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using CampusEmergency.Player;

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
        public bool LastTraceBlockedByFire { get; private set; }
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
            feedback.SetAim(valid
                ? LanguageSelectionUI.T("RIGHT TRIGGER: TELEPORT / A: INTERACT", "右扳机：传送 / A键：交互", "右扳機：傳送 / A鍵：互動")
                : reason + LanguageSelectionUI.T(" / A: INTERACT", " / A键：交互", " / A鍵：互動"));
            // Require a release before every click, including after focus/tracking loss.
            if (!held || !armed) return;
            armed = false;
            if (!valid)
            {
                if (LastTraceBlockedByFire)
                    feedback.Notify(LanguageSelectionUI.T(
                        "The route is blocked by fire. Please find a fire extinguisher.",
                        "当前路线被火挡住，请寻找灭火器。",
                        "目前路線被火擋住，請尋找滅火器。"), 5f);
                return;
            }
            if (Time.unscaledTime - lastTeleport < .25f
                || provider.system == null || provider.system.busy) return;
            if (provider.QueueTeleportRequest(new TeleportRequest {
                destinationPosition = hit.point + Vector3.up * .035f,
                destinationRotation = Quaternion.identity,
                matchOrientation = MatchOrientation.WorldSpaceUp }))
                lastTeleport = Time.unscaledTime;
        }

        public bool Trace(out RaycastHit hit, out int count, out string reason)
        {
            LastTraceBlockedByFire = false;
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
                reason = LanguageSelectionUI.T("Move your head and controller clear of the wall", "请将头部和控制器移离墙体", "請將頭部和控制器移離牆體");
                return false;
            }
            count = 2;
            points[1] = start + rayOrigin.forward * rayDistance;
            bool hasSurface = Physics.Raycast(start, rayOrigin.forward, out hit, rayDistance,
                validator.EnvironmentMask, QueryTriggerInteraction.Ignore);
            if (hasSurface) points[1] = hit.point;
            if (CampusEmergency.Interaction.FireSafety.CampusFireBarrier.TryIntersect(start, points[1], out _))
            {
                LastTraceBlockedByFire = true;
                // Keep drawing through the nonphysical fire region, but never queue a teleport.
                reason = LanguageSelectionUI.T("Ray crosses fire: extinguish it before teleporting", "射线穿过火区：请先灭火再传送", "射線穿過火區：請先滅火再傳送");
                return false;
            }
            if (hasSurface)
            {
                Vector3 feet = origin.Camera.transform.position;
                feet.y = origin.transform.position.y;
                return validator.IsValid(hit, feet, origin.CameraInOriginSpaceHeight, out reason);
            }
            reason = LanguageSelectionUI.T("Point the RIGHT controller at a floor within 12m", "请将右控制器指向12米内的地面", "請將右控制器指向12米內的地面");
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
