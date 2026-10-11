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
            var extinguisher = GetComponent<CampusXRExtinguisherController>();
            if (!input.RightTracked || teleport.IsAiming || !hasFocus || (extinguisher != null && extinguisher.Held != null && !input.InteractionHeld))
            { line.enabled = false; feedback.SetPrompt(string.Empty); return; }
            IPrototypeInteractable target = null;
            Vector3 end = rayOrigin.position + rayOrigin.forward * reach;
            var hits = Physics.RaycastAll(rayOrigin.position, rayOrigin.forward, reach, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.isTrigger && hit.collider.GetComponent<CampusEmergency.Interaction.FireSafety.CampusFireTarget>() == null) continue;
                end = hit.point;
                foreach (var component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                    if (component is IPrototypeInteractable found) { target = found; break; }
                break;
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
