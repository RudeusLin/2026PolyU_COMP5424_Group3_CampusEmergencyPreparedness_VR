using UnityEngine;
using UnityEngine.UI;
using CampusEmergency.Prototype;
using CampusEmergency.Player;

namespace CampusEmergency.Player.XR
{
    /// <summary>Flat parking target with a downward arrow; either target accepts the same A interaction.</summary>
    public sealed class CampusRefugeCheckpoint : MonoBehaviour, IPrototypeInteractable
    {
        private CampusWheelchairRescue rescue;
        private Text label;
        private Transform visual, haloTransform, arrow, viewer;
        private LineRenderer halo;
        private Material dotMaterial, haloMaterial;
        private Mesh arrowMesh;
        private bool hovered, completed;
        private int labelState = -1;
        private float completedAt;
        public static Vector3 MarkerPosition => CampusWheelchairRescue.ParkingPosition + Vector3.up * .025f;
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
            if (arrowMesh != null) Destroy(arrowMesh);
        }
        public void SetRayHovered(bool value) { hovered = value; RefreshVisual(); }
        public string Prompt => LanguageSelectionUI.T("A: CONFIRM WHEELCHAIR PARKING", "A：确认安置轮椅人物", "A：確認安置輪椅人物");
        public string Interact(ICampusInteractor actor = null) => rescue.ConfirmParking();
        public void ShowCompleted()
        {
            completed = true; hovered = false; completedAt = Time.unscaledTime;
            foreach(var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            arrow.gameObject.SetActive(false);
            label.transform.parent.GetComponent<CampusHintAttention>()?.Configure(false,viewer,true);
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
            visual.localScale = new Vector3(state == 2 ? .98f : .9f, .004f, state == 2 ? .98f : .9f);
            halo.startColor = halo.endColor = color;
            halo.enabled = !completed || Time.unscaledTime - completedAt < .85f;
            haloTransform.localScale = Vector3.one * (state == 2 ? 1f + .06f * Mathf.Sin(Time.unscaledTime * 6f) : 1f);
            if (completed)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - completedAt) / .85f);
                haloTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.6f, t);
                color.a = 1f - t; halo.startColor = halo.endColor = color;
            }
            if (labelState == state) return;
            labelState = state; label.font = LanguageSelectionUI.CurrentFont;
            string text = state == 3 ? LanguageSelectionUI.T("SAFE\nWheelchair parking confirmed", "已安置\n轮椅人物已安全停放", "已安置\n輪椅人物已安全停放")
                : state == 2 ? LanguageSelectionUI.T("READY TO CONFIRM\nPress A to park facing the door", "可以确认\n按A自动安置，面朝房门", "可以確認\n按A自動安置，面朝房門")
                : state == 1 ? LanguageSelectionUI.T("PARK HERE\nAim at the floor or arrow; press A", "安置确认点\n对准地面标记或箭头，按A确认", "安置確認點\n對準地面標記或箭頭，按A確認")
                : LanguageSelectionUI.T("REFUGE PARKING\nEscort the wheelchair into this room", "安全屋安置点\n请先护送轮椅进入房间", "安全屋安置點\n請先護送輪椅進入房間");
            CampusUIKeycaps.Apply(label, text, 32);
            string hex = ColorUtility.ToHtmlStringRGB(color);
            label.text = "<b><color=#" + hex + ">" + label.text.Replace("\n", "</color></b>\n");
        }
        public static CampusRefugeCheckpoint Create(CampusWheelchairRescue owner)
        {
            var go = new GameObject("Refuge Confirm Parking"); go.transform.position = MarkerPosition;
            var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(1.05f,.09f,1.05f); collider.center=Vector3.up*.04f;collider.isTrigger = true;
            var c = go.AddComponent<CampusRefugeCheckpoint>(); c.rescue = owner;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dot.name = "Flat floor parking marker"; dot.transform.SetParent(go.transform, false);
            dot.GetComponent<Collider>().enabled = false; Destroy(dot.GetComponent<Collider>()); c.visual = dot.transform;
            c.dotMaterial = new Material(Shader.Find("Unlit/Color")); dot.GetComponent<Renderer>().sharedMaterial = c.dotMaterial;
            var arrowObject=new GameObject("Downward parking arrow",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));
            arrowObject.transform.SetParent(go.transform,false);arrowObject.transform.localPosition=Vector3.up*1.05f;c.arrow=arrowObject.transform;
            c.arrowMesh=new Mesh{name="Parking downward arrow"};
            c.arrowMesh.vertices=new[]{new Vector3(-.07f,.25f,0),new Vector3(.07f,.25f,0),new Vector3(.07f,.02f,0),new Vector3(.22f,.02f,0),new Vector3(0,-.25f,0),new Vector3(-.22f,.02f,0),new Vector3(-.07f,.02f,0)};
            c.arrowMesh.triangles=new[]{0,1,2,0,2,6,5,3,4,2,1,0,6,2,0,4,3,5};c.arrowMesh.RecalculateNormals();
            arrowObject.GetComponent<MeshFilter>().sharedMesh=c.arrowMesh;arrowObject.GetComponent<MeshRenderer>().sharedMaterial=c.dotMaterial;
            var arrowCollider=arrowObject.GetComponent<BoxCollider>();arrowCollider.size=new Vector3(.48f,.55f,.08f);arrowCollider.isTrigger=true;
            var ring = new GameObject("Confirmation floor halo"); ring.transform.SetParent(go.transform, false);ring.transform.localPosition=Vector3.up*.012f;ring.transform.localRotation=Quaternion.Euler(90,0,0);
            c.haloTransform = ring.transform; c.halo = ring.AddComponent<LineRenderer>();
            c.haloMaterial = new Material(Shader.Find("Sprites/Default")); c.halo.sharedMaterial = c.haloMaterial;
            c.halo.useWorldSpace = false; c.halo.loop = true; c.halo.widthMultiplier = .025f; c.halo.positionCount = 48;
            for (int i = 0; i < 48; i++) { float angle = i * Mathf.PI * 2f / 48; c.halo.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * .53f); }
            var panel = new GameObject("Confirm label", typeof(RectTransform), typeof(Canvas), typeof(Image));
            panel.transform.SetParent(go.transform, false); panel.transform.position=CampusWheelchairRescue.ParkingPosition+new Vector3(1.005f,1.55f,.15f);panel.transform.rotation=Quaternion.Euler(0,90,0);
            panel.transform.localScale = Vector3.one * .00128f;
            panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)panel.transform).sizeDelta = new Vector2(740, 180);
            panel.GetComponent<Image>().color = new Color(.025f, .065f, .09f, .96f);
            var glyph = new GameObject("Confirmation instructions", typeof(RectTransform), typeof(Text));
            glyph.transform.SetParent(panel.transform, false); ((RectTransform)glyph.transform).sizeDelta = new Vector2(700, 155);
            c.label = glyph.GetComponent<Text>(); c.label.fontSize = 28; c.label.color = Color.white;
            c.label.alignment = TextAnchor.MiddleCenter; c.label.lineSpacing = 1.25f;
            var origin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null && origin.Camera != null) c.viewer = origin.Camera.transform;
            c.RefreshVisual();panel.AddComponent<CampusHintAttention>().Configure(true,c.viewer);return c;
        }
        private void LateUpdate()
        {
            RefreshVisual();
            if (viewer == null || label == null) return;
            // The confirmation label stays on the refuge side wall; the arrow remains above its floor target.
            if(!completed)
            {
                Vector3 facing=Vector3.ProjectOnPlane(arrow.position-viewer.position,Vector3.up);
                if(facing.sqrMagnitude>.01f)arrow.rotation=Quaternion.LookRotation(facing);
                arrow.localPosition=Vector3.up*(1.05f+.04f*Mathf.Sin(Time.unscaledTime*3f));
            }
        }
    }
}
