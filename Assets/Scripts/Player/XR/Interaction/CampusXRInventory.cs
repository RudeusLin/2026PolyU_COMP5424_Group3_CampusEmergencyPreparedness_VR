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
        private InputAction returnTowel, raiseTowel;
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
        public string Controls => LanguageSelectionUI.T(
            "LEFT TRIGGER: TOGGLE WET TOWEL COVER\nRIGHT TRIGGER: TELEPORT / A: INTERACT / RIGHT GRIP: RETURN",
            "左扳机：切换湿毛巾捂住口鼻\n右扳机：传送 / A键：交互 / 右握把：归还",
            "左扳機：切換濕毛巾捂住口鼻\n右扳機：傳送 / A鍵：互動 / 右握把：歸還");

        public void Configure(CampusXRInput controls, CampusTeleportController movement,
            Transform hand, CampusXRFeedback messages)
        { input = controls; teleport = movement; holdPoint = hand; feedback = messages; }

        private void OnEnable()
        {
            if (returnTowel == null) returnTowel = new InputAction("Return towel", InputActionType.Button,
                "<XRController>{RightHand}/gripPressed");
            if (raiseTowel == null) raiseTowel = new InputAction("Raise towel", InputActionType.Button,
                "<XRController>{LeftHand}/triggerPressed");
            returnTowel.Enable(); raiseTowel.Enable();
            Application.onBeforeRender += BeforeRender;
        }
        private void Update() => ProcessInput(input != null && input.HasInteractionFocus);
        private void ProcessInput(bool focus)
        {
            if (HeldTowel == null) return;
            visible = input != null && input.LeftTracked && focus;
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
            var extinguisher = GetComponent<CampusXRExtinguisherController>();
            bool extinguisherHandledGrip = extinguisher != null &&
                (extinguisher.Held != null || extinguisher.LastReturnFrame == Time.frameCount);
            if (input.RightTracked && !input.TeleportHeld && returnTowel.WasPressedThisFrame() && !extinguisherHandledGrip)
            { ReturnHeldTowel(); feedback.Notify(LanguageSelectionUI.T("Towel returned to the counter.", "毛巾已放回台面。", "毛巾已放回檯面。")); }
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
            if (towel == null || HeldTowel != null || holdPoint == null || (extinguisher != null && extinguisher.Held != null)) return false;
            HeldTowel = towel;
            restingPosition = holdPoint.localPosition; restingRotation = holdPoint.localRotation;
            var origin = GetComponent<XROrigin>(); head = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            surfaces = towel.GetComponentsInChildren<Renderer>(); originalVisibility = new bool[surfaces.Length];
            for(int i=0;i<surfaces.Length;i++) originalVisibility[i]=surfaces[i].enabled;
            raisedBlend = 0f; IsTowelRaised = false; visible = true;
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
            returnTowel?.Disable(); raiseTowel?.Disable(); ReturnHeldTowel();
        }
        private void OnDestroy() { returnTowel?.Dispose(); raiseTowel?.Dispose(); }
    }
}
