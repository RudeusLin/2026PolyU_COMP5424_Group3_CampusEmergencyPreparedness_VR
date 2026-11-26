using UnityEngine;
using UnityEngine.UI;

namespace CampusEmergency.Player
{
    public sealed partial class LanguageSelectionUI
    {
        // 0/1 keep their existing restart/exit semantics; 2..7 open nodes; 8 returns.
        private int debriefDetail = -1;
        private const int DebriefBack = 8;
        private static readonly Color DebriefGold = new Color(.957f, .835f, .541f);
        private static readonly Color DebriefTeal = new Color(.4f, .89f, .82f);

        private Transform PrepareDebriefPage(bool detail)
        {
            System.Array.Clear(choices, 0, choices.Length);
            System.Array.Clear(choiceImages, 0, choiceImages.Length);
            buttonColors.Clear(); hover = -1; clickArmed = false;
            Transform root = canvas.transform;
            if (xrMode)
            {
                if (pointer == null) BuildPointer();
                ((RectTransform)root).sizeDelta = new Vector2(detail ? 1780 : 1100, 1040);
                root.localScale = Vector3.one * .001f;
            }
            foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            Stretch(Image(root, "Completion Background", new Color(.01f, .03f, .05f, .88f)).rectTransform);
            var card = Image(root, "Completion Debrief Card", new Color(.025f, .10f, .125f, .98f));
            SetRect(card.rectTransform, detail ? 1740 : 1050, 1000, 0, 0);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = DebriefTeal;
            outline.effectDistance = new Vector2(2, -2);
            return root;
        }

        private void BuildDebriefOverview()
        {
            debriefDetail = -1;
            Transform root = PrepareDebriefPage(false);
            Text(root, "Completion Eyebrow", T("PRACTICE COMPLETE", "演练完成", "演練完成"), 25,
                DebriefTeal, new Vector2(0, 432), new Vector2(960, 40), FontStyle.Bold);
            Text(root, "Completion Title", T("Evacuation Review", "疏散流程复盘", "疏散流程復盤"), 44,
                DebriefGold, new Vector2(0, 365), new Vector2(960, 65), FontStyle.Bold);
            Text(root, "Debrief List Hint", T("Select a step to explore why it matters", "选择条目，查看原因与提示", "選擇條目，查看原因與提示"), 25,
                new Color(.90f, .96f, .96f), new Vector2(0, 307), new Vector2(960, 40), FontStyle.Normal);
            for (int node = 0; node < CampusDebriefContent.Count; node++)
            {
                int index = node + 2;
                var row = Image(root, "Debrief Node " + (node + 1), new Color(.045f, .19f, .21f, .98f));
                SetRect(row.rectTransform, 940, 74, 0, 224 - node * 88);
                RegisterDebriefChoice(row, index);
                var label = Text(row.transform, "Node Title", CampusDebriefContent.Get(node).Title,
                    Current == CampusLanguage.English ? 28 : 32, Color.white,
                    new Vector2(-40, 0), new Vector2(790, 65), FontStyle.Bold);
                label.alignment = TextAnchor.MiddleLeft;
                Text(row.transform, "Open Detail Arrow", "→", 44, DebriefGold,
                    new Vector2(410, 0), new Vector2(65, 65), FontStyle.Bold);
            }
            CreateBriefingButton(root, 0, T("RESTART", "重新开始", "重新開始"), -235, -366,
                new Color(.05f, .52f, .43f, .98f), 420);
            CreateBriefingButton(root, 1, T("EXIT PRACTICE", "退出演习", "退出演習"), 235, -366,
                new Color(.45f, .16f, .18f, .98f), 420);
            Text(root, "Debrief Safety Reminder", T("Follow your building's emergency procedures in a real fire.",
                "真实火灾中，请遵循所在建筑的应急程序。", "真實火災中，請遵循所在建築的應急程序。"),
                21, new Color(.70f, .82f, .84f), new Vector2(0, -444), new Vector2(960, 38), FontStyle.Normal);
            FinishDebriefTypography();
        }

        private void BuildDebriefDetail(int node)
        {
            var entry = CampusDebriefContent.Get(node);
            debriefDetail = node;
            Transform root = PrepareDebriefPage(true);
            Text(root, "Detail Title", entry.Title, Current == CampusLanguage.English ? 40 : 44,
                DebriefGold, new Vector2(0, 408), new Vector2(1620, 65), FontStyle.Bold);
            SetRect(Image(root, "Detail Divider", DebriefTeal).rectTransform, 1620, 3, 0, 350);

            var picture = new GameObject("Debrief Illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            picture.transform.SetParent(root, false);
            var raw = picture.GetComponent<RawImage>();
            raw.material = OverlayMaterial; raw.raycastTarget = false;
            raw.texture = Resources.Load<Texture2D>(CampusDebriefContent.ImageResource(node));
            float imageHeight = raw.texture != null ? 740f * raw.texture.height / raw.texture.width : 416f;
            SetRect(raw.rectTransform, 740, imageHeight, -440, 20);
            if (raw.texture == null) Debug.LogError("Missing debrief illustration: " + CampusDebriefContent.ImageResource(node));

            float top = 300;
            top = DebriefParagraph(root, "Detail Reason Heading", T("Why does this matter?", "为什么要这样做？", "為什麼要這樣做？"), top, true);
            top = DebriefParagraph(root, "Detail Reason", entry.Reason, top - 10, false);
            top = DebriefParagraph(root, "Detail Caution Heading", T("What should you remember?", "需要注意什么？", "需要注意什麼？"), top - 35, true);
            DebriefParagraph(root, "Detail Caution", entry.Caution, top - 10, false);

            CreateBriefingButton(root, DebriefBack, T("← BACK TO REVIEW", "← 返回复盘", "← 返回復盤"), -600, -428,
                new Color(.07f, .34f, .48f, .98f), 420);
            Text(root, "Detail Input Hint", xrMode ? T("Aim the right ray and press A", "右手射线对准按钮，按 A 确认", "右手射線對準按鈕，按 A 確認")
                : T("Select Back to Review to choose another step", "点击返回复盘，查看其他节点", "點擊返回復盤，查看其他節點"),
                22, new Color(.70f, .82f, .84f), new Vector2(365, -428), new Vector2(840, 45), FontStyle.Normal);
            FinishDebriefTypography();
        }

        private float DebriefParagraph(Transform parent, string name, string value, float top, bool heading)
        {
            int size = heading ? (Current == CampusLanguage.English ? 29 : 32) : (Current == CampusLanguage.English ? 26 : 28);
            var text = Text(parent, name, value, size, heading ? DebriefTeal : new Color(.90f, .96f, .96f),
                Vector2.zero, new Vector2(800, 1000), heading ? FontStyle.Bold : FontStyle.Normal);
            text.font = CurrentFont;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1.18f;
            float height = Mathf.Ceil(text.preferredHeight) + 6;
            SetRect(text.rectTransform, 800, height, 390, top - height * .5f);
            return top - height;
        }

        private void RegisterDebriefChoice(Image image, int index)
        {
            choices[index] = image.rectTransform; choiceImages[index] = image;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => RequestSelect(index));
        }

        private void FinishDebriefTypography()
        {
            ApplyCurrentFontToAllUI();
            foreach (var choice in choiceImages)
            {
                if (choice == null) continue;
                var label = choice.transform.Find("Label")?.GetComponent<Text>();
                if (label != null) label.fontSize = 25;
            }
        }
    }
}
