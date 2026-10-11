using UnityEngine;

namespace CampusEmergency.Player.XR
{
    public sealed class CampusTeleportValidator : MonoBehaviour
    {
        [SerializeField] private LayerMask environmentMask = ~0;
        [SerializeField] private float bodyRadius = .22f;
        [SerializeField] private float maxSlope = 35f;
        [SerializeField] private float maxDistance = 12f;
        [SerializeField] private float maxHeightChange = 4f;
        public LayerMask EnvironmentMask => environmentMask;

        public bool IsValid(RaycastHit hit, Vector3 feet, float headHeight, out string reason)
        {
            if (hit.collider != null && hit.collider.GetComponent<CampusEmergency.Interaction.FireSafety.CampusFireTarget>() != null)
                return Reject("Extinguish the fire to clear this passage", out reason);
            if (hit.collider == null || hit.collider.GetComponent<CampusTeleportSurface>() == null)
                return Reject("Aim at a floor or stair landing", out reason);
            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlope)
                return Reject("Surface is too steep", out reason);
            if (Vector3.Distance(feet, hit.point) > maxDistance || Mathf.Abs(feet.y - hit.point.y) > maxHeightChange)
                return Reject("Destination is too far away", out reason);
            if (CampusEmergency.Interaction.FireSafety.CampusFireBarrier.BlocksTraversal(feet, hit.point, headHeight))
                return Reject("Extinguish the fire before crossing this passage", out reason);
            return HasClearance(hit.point, headHeight, out reason);
        }

        public bool HasClearance(Vector3 point, float headHeight, out string reason)
        {
            float height = Mathf.Clamp(headHeight + .12f, 1.25f, 2.2f);
            Vector3 bottom = point + Vector3.up * (bodyRadius + .035f);
            Vector3 top = point + Vector3.up * (height - bodyRadius + .035f);
            if (Physics.CheckCapsule(bottom, top, bodyRadius, environmentMask, QueryTriggerInteraction.Ignore))
                return Reject("Not enough standing space", out reason);
            // Require support around the feet: prevents choosing a railing or a slab edge.
            Vector3[] offsets = { Vector3.zero, Vector3.right * .16f, Vector3.left * .16f,
                Vector3.forward * .16f, Vector3.back * .16f };
            foreach (Vector3 offset in offsets)
            {
                if (!Physics.Raycast(point + offset + Vector3.up * .22f, Vector3.down, out var support, .45f,
                    environmentMask, QueryTriggerInteraction.Ignore)
                    || support.collider.GetComponent<CampusTeleportSurface>() == null
                    || Vector3.Angle(support.normal, Vector3.up) > maxSlope)
                    return Reject("Move away from the edge", out reason);
            }
            reason = string.Empty;
            return true;
        }

        private static bool Reject(string message, out string reason) { reason = message; return false; }
    }
}
