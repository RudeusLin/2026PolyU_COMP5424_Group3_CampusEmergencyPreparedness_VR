using System.Collections.Generic;
using UnityEngine;

namespace CampusEmergency.Interaction.FireSafety
{
    /// <summary>Analytic teleport-ray exclusion region. No collider and no body movement constraint.</summary>
    public sealed class CampusFireBarrier : MonoBehaviour
    {
        [SerializeField] private Vector3 localCenter;
        [SerializeField] private Vector3 localSize = new Vector3(4.2f, 3.4f, 4.2f);
        private static readonly HashSet<CampusFireBarrier> active = new HashSet<CampusFireBarrier>();
        public CampusFireTarget Target => GetComponent<CampusFireTarget>();
        public Bounds VolumeBounds
        {
            get
            {
                var bounds = new Bounds(transform.TransformPoint(localCenter), Vector3.zero);
                for (int i = 0; i < 8; i++)
                    bounds.Encapsulate(transform.TransformPoint(localCenter + Vector3.Scale(localSize * .5f,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                return bounds;
            }
        }
        public void Configure(Vector3 center, Vector3 size) { localCenter = center; localSize = size; }
        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        // Test only the visible ray segment, ending at the nearest wall/floor. Fire behind it is irrelevant.
        // Contains handles a controller already inside the region, unlike a physics raycast.
        public static bool TryIntersect(Vector3 start, Vector3 end, out Vector3 contact)
            => TryIntersect(start, end, out contact, out _);
        public static bool TryIntersect(Vector3 start, Vector3 end, out Vector3 contact, out CampusFireTarget target)
        {
            contact = end; target = null;
            Vector3 delta = end - start;
            float length = delta.magnitude, nearest = float.PositiveInfinity;
            var ray = new Ray(start, length > .000001f ? delta / length : Vector3.forward);
            IEnumerable<CampusFireBarrier> regions = Application.isPlaying
                ? (IEnumerable<CampusFireBarrier>)active : FindObjectsOfType<CampusFireBarrier>();
            foreach (var region in regions)
            {
                if (region == null || !region.isActiveAndEnabled) continue;
                var fire = region.Target;
                if (fire == null || !fire.isActiveAndEnabled || fire.Extinguished) continue;
                var bounds = region.VolumeBounds;
                float distance = 0f;
                if (!bounds.Contains(start) && (!bounds.IntersectRay(ray, out distance) || distance > length)) continue;
                if (distance >= nearest) continue;
                nearest = distance; target = fire; contact = start + ray.direction * distance;
            }
            return target != null;
        }
        private void OnDrawGizmosSelected()
        { Gizmos.color = new Color(1f, .3f, .1f, .7f); var b = VolumeBounds; Gizmos.DrawWireCube(b.center, b.size); }
    }
}
