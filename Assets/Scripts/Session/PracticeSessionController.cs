using System;
using UnityEngine;
using UnityEngine.Events;

namespace CampusEmergency.Session
{
    [Serializable] public sealed class StageChangedEvent : UnityEvent<int> { }
    [DisallowMultipleComponent]
    public sealed class PracticeSessionController : MonoBehaviour
    {
        public StageChangedEvent stageChanged = new StageChangedEvent();
        public UnityEvent sessionCleared = new UnityEvent();
        public UnityEvent sessionChanged = new UnityEvent();
        private PracticeSession model;
        public PracticeSession Model
        {
            get { EnsureModel(); return model; }
        }
        public event Action Changed;
        public event Action Cleared;
        private void Awake() { EnsureModel(); }
        private void OnEnable() { EnsureModel(); }
        private void EnsureModel()
        {
            // Managed session data is not restored by Unity's script reload.
            if (model == null) model = new PracticeSession();
        }
        public bool Report(PracticeAction action) { EnsureModel(); return Report(action, Model.SessionId); }
        // Capture SessionId when starting a coroutine/task. Its completion must use this overload.
        public bool Report(PracticeAction action, int expectedSessionId)
        {
            EnsureModel();
            if (!isActiveAndEnabled || expectedSessionId != Model.SessionId || !Enum.IsDefined(typeof(PracticeAction), action)) return false;
            PracticeStage before = Model.Stage;
            bool accepted = Model.Report(action, expectedSessionId);
            Publish(before);
            return accepted;
        }
        public void SetAssemblyPresence(bool inside)
        {
            EnsureModel();
            if (!isActiveAndEnabled) return;
            PracticeStage before = Model.Stage;
            Model.SetAssemblyPresence(inside);
            if (before != Model.Stage) Publish(before);
        }
        public void BeginPractice() { Report(PracticeAction.Consent); }
        public void PreviewRoute() { Report(PracticeAction.PreviewRoute); }
        public void SkipPreview() { Report(PracticeAction.SkipPreview); }
        public void ResetPractice() { ChangeSession(false, false); }
        public void ReplayPractice() { ChangeSession(false, true); }
        public void ExitPractice() { ChangeSession(true, false); }
        private void ChangeSession(bool exit, bool replay)
        {
            EnsureModel();
            PracticeStage before = Model.Stage;
            if (exit) Model.Exit();
            else if (replay) { if (!Model.Replay()) return; }
            else Model.Reset();
            // State/data are cleared before scene subscribers are notified.
            Cleared?.Invoke(); sessionCleared.Invoke(); Publish(before);
        }
        private void Publish(PracticeStage before)
        {
            if (before != Model.Stage) stageChanged.Invoke((int)Model.Stage);
            Changed?.Invoke(); sessionChanged.Invoke();
        }
        private void OnDisable() { if (model != null) ExitPractice(); }
        private void OnApplicationQuit() { if (model != null) ExitPractice(); }
    }
}
