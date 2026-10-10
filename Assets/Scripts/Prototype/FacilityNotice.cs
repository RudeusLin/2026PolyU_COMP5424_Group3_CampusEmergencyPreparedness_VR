using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed class FacilityNotice : MonoBehaviour, IPrototypeInteractable
    {
        public enum FacilityKind { Lift, Alarm, FireEquipment }

        [SerializeField] private FacilityKind kind;
        public FacilityKind Kind => kind;

        public string Prompt
        {
            get
            {
                switch (kind)
                {
                    case FacilityKind.Lift: return "[E] Lift information";
                    case FacilityKind.Alarm: return "[E] Inspect alarm point";
                    default: return "[E] Inspect fire equipment";
                }
            }
        }

        public void Configure(FacilityKind facilityKind)
        {
            kind = facilityKind;
        }

        public string Interact(PrototypeExplorer explorer = null)
        {
            switch (kind)
            {
                case FacilityKind.Lift:
                    return "LIFT OUT OF SERVICE\nDo not use the lift in case of fire. Use the stairs.";
                case FacilityKind.Alarm:
                    return "MANUAL FIRE ALARM\nInspection preview. Alarm logic will be connected by the gameplay team.";
                default:
                    return "FIRE EQUIPMENT\nEnvironment preview. The current practice route leads to the assembly point.";
            }
        }
    }
}
