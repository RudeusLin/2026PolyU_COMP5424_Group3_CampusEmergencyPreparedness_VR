using UnityEngine;
using UnityEngine.UI;

namespace CampusEmergency.Player.XR
{
    /// <summary>World-space headset HUD; messages are shared by teleport and facility interaction.</summary>
    public sealed class CampusXRFeedback : MonoBehaviour
    {
        [SerializeField] private Text label;
        private string aim, prompt, notice;
        private float noticeUntil;
        private CampusXRInput input;
        private void OnEnable() => input = GetComponentInParent<CampusXRInput>();
        public void Configure(Text text) => label = text;
        public void SetAim(string value) => aim = value;
        public void SetPrompt(string value) => prompt = value;
        public void Notify(string message, float seconds = 5f) { notice = message; noticeUntil = Time.unscaledTime + seconds; }
        private void LateUpdate()
        {
            if (label == null) return;
            string status = Time.unscaledTime < noticeUntil && !string.IsNullOrEmpty(notice) ? notice
                : !string.IsNullOrEmpty(aim) ? aim : prompt;
            if (input != null)
            {
                if (!input.HeadTracked) status = "Waiting for headset tracking. Connect Link before Play.";
                else if (!input.HasInteractionFocus) status = "Return to the app from the headset system menu.";
                else if (!input.LeftTracked || !input.RightTracked)
                    status = "Controller tracking: LEFT " + (input.LeftTracked ? "OK" : "MISSING")
                        + " / RIGHT " + (input.RightTracked ? "OK" : "MISSING");
            }
            var extinguisher = input != null ? input.GetComponent<CampusXRExtinguisherController>() : null;
            if (extinguisher != null && extinguisher.Held != null)
            {
                label.text = extinguisher.Controls + "\n" + (Time.unscaledTime < noticeUntil || string.IsNullOrEmpty(extinguisher.Status) ? status : extinguisher.Status);
                return;
            }
            label.text = "RIGHT RAY: AIM / RIGHT TRIGGER: TELEPORT\n"
                + "A: INTERACT / RIGHT STICK: TURN / RIGHT GRIP: RETURN TOWEL\n" + status;
        }
    }
}
