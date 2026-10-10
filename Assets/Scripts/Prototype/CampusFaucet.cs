using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed class CampusFaucet : MonoBehaviour, IPrototypeInteractable
    {
        [SerializeField] private GameObject waterFlow;
        public bool IsRunning => waterFlow != null && waterFlow.activeSelf;
        public string Prompt => "[E] Use tap / wet held towel";

        public void Configure(GameObject flow)
        {
            waterFlow = flow;
            SetRunning(false);
        }

        public void SetRunning(bool running)
        {
            if (waterFlow != null) waterFlow.SetActive(running);
        }

        public string Interact(PrototypeExplorer explorer)
        {
            if (explorer != null && explorer.HeldTowel != null && !explorer.HeldTowel.IsWet)
            {
                SetRunning(true);
                explorer.HeldTowel.Wet();
                return "TOWEL WET\nPress E on the tap again to turn off the water.";
            }
            SetRunning(!IsRunning);
            return IsRunning ? "TAP ON\nPick up the towel, then use the tap to wet it." : "TAP OFF";
        }

        private void OnDisable() => SetRunning(false);
    }
}
