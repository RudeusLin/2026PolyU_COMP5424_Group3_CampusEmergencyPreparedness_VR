using UnityEngine;

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

            Color previous = GUI.color;
            GUI.color = new Color(.03f, .12f, .16f, .92f);
            GUI.DrawTexture(new Rect(20, 20, 350, walking ? 142 : 193), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(38, 32, 315, 22), "HARBOURVIEW UNIVERSITY", badgeStyle);
            GUI.Label(new Rect(38, 56, 320, 34), "Teaching Block A", titleStyle);
            GUI.Label(new Rect(38, 94, 310, 58), walking
                ? "WASD move / Shift run / Space jump\nMouse look / E interact / V camera\nTab overview / R restart"
                : "4 floors / 2 stairwells / 1 lift\nRight-drag orbit / Scroll zoom / C cutaway", textStyle);
            if (!walking)
            {
                if (GUI.Button(new Rect(38, 147, 147, 34), "Enter fourth floor [F4]")) EnterFloor(spawnFourthFloor);
                if (GUI.Button(new Rect(194, 147, 147, 34), "Enter second floor [F2]")) EnterFloor(spawnSecondFloor);
            }
            GUI.Label(new Rect(22, Screen.height - 38, 600, 25), "ENVIRONMENT PROTOTYPE  /  F2 & F4 start points  /  H smoke visual only", textStyle);

            if (walking)
            {
                GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 12, 12, 24), "+");
                int floor = Mathf.Clamp(Mathf.FloorToInt((transform.position.y + .15f) / 3.6f) + 1, 1, 4);
                GUI.Label(new Rect(Screen.width - 165, 24, 145, 25), "CURRENT FLOOR  " + floor.ToString("00"), badgeStyle);
                if (HeldTowel != null)
                    GUI.Label(new Rect(Screen.width - 165, 55, 145, 25),
                        HeldTowel.IsWet ? "TOWEL  WET" : "TOWEL  DRY", badgeStyle);
                if (focused != null) GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height / 2f + 25, 300, 28), focused.Prompt, badgeStyle);
                if (Time.time < noticeUntil && !string.IsNullOrEmpty(notice))
                {
                    GUI.color = new Color(.03f, .12f, .16f, .92f);
                    GUI.DrawTexture(new Rect(Screen.width / 2f - 260, Screen.height - 130, 520, 76), Texture2D.whiteTexture);
                    GUI.color = previous;
                    GUI.Label(new Rect(Screen.width / 2f - 244, Screen.height - 118, 488, 65), notice, textStyle);
                }
            }
        }
    }
}
