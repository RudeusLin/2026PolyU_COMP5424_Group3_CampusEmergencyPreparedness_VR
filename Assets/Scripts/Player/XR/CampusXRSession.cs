using UnityEngine;
using Unity.XR.CoreUtils;
using CampusEmergency.Player;

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
        private const float StandingEyeHeight = 1.65f;
        private float trackingReadySince = -1f;
        private bool positioned, reachedAssembly;
        private void Awake()
        {
            if (origin == null) origin = GetComponent<XROrigin>();
            ConfigureEyeHeight();
        }
        private void ConfigureEyeHeight()
        {
            if (origin == null) return;
            // Device space supplies head motion relative to the recentered headset.
            // XROrigin adds this height to the shared head/hand parent, not to the camera pose.
            // Floor mode ignores CameraYOffset and can inherit an incorrect runtime floor.
            origin.CameraYOffset = StandingEyeHeight;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
        }
        public void Configure(XROrigin o, CampusXRInput i, Transform start, Transform goal, CampusXRFeedback f)
        { origin = o; input = i; spawn = start; assembly = goal; feedback = f; ConfigureEyeHeight(); }
        private void Update()
        {
            if (!input.HeadTracked) { trackingReadySince = -1f; return; }
            if (!positioned)
            {
                // isTracked may become true before the first position/recenter update.
                if (trackingReadySince < 0f) trackingReadySince = Time.unscaledTime;
                if (Time.unscaledTime - trackingReadySince < .25f
                    || origin.CurrentTrackingOriginMode == UnityEngine.XR.TrackingOriginModeFlags.Unknown) return;
                origin.MatchOriginUpCameraForward(Vector3.up, spawn.forward);
                origin.MoveCameraToWorldLocation(spawn.position + Vector3.up * origin.CameraInOriginSpaceHeight);
                positioned = true;
                if (!LanguageSelectionUI.IsBlocking)
                    feedback.Notify(LanguageSelectionUI.WelcomeMessage());
            }
            Vector3 delta = origin.Camera.transform.position - assembly.position;
            delta.y = 0;
            var route = GetComponent<CampusRouteAFlow>();
            if (!reachedAssembly && route != null && route.CanCall && origin.transform.position.y < 1f && delta.magnitude < 2.4f)
            {
                reachedAssembly = true;
                feedback.Notify(LanguageSelectionUI.T(
                    "ASSEMBLY POINT REACHED. Take out your phone and call emergency services.",
                    "已到达集合点。请掏出手机拨打紧急电话。",
                    "已到達集合點。請掏出手機撥打緊急電話。"), 8f);
                LanguageSelectionUI.ShowEmergencyCall();
            }
        }
    }
}
