using UnityEngine;

namespace CampusEmergency.Player.XR
{
    /// <summary>Explicit whitelist marker on a walkable collider, not on furniture or walls.</summary>
    [DisallowMultipleComponent]
    public sealed class CampusTeleportSurface : MonoBehaviour { }
}
