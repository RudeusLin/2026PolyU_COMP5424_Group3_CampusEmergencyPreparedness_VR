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
                    case FacilityKind.Alarm: return "[E] Activate fire alarm";
                    default: return "[E] Inspect fire equipment";
                }
            }
        }

        public void Configure(FacilityKind facilityKind)
        {
            kind = facilityKind;
        }

        public string Interact(ICampusInteractor explorer = null)
        {
            switch (kind)
            {
                case FacilityKind.Lift:
                    return "LIFT OUT OF SERVICE\nDo not use the lift in case of fire. Use the stairs.";
                case FacilityKind.Alarm:
                    CampusDrillAtmosphere atmosphere = FindObjectOfType<CampusDrillAtmosphere>(true);
                    if (atmosphere == null)
                        return "FIRE ALARM UNAVAILABLE\nThe alarm system could not be found in this scene.";
                    if (atmosphere.IsAlarmActive)
                        return "SIMULATED FIRE ALARM ACTIVE\nThe alarm is already running.";
                    atmosphere.ActivateAlarm();
                    return "SIMULATED FIRE ALARM ACTIVATED\nEvacuate via the stairs.";
                default:
                    return "FIRE EQUIPMENT\nEnvironment preview. The current practice route leads to the assembly point.";
            }
        }
    }
}
