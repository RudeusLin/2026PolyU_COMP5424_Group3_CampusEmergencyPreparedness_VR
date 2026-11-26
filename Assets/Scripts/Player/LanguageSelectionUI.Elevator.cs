using UnityEngine;
using UnityEngine.UI;

namespace CampusEmergency.Player
{
    public sealed partial class LanguageSelectionUI
    {
        private bool elevatorWarningVisible;

        public static void ShowElevatorWarning()
        {
            if (instance == null || !instance.selectionComplete || instance.emergencyCallVisible || instance.completionVisible || instance.pauseMenuVisible || instance.leavingDebrief) return;
            // Reuse the pause lifetime and fixed startup/review-style player/panel anchor.
            instance.OpenPauseMenu();
            instance.elevatorWarningVisible = true;
            instance.BuildElevatorWarning();
        }

        private void BuildElevatorWarning()
        {
            Transform root = PrepareDebriefPage(true);
            Color warning = new Color(1f, .3f, .3f);
            Text(root, "Elevator Warning Title", T("DO NOT USE LIFTS DURING A FIRE", "火灾时请勿乘坐电梯", "火災時請勿乘坐升降機"),
                46, warning, new Vector2(0, 270), new Vector2(1540, 85), FontStyle.Bold);
            SetRect(Image(root, "Elevator Warning Divider", DebriefTeal).rectTransform, 1480, 2, 0, 190);
            var explanation = Text(root, "Elevator Warning Reason", T(
                "Fire may cause a power failure or lift malfunction.\nSmoke may also enter the lift shaft.",
                "火灾可能导致电梯停电或故障，\n烟雾也可能进入电梯井。",
                "火災可能導致升降機停電或故障，\n煙霧也可能進入升降機井。"),
                32, new Color(.9f, .95f, .96f), new Vector2(0, 72), new Vector2(1450, 125), FontStyle.Normal);
            explanation.lineSpacing = 1.3f;
            var instruction = Text(root, "Elevator Warning Instruction", T(
                "Follow exit signs and staff instructions.\nUse a safe staircase to evacuate.",
                "请按照出口标志和现场指引，\n使用安全楼梯撤离。",
                "請按照出口標誌和現場指引，\n使用安全樓梯撤離。"),
                34, DebriefGold, new Vector2(0, -92), new Vector2(1450, 125), FontStyle.Bold);
            instruction.lineSpacing = 1.3f;
            CreateBriefingButton(root, 0, T("I UNDERSTAND", "我知道了", "我知道了"), 0, -265,
                new Color(.05f, .52f, .43f, .98f), 700);
            SetRect(choices[0], 700, 78, 0, -265);
            Text(root, "Elevator Warning Hint", xrMode
                ? T("Aim the right ray at the button and press A", "右手射线对准按钮，按 A 确认", "右手射線對準按鈕，按 A 確認")
                : T("Select I UNDERSTAND to continue", "点击“我知道了”继续", "點擊「我知道了」繼續"),
                25, new Color(.7f, .82f, .84f), new Vector2(0, -365), new Vector2(1450, 50), FontStyle.Normal);
            FinishDebriefTypography();
        }
    }
}
