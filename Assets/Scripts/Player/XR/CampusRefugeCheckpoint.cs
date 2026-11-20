using UnityEngine;
using UnityEngine.UI;
using CampusEmergency.Prototype;
using CampusEmergency.Player;

namespace CampusEmergency.Player.XR
{
    public sealed class CampusRefugeCheckpoint : MonoBehaviour, IPrototypeInteractable
    {
        private CampusWheelchairRescue rescue;
        private Text label;
        private Transform visual, haloTransform, viewer;
        private LineRenderer halo;
        private Material dotMaterial, haloMaterial;
        private bool hovered, completed;
        private int labelState = -1;
        private float completedAt;
        public static Vector3 MarkerPosition => CampusWheelchairRescue.ParkingPosition + new Vector3(0f, .95f, -.45f);
        public bool IsRayHovered => hovered;
        public Color CurrentColor => dotMaterial.color;
        private static readonly Color Waiting = new Color(.45f, .53f, .57f);
        private static readonly Color Ready = new Color(.1f, .95f, .65f);
        private static readonly Color Focused = new Color(1f, .78f, .2f);
        private static readonly Color Confirmed = new Color(.2f, .85f, 1f);

        private void OnEnable() => LanguageSelectionUI.LanguageChanged += RefreshLabel;
        private void OnDisable() { LanguageSelectionUI.LanguageChanged -= RefreshLabel; hovered = false; }
        private void OnDestroy()
        {
            if (dotMaterial != null) Destroy(dotMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }
        public void SetRayHovered(bool value) { hovered = value; RefreshVisual(); }
        public string Prompt => LanguageSelectionUI.T("A: CONFIRM WHEELCHAIR PARKING", "A：确认安置轮椅人物", "A：確認安置輪椅人物");
        public string Interact(ICampusInteractor actor = null) => rescue.ConfirmParking();
        public void ShowCompleted()
        {
            completed = true;
            hovered = false;
            completedAt = Time.unscaledTime;
            label.transform.parent.localPosition = Vector3.up * 1.05f;
            GetComponent<Collider>().enabled = false;
            RefreshVisual();
        }
        private void RefreshLabel() { labelState = -1; RefreshVisual(); }
        private void RefreshVisual()
        {
            if (rescue == null || label == null) return;
            bool ready = !completed && rescue.CanConfirmParking(out _);
            int state = completed ? 3 : hovered && ready ? 2 : ready ? 1 : 0;
            Color color = completed ? Confirmed : state == 2 ? Focused : ready ? Ready : Waiting;
            dotMaterial.color = color;
            visual.localScale = Vector3.one * (state == 2 ? .37f : .32f);
            halo.startColor = halo.endColor = color;
            halo.enabled = state == 2 || completed && Time.unscaledTime - completedAt < .85f;
            if (state == 2) haloTransform.localScale = Vector3.one * (1f + .08f * Mathf.Sin(Time.unscaledTime * 6f));
            if (completed)
            {
                haloTransform.position = CampusWheelchairRescue.ParkingPosition + Vector3.up * .025f;
                haloTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
                float t = Mathf.Clamp01((Time.unscaledTime - completedAt) / .85f);
                haloTransform.localScale = Vector3.one * Mathf.Lerp(2.6f, 4.2f, t);
                color.a = 1f - t;
                halo.startColor = halo.endColor = color;
                visual.gameObject.SetActive(t < 1f);
            }
            if (labelState == state) return;
            labelState = state;
            label.font = LanguageSelectionUI.CurrentFont;
            string text = state == 3 ? LanguageSelectionUI.T("SAFE\nWheelchair parking confirmed", "已安置\n轮椅人物已安全停放", "已安置\n輪椅人物已安全停放")
                : state == 2 ? LanguageSelectionUI.T("READY TO CONFIRM\nPress A to park facing the door", "可以确认\n按A自动安置，面朝房门", "可以確認\n按A自動安置，面朝房門")
                : state == 1 ? LanguageSelectionUI.T("PARK HERE\nAim and press A to confirm", "安置确认点\n射线对准后，按A确认", "安置確認點\n射線對準後，按A確認")
                : LanguageSelectionUI.T("REFUGE PARKING\nEscort the wheelchair into this room", "安全屋安置点\n请先护送轮椅进入房间", "安全屋安置點\n請先護送輪椅進入房間");
            CampusUIKeycaps.Apply(label, text, 32);
            string hex = ColorUtility.ToHtmlStringRGB(color);
            label.text = "<b><color=#" + hex + ">" + label.text.Replace("\n", "</color></b>\n");
        }
        public static CampusRefugeCheckpoint Create(CampusWheelchairRescue owner)
        {
            var go = new GameObject("Refuge Confirm Parking");
            go.transform.position = MarkerPosition;
            var collider = go.AddComponent<SphereCollider>(); collider.radius = .24f; collider.isTrigger = true;
            var c = go.AddComponent<CampusRefugeCheckpoint>(); c.rescue = owner;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Checkpoint indicator"; dot.transform.SetParent(go.transform, false);
            dot.GetComponent<Collider>().enabled = false; Destroy(dot.GetComponent<Collider>()); c.visual = dot.transform;
            c.dotMaterial = new Material(Shader.Find("Unlit/Color")); dot.GetComponent<Renderer>().sharedMaterial = c.dotMaterial;
            var ring = new GameObject("Confirmation halo"); ring.transform.SetParent(go.transform, false);
            c.haloTransform = ring.transform; c.halo = ring.AddComponent<LineRenderer>();
            c.haloMaterial = new Material(Shader.Find("Sprites/Default")); c.halo.sharedMaterial = c.haloMaterial;
            c.halo.useWorldSpace = false; c.halo.loop = true; c.halo.widthMultiplier = .018f; c.halo.positionCount = 48;
            for (int i = 0; i < 48; i++) { float angle = i * Mathf.PI * 2f / 48; c.halo.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * .25f); }
            var panel = new GameObject("Confirm label", typeof(RectTransform), typeof(Canvas), typeof(Image));
            panel.transform.SetParent(go.transform, false); panel.transform.localPosition = Vector3.up * .65f;
            panel.transform.localScale = Vector3.one * .00128f;
            panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)panel.transform).sizeDelta = new Vector2(650, 180);
            panel.GetComponent<Image>().color = new Color(.025f, .065f, .09f, .96f);
            var glyph = new GameObject("Confirmation instructions", typeof(RectTransform), typeof(Text));
            glyph.transform.SetParent(panel.transform, false); ((RectTransform)glyph.transform).sizeDelta = new Vector2(600, 155);
            c.label = glyph.GetComponent<Text>(); c.label.fontSize = 28; c.label.color = Color.white;
            c.label.alignment = TextAnchor.MiddleCenter; c.label.lineSpacing = 1.25f;
            var origin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null && origin.Camera != null) c.viewer = origin.Camera.transform;
            c.RefreshVisual();
            return c;
        }
        private void LateUpdate()
        {
            RefreshVisual();
            if (viewer == null || label == null) return;
            label.transform.parent.rotation = Quaternion.LookRotation(label.transform.parent.position - viewer.position);
            if (!completed) haloTransform.rotation = Quaternion.LookRotation(transform.position - viewer.position);
        }
    }
}
