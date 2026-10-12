using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using CampusEmergency.Player.XR;

namespace CampusEmergency.Player
{
    public enum CampusLanguage { English, SimplifiedChinese, TraditionalChinese }

    /// <summary>First-run language gate shared by desktop and XR scenes.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class LanguageSelectionUI : MonoBehaviour
    {
        private static LanguageSelectionUI instance;
        public static bool IsBlocking => instance != null &&
            (!instance.selectionComplete || instance.emergencyCallVisible || instance.completionVisible);
        public static CampusLanguage Current { get; private set; } = CampusLanguage.English;
        public static int SelectedRoute => instance != null ? instance.selectedRoute : 0;
        public static string SelectedRouteName => SelectedRoute == 0
            ? T("ROUTE A · EAST STAIR", "路线 A · 东侧楼梯", "路線 A · 東側樓梯")
            : T("ROUTE B · WEST STAIR", "路线 B · 西侧楼梯", "路線 B · 西側樓梯");
        public static event Action LanguageChanged;
        private static Font englishFont, simplifiedFont, traditionalFont;

        private readonly RectTransform[] choices = new RectTransform[12];
        private readonly Image[] choiceImages = new Image[12];
        private Canvas canvas;
        private Camera targetCamera;
        private Transform rightHand;
        private LineRenderer pointer;
        private Material pointerMaterial;
        private bool selectionComplete;
        private bool briefingVisible;
        private bool emergencyCallVisible;
        private bool completionVisible;
        private string dialedNumber = string.Empty;
        private Text phoneDisplay;
        private Text phoneMessage;
        private int selectedRoute;
        private Text routeTitle;
        private Text routeDescription;
        private bool xrMode;
        private int hover = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindObjectOfType<LanguageSelectionUI>() != null) return;
            var xr = FindObjectOfType<CampusXRInput>();
            var desktop = FindObjectOfType<CampusEmergency.Prototype.PrototypeExplorer>();
            if (xr == null && desktop == null) return;
            (xr != null ? xr.gameObject : desktop.gameObject).AddComponent<LanguageSelectionUI>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            xrMode = GetComponent<CampusXRInput>() != null;
            targetCamera = xrMode ? GetComponent<Unity.XR.CoreUtils.XROrigin>()?.Camera : Camera.main;
            BuildInterface();
        }

        private void BuildInterface()
        {
            var root = new GameObject("Language Selection Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.sortingOrder = 500;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            if (xrMode && targetCamera != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                var rt = root.GetComponent<RectTransform>();
                rt.SetParent(targetCamera.transform, false);
                rt.sizeDelta = new Vector2(920, 600);
                rt.localPosition = new Vector3(0, 0, 1.45f);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one * .00125f;
                FindXRHand();
                BuildPointer();
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                EnsureEventSystem();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Image veil = Image(root.transform, "Dimmed Background", new Color(.015f, .035f, .055f, xrMode ? .82f : .68f));
            Stretch(veil.rectTransform);
            Image card = Image(root.transform, "Language Card", new Color(.035f, .105f, .14f, .94f));
            SetRect(card.rectTransform, 760, 470, 0, 0);
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.22f, .82f, .76f, .5f);
            outline.effectDistance = new Vector2(2, -2);

            Text(root.transform, "Campus Safety", "HARBOURVIEW UNIVERSITY", 19,
                new Color(.4f, .92f, .82f), new Vector2(0, 176), new Vector2(650, 30), FontStyle.Bold);
            Text(root.transform, "Title", "Choose your language", 38, Color.white,
                new Vector2(0, 118), new Vector2(680, 58), FontStyle.Bold);
            Text(root.transform, "Subtitle", "選擇語言  ·  选择语言", 20,
                new Color(.72f, .82f, .85f), new Vector2(0, 72), new Vector2(680, 36), FontStyle.Normal);

            string[] labels = { "English", "简体中文", "繁體中文" };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = (i - 1) * 220f;
                Image button = Image(root.transform, "Language " + labels[i], new Color(.075f, .20f, .24f, .96f));
                SetRect(button.rectTransform, 190, 92, x, -30);
                choices[i] = button.rectTransform;
                choiceImages[i] = button;
                int selected = i;
                Button b = button.gameObject.AddComponent<Button>();
                b.targetGraphic = button;
                b.onClick.AddListener(() => Select(selected));
                Text(button.transform, "Label", labels[i], i == 0 ? 25 : 28, Color.white,
                    Vector2.zero, new Vector2(180, 80), FontStyle.Bold);
            }
            Text(root.transform, "Hint", xrMode ? "Point with the right controller and press A" : "Select a language to begin",
                16, new Color(.62f, .73f, .76f), new Vector2(0, -126), new Vector2(650, 30), FontStyle.Normal);
            Text(root.transform, "Privacy", "You can change this choice when the experience restarts.", 13,
                new Color(.42f, .57f, .61f), new Vector2(0, -174), new Vector2(650, 26), FontStyle.Normal);
        }

        private void Update()
        {
            if ((selectionComplete && !emergencyCallVisible) || completionVisible || !xrMode) return;
            if (rightHand == null) FindXRHand();
            hover = -1;
            if (rightHand == null || targetCamera == null) { UpdateChoiceVisuals(); return; }
            Plane plane = new Plane(canvas.transform.forward, canvas.transform.position);
            Ray ray = new Ray(rightHand.position, rightHand.forward);
            if (plane.Raycast(ray, out float distance) && distance < 5f)
            {
                Vector3 hit = ray.GetPoint(distance);
                for (int i = 0; i < choices.Length; i++)
                    if (choices[i] != null && RectTransformUtility.RectangleContainsScreenPoint(choices[i],
                        RectTransformUtility.WorldToScreenPoint(targetCamera, hit), targetCamera)) { hover = i; break; }
                if (pointer != null)
                {
                    pointer.enabled = true; pointer.SetPosition(0, rightHand.position); pointer.SetPosition(1, hit);
                    pointer.startColor = pointer.endColor = hover >= 0 ? new Color(.35f, 1f, .82f) : new Color(.65f, .72f, .75f);
                }
            }
            else if (pointer != null) pointer.enabled = false;
            UpdateChoiceVisuals();
            InputActionPressed();
        }

        private void InputActionPressed()
        {
            if (hover < 0) return;
            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!device.isValid) return;
            device.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed);
            if (!pressed) { clickArmed = true; return; }
            if (clickArmed) { clickArmed = false; Select(hover); }
        }
        private bool clickArmed = true;

        private void Select(int index)
        {
            if (emergencyCallVisible)
            {
                HandlePhoneKey(index);
                return;
            }
            if (briefingVisible)
            {
                if (index < 2) PreviewRoute(index);
                else BeginExperience();
                return;
            }
            Current = (CampusLanguage)Mathf.Clamp(index, 0, 2);
            ApplyCurrentFontToAllUI();
            LanguageChanged?.Invoke();
            briefingVisible = true;
            selectedRoute = 0;
            BuildBriefing();
        }

        private void BeginExperience()
        {
            selectionComplete = true;
            briefingVisible = false;
            if (pointer != null) pointer.enabled = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            var feedback = FindObjectOfType<CampusXRFeedback>();
            if (feedback != null) feedback.Notify(WelcomeMessage(), 7f);
        }

        public static void ShowCompletionDebrief()
        {
            if (instance == null || instance.completionVisible) return;
            instance.completionVisible = true;
            if (instance.pointer != null) instance.pointer.enabled = false;
            if (instance.canvas != null)
            {
                instance.canvas.gameObject.SetActive(true);
                instance.BuildCompletionDebrief();
            }
        }

        public static void ShowEmergencyCall()
        {
            if (instance == null || instance.emergencyCallVisible || instance.completionVisible) return;
            instance.emergencyCallVisible = true;
            instance.briefingVisible = false;
            instance.dialedNumber = string.Empty;
            if (instance.canvas != null)
            {
                instance.canvas.gameObject.SetActive(true);
                instance.BuildEmergencyPhone();
            }
        }

        private void BuildEmergencyPhone()
        {
            Transform root = canvas.transform;
            foreach (Transform child in root) Destroy(child.gameObject);
            for (int i = 0; i < choices.Length; i++) { choices[i] = null; choiceImages[i] = null; }

            Image veil = Image(root, "Emergency Call Background", new Color(.01f, .025f, .04f, xrMode ? .88f : .72f));
            Stretch(veil.rectTransform);
            Image phone = Image(root, "Emergency Phone", new Color(.025f, .07f, .085f, .98f));
            SetRect(phone.rectTransform, 390, 550, 0, 0);
            Outline outline = phone.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.25f, .9f, .72f, .65f);
            outline.effectDistance = new Vector2(2, -2);

            Text(root, "Emergency Call Title", T("CALL EMERGENCY SERVICES", "拨打紧急电话", "撥打緊急電話"),
                25, Color.white, new Vector2(0, 235), new Vector2(350, 42), FontStyle.Bold);
            phoneMessage = Text(root, "Emergency Call Instruction", T(
                    "Enter the correct emergency number, then press CALL",
                    "输入正确的紧急电话号码，然后按拨打",
                    "輸入正確的緊急電話號碼，然後按撥打"),
                15, new Color(.65f, .82f, .84f), new Vector2(0, 192), new Vector2(340, 42), FontStyle.Normal);
            phoneDisplay = Text(root, "Phone Number", "_", 38, new Color(.45f, 1f, .76f),
                new Vector2(0, 137), new Vector2(320, 64), FontStyle.Bold);
            phoneDisplay.alignment = TextAnchor.MiddleRight;

            int[,] digits = { {1,2,3}, {4,5,6}, {7,8,9} };
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    int digit = digits[row, col];
                    CreatePhoneButton(root, digit, digit.ToString(), (col - 1) * 92f, 68f - row * 78f, 72f,
                        new Color(.075f, .20f, .24f, .98f));
                }
            CreatePhoneButton(root, 10, T("DELETE", "删除", "刪除"), -92f, -166f, 78f, new Color(.28f, .15f, .12f, .98f));
            CreatePhoneButton(root, 0, "0", 0f, -166f, 72f, new Color(.075f, .20f, .24f, .98f));
            CreatePhoneButton(root, 11, T("CALL", "拨打", "撥打"), 92f, -166f, 78f, new Color(.05f, .52f, .35f, .98f));
            Text(root, "Emergency Call Hint", xrMode
                    ? T("Point with the right controller and press A", "用右手柄指向并按A键", "用右手柄指向並按A鍵")
                    : T("Select the number keys", "点击数字按键", "點擊數字按鍵"),
                13, new Color(.5f, .65f, .68f), new Vector2(0, -235), new Vector2(340, 28), FontStyle.Normal);
            ApplyCurrentFontToAllUI();
        }

        private void CreatePhoneButton(Transform root, int index, string label, float x, float y, float width, Color color)
        {
            Image image = Image(root, "Phone Key " + index, color);
            SetRect(image.rectTransform, width, 62, x, y);
            choices[index] = image.rectTransform;
            choiceImages[index] = image;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Select(index));
            Text(image.transform, "Label", label, index < 10 ? 24 : 14, Color.white,
                Vector2.zero, new Vector2(width - 8, 54), FontStyle.Bold);
        }

        private void HandlePhoneKey(int index)
        {
            if (index >= 0 && index <= 9)
            {
                if (dialedNumber.Length < 3) dialedNumber += index.ToString();
                phoneMessage.text = T("Emergency number required: three digits", "请输入三位紧急电话号码", "請輸入三位緊急電話號碼");
                phoneMessage.color = new Color(.65f, .82f, .84f);
            }
            else if (index == 10 && dialedNumber.Length > 0)
            {
                dialedNumber = dialedNumber.Substring(0, dialedNumber.Length - 1);
                phoneMessage.color = new Color(.65f, .82f, .84f);
            }
            else if (index == 11)
            {
                if (dialedNumber == "999")
                {
                    phoneMessage.text = T("CONNECTED TO EMERGENCY SERVICES: 999", "已接通紧急服务：999", "已接通緊急服務：999");
                    phoneMessage.color = new Color(.45f, 1f, .76f);
                    foreach (Image image in choiceImages) if (image != null) image.raycastTarget = false;
                    StartCoroutine(CompleteEmergencyCall());
                }
                else
                {
                    phoneMessage.text = T("Incorrect number. In Hong Kong, dial 999.",
                        "号码错误。在香港应拨打999。", "號碼錯誤。在香港應撥打999。");
                    phoneMessage.color = new Color(1f, .55f, .42f);
                    dialedNumber = string.Empty;
                }
            }
            if (phoneDisplay != null) phoneDisplay.text = string.IsNullOrEmpty(dialedNumber) ? "_" : dialedNumber;
        }

        private IEnumerator CompleteEmergencyCall()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            emergencyCallVisible = false;
            ShowCompletionDebrief();
        }

        private void BuildCompletionDebrief()
        {
            Transform root = canvas.transform;
            foreach (Transform child in root) Destroy(child.gameObject);

            Image veil = Image(root, "Completion Background", new Color(.01f, .03f, .05f, xrMode ? .88f : .74f));
            Stretch(veil.rectTransform);
            Image card = Image(root, "Completion Debrief Card", new Color(.025f, .10f, .125f, .96f));
            SetRect(card.rectTransform, 850, 570, 0, 0);
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.28f, .95f, .70f, .65f);
            outline.effectDistance = new Vector2(2, -2);

            Text(root, "Completion Eyebrow", T("PRACTICE COMPLETE", "演练完成", "演練完成"),
                17, new Color(.40f, .95f, .76f), new Vector2(0, 247), new Vector2(740, 28), FontStyle.Bold);
            Text(root, "Completion Title", T("Congratulations! You have completed the practice",
                    "恭喜你已完成练习", "恭喜你已完成練習"),
                31, Color.white, new Vector2(0, 204), new Vector2(760, 50), FontStyle.Bold);

            Image reviewPanel = Image(root, "Debrief Panel", new Color(.045f, .15f, .17f, .92f));
            SetRect(reviewPanel.rectTransform, 760, 365, 0, -18);
            Text heading = Text(reviewPanel.transform, "Debrief Heading", T("REVIEW: REASON & RISK", "复盘：原因与风险", "復盤：原因與風險"),
                19, new Color(.48f, .96f, .78f), new Vector2(0, 157), new Vector2(690, 30), FontStyle.Bold);
            heading.alignment = TextAnchor.MiddleLeft;
            Text review = Text(reviewPanel.transform, "Debrief Content", T(
                    "1  RAISE ALARM — warns others and starts the response. Delay can leave people unaware and trapped.\n" +
                    "2  WET TOWEL — reduces inhalation of some smoke particles. No protection increases coughing, disorientation and breathing risk.\n" +
                    "3  USE STAIRS / " + SelectedRouteName + " — stairs remain the safer signed escape route. A lift may stop, open onto smoke or trap occupants.\n" +
                    "4  AIM AT FIRE BASE — interrupts the fuel source. Spraying the flames above wastes agent and may not extinguish the fire.\n" +
                    "5  ASSEMBLY POINT — enables headcount and keeps access clear. Leaving or re-entering can cause a missing-person search and renewed exposure.\n" +
                    "6  CALL 999 — gives emergency services the location and incident details. A wrong number or no call delays professional rescue and fire response.",
                    "1  启动警报——及时通知他人并启动应急响应；延误可能使人员毫无准备，错过逃生时机。\n" +
                    "2  湿毛巾捂住口鼻——可减少吸入部分烟尘；不防护会增加咳嗽、迷失方向和呼吸困难的风险。\n" +
                    "3  使用楼梯 / " + SelectedRouteName + "——楼梯是有标识的安全疏散路线；电梯可能停运、开门遇烟或困住人员。\n" +
                    "4  瞄准火源根部——切断燃烧源；只喷火焰上方会浪费灭火剂，也可能无法灭火。\n" +
                    "5  前往集合点——便于清点人数并保持救援通道畅通；擅自离开或返回建筑会引发搜救并再次暴露于危险。\n" +
                    "6  拨打999——向紧急服务提供地点和事故信息；拨错号码或不拨打会延误专业救援和消防响应。",
                    "1  啟動警報——及時通知他人並啟動應急響應；延誤可能使人員毫無準備，錯過逃生時機。\n" +
                    "2  濕毛巾捂住口鼻——可減少吸入部分煙塵；不防護會增加咳嗽、迷失方向和呼吸困難的風險。\n" +
                    "3  使用樓梯 / " + SelectedRouteName + "——樓梯是有標識的安全疏散路線；電梯可能停運、開門遇煙或困住人員。\n" +
                    "4  瞄準火源根部——切斷燃燒源；只噴火焰上方會浪費滅火劑，也可能無法滅火。\n" +
                    "5  前往集合點——便於清點人數並保持救援通道暢通；擅自離開或返回建築會引發搜救並再次暴露於危險。\n" +
                    "6  撥打999——向緊急服務提供地點和事故資訊；撥錯號碼或不撥打會延誤專業救援和消防響應。"),
                14, new Color(.90f, .96f, .96f), new Vector2(0, -18), new Vector2(690, 300), FontStyle.Normal);
            review.alignment = TextAnchor.UpperLeft;
            review.lineSpacing = 1.15f;
            review.resizeTextForBestFit = true;
            review.resizeTextMinSize = 11;
            review.resizeTextMaxSize = 14;

            Text(root, "Debrief Reminder", T(
                    "In a real emergency: raise the alarm, follow staff instructions, use stairs, and never re-enter the building.",
                    "真实火灾中：立即报警、服从现场指引、使用楼梯疏散，切勿重新进入建筑。",
                    "真實火災中：立即報警、服從現場指引、使用樓梯疏散，切勿重新進入建築。"),
                13, new Color(.70f, .82f, .84f), new Vector2(0, -250), new Vector2(740, 40), FontStyle.Normal);

            ApplyCurrentFontToAllUI();
        }

        private void BuildBriefing()
        {
            Transform root = canvas.transform;
            foreach (Transform child in root) Destroy(child.gameObject);

            Image veil = Image(root, "Briefing Background", new Color(.015f, .035f, .055f, xrMode ? .84f : .7f));
            Stretch(veil.rectTransform);
            Image card = Image(root, "Practice Briefing Card", new Color(.03f, .095f, .13f, .96f));
            SetRect(card.rectTransform, 840, 550, 0, 0);
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.22f, .82f, .76f, .5f);
            outline.effectDistance = new Vector2(2, -2);

            Text(root, "Briefing Eyebrow", T("CAMPUS EMERGENCY PREPAREDNESS", "校园应急准备演练", "校園應急準備演練"),
                17, new Color(.4f, .92f, .82f), new Vector2(0, 233), new Vector2(760, 28), FontStyle.Bold);
            Text(root, "Briefing Title", T("Practice overview & route preview", "项目流程与路线预览", "項目流程與路線預覽"),
                32, Color.white, new Vector2(0, 191), new Vector2(760, 48), FontStyle.Bold);

            Image flowPanel = Image(root, "Practice Flow Panel", new Color(.045f, .14f, .17f, .92f));
            SetRect(flowPanel.rectTransform, 370, 320, -205, 10);
            Text flowHeading = Text(flowPanel.transform, "Flow Heading", T("PRACTICE FLOW", "演练流程", "演練流程"),
                19, new Color(.45f, .94f, .77f), new Vector2(0, 127), new Vector2(320, 30), FontStyle.Bold);
            flowHeading.alignment = TextAnchor.MiddleLeft;
            Text flow = Text(flowPanel.transform, "Flow Steps", T(
                "1  Raise the simulated fire alarm\n2  Pick up and wet a towel; cover mouth and nose\n3  Follow EXIT signs; use the stairs\n4  Pull the extinguisher pin\n5  Aim at the fire base for 3 seconds\n6  Reach the outdoor assembly point\n7  Take out phone and call 999",
                "1  启动模拟火警\n2  拿取并打湿毛巾，捂住口鼻\n3  跟随出口标志，经楼梯疏散\n4  拔除灭火器保险销\n5  对准火源根部持续喷射3秒\n6  到达室外集合点\n7  掏出手机拨打999",
                "1  啟動模擬火警\n2  拿取並打濕毛巾，捂住口鼻\n3  跟隨出口標誌，經樓梯疏散\n4  拔除滅火器保險銷\n5  對準火源根部持續噴射3秒\n6  到達室外集合點\n7  掏出手機撥打999"),
                16, new Color(.88f, .94f, .95f), new Vector2(0, -15), new Vector2(320, 245), FontStyle.Normal);
            flow.alignment = TextAnchor.UpperLeft;
            flow.lineSpacing = 1.35f;

            Image routePanel = Image(root, "Route Preview Panel", new Color(.045f, .14f, .17f, .92f));
            SetRect(routePanel.rectTransform, 370, 320, 205, 10);
            routeTitle = Text(routePanel.transform, "Route Heading", string.Empty, 19,
                new Color(.45f, .94f, .77f), new Vector2(0, 127), new Vector2(320, 30), FontStyle.Bold);
            routeTitle.alignment = TextAnchor.MiddleLeft;
            Text map = Text(routePanel.transform, "Route Diagram",
                T("CLASSROOM 401\n      ↓\n4F CORRIDOR  →  STAIR\n      ↓\nGROUND EXIT  →  ASSEMBLY",
                  "401教室\n      ↓\n四楼走廊  →  楼梯间\n      ↓\n首层出口  →  集合点",
                  "401教室\n      ↓\n四樓走廊  →  樓梯間\n      ↓\n首層出口  →  集合點"),
                18, Color.white, new Vector2(0, 25), new Vector2(320, 150), FontStyle.Bold);
            map.lineSpacing = 1.15f;
            routeDescription = Text(routePanel.transform, "Route Details", string.Empty, 15,
                new Color(.7f, .82f, .84f), new Vector2(0, -105), new Vector2(320, 62), FontStyle.Normal);
            routeDescription.alignment = TextAnchor.UpperLeft;

            CreateBriefingButton(root, 0, T("ROUTE A", "路线 A", "路線 A"), -230, -205, new Color(.07f, .34f, .48f, .98f));
            CreateBriefingButton(root, 1, T("ROUTE B", "路线 B", "路線 B"), -25, -205, new Color(.46f, .29f, .08f, .98f));
            CreateBriefingButton(root, 2, T("BEGIN PRACTICE", "开始演练", "開始演練"), 245, -205, new Color(.05f, .52f, .43f, .98f), 250);
            Text(root, "Briefing Hint", xrMode
                    ? T("Point with the right controller and press A", "使用右控制器指向并按A键", "使用右控制器指向並按A鍵")
                    : T("Select a route to preview, then begin", "选择路线预览，然后开始演练", "選擇路線預覽，然後開始演練"),
                13, new Color(.5f, .65f, .68f), new Vector2(0, -252), new Vector2(720, 24), FontStyle.Normal);

            ApplyCurrentFontToAllUI();
            PreviewRoute(selectedRoute);
        }

        private void CreateBriefingButton(Transform root, int index, string label, float x, float y, Color color, float width = 175)
        {
            Image image = Image(root, "Briefing Choice " + index, color);
            SetRect(image.rectTransform, width, 58, x, y);
            choices[index] = image.rectTransform;
            choiceImages[index] = image;
            int selected = index;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Select(selected));
            Text(image.transform, "Label", label, 17, Color.white, Vector2.zero, new Vector2(width - 12, 50), FontStyle.Bold);
        }

        private void PreviewRoute(int route)
        {
            selectedRoute = Mathf.Clamp(route, 0, 1);
            if (routeTitle == null || routeDescription == null) return;
            bool a = selectedRoute == 0;
            routeTitle.text = a
                ? T("ROUTE A · EAST STAIR", "路线 A · 东侧楼梯", "路線 A · 東側樓梯")
                : T("ROUTE B · WEST STAIR", "路线 B · 西侧楼梯", "路線 B · 西側樓梯");
            routeDescription.text = a
                ? T("Leave 401, turn right into the corridor, enter Stair A and descend to the main exit.",
                    "离开401后右转进入走廊，经A楼梯下行至主出口。", "離開401後右轉進入走廊，經A樓梯下行至主出口。")
                : T("Leave 401, turn left along the corridor, enter Stair B and descend to the side exit.",
                    "离开401后左转沿走廊前行，经B楼梯下行至侧门出口。", "離開401後左轉沿走廊前行，經B樓梯下行至側門出口。")
                ;
            UpdateChoiceVisuals();
        }

        public static string WelcomeMessage()
        {
            switch (Current)
            {
                case CampusLanguage.SimplifiedChinese: return "语言：简体中文\n请跟随出口标志前往楼梯。";
                case CampusLanguage.TraditionalChinese: return "語言：繁體中文\n請跟隨出口標誌前往樓梯。";
                default: return "LANGUAGE: ENGLISH\nFollow EXIT signs to a stairwell.";
            }
        }

        public static string T(string english, string simplified, string traditional)
        {
            switch (Current)
            {
                case CampusLanguage.SimplifiedChinese: return simplified;
                case CampusLanguage.TraditionalChinese: return traditional;
                default: return english;
            }
        }

        public static string Localize(string value)
        {
            if (string.IsNullOrEmpty(value) || Current == CampusLanguage.English) return value;
            string sc = value, tc = value;
            switch (value)
            {
                case "[E] Pick up towel": sc = "[A键] 拿起毛巾"; tc = "[A鍵] 拿起毛巾"; break;
                case "[A button] Pick up towel": sc = "[A键] 拿起毛巾"; tc = "[A鍵] 拿起毛巾"; break;
                case "[E] Use tap / wet held towel": sc = "[A键] 使用水龙头 / 打湿毛巾"; tc = "[A鍵] 使用水龍頭 / 打濕毛巾"; break;
                case "[A button] Use tap / wet held towel": sc = "[A键] 使用水龙头 / 打湿毛巾"; tc = "[A鍵] 使用水龍頭 / 打濕毛巾"; break;
                case "[E] Lift information": sc = "[A键] 查看电梯提示"; tc = "[A鍵] 查看電梯提示"; break;
                case "[A button] Lift information": sc = "[A键] 查看电梯提示"; tc = "[A鍵] 查看電梯提示"; break;
                case "[E] Inspect alarm point": sc = "[A键] 查看手动报警器"; tc = "[A鍵] 查看手動警報器"; break;
                case "[A button] Inspect alarm point": sc = "[A键] 查看手动报警器"; tc = "[A鍵] 查看手動警報器"; break;
                case "[E] Activate fire alarm": sc = "[A键] 启动火警"; tc = "[A鍵] 啟動火警"; break;
                case "[A button] Activate fire alarm": sc = "[A键] 启动火警"; tc = "[A鍵] 啟動火警"; break;
                case "[E] Inspect fire equipment": sc = "[A键] 查看消防设备"; tc = "[A鍵] 查看消防設備"; break;
                case "[A button] Inspect fire equipment": sc = "[A键] 查看消防设备"; tc = "[A鍵] 查看消防設備"; break;
                case "Fire extinguished": sc = "火源已扑灭"; tc = "火源已撲滅"; break;
                case "FIRE: aim extinguisher at the base for 3 continuous seconds": sc = "火源：对准根部连续喷射3秒"; tc = "火源：對準根部連續噴射3秒"; break;
                case "[A button] Pick up extinguisher": sc = "[A键] 拿起灭火器"; tc = "[A鍵] 拿起滅火器"; break;
                case "You are already holding a towel.": sc = "你已经拿着一条毛巾。"; tc = "你已經拿著一條毛巾。"; break;
                case "TOWEL PICKED UP\nUse the tap to wet the towel. Right grip returns it to the counter.": sc = "已拿起毛巾\n使用水龙头打湿毛巾，按右握把可将其归还。"; tc = "已拿起毛巾\n使用水龍頭打濕毛巾，按右握把可將其歸還。"; break;
                case "TOWEL PICKED UP\nUse the tap to wet the towel. R returns it to the counter.": sc = "已拿起毛巾\n使用水龙头打湿毛巾，按R键可将其归还。"; tc = "已拿起毛巾\n使用水龍頭打濕毛巾，按R鍵可將其歸還。"; break;
                case "TOWEL WET\nPress A on the tap again to turn off the water.": sc = "毛巾已打湿\n再次对水龙头按A键可关闭水流。"; tc = "毛巾已打濕\n再次對水龍頭按A鍵可關閉水流。"; break;
                case "TOWEL WET\nPress E on the tap again to turn off the water.": sc = "毛巾已打湿\n再次对水龙头按E键可关闭水流。"; tc = "毛巾已打濕\n再次對水龍頭按E鍵可關閉水流。"; break;
                case "TAP ON\nPick up the towel, then use the tap to wet it.": sc = "水龙头已开启\n拿起毛巾后再次使用水龙头将其打湿。"; tc = "水龍頭已開啟\n拿起毛巾後再次使用水龍頭將其打濕。"; break;
                case "TAP OFF": sc = "水龙头已关闭"; tc = "水龍頭已關閉"; break;
                case "LIFT OUT OF SERVICE\nDo not use the lift in case of fire. Use the stairs.": sc = "电梯暂停使用\n发生火灾时请勿使用电梯，请走楼梯。"; tc = "電梯暫停使用\n發生火災時請勿使用電梯，請走樓梯。"; break;
                case "MANUAL FIRE ALARM\nInspection preview. Alarm logic will be connected by the gameplay team.": sc = "手动火灾报警器\n当前为检查预览，报警逻辑尚待接入。"; tc = "手動火災警報器\n目前為檢查預覽，警報邏輯尚待接入。"; break;
                case "SIMULATED FIRE ALARM ACTIVATED\nEvacuate via the stairs.": sc = "模拟警报已启动\n请通过楼梯疏散。"; tc = "模擬警報已啟動\n請通過樓梯疏散。"; break;
                case "SIMULATED FIRE ALARM ACTIVE\nThe alarm is already running.": sc = "模拟警报运行中\n警报已经启动。"; tc = "模擬警報運行中\n警報已經啟動。"; break;
                case "FIRE ALARM UNAVAILABLE\nThe alarm system could not be found in this scene.": sc = "火警不可用\n当前场景中未找到报警系统。"; tc = "火警不可用\n目前場景中未找到警報系統。"; break;
                case "FIRE EQUIPMENT\nEnvironment preview. The current practice route leads to the assembly point.": sc = "消防设备\n当前演练路线通往室外集合点。"; tc = "消防設備\n目前演練路線通往室外集合點。"; break;
                case "EXTINGUISHER: ACTION mode. B switches mode. Left grip near the yellow pin removes it.": sc = "灭火器：当前为移动模式。按B切换模式，左手靠近黄色保险销后按握把拔除。"; tc = "滅火器：目前為移動模式。按B切換模式，左手靠近黃色保險銷後按握把拔除。"; break;
                case "Return your held item first. Extinguisher pickup requires the XR player.": sc = "请先归还手中物品。灭火器仅支持XR玩家拿取。"; tc = "請先歸還手中物品。滅火器僅支援XR玩家拿取。"; break;
                case "Follow EXIT signs to a stairwell. Press E to open the stair doors.": sc = "请跟随出口标志前往楼梯间，按E键打开楼梯门。"; tc = "請跟隨出口標誌前往樓梯間，按E鍵打開樓梯門。"; break;
                case "ASSEMBLY POINT REACHED\nYou have completed the prototype walkthrough.": sc = "已到达集合点\n你已完成本次原型演练。"; tc = "已到達集合點\n你已完成本次原型演練。"; break;
            }
            return Current == CampusLanguage.SimplifiedChinese ? sc : tc;
        }

        public static Font CurrentFont
        {
            get
            {
                if (Current == CampusLanguage.SimplifiedChinese)
                    return simplifiedFont ?? (simplifiedFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial Unicode MS" }, 24));
                if (Current == CampusLanguage.TraditionalChinese)
                    return traditionalFont ?? (traditionalFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft JhengHei UI", "Microsoft JhengHei", "Noto Sans CJK TC", "Arial Unicode MS" }, 24));
                return englishFont ?? (englishFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Segoe UI", "Arial", "Liberation Sans" }, 24));
            }
        }

        public static void ApplyCurrentFontToAllUI()
        {
            Font font = CurrentFont;
            foreach (Text text in FindObjectsOfType<Text>(true)) text.font = font;
        }

        private void UpdateChoiceVisuals()
        {
            for (int i = 0; i < choiceImages.Length; i++)
            {
                if (choiceImages[i] == null) continue;
                if (i == hover) choiceImages[i].color = new Color(.08f, .62f, .52f, 1f);
                else if (briefingVisible && i < 2 && i == selectedRoute)
                    choiceImages[i].color = i == 0 ? new Color(.08f, .45f, .62f, 1f) : new Color(.62f, .38f, .08f, 1f);
                else if (briefingVisible && i == 2) choiceImages[i].color = new Color(.05f, .52f, .43f, .98f);
                else choiceImages[i].color = new Color(.075f, .20f, .24f, .96f);
            }
        }

        private void FindXRHand()
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    child.name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0) { rightHand = child; return; }
        }

        private void BuildPointer()
        {
            var go = new GameObject("Language Pointer"); go.transform.SetParent(transform, false);
            pointer = go.AddComponent<LineRenderer>(); pointer.positionCount = 2; pointer.useWorldSpace = true;
            pointer.startWidth = pointer.endWidth = .006f; pointer.enabled = false;
            pointerMaterial = new Material(Shader.Find("Sprites/Default")); pointer.sharedMaterial = pointerMaterial;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Font UiFont()
        {
            string[] fonts = { "Microsoft YaHei UI", "Microsoft JhengHei UI", "Noto Sans CJK SC", "Arial Unicode MS", "Arial" };
            return Font.CreateDynamicFontFromOSFont(fonts, 24);
        }
        private static Image Image(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false); var image = go.GetComponent<Image>(); image.color = color; return image;
        }
        private static Text Text(Transform parent, string name, string value, int size, Color color,
            Vector2 position, Vector2 dimensions, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false); var text = go.GetComponent<Text>(); text.text = value;
            text.font = UiFont(); text.fontSize = size; text.fontStyle = style; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            SetRect(text.rectTransform, dimensions.x, dimensions.y, position.x, position.y); return text;
        }
        private static void SetRect(RectTransform rect, float width, float height, float x, float y)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, y); }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private void OnDestroy()
        { if (instance == this) instance = null; if (pointerMaterial != null) Destroy(pointerMaterial); }
    }
}
