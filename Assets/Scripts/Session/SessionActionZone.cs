using System.Collections.Generic;
using UnityEngine;

namespace CampusEmergency.Session
{
    [RequireComponent(typeof(Collider))]
    public sealed class SessionActionZone : MonoBehaviour
    {
        [SerializeField] private PracticeSessionController session = null;
        [SerializeField] private PracticeAction action = PracticeAction.ChooseStairs;
        [SerializeField] private bool assemblyPresenceOnly = false;
        [SerializeField] private SessionParticipant participant = null;
        private readonly HashSet<Collider> overlaps = new HashSet<Collider>();
        private int reportedSession = -1;
        private PracticeStage reportedStage;
        public void Configure(PracticeSessionController owner, SessionParticipant player, PracticeAction value, bool isAssembly = false)
        {
            session = owner; participant = player; action = value; assemblyPresenceOnly = isAssembly;
            GetComponent<Collider>().isTrigger = true;
        }
        private void Reset() { GetComponent<Collider>().isTrigger = true; }
        private void OnTriggerEnter(Collider other) { Track(other); }
        private void OnTriggerStay(Collider other) { Track(other); }
        private void Track(Collider other)
        {
            if (session == null || participant == null || other.GetComponentInParent<SessionParticipant>() != participant) return;
            overlaps.Add(other);
            if (assemblyPresenceOnly) session.SetAssemblyPresence(true);
            else if (reportedSession != session.Model.SessionId || reportedStage != session.Model.Stage)
            {
                // A player already inside a zone can become eligible when the stage changes.
                reportedSession = session.Model.SessionId; reportedStage = session.Model.Stage;
                session.Report(action, reportedSession);
                reportedStage = session.Model.Stage;
            }
        }
        private void OnTriggerExit(Collider other)
        {
            overlaps.Remove(other);
            if (overlaps.Count != 0) return;
            reportedSession = -1;
            if (assemblyPresenceOnly && session != null) session.SetAssemblyPresence(false);
        }
        private void OnDisable()
        {
            overlaps.Clear(); reportedSession = -1;
            if (assemblyPresenceOnly && session != null) session.SetAssemblyPresence(false);
        }
    }
}
