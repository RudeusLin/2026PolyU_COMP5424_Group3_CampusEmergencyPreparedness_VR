using UnityEngine;
using CampusEmergency.Prototype;
using CampusEmergency.Player.XR;

namespace CampusEmergency.Interaction.FireSafety
{
    /// <summary>Owns the complete existing model, pin and nozzle, and restores its authored pose.</summary>
    public sealed class CampusExtinguisher : MonoBehaviour, IPrototypeInteractable
    {
        [SerializeField] private Transform pin;
        [SerializeField] private Transform nozzle;
        private Transform home;
        private Vector3 homePosition, homeScale;
        private Quaternion homeRotation;
        private Collider[] colliders;
        private bool[] colliderStates;
        public bool PinRemoved { get; private set; }
        public bool IsHeld { get; private set; }
        public Transform Pin => pin;
        public Transform Nozzle => nozzle;
        public string Prompt => "[A button] Pick up extinguisher";
        public void Configure(Transform safetyPin, Transform outlet) { pin = safetyPin; nozzle = outlet; }
        private void Awake()
        {
            home = transform.parent; homePosition = transform.localPosition;
            homeRotation = transform.localRotation; homeScale = transform.localScale;
            colliders = GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) colliderStates[i] = colliders[i].enabled;
        }
        public string Interact(ICampusInteractor actor = null)
        {
            var inventory = actor as CampusXRInventory;
            var controller = inventory != null ? inventory.GetComponent<CampusXRExtinguisherController>() : null;
            return controller != null && controller.TryPickup(this)
                ? "EXTINGUISHER: ACTION mode. B switches mode. Left grip near the yellow pin removes it."
                : "Return your held item first. Extinguisher pickup requires the XR player.";
        }
        public void Hold(Transform hand)
        {
            IsHeld = true;
            foreach (var c in colliders) c.enabled = false;
            transform.SetParent(hand, false);
            // Group origin is the original handle: the cylinder hangs below the hand.
            transform.localPosition = new Vector3(0f, -.04f, .08f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
        public bool TryPullPin(Vector3 leftHandPosition)
        {
            if (!IsHeld || PinRemoved || pin == null || Vector3.Distance(leftHandPosition, pin.position) > .18f) return false;
            PinRemoved = true;
            pin.gameObject.SetActive(false);
            return true;
        }
        public void ReturnHome()
        {
            IsHeld = false; PinRemoved = false;
            transform.SetParent(home, false);
            transform.localPosition = homePosition; transform.localRotation = homeRotation; transform.localScale = homeScale;
            if (pin != null) pin.gameObject.SetActive(true);
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
        }
    }
}
