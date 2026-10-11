using UnityEngine;
using UnityEngine.InputSystem;
using CampusEmergency.Prototype;

namespace CampusEmergency.Player.XR
{
    /// <summary>Holds a campus towel on the tracked right controller without changing locomotion.</summary>
    public sealed class CampusXRInventory : MonoBehaviour, ICampusInteractor
    {
        [SerializeField] private CampusXRInput input;
        [SerializeField] private CampusTeleportController teleport;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private CampusXRFeedback feedback;
        private InputAction returnTowel;
        public CampusTowel HeldTowel { get; private set; }
        public Transform TowelHoldPoint => holdPoint;

        public void Configure(CampusXRInput controls, CampusTeleportController movement,
            Transform hand, CampusXRFeedback messages)
        { input = controls; teleport = movement; holdPoint = hand; feedback = messages; }

        private void OnEnable()
        {
            if (returnTowel == null)
                returnTowel = new InputAction("Return towel", InputActionType.Button,
                    "<XRController>{RightHand}/gripPressed");
            returnTowel.Enable();
        }

        private void Update()
        {
            if (input == null || !input.RightTracked || !input.HasInteractionFocus
                || input.TeleportHeld) return;
            if (returnTowel.WasPressedThisFrame() && HeldTowel != null)
            {
                ReturnHeldTowel();
                feedback.Notify("Towel returned to the counter.");
            }
        }

        public bool TryHoldTowel(CampusTowel towel)
        {
            var extinguisher = GetComponent<CampusXRExtinguisherController>();
            if (towel == null || HeldTowel != null || holdPoint == null || (extinguisher != null && extinguisher.Held != null)) return false;
            HeldTowel = towel;
            return true;
        }

        public void ReleaseTowel(CampusTowel towel)
        { if (HeldTowel == towel) HeldTowel = null; }

        public void ReturnHeldTowel()
        { if (HeldTowel != null) HeldTowel.ResetToCounter(); }

        private void OnDisable()
        {
            returnTowel?.Disable();
            ReturnHeldTowel();
        }

        private void OnDestroy() { returnTowel?.Dispose(); }
    }
}
