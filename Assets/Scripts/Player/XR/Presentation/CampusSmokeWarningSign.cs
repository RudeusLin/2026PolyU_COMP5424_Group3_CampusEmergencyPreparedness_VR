using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace CampusEmergency.Player.XR
{
    /// <summary>Fixed, non-colliding passage warning, readable when approaching from Block B.</summary>
    public sealed class CampusSmokeWarningSign : MonoBehaviour
    {
        private Canvas canvas;
        private Text heading, body;
        private Transform head;
        private CampusHintAttention attention;
        private float floor;
        public static void Install(float floorHeight)
        {
            if (GameObject.Find("401 Smoke Passage Warning") != null) return;
            var root = new GameObject("401 Smoke Passage Warning", typeof(RectTransform), typeof(Canvas), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(960, 360);
            rect.localScale = Vector3.one * .001f;
            // Text's readable side is local -Z: face east, towards Block B (+X).
            rect.SetPositionAndRotation(new Vector3(-10.875f, floorHeight + 1.65f, 0), Quaternion.LookRotation(Vector3.left));
            var sign = root.AddComponent<CampusSmokeWarningSign>();
            sign.floor = floorHeight;
            sign.canvas = root.GetComponent<Canvas>(); sign.canvas.renderMode = RenderMode.WorldSpace;
            var image = root.GetComponent<Image>(); image.color = new Color(.025f, .075f, .09f, .95f); image.raycastTarget = false;
            sign.heading = Label(root.transform, "Smoke Warning Heading", new Vector2(0, 108), new Vector2(880, 65), 36, FontStyle.Bold, new Color(.4f, .89f, .82f));
            sign.body = Label(root.transform, "Smoke Warning Body", new Vector2(0, -35), new Vector2(880, 205), 30, FontStyle.Normal, new Color(.9f, .96f, .96f));
            sign.attention = root.AddComponent<CampusHintAttention>();
            sign.RefreshLanguage();
        }
        private static Text Label(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, FontStyle style, Color colour)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text)); root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
            var text = root.GetComponent<Text>(); text.font = LanguageSelectionUI.CurrentFont;
            text.fontSize = fontSize; text.fontStyle = style; text.alignment = TextAnchor.MiddleLeft;
            text.color = colour; text.raycastTarget = false; text.lineSpacing = 1.2f;
            return text;
        }
        private void OnEnable() => LanguageSelectionUI.LanguageChanged += RefreshLanguage;
        private void OnDisable() => LanguageSelectionUI.LanguageChanged -= RefreshLanguage;
        public void RefreshLanguage()
        {
            if (heading == null || body == null) return;
            heading.font = body.font = LanguageSelectionUI.CurrentFont;
            heading.text = LanguageSelectionUI.T("02 Smoke ahead", "02 烟雾区域", "02 煙霧區域");
            string message = LanguageSelectionUI.T(
                "Dense smoke ahead.\nWet your towel at the washroom tap.\nPress LEFT TRIGGER to raise it before entering.",
                "前方烟雾较浓。\n先用水龙头打湿毛巾，\n再按左扳机举起后进入。",
                "前方煙霧較濃。\n先用水龍頭打濕毛巾，\n再按左扳機舉起後進入。");
            CampusUIKeycaps.Apply(body, message, 30);
        }
        private void LateUpdate()
        {
            if (head == null)
            {
                var origin = FindObjectOfType<XROrigin>(); head = origin != null && origin.Camera != null ? origin.Camera.transform : null;
                canvas.worldCamera = head != null ? head.GetComponent<Camera>() : null;
            }
            bool visible = head != null && !LanguageSelectionUI.IsBlocking && Mathf.Abs(head.position.y - floor - 1.65f) < 2f
                && Vector3.Distance(head.position, transform.position) < 10f && head.position.x > transform.position.x - .05f;
            canvas.enabled = visible;
            attention.Configure(visible, head);
        }
    }
}
