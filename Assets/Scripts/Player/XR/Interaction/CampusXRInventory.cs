using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
using CampusEmergency.Prototype;

namespace CampusEmergency.Player.XR
{
    /// <summary>Left-hand towel inventory and hold-to-raise presentation. Desktop towel behavior is unchanged.</summary>
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
        private bool raiseArmed, visible;
        public CampusTowel HeldTowel { get; private set; }
        public Transform TowelHoldPoint => holdPoint;
        public bool IsTowelRaised { get; private set; }
        public string Controls => "LEFT TRIGGER: HOLD TOWEL UP / RELEASE: LOWER\nRIGHT TRIGGER: TELEPORT / A: INTERACT / RIGHT GRIP: RETURN";

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
            if (!visible) { raiseArmed = false; IsTowelRaised = false; raisedBlend = 0f; ResetAnchor(); return; }
            bool pressed = raiseTowel.IsPressed();
            if (!pressed) raiseArmed = true;
            IsTowelRaised = raiseArmed && pressed;
            if (input.RightTracked && !input.TeleportHeld && returnTowel.WasPressedThisFrame())
            { ReturnHeldTowel(); feedback.Notify("Towel returned to the counter."); }
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
            raisedBlend = 0f; IsTowelRaised = false; raiseArmed = false; visible = true;
            return true;
        }
        public void ReleaseTowel(CampusTowel towel)
        {
            if (HeldTowel != towel) return;
            SetVisible(true); ResetAnchor(); HeldTowel = null; surfaces = null; originalVisibility = null;
            raisedBlend = 0f; IsTowelRaised = false; raiseArmed = false;
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
