using UnityEngine;
using CampusEmergency.Player;

namespace CampusEmergency.Prototype
{
    public sealed partial class PrototypeExplorer
    {
        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = Color.white;
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                textStyle.normal.textColor = new Color(.82f, .9f, .92f);
                badgeStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                badgeStyle.normal.textColor = new Color(.45f, .94f, .77f);
            }
            titleStyle.font = textStyle.font = badgeStyle.font = LanguageSelectionUI.CurrentFont;

            Color previous = GUI.color;
            GUI.color = new Color(.03f, .12f, .16f, .92f);
            GUI.DrawTexture(new Rect(20, 20, 350, walking ? 142 : 193), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(38, 32, 315, 22), "HARBOURVIEW UNIVERSITY", badgeStyle);
            GUI.Label(new Rect(38, 56, 320, 34), LanguageSelectionUI.T("Teaching Block A", "教学楼A座", "教學樓A座"), titleStyle);
            GUI.Label(new Rect(38, 94, 310, 58), walking
                ? LanguageSelectionUI.T("WASD move / Shift run / Space jump\nMouse look / E interact / V camera\nTab overview / R restart", "WASD移动 / Shift奔跑 / 空格跳跃\n鼠标观察 / E交互 / V切换视角\nTab全景 / R重置", "WASD移動 / Shift奔跑 / 空格跳躍\n滑鼠觀察 / E互動 / V切換視角\nTab全景 / R重設")
                : LanguageSelectionUI.T("4 floors / 2 stairwells / 1 lift\nRight-drag orbit / Scroll zoom / C cutaway", "4层 / 2个楼梯间 / 1部电梯\n右键拖动旋转 / 滚轮缩放 / C剖切", "4層 / 2個樓梯間 / 1部電梯\n右鍵拖動旋轉 / 滾輪縮放 / C剖切"), textStyle);
            if (!walking)
            {
                if (GUI.Button(new Rect(38, 147, 147, 34), LanguageSelectionUI.T("Enter fourth floor [F4]", "进入四楼 [F4]", "進入四樓 [F4]"))) EnterFloor(spawnFourthFloor);
                if (GUI.Button(new Rect(194, 147, 147, 34), LanguageSelectionUI.T("Enter second floor [F2]", "进入二楼 [F2]", "進入二樓 [F2]"))) EnterFloor(spawnSecondFloor);
            }
            GUI.Label(new Rect(22, Screen.height - 38, 600, 25), LanguageSelectionUI.T(
                "ENVIRONMENT PROTOTYPE  /  F2 & F4 start points",
                "环境原型  /  F2与F4出生点",
                "環境原型  /  F2與F4出生點"), textStyle);

            if (walking)
            {
                GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 12, 12, 24), "+");
                int floor = Mathf.Clamp(Mathf.FloorToInt((transform.position.y + .15f) / 3.6f) + 1, 1, 4);
                GUI.Label(new Rect(Screen.width - 165, 24, 145, 25), LanguageSelectionUI.T("CURRENT FLOOR  ", "当前楼层  ", "目前樓層  ") + floor.ToString("00"), badgeStyle);
                if (HeldTowel != null)
                    GUI.Label(new Rect(Screen.width - 165, 55, 145, 25),
                        HeldTowel.IsWet ? LanguageSelectionUI.T("TOWEL  WET", "毛巾  已打湿", "毛巾  已打濕") : LanguageSelectionUI.T("TOWEL  DRY", "毛巾  干燥", "毛巾  乾燥"), badgeStyle);
                if (focused != null) GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height / 2f + 25, 300, 28), LanguageSelectionUI.Localize(focused.Prompt), badgeStyle);
                if (Time.time < noticeUntil && !string.IsNullOrEmpty(notice))
                {
                    GUI.color = new Color(.03f, .12f, .16f, .92f);
                    GUI.DrawTexture(new Rect(Screen.width / 2f - 260, Screen.height - 130, 520, 76), Texture2D.whiteTexture);
                    GUI.color = previous;
                    GUI.Label(new Rect(Screen.width / 2f - 244, Screen.height - 118, 488, 65), LanguageSelectionUI.Localize(notice), textStyle);
                }
            }
        }
    }
}
