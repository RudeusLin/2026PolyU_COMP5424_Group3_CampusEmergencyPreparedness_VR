using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed partial class PrototypeExplorer
    {
        public bool TryHoldTowel(CampusTowel towel)
        {
            if (HeldTowel != null) return false;
            HeldTowel = towel;
            return true;
        }

        public void ReleaseTowel(CampusTowel towel)
        {
            if (HeldTowel == towel) HeldTowel = null;
        }

        private void UpdateFocus()
        {
            focused = null;
            // Interaction distance stays anchored to the student in either camera mode.
            Vector3 eye = transform.position + Vector3.up * 1.65f;
            if (!Physics.Raycast(eye, walkCamera.transform.forward, out RaycastHit hit, 2.6f)) return;
            foreach (MonoBehaviour component in hit.collider.GetComponentsInParent<MonoBehaviour>())
            {
                if (component is IPrototypeInteractable interactable)
                {
                    focused = interactable;
                    break;
                }
            }
        }

        public void ResetWashroomInteractions()
        {
            if (HeldTowel != null) HeldTowel.ResetToCounter();
            foreach (CampusFaucet faucet in FindObjectsOfType<CampusFaucet>(true)) faucet.SetRunning(false);
        }
    }
}
