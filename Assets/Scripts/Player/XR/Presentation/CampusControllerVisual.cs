using UnityEngine;
namespace CampusEmergency.Player.XR
{
    /// <summary>Visible controller proxy, only shown while its actual pose is tracked.</summary>
    public sealed class CampusControllerVisual : MonoBehaviour
    {
        [SerializeField] private CampusXRInput input;
        [SerializeField] private bool leftHand;
        [SerializeField] private Renderer[] surfaces;
        public void Configure(CampusXRInput source, bool left)
        { input = source; leftHand = left; surfaces = GetComponentsInChildren<Renderer>(true); }
        private void LateUpdate()
        {
            bool visible = input != null && (leftHand ? input.LeftTracked : input.RightTracked);
            foreach (var surface in surfaces) if (surface != null) surface.enabled = visible;
        }
        private void OnDisable()
        { if (surfaces != null) foreach (var surface in surfaces) if (surface != null) surface.enabled = false; }
    }
}
