using UnityEngine;
using CampusEmergency.Prototype;
using CampusEmergency.Player.XR;

namespace CampusEmergency.Interaction
{
    public sealed class CampusXRInteractionRay : MonoBehaviour
    {
        [SerializeField] private CampusXRInput input;
        [SerializeField] private CampusTeleportController teleport;
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private LineRenderer line;
        [SerializeField] private CampusXRFeedback feedback;
        [SerializeField] private float reach = 2.6f;

        public void Configure(
            CampusXRInput i, 
            CampusTeleportController t, 
            Transform ray, 
            LineRenderer display, 
            CampusXRFeedback message)
        { 
            input = i; 
            teleport = t; 
            rayOrigin = ray; 
            line = display; 
            feedback = message; 
            }
            
        private CampusXRInventory inventory;
        private void Awake() => inventory = GetComponent<CampusXRInventory>();
        private static string XRMessage(string message) => message.Replace("[E]", "[A button]")
            .Replace("Press E", "Press A").Replace("R returns", "Right grip returns");
        private void Update() => ProcessInput(input.HasInteractionFocus);
        private void ProcessInput(bool hasFocus)
        {
            if (!input.RightTracked || teleport.IsAiming || !hasFocus)
            { line.enabled = false; feedback.SetPrompt(string.Empty); return; }
            IPrototypeInteractable target = null;
            Vector3 end = rayOrigin.position + rayOrigin.forward * reach;
            if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                foreach (var component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                    if (component is IPrototypeInteractable found) { target = found; break; }
            }
            line.enabled = true;
            line.positionCount = 2;
            line.SetPosition(0, rayOrigin.position);
            line.SetPosition(1, end);
            line.startColor = line.endColor = target != null ? Color.cyan : new Color(.55f, .65f, .7f);
            feedback.SetPrompt(target != null ? XRMessage(target.Prompt) : string.Empty);
            if (target != null && input.InteractPressed) feedback.Notify(XRMessage(target.Interact(inventory)));
        }
        private void OnDisable() { if (line != null) line.enabled = false; if (feedback != null) feedback.SetPrompt(string.Empty); }
    }
}
