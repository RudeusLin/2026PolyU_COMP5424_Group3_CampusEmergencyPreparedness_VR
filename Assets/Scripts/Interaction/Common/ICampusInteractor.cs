using UnityEngine;

namespace CampusEmergency.Prototype
{
    /// <summary>Shared held-object capability; independent of desktop or XR locomotion.</summary>
    public interface ICampusInteractor
    {
        CampusTowel HeldTowel { get; }
        Transform TowelHoldPoint { get; }
        bool TryHoldTowel(CampusTowel towel);
        void ReleaseTowel(CampusTowel towel);
    }
}
