using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CampusEmergency.Player;
using CampusEmergency.Prototype;
using CampusEmergency.Interaction.FireSafety;

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
        private bool hiddenForLanguage;
        private CampusDrillAtmosphere atmosphere;
        private CampusFireTarget[] fireTargets;
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
                rect.sizeDelta = new Vector2(900f, 330f);
            }
            if (label == null) return;
            label.font = LanguageSelectionUI.CurrentFont;
            label.fontSize = 22; label.lineSpacing = 1.3f;
            label.alignment = TextAnchor.UpperLeft;
            label.color = new Color(.9f,.94f,.98f);
            label.supportRichText = true;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 15; label.resizeTextMaxSize = 22;
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
            if (LanguageSelectionUI.IsBlocking)
            {
                if (canvas == null) canvas = GetComponent<Canvas>();
                if (canvas != null) canvas.enabled = false;
                hiddenForLanguage = true;
                return;
            }
            if (hiddenForLanguage)
            {
                if (canvas != null) canvas.enabled = true;
                hiddenForLanguage = false;
            }
            ProcessToggle(input != null && input.HasInteractionFocus);
            if (label == null) return;
            label.font = LanguageSelectionUI.CurrentFont;
            string controls = LanguageSelectionUI.T(
                "TRIGGER: TELEPORT    A: INTERACT    RIGHT STICK: TURN\nRIGHT GRIP: RETURN TOWEL",
                "扳机：传送    A键：交互    右摇杆：转向\n右握把：归还毛巾",
                "扳機：傳送    A鍵：互動    右搖桿：轉向\n右握把：歸還毛巾");
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
                // Keep extinguisher controls/status visible while the towel remains equipped.
                if (extinguisher == null || extinguisher.Held == null) controls = inventory.Controls;
                string towelStatus = inventory.HeldTowel.IsWet
                    ? LanguageSelectionUI.T("WET TOWEL", "湿毛巾", "濕毛巾")
                    : LanguageSelectionUI.T("DRY TOWEL: use the tap", "干毛巾：请使用水龙头打湿", "乾毛巾：請使用水龍頭打濕");
                towelStatus += inventory.IsTowelRaised
                    ? LanguageSelectionUI.T(" / COVERING MOUTH AND NOSE", " / 正捂住口鼻", " / 正捂住口鼻")
                    : LanguageSelectionUI.T(" / LOWERED", " / 已放下", " / 已放下");
                // Towel protection is a persistent status, not a replacement for interaction feedback.
                live = string.IsNullOrEmpty(live) ? towelStatus : towelStatus + "\n" + live;
            }
            string alert = Time.unscaledTime < noticeUntil ? notice : string.Empty;
            string objective = BuildObjective(inventory, extinguisher);
            if (input != null)
            {
                if (!input.HeadTracked) { live = LanguageSelectionUI.T("Waiting for headset tracking. Connect Link before Play.", "正在等待头显追踪，请先连接 Link。", "正在等待頭顯追蹤，請先連接 Link。"); alert = null; }
                else if (!input.HasInteractionFocus) { live = LanguageSelectionUI.T("Return from the headset system menu.", "请从头显系统菜单返回。", "請從頭顯系統選單返回。"); alert = null; }
                else if (!input.LeftTracked || !input.RightTracked)
                { live = LanguageSelectionUI.T("Tracking: LEFT ", "追踪：左手 ", "追蹤：左手 ") + (input.LeftTracked ? "OK" : LanguageSelectionUI.T("MISSING", "丢失", "遺失"))
                    + LanguageSelectionUI.T(" / RIGHT ", " / 右手 ", " / 右手 ") + (input.RightTracked ? "OK" : LanguageSelectionUI.T("MISSING", "丢失", "遺失")); alert = null; }
            }
            label.text = "<color=#8ADFFF><b>CAMPUS XR</b></color>    <size=18>" + LanguageSelectionUI.T("X / H: SHOW OR HIDE", "X / H：显示或隐藏", "X / H：顯示或隱藏") + "</size>\n"
                + controls + "\n\n<color=#72F1C8><b>" + LanguageSelectionUI.T("NEXT", "下一步", "下一步") + " · "
                + LanguageSelectionUI.SelectedRouteName + "</b></color>  " + objective
                + "\n<color=#8ADFFF><b>" + LanguageSelectionUI.T("STATUS", "状态", "狀態") + "</b></color>  " + (string.IsNullOrEmpty(live) ? LanguageSelectionUI.T("Ready", "就绪", "就緒") : live)
                + (string.IsNullOrEmpty(alert) || alert == live ? string.Empty : "\n<color=#FFE0A0>" + alert + "</color>");
        }

        private string BuildObjective(CampusXRInventory inventory, CampusXRExtinguisherController extinguisher)
        {
            if (atmosphere == null) atmosphere = FindObjectOfType<CampusDrillAtmosphere>(true);
            if (atmosphere != null && !atmosphere.IsAlarmActive)
                return LanguageSelectionUI.T("Find the manual alarm point and press A.",
                    "找到手动火警按钮并按A键启动模拟警报。", "找到手動火警按鈕並按A鍵啟動模擬警報。");

            if (inventory == null || inventory.HeldTowel == null)
                return LanguageSelectionUI.T("Go to the wash station and pick up a towel.",
                    "前往洗手台并拿起毛巾。", "前往洗手台並拿起毛巾。");
            if (!inventory.HeldTowel.IsWet)
                return LanguageSelectionUI.T("Use the tap to wet the towel.",
                    "使用水龙头将毛巾打湿。", "使用水龍頭將毛巾打濕。");
            if (!inventory.IsTowelRaised)
                return LanguageSelectionUI.T("Press the left trigger to cover your mouth and nose with the wet towel.",
                    "按下左手柄扳机，用湿毛巾捂住口鼻。", "按下左手柄扳機，用濕毛巾捂住口鼻。");

            float height = input != null ? input.transform.position.y : transform.position.y;
            if (height > 1.2f)
            {
                return LanguageSelectionUI.SelectedRoute == 0
                    ? LanguageSelectionUI.T("Follow EXIT signs to Stair A on the east side and descend to the main exit.",
                        "跟随出口标志前往东侧A楼梯，下行至主出口。", "跟隨出口標誌前往東側A樓梯，下行至主出口。")
                    : LanguageSelectionUI.T("Follow the corridor west to Stair B and descend to the side exit.",
                        "沿走廊向西前往B楼梯，下行至侧门出口。", "沿走廊向西前往B樓梯，下行至側門出口。");
            }

            if (fireTargets == null || fireTargets.Length == 0) fireTargets = FindObjectsOfType<CampusFireTarget>(true);
            bool liveFire = false;
            foreach (CampusFireTarget fire in fireTargets)
                if (fire != null && fire.gameObject.activeInHierarchy && !fire.Extinguished) { liveFire = true; break; }

            if (liveFire && extinguisher != null && extinguisher.Held != null && !extinguisher.Held.PinRemoved)
                return LanguageSelectionUI.T("Move the left hand near the yellow pin and press left grip.",
                    "将左手靠近黄色保险销并按左握把拔销。", "將左手靠近黃色保險銷並按左握把拔銷。");
            if (liveFire && extinguisher != null && extinguisher.Held != null && extinguisher.Held.PinRemoved)
                return LanguageSelectionUI.T("Press B for spray mode; aim at the fire base and hold the trigger for 3 seconds.",
                    "按B切换喷射模式，对准火源根部并持续按扳机3秒。", "按B切換噴射模式，對準火源根部並持續按扳機3秒。");
            return LanguageSelectionUI.T("Continue outdoors to the marked assembly point.",
                "继续前往室外标记的集合点。", "繼續前往室外標記的集合點。");
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
