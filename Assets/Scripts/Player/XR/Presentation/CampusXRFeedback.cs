using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace CampusEmergency.Player.XR
{
    /// <summary>Head-relative instructions and status. Canvas visibility never disables gameplay or the toggle input.</summary>
    public sealed class CampusXRFeedback : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private float panelHeight = .24f;
        private string aim, prompt, notice;
        private float noticeUntil;
        private CampusXRInput input;
        private Canvas canvas;
        private InputAction controllerToggle, keyboardToggle;
        public bool IsVisible => canvas != null && canvas.enabled;

        private void OnEnable()
        {
            input = GetComponentInParent<CampusXRInput>();
            ApplyPresentation();
            SetVisible(showOnStart);
            if (!Application.isPlaying) return;
            controllerToggle = new InputAction("Toggle HUD X", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            keyboardToggle = new InputAction("Toggle HUD H", InputActionType.Button, "<Keyboard>/h");
            controllerToggle.Enable(); keyboardToggle.Enable();
        }
        private void OnDisable()
        { controllerToggle?.Dispose(); keyboardToggle?.Dispose(); controllerToggle = keyboardToggle = null; }
        public void Configure(Text text) { label = text; ApplyPresentation(); }
        public void SetVisible(bool visible)
        { if (canvas == null) canvas = GetComponent<Canvas>(); if (canvas != null) canvas.enabled = visible; }
        [ContextMenu("Toggle HUD visibility")]
        public void ToggleVisibility() => SetVisible(!IsVisible);
        public void SetAim(string value) => aim = value;
        public void SetPrompt(string value) => prompt = value;
        public void Notify(string message, float seconds = 5f) { notice = message; noticeUntil = Time.unscaledTime + seconds; }

        public void ApplyPresentation()
        {
            canvas = GetComponent<Canvas>();
            var rect = transform as RectTransform;
            if (rect != null)
            {
                rect.localPosition = new Vector3(0f, panelHeight, 1.1f);
                rect.localScale = Vector3.one * .0008f;
                rect.sizeDelta = new Vector2(900f, 260f);
            }
            if (label == null) return;
            label.fontSize = 22; label.lineSpacing = 1.3f;
            label.alignment = TextAnchor.UpperLeft;
            label.color = new Color(.9f,.94f,.98f);
            label.supportRichText = true;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 17; label.resizeTextMaxSize = 22;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.rectTransform.offsetMin = new Vector2(28f,20f);
            label.rectTransform.offsetMax = new Vector2(-28f,-20f);
            var panel = label.transform.parent.GetComponent<Image>();
            if (panel != null) { panel.color = new Color(.025f,.055f,.09f,.72f); panel.raycastTarget = false; }
        }
        private void LateUpdate()
        {
            ProcessToggle(input != null && input.HasInteractionFocus);
            if (label == null) return;
            string controls = "TRIGGER: TELEPORT    A: INTERACT    RIGHT STICK: TURN\nRIGHT GRIP: RETURN TOWEL";
            string live = !string.IsNullOrEmpty(aim) ? aim : prompt;
            var extinguisher = input != null ? input.GetComponent<CampusXRExtinguisherController>() : null;
            if (extinguisher != null && extinguisher.Held != null)
            {
                controls = extinguisher.Controls;
                if (!string.IsNullOrEmpty(extinguisher.Status)) live = extinguisher.Status;
            }
            var inventory = input != null ? input.GetComponent<CampusXRInventory>() : null;
            if (inventory != null && inventory.HeldTowel != null)
            {
                controls = inventory.Controls;
                live = (inventory.HeldTowel.IsWet ? "WET TOWEL" : "DRY TOWEL: use the tap")
                    + (inventory.IsTowelRaised ? " / RAISED BELOW VIEW" : " / HELD IN LEFT HAND");
            }
            string alert = Time.unscaledTime < noticeUntil ? notice : string.Empty;
            if (input != null)
            {
                if (!input.HeadTracked) { live = "Waiting for headset tracking. Connect Link before Play."; alert = null; }
                else if (!input.HasInteractionFocus) { live = "Return from the headset system menu."; alert = null; }
                else if (!input.LeftTracked || !input.RightTracked)
                { live = "Tracking: LEFT " + (input.LeftTracked ? "OK" : "MISSING") + " / RIGHT " + (input.RightTracked ? "OK" : "MISSING"); alert = null; }
            }
            label.text = "<color=#8ADFFF><b>CAMPUS XR</b></color>    <size=18>X / H: SHOW OR HIDE</size>\n"
                + controls + "\n\n<color=#8ADFFF><b>STATUS</b></color>  " + (string.IsNullOrEmpty(live) ? "Ready" : live)
                + (string.IsNullOrEmpty(alert) || alert == live ? string.Empty : "\n<color=#FFE0A0>" + alert + "</color>");
        }
        private void ProcessToggle(bool focus)
        {
            if (!focus) return;
            bool x = input != null && input.LeftTracked && controllerToggle?.WasPressedThisFrame() == true;
            bool h = keyboardToggle?.WasPressedThisFrame() == true;
            if (x || h) ToggleVisibility();
        }
    }
}
