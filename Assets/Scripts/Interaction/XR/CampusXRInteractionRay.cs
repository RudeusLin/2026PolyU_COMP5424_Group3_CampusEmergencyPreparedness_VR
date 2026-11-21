using UnityEngine;
using CampusEmergency.Prototype;
using CampusEmergency.Player.XR;
using CampusEmergency.Player;

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
            .Replace("Press E", "Press A").Replace("R returns it to the counter.", "X switches equipment; wetness is retained.");
        private CampusRefugeCheckpoint hoveredCheckpoint;
        private void Update()
        {
            bool focus = input.HasInteractionFocus;
            UpdateCheckpointHover(focus);
            ProcessInput(focus);
        }
        // Run even while the teleport ray is visible: hover must appear before pressing A.
        private void UpdateCheckpointHover(bool focus)
        {
            CampusRefugeCheckpoint next = null;
            if (focus && input.RightTracked && rayOrigin != null)
            {
                var hits = Physics.RaycastAll(rayOrigin.position, rayOrigin.forward, reach, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (CampusWheelchairRescue.IsPushing && hit.collider.transform.IsChildOf(CampusWheelchairRescue.Active.transform)) continue;
                    var checkpoint = hit.collider.GetComponentInParent<CampusRefugeCheckpoint>();
                    if (hit.collider.isTrigger && checkpoint == null
                        && hit.collider.GetComponent<CampusEmergency.Interaction.FireSafety.CampusFireTarget>() == null) continue;
                    if (checkpoint != null && !CampusEmergency.Interaction.FireSafety.CampusFireBarrier.TryIntersect(rayOrigin.position, hit.point, out _)) next = checkpoint;
                    break;
                }
            }
            if (hoveredCheckpoint != null && hoveredCheckpoint != next) hoveredCheckpoint.SetRayHovered(false);
            hoveredCheckpoint = next;
            if (next != null) next.SetRayHovered(true);
        }
        private void ProcessInput(bool hasFocus)
        {
            var extinguisher = GetComponent<CampusXRExtinguisherController>();
            if (!input.RightTracked || teleport.IsAiming || !hasFocus)
            { line.enabled = false; feedback.SetPrompt(string.Empty); return; }
            if(extinguisher!=null && extinguisher.TryPointAtPin(new Ray(rayOrigin.position,rayOrigin.forward),reach,out var pinPoint))
            {
                line.enabled=true;line.positionCount=2;line.SetPosition(0,rayOrigin.position);line.SetPosition(1,pinPoint);
                line.startColor=line.endColor=Color.cyan;
                feedback.SetPrompt(LanguageSelectionUI.T("A: REMOVE PIN","A：拔出保险销","A：拔出保險銷"));
                if(input.InteractPressed)feedback.Notify(extinguisher.PullPin());return;
            }
            IPrototypeInteractable target = null;
            Vector3 end = rayOrigin.position + rayOrigin.forward * reach;
            var hits = Physics.RaycastAll(rayOrigin.position, rayOrigin.forward, reach, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if(CampusWheelchairRescue.IsPushing && hit.collider.transform.IsChildOf(CampusWheelchairRescue.Active.transform))continue;
                if (hit.collider.isTrigger && hit.collider.GetComponent<CampusEmergency.Interaction.FireSafety.CampusFireTarget>() == null && hit.collider.GetComponentInParent<CampusRefugeCheckpoint>()==null) continue;
                end = hit.point;
                foreach (var component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                    if (component is IPrototypeInteractable found) { target = found; break; }
                break;
            }
            // Fire is collider-free: preserve its interaction prompt via the analytic ray region.
            if (CampusEmergency.Interaction.FireSafety.CampusFireBarrier.TryIntersect(rayOrigin.position, end, out var firePoint, out var fire))
            { target = fire; end = firePoint; }
            line.enabled = true;
            line.positionCount = 2;
            line.SetPosition(0, rayOrigin.position);
            line.SetPosition(1, end);
            line.startColor = line.endColor = target != null ? Color.cyan : new Color(.55f, .65f, .7f);
            feedback.SetPrompt(target != null ? LanguageSelectionUI.Localize(XRMessage(target.Prompt)) : string.Empty);
            if(target==null && CampusWheelchairRescue.IsPushing && input.InteractPressed){feedback.Notify(CampusWheelchairRescue.Active.Interact(inventory));return;}
            if (target != null && input.InteractPressed) feedback.Notify(LanguageSelectionUI.Localize(XRMessage(target.Interact(inventory))));
        }
        private void OnDisable() { if (hoveredCheckpoint != null) hoveredCheckpoint.SetRayHovered(false); hoveredCheckpoint = null; if (line != null) line.enabled = false; if (feedback != null) feedback.SetPrompt(string.Empty); }
    }
}
