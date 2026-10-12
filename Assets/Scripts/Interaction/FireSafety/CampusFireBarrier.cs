using System.Collections.Generic;
using UnityEngine;

namespace CampusEmergency.Interaction.FireSafety
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CampusFireBarrier : MonoBehaviour
    {
        private static readonly HashSet<CampusFireBarrier> active = new HashSet<CampusFireBarrier>();
        private BoxCollider volume;
        public BoxCollider Volume => volume != null ? volume : (volume = GetComponent<BoxCollider>());
        public CampusFireTarget Target => GetComponent<CampusFireTarget>();
        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        // Swept virtual-body bounds close the loophole of aiming past an edge or above a fire.
        public static bool BlocksTraversal(Vector3 fromFeet, Vector3 toFeet, float bodyHeight, bool teleport = false)
        {
            IEnumerable<CampusFireBarrier> barriers = Application.isPlaying ? (IEnumerable<CampusFireBarrier>)active : FindObjectsOfType<CampusFireBarrier>();
            foreach (var barrier in barriers)
            {
                if (barrier == null || !barrier.Volume.enabled) continue;
                Bounds bounds = barrier.Volume.bounds;
                // A supported endpoint above the clipped fire belongs to the upper surface.
                // A mere step-height difference inside the fire volume is not an exemption.
                if (teleport && (fromFeet.y >= bounds.max.y + .02f || toFeet.y >= bounds.max.y + .02f)) continue;
                bounds.SetMinMax(bounds.min - new Vector3(.22f, Mathf.Clamp(bodyHeight,1.25f,2.2f),.22f),
                    bounds.max + new Vector3(.22f,0f,.22f));
                if (Crosses(bounds,fromFeet,toFeet)) return true;
            }
            return false;
        }
        private static bool Crosses(Bounds bounds, Vector3 start, Vector3 end)
        {
            if (bounds.Contains(start) || bounds.Contains(end)) return true;
            var delta=end-start;
            return delta.sqrMagnitude>.000001f && bounds.IntersectRay(new Ray(start,delta.normalized),out float distance)
                && distance<=delta.magnitude;
        }
    }
}
