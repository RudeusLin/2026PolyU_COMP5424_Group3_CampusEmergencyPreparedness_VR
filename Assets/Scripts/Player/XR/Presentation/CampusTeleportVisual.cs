using UnityEngine;

namespace CampusEmergency.Player.XR
{
    public sealed class CampusTeleportVisual : MonoBehaviour
    {
        [SerializeField] private LineRenderer arc;
        [SerializeField] private LineRenderer ring;
        public void Configure(LineRenderer line, LineRenderer target) { arc = line; ring = target; }
        public void Show(Vector3[] points, int count, bool valid, Vector3 destination)
        {
            Color color = valid ? new Color(.15f, 1f, .6f) : new Color(1f, .22f, .16f);
            arc.enabled = true;
            arc.positionCount = count;
            for (int n = 0; n < count; n++) arc.SetPosition(n, points[n]);
            arc.startColor = arc.endColor = color;
            ring.enabled = valid;
            if (!valid) return;
            ring.positionCount = 33;
            ring.startColor = ring.endColor = color;
            for (int n = 0; n <= 32; n++)
            {
                float angle = n / 32f * Mathf.PI * 2f;
                ring.SetPosition(n, destination + new Vector3(Mathf.Cos(angle) * .22f, .018f, Mathf.Sin(angle) * .22f));
            }
        }
        public void Hide() { if (arc != null) arc.enabled = false; if (ring != null) ring.enabled = false; }
        private void OnDisable() => Hide();
    }
}
