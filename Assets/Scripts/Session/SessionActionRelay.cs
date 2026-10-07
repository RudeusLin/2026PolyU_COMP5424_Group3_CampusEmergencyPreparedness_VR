using UnityEngine;
using UnityEngine.Events;

namespace CampusEmergency.Session
{
    // Wire UI Button.onClick or an XR interaction event to InvokeAction in the Inspector.
    public sealed class SessionActionRelay : MonoBehaviour
    {
        [SerializeField] private PracticeSessionController session;
        [SerializeField] private PracticeAction action;
        public UnityEvent accepted = new UnityEvent();
        public UnityEvent rejected = new UnityEvent();
        public void Configure(PracticeSessionController owner, PracticeAction value) { session = owner; action = value; }
        public void InvokeAction()
        {
            if (session == null) return;
            if (session.Report(action)) accepted.Invoke(); else rejected.Invoke();
        }
    }
}
