using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
using CampusEmergency.Prototype;
using CampusEmergency.Player;

namespace CampusEmergency.Player.XR
{
    /// <summary>Left-hand towel inventory and toggle-to-cover presentation. Desktop towel behavior is unchanged.</summary>
    public sealed class CampusXRInventory : MonoBehaviour, ICampusInteractor
    {
        [SerializeField] private CampusXRInput input;
        [SerializeField] private CampusTeleportController teleport;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private CampusXRFeedback feedback;
        [SerializeField] private Vector3 raisedOffset = new Vector3(-.06f, -.24f, .32f);
        [SerializeField] private float raiseDuration = .2f;
        private InputAction switchItem, raiseTowel;
        public enum EquipmentSlot { Empty, Towel, Extinguisher }
        public EquipmentSlot Equipped { get; private set; }
        public bool IsEmpty => Equipped == EquipmentSlot.Empty;
        public CampusTowel OwnedTowel => HeldTowel;
        public bool TowelEquipped => Equipped == EquipmentSlot.Towel;
        private Transform head;
        private Vector3 restingPosition;
        private Quaternion restingRotation;
        private Renderer[] surfaces;
        private bool[] originalVisibility;
        private float raisedBlend;
        private bool visible;
        public CampusTowel HeldTowel { get; private set; }
        public Transform TowelHoldPoint => holdPoint;
        public bool IsTowelRaised { get; private set; }
        public bool IsProtectedFromSmoke => TowelEquipped && HeldTowel != null && HeldTowel.IsWet && IsTowelRaised;
        public string Controls => LanguageSelectionUI.T("X: SWITCH ITEM / LEFT TRIGGER: USE / RIGHT TRIGGER: TELEPORT / A: INTERACT", "X：切换物品 / 左扳机：使用 / 右扳机：传送 / A：交互", "X：切換物品 / 左扳機：使用 / 右扳機：傳送 / A：互動");
        public void CycleEquipment()
        {
            if(CampusWheelchairRescue.IsPushing)return;
            var tool=GetComponent<CampusXRExtinguisherController>();
            int next=(int)Equipped;
            do { next=(next+1)%3; } while((next==1 && HeldTowel==null)||(next==2 && (tool==null||tool.Owned==null)));
            Equip((EquipmentSlot)next);
        }
        public void Equip(EquipmentSlot slot)
        {
            if(slot==EquipmentSlot.Towel && HeldTowel==null)return;
            var tool=GetComponent<CampusXRExtinguisherController>();
            if(slot==EquipmentSlot.Extinguisher && (tool==null||tool.Owned==null))return;
            Equipped=slot;IsTowelRaised=false;raisedBlend=0;ResetAnchor();
            visible=TowelEquipped;SetVisible(visible);
            if(tool!=null)tool.SetEquipped(slot==EquipmentSlot.Extinguisher);
            feedback?.Notify(LanguageSelectionUI.T("Equipped: ","当前物品：","當前物品：")+(slot==EquipmentSlot.Empty?LanguageSelectionUI.T("Empty hands","空手","空手"):slot==EquipmentSlot.Towel?LanguageSelectionUI.T("Towel","毛巾","毛巾"):LanguageSelectionUI.T("Extinguisher","灭火器","滅火器")));
        }

        public void Configure(CampusXRInput controls, CampusTeleportController movement,
            Transform hand, CampusXRFeedback messages)
        { input = controls; teleport = movement; holdPoint = hand; feedback = messages; }

        private void OnEnable()
        {
            if(switchItem==null)switchItem=new InputAction("Switch equipment",InputActionType.Button,"<XRController>{LeftHand}/primaryButton");
            if (raiseTowel == null) raiseTowel = new InputAction("Raise towel", InputActionType.Button,
                "<XRController>{LeftHand}/triggerPressed");
            switchItem.Enable(); raiseTowel.Enable();
            Application.onBeforeRender += BeforeRender;
        }
        private void Update() => ProcessInput(input != null && input.HasInteractionFocus);
        private void ProcessInput(bool focus)
        {
            // Hide the towel during the menu without losing its raised/lowered state.
            if (LanguageSelectionUI.IsPauseMenuOpen) { visible = false; SetVisible(false); return; }
            if(focus && input.LeftTracked && switchItem.WasPressedThisFrame())CycleEquipment();
            if (HeldTowel == null) return;
            visible = input != null && input.LeftTracked && focus && TowelEquipped;
            SetVisible(visible);
            if (!visible) { IsTowelRaised = false; raisedBlend = 0f; ResetAnchor(); return; }
            if (raiseTowel.WasPressedThisFrame())
            {
                if (!HeldTowel.IsWet)
                {
                    IsTowelRaised = false;
                    feedback.Notify(LanguageSelectionUI.T(
                        "Wet the towel before covering your mouth and nose.",
                        "请先打湿毛巾，再用它捂住口鼻。",
                        "請先打濕毛巾，再用它捂住口鼻。"));
                }
                else
                {
                    IsTowelRaised = !IsTowelRaised;
                    feedback.Notify(IsTowelRaised
                        ? LanguageSelectionUI.T("Wet towel covering mouth and nose.", "已用湿毛巾捂住口鼻。", "已用濕毛巾捂住口鼻。")
                        : LanguageSelectionUI.T("Wet towel lowered.", "湿毛巾已放下。", "濕毛巾已放下。"));
                }
            }

        }
        private void LateUpdate()
        {
            if (HeldTowel == null || !visible) return;
            raisedBlend = Mathf.MoveTowards(raisedBlend, IsTowelRaised ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(.01f, raiseDuration));
            UpdatePose();
        }
        private void BeforeRender() { if (HeldTowel != null && visible) UpdatePose(); }
        private void UpdatePose()
        {
            if (holdPoint == null || head == null) return;
            Transform hand = holdPoint.parent;
            holdPoint.SetPositionAndRotation(
                Vector3.Lerp(hand.TransformPoint(restingPosition), head.TransformPoint(raisedOffset), raisedBlend),
                Quaternion.Slerp(hand.rotation * restingRotation, head.rotation * Quaternion.Euler(90f,0f,0f), raisedBlend));
            if (raisedBlend <= 0f) return;
            // Clamp the entire rendered towel below the central view, including during the transition.
            float top = float.NegativeInfinity;
            foreach (var surface in surfaces)
            {
                var b = surface.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1));
                    top = Mathf.Max(top, head.InverseTransformPoint(corner).y);
                }
            }
            if (top > -.12f) holdPoint.position -= head.up * (top + .12f);
        }
        private void ResetAnchor()
        { if (holdPoint != null) holdPoint.SetLocalPositionAndRotation(restingPosition, restingRotation); }
        private void SetVisible(bool value)
        { if (surfaces != null) for (int i=0;i<surfaces.Length;i++) if(surfaces[i]!=null) surfaces[i].enabled = value && originalVisibility[i]; }

        public bool TryHoldTowel(CampusTowel towel)
        {
            var extinguisher = GetComponent<CampusXRExtinguisherController>();
            if (towel == null || HeldTowel != null || holdPoint == null || !IsEmpty) return false;
            HeldTowel = towel;
            restingPosition = holdPoint.localPosition; restingRotation = holdPoint.localRotation;
            var origin = GetComponent<XROrigin>(); head = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            surfaces = towel.GetComponentsInChildren<Renderer>(); originalVisibility = new bool[surfaces.Length];
            for(int i=0;i<surfaces.Length;i++) originalVisibility[i]=surfaces[i].enabled;
            raisedBlend = 0f; IsTowelRaised = false; visible = true;
            Equip(EquipmentSlot.Towel);
            return true;
        }
        public void ReleaseTowel(CampusTowel towel)
        {
            if (HeldTowel != towel) return;
            SetVisible(true); ResetAnchor(); HeldTowel = null; surfaces = null; originalVisibility = null;
            raisedBlend = 0f; IsTowelRaised = false;
        }
        public void ReturnHeldTowel() { if (HeldTowel != null) HeldTowel.ResetToCounter(); }
        private void OnDisable()
        {
            Application.onBeforeRender -= BeforeRender;
            switchItem?.Disable(); raiseTowel?.Disable(); SetVisible(false);
        }
        private void OnDestroy() { switchItem?.Dispose(); raiseTowel?.Dispose(); }
    }
}
