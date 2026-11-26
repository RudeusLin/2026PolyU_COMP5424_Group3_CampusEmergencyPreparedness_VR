using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CampusEmergency.Player
{
    public sealed partial class LanguageSelectionUI
    {
        private InputAction pauseToggle;
        private bool pauseMenuVisible, pauseStateSaved;
        private int pendingPauseAction;
        private float timeScaleBeforePause;
        private bool audioBeforePause;
        public static bool IsPauseMenuOpen => instance != null && instance.pauseMenuVisible;

        private void OnEnable()
        {
            if (pauseToggle == null)
                pauseToggle = new InputAction("Practice menu", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            pauseToggle.Enable();
        }
        private void OnDisable()
        {
            pauseToggle?.Disable();
            if (pauseMenuVisible) ResumePractice();
        }
        private void ProcessPauseInput(bool inputReady)
        {
            if (!xrMode || !inputReady || confirming || leavingDebrief || pauseToggle == null || !pauseToggle.WasPressedThisFrame()) return;
            if (elevatorWarningVisible) return;
            if (pauseMenuVisible) ResumePractice();
            else if (selectionComplete && !emergencyCallVisible && !completionVisible) OpenPauseMenu();
        }
        private void OpenPauseMenu()
        {
            if (!selectionComplete || emergencyCallVisible || completionVisible || pauseMenuVisible || leavingDebrief) return;
            pauseMenuVisible = true;
            pendingPauseAction = 0;
            timeScaleBeforePause = Time.timeScale;
            audioBeforePause = AudioListener.pause;
            pauseStateSaved = true;
            Time.timeScale = 0;
            AudioListener.pause = true;
            GetComponent<XR.CampusXRExtinguisherController>()?.SuspendForMenu();
            CaptureFixedPanelPose();
            canvas.gameObject.SetActive(true);
            BuildPauseMenu();
        }
        private void RestorePauseState()
        {
            if (!pauseStateSaved) return;
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = audioBeforePause;
            pauseStateSaved = false;
        }
        private void ResumePractice()
        {
            if (!pauseMenuVisible) return;
            pauseMenuVisible = false;
            elevatorWarningVisible = false;
            pendingPauseAction = 0;
            RestorePauseState();
            if (pointer != null) pointer.enabled = false;
            if (canvas != null)
            {
                foreach (Transform child in canvas.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                canvas.gameObject.SetActive(false);
            }
            System.Array.Clear(choices, 0, choices.Length);
            System.Array.Clear(choiceImages, 0, choiceImages.Length);
            buttonColors.Clear(); hover = -1; clickArmed = false;
        }
        private void SelectPauseChoice(int index)
        {
            if (elevatorWarningVisible)
            {
                if (index == 0) ResumePractice();
                return;
            }
            if (pendingPauseAction == 0)
            {
                if (index == 0) ResumePractice();
                else if (index == 1 || index == 2)
                {
                    pendingPauseAction = index;
                    BuildPauseMenu();
                }
                return;
            }
            if (index == 0)
            {
                pendingPauseAction = 0;
                BuildPauseMenu();
            }
            else if (index == 1)
            {
                int action = pendingPauseAction;
                ResumePractice();
                if (action == 1) RestartPractice();
                else ExitPractice();
            }
        }
        private void BuildPauseMenu()
        {
            Transform root = PrepareDebriefPage(false);
            Text(root, "Pause Eyebrow", T("PRACTICE PAUSED", "演练已暂停", "演練已暫停"), 25,
                DebriefTeal, new Vector2(0, 345), new Vector2(940, 42), FontStyle.Bold);
            bool confirmation = pendingPauseAction != 0;
            string title = confirmation
                ? (pendingPauseAction == 1 ? T("Restart practice?", "重新开始演练？", "重新開始演練？") : T("Exit practice?", "退出本次演练？", "退出本次演練？"))
                : T("Practice Menu", "演练菜单", "演練選單");
            Text(root, "Pause Title", title, 44, DebriefGold, new Vector2(0, 265), new Vector2(940, 70), FontStyle.Bold);
            SetRect(Image(root, "Pause Divider", new Color(.4f, .89f, .82f, .6f)).rectTransform, 850, 2, 0, 205);
            if (!confirmation)
            {
                PauseButton(root, 0, T("CONTINUE PRACTICE", "继续演练", "繼續演練"), 90, new Color(.05f, .52f, .43f, .98f));
                PauseButton(root, 1, T("RESTART", "重新开始", "重新開始"), -25, new Color(.07f, .34f, .48f, .98f));
                PauseButton(root, 2, T("EXIT PRACTICE", "退出演习", "退出演習"), -140, new Color(.45f, .16f, .18f, .98f));
                Text(root, "Pause Message", T("Your current progress is kept while this menu is open.",
                    "菜单打开期间，保留当前演练进度。", "選單開啟期間，保留目前演練進度。"), 25,
                    new Color(.9f, .95f, .96f), new Vector2(0, -255), new Vector2(900, 75), FontStyle.Normal);
            }
            else
            {
                string message = pendingPauseAction == 1
                    ? T("Your current progress will be cleared.\nReturn to room 401 and choose a language again.",
                        "当前进度将被清空。\n返回 401 起点，重新选择语言并开始演练。",
                        "目前進度將被清除。\n返回 401 起點，重新選擇語言並開始演練。")
                    : T("The application will close.\nProgress from this run will not be saved.",
                        "程序将关闭。\n本次演练进度不会保存。", "程式將關閉。\n本次演練進度不會儲存。");
                var messageText = Text(root, "Pause Confirmation Message", message, 29, new Color(.9f, .95f, .96f),
                    new Vector2(0, 65), new Vector2(900, 160), FontStyle.Normal);
                messageText.lineSpacing = 1.3f;
                CreateBriefingButton(root, 0, T("CANCEL", "取消", "取消"), -225, -140, new Color(.07f, .34f, .48f, .98f), 400);
                CreateBriefingButton(root, 1, pendingPauseAction == 1 ? T("CONFIRM RESTART", "确认重开", "確認重開") : T("CONFIRM EXIT", "确认退出", "確認退出"),
                    225, -140, new Color(.45f, .16f, .18f, .98f), 400);
            }
            var hint = Text(root, "Pause Input Hint", T("Right ray + A: select\nB: return to practice",
                "右手射线 + A：选择\nB：返回演练", "右手射線 + A：選擇\nB：返回演練"), 24,
                new Color(.7f, .82f, .84f), new Vector2(0, -365), new Vector2(940, 85), FontStyle.Normal);
            hint.lineSpacing = 1.25f;
            FinishDebriefTypography();
        }
        private void PauseButton(Transform root, int index, string label, float y, Color color)
        {
            CreateBriefingButton(root, index, label, 0, y, color, 760);
            var choice = choices[index];
            SetRect(choice, 760, 80, 0, y);
            SetRect(choice.Find("Label").GetComponent<RectTransform>(), 735, 70, 0, 0);
        }
    }
}
