using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CampusEmergency.Player;
using CampusEmergency.Prototype;

namespace CampusEmergency.Player.XR
{
    /// <summary>Compact head-relative state only. Instructions live at their world-space objectives.</summary>
    public sealed class CampusXRFeedback : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private float panelHeight = .24f;
        private CampusXRInput input;
        private Canvas canvas;
        private bool hiddenForLanguage, userVisible;
        private InputAction controllerToggle, keyboardToggle;
        public bool IsVisible => canvas != null && canvas.enabled;
        public string StatusText { get; private set; }
        private void OnEnable()
        {
            input=GetComponentInParent<CampusXRInput>();ApplyPresentation();SetVisible(showOnStart);
            if(!Application.isPlaying)return;
            if(input!=null && input.GetComponent<CampusXRWorldHints>()==null)input.gameObject.AddComponent<CampusXRWorldHints>();
            controllerToggle=new InputAction("Toggle HUD X",InputActionType.Button,"<XRController>{LeftHand}/primaryButton");
            keyboardToggle=new InputAction("Toggle HUD H",InputActionType.Button,"<Keyboard>/h");
            controllerToggle.Enable();keyboardToggle.Enable();
        }
        private void OnDisable(){controllerToggle?.Dispose();keyboardToggle?.Dispose();controllerToggle=keyboardToggle=null;}
        public void Configure(Text text){label=text;ApplyPresentation();}
        public void SetVisible(bool visible){userVisible=visible;if(canvas==null)canvas=GetComponent<Canvas>();if(canvas!=null)canvas.enabled=visible;}
        [ContextMenu("Toggle HUD visibility")] public void ToggleVisibility()=>SetVisible(!userVisible);
        // Retain the interaction API; action text must never reappear in the head-locked panel.
        public void SetAim(string value) { }
        public void SetPrompt(string value) { }
        public void Notify(string message,float seconds=5f) { }
        private static string T(string en,string sc,string tc)=>LanguageSelectionUI.T(en,sc,tc);
        public void ApplyPresentation()
        {
            canvas=GetComponent<Canvas>();
            if(transform is RectTransform rect){rect.localPosition=new Vector3(0,panelHeight,1.1f);rect.localScale=Vector3.one*.0008f;rect.sizeDelta=new Vector2(650,125);}
            if(label==null)return;
            label.font=LanguageSelectionUI.CurrentFont;label.fontSize=21;label.lineSpacing=1.15f;
            label.alignment=TextAnchor.MiddleLeft;label.color=new Color(.9f,.97f,.98f);label.supportRichText=false;
            label.resizeTextForBestFit=true;label.resizeTextMinSize=17;label.resizeTextMaxSize=21;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.raycastTarget=false;
            label.rectTransform.offsetMin=new Vector2(20,12);label.rectTransform.offsetMax=new Vector2(-20,-12);
            var panel=label.transform.parent.GetComponent<Image>();if(panel!=null){panel.color=new Color(.025f,.055f,.09f,.64f);panel.raycastTarget=false;}
        }
        private string BuildStatus()
        {
            if(input==null || !input.HeadTracked)return T("Headset tracking unavailable","头显追踪中断","頭顯追蹤中斷");
            if(!input.HasSessionFocus)return T("Session paused","会话已暂停","工作階段已暫停");
            if(!input.LeftTracked || !input.RightTracked)return T("Controller tracking unavailable","控制器追踪中断","控制器追蹤中斷");
            var flow=input.GetComponent<CampusRouteAFlow>();int node=flow!=null?flow.Node:0;
            string state=node==1?T("Alarm inactive","火警未启动","火警未啟動"):
                node==2?T("Smoke protection preparation","防烟准备中","防煙準備中"):
                node==4?T("Fire practice in progress","灭火练习中","滅火練習中"):T("Evacuation in progress","疏散中","疏散中");
            var inventory=input.GetComponent<CampusXRInventory>();
            if(inventory!=null && inventory.HeldTowel!=null)state+="\n"+(inventory.HeldTowel.IsWet?T("Wet towel","湿毛巾","濕毛巾"):T("Dry towel","干毛巾","乾毛巾"))
                +(inventory.IsTowelRaised?T(" · raised"," · 已举起"," · 已舉起"):T(" · lowered"," · 已放下"," · 已放下"));
            var extinguisher=input.GetComponent<CampusXRExtinguisherController>();
            if(extinguisher!=null && extinguisher.Held!=null)state+="\n"+(extinguisher.SprayMode?T("Spray mode","喷射模式","噴射模式"):T("Action mode","移动模式","移動模式"))
                +(extinguisher.Held.PinRemoved?T(" · pin removed"," · 保险销已拔"," · 保險銷已拔"):T(" · pin secured"," · 保险销未拔"," · 保險銷未拔"));
            return state;
        }
        private void LateUpdate()
        {
            if(LanguageSelectionUI.IsBlocking){if(canvas!=null)canvas.enabled=false;hiddenForLanguage=true;return;}
            if(hiddenForLanguage){if(canvas!=null)canvas.enabled=userVisible;hiddenForLanguage=false;}
            if(input!=null && input.HasInteractionFocus && ((input.LeftTracked && controllerToggle?.WasPressedThisFrame()==true)||keyboardToggle?.WasPressedThisFrame()==true))ToggleVisibility();
            StatusText=BuildStatus();if(label!=null){label.font=LanguageSelectionUI.CurrentFont;label.text=StatusText;}
        }
    }
}
