using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CampusEmergency.Session
{
    public enum PracticeStage { Consent, RoutePreview, Alarm, SmokeProtection, RouteChoice, FirePractice, Assembly, Debrief, Exited }
    public enum PracticeAction
    {
        Consent, PreviewRoute, SkipPreview, AlarmStarted,
        // Value 4 is retired; retain existing serialized action IDs.
        TakeTowel = 5, WetTowel,
        CoverMouth, Crouch, StandUpright, UncoverMouth, ChooseStairs, ChooseLift,
        TakeExtinguisher, RemovePin, AimAtBase, AimAtTop, Spray, FireExtinguished,
        ConfirmAssembly
        // Values 20-22 are retired. Do not reuse these serialized action IDs.
    }

    public sealed class DecisionRecord
    {
        public PracticeStage Stage { get; private set; }
        public PracticeAction Action { get; private set; }
        public bool Accepted { get; private set; }
        public string Explanation { get; private set; }
        public string Advice { get; private set; }
        public int Occurrences { get; internal set; }
        public string ReviewStatus
        {
            get
            {
                if (!Accepted) return "Needs improvement";
                if (Action == PracticeAction.Consent || Action == PracticeAction.PreviewRoute || Action == PracticeAction.SkipPreview) return "Your choice";
                if (Action == PracticeAction.FireExtinguished || Action == PracticeAction.ConfirmAssembly) return "Completed";
                return "Correct";
            }
        }
        public string ReviewAction
        {
            get
            {
                switch (Action)
                {
                    case PracticeAction.Consent: return "Agreeing to take part";
                    case PracticeAction.PreviewRoute: return "Viewing the evacuation route";
                    case PracticeAction.SkipPreview: return "Choosing to skip the route preview";
                    case PracticeAction.AlarmStarted: return "Activating the alarm";
                    case PracticeAction.TakeTowel: return "Picking up the towel";
                    case PracticeAction.WetTowel: return "Wetting the towel at the tap";
                    case PracticeAction.CoverMouth: return "Covering your mouth with the towel";
                    case PracticeAction.Crouch: return "Lowering your body posture";
                    case PracticeAction.StandUpright: return "Standing upright during smoke protection";
                    case PracticeAction.UncoverMouth: return "Removing the towel from your face";
                    case PracticeAction.ChooseStairs: return "Choosing the stairs";
                    case PracticeAction.ChooseLift: return "Choosing the lift";
                    case PracticeAction.TakeExtinguisher: return "Picking up the extinguisher";
                    case PracticeAction.RemovePin: return "Removing the safety pin";
                    case PracticeAction.AimAtBase: return "Aiming at the base of the fire";
                    case PracticeAction.AimAtTop: return "Aiming at the top of the flames";
                    case PracticeAction.Spray: return "Spraying the extinguisher";
                    case PracticeAction.FireExtinguished: return "Putting out the simulated fire";
                    case PracticeAction.ConfirmAssembly: return "Reaching the assembly point";
                    default: return "Taking an action";
                }
            }
        }
        public string ReviewText
        {
            get { return ReviewStatus + ": " + ReviewAction + (Occurrences > 1 ? " (" + Occurrences + " attempts)" : ""); }
        }
        internal DecisionRecord(PracticeStage stage, PracticeAction action, bool accepted, string reason, string advice)
        { Stage = stage; Action = action; Accepted = accepted; Explanation = reason; Advice = advice; Occurrences = 1; }
    }

    // Pure C#: no scene objects, file output, PlayerPrefs, accounts or personal identifiers.
    public sealed class PracticeSession
    {
        private readonly List<DecisionRecord> records = new List<DecisionRecord>();
        private readonly HashSet<PracticeAction> completed = new HashSet<PracticeAction>();
        private bool sprayedAtBase;
        public const int RecordLimit = 256;
        public ReadOnlyCollection<DecisionRecord> Records { get; private set; }
        public PracticeStage Stage { get; private set; }
        public int SessionId { get; private set; }
        public int SmokeStep { get; private set; }
        public int FireStep { get; private set; }
        public int DroppedRecords { get; private set; }
        public bool IsCovered { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool AimingAtBase { get; private set; }
        public bool IsAtAssembly { get; private set; }
        public bool PreviewUsed { get; private set; }
        public string Feedback { get; private set; }
        public bool Active { get { return Stage >= PracticeStage.RoutePreview && Stage <= PracticeStage.Assembly; } }

        public PracticeSession()
        {
            Records = records.AsReadOnly();
            Reset();
        }

        public void Reset() { Clear(); Stage = PracticeStage.Consent; Feedback = "Read the practice information and choose whether to begin."; }
        public void Exit() { Clear(); Stage = PracticeStage.Exited; Feedback = "Practice ended. Temporary session data has been cleared."; }
        public bool Replay()
        {
            if (Stage != PracticeStage.Debrief) return false;
            Clear(); Stage = PracticeStage.RoutePreview;
            Feedback = "New practice. Preview the route or skip the preview.";
            return true;
        }
        private void Clear()
        {
            SessionId++;
            records.Clear(); completed.Clear(); SmokeStep = FireStep = DroppedRecords = 0;
            IsCovered = IsCrouching = AimingAtBase = IsAtAssembly = PreviewUsed = sprayedAtBase = false;
            Feedback = string.Empty;
        }
        public void SetAssemblyPresence(bool present)
        {
            if (!Active) return;
            IsAtAssembly = present;
            if (present && Stage == PracticeStage.Assembly) Report(PracticeAction.ConfirmAssembly);
        }

        public bool Report(PracticeAction action, int sessionId)
        { return sessionId == SessionId && Report(action); }

        public bool Report(PracticeAction action)
        {
            if (!Enum.IsDefined(typeof(PracticeAction), action)) return false;
            if (Stage == PracticeStage.Exited || Stage == PracticeStage.Debrief) return false;
            if (action == PracticeAction.Consent)
            {
                if (Stage != PracticeStage.Consent) return false;
                return Accept(action, "Participation confirmed. You can leave at any time.", PracticeStage.RoutePreview);
            }
            if (!Active) return false;
            if (action == PracticeAction.Crouch || action == PracticeAction.StandUpright || action == PracticeAction.UncoverMouth)
            {
                // Posture and face covering are evaluated only within smoke protection.
                if (Stage != PracticeStage.SmokeProtection) return false;
                if (action == PracticeAction.Crouch)
                {
                    if (IsCrouching) return false;
                    if (SmokeStep < 3 || !IsCovered) return Reject(action, "Complete the towel sequence before this action. If the covering was removed, use Restart practice to try again.");
                    IsCrouching = true;
                    SmokeStep = 4;
                    return Accept(action, "Lower posture recorded for this simulation.", PracticeStage.RouteChoice, false);
                }
                if (action == PracticeAction.UncoverMouth)
                {
                    if (!IsCovered) return false;
                    IsCovered = false;
                    return Reject(action, "Face covering removed during the simulated smoke segment. Use Restart practice to repeat the towel sequence.");
                }
                IsCrouching = false;
                return Reject(action, "Upright posture recorded. Review the posture cue for this simulation.");
            }
            // Ignore duplicate success callbacks (e.g. repeated overlap/selection events).
            if (completed.Contains(action) && action != PracticeAction.AimAtBase) return false;
            switch (Stage)
            {
                case PracticeStage.RoutePreview:
                    if (action == PracticeAction.PreviewRoute || action == PracticeAction.SkipPreview)
                    { PreviewUsed = action == PracticeAction.PreviewRoute; return Accept(action, PreviewUsed ? "Route preview selected." : "Route preview skipped by choice.", PracticeStage.Alarm); }
                    break;
                case PracticeStage.Alarm:
                    if (action == PracticeAction.AlarmStarted) return Accept(action, "Simulated alarm started. Follow the practice prompts.", PracticeStage.SmokeProtection);
                    break;
                case PracticeStage.SmokeProtection:
                    PracticeAction[] smokeOrder = { PracticeAction.TakeTowel, PracticeAction.WetTowel, PracticeAction.CoverMouth };
                    if (SmokeStep < smokeOrder.Length && action == smokeOrder[SmokeStep])
                    {
                        SmokeStep++;
                        if (action == PracticeAction.CoverMouth) IsCovered = true;
                        return Accept(action, SmokeStep == 3 ? "Towel sequence complete. Use the lower-posture control." : "Checklist step recorded.",
                            Stage);
                    }
                    break;
                case PracticeStage.RouteChoice:
                    if (action == PracticeAction.ChooseLift) return Reject(action, "Lift choice recorded. Recover by selecting the signed stair route.");
                    if (action == PracticeAction.ChooseStairs) return Accept(action, "Stair route selected.", PracticeStage.FirePractice);
                    break;
                case PracticeStage.FirePractice:
                    if (action == PracticeAction.TakeExtinguisher && FireStep == 0)
                    { FireStep = 1; return Accept(action, "Training extinguisher selected.", Stage); }
                    if (action == PracticeAction.RemovePin && FireStep == 1)
                    { FireStep = 2; return Accept(action, "Training pin removed.", Stage); }
                    if (action == PracticeAction.AimAtTop)
                    { AimingAtBase = false; sprayedAtBase = false; return Reject(action, "Aim is away from the simulated target base. Adjust before spraying."); }
                    if (action == PracticeAction.AimAtBase && FireStep >= 2)
                    {
                        if (AimingAtBase) return false;
                        AimingAtBase = true; FireStep = 3;
                        return Accept(action, "Aim aligned with the simulated target base.", Stage, false);
                    }
                    if (action == PracticeAction.Spray)
                    {
                        if (FireStep < 2) return Reject(action, "Spray attempted before removing the training pin.");
                        if (!AimingAtBase) return Reject(action, "Spray has no effect until aim is aligned with the target base.");
                        sprayedAtBase = true;
                        return Accept(action, "Valid spray started. Wait for the fire module to confirm completion.", Stage, false);
                    }
                    if (action == PracticeAction.FireExtinguished && sprayedAtBase && AimingAtBase)
                    { FireStep = 4; return Accept(action, "Simulated fire module confirmed completion. Continue to the assembly point.", PracticeStage.Assembly); }
                    break;
                case PracticeStage.Assembly:
                    if (action == PracticeAction.ConfirmAssembly)
                    {
                        if (!IsAtAssembly) return Reject(action, "Reach the marked outdoor assembly area before confirming arrival.");
                        return Accept(action, "Arrival confirmed. Review the practice decisions below.", PracticeStage.Debrief);
                    }
                    break;
            }
            return Reject(action, "Action is out of sequence. " + NextInstruction);
        }

        private bool Accept(PracticeAction action, string reason, PracticeStage next, bool once = true)
        { Add(action, true, reason); if (once) completed.Add(action); Stage = next; return true; }
        private bool Reject(PracticeAction action, string reason) { Add(action, false, reason, ReviewAdvice(action)); return false; }
        private string ReviewAdvice(PracticeAction action)
        {
            // Capture guidance now, while the prerequisite that failed is still known.
            if (Stage == PracticeStage.SmokeProtection &&
                (action == PracticeAction.UncoverMouth || (SmokeStep >= 3 && !IsCovered)))
                return "Restart the practice and repeat the towel steps, keeping the towel over your mouth.";
            if (Stage == PracticeStage.RouteChoice && action == PracticeAction.ChooseLift)
                return "Choose the signed stairs to continue this practice.";
            if (Stage == PracticeStage.FirePractice && FireStep >= 2 &&
                (action == PracticeAction.AimAtTop || (action == PracticeAction.Spray && !AimingAtBase)))
                return "Aim at the base of the fire before spraying.";
            return NextInstruction;
        }
        private void Add(PracticeAction action, bool accepted, string reason, string advice = "")
        {
            Feedback = reason;
            DecisionRecord last = records.Count == 0 ? null : records[records.Count - 1];
            if (last != null && last.Stage == Stage && last.Action == action && last.Accepted == accepted && last.Explanation == reason && last.Advice == advice)
            { last.Occurrences++; return; }
            if (records.Count == RecordLimit) { records.RemoveAt(0); DroppedRecords++; }
            records.Add(new DecisionRecord(Stage, action, accepted, reason, advice));
        }

        public string NextInstruction
        {
            get
            {
                switch (Stage)
                {
                    case PracticeStage.Consent: return "Read the information and consent to start, or leave.";
                    case PracticeStage.RoutePreview: return "Preview the signed route, or choose Skip.";
                    case PracticeStage.Alarm: return "Activate the nearby manual alarm in this simulation.";
                    case PracticeStage.SmokeProtection:
                        string[] steps = { "Select the towel.", "Interact with the tap using the towel.", "Bring the towel to the face.", "Use the lower-posture control." };
                        return steps[Math.Min(SmokeStep, 3)];
                    case PracticeStage.RouteChoice: return "Choose the signed stairs. The lift choice can be corrected.";
                    case PracticeStage.FirePractice:
                        if (FireStep == 0) return "Select the training extinguisher.";
                        if (FireStep == 1) return "Remove the training pin.";
                        if (!AimingAtBase) return "Aim at the simulated target base.";
                        return "Spray until the fire module confirms completion.";
                    case PracticeStage.Assembly: return "Enter the marked outdoor assembly area to complete the practice.";
                    case PracticeStage.Debrief: return "Review the explanations, practise again, or exit.";
                    default: return "Session ended. Start a new session to practise again.";
                }
            }
        }

        public static string ReviewStageTitle(PracticeStage stage)
        {
            switch (stage)
            {
                case PracticeStage.Consent: return "Getting started";
                case PracticeStage.RoutePreview: return "Route preview";
                case PracticeStage.Alarm: return "Raising the alarm";
                case PracticeStage.SmokeProtection: return "Smoke protection";
                case PracticeStage.RouteChoice: return "Choosing an evacuation route";
                case PracticeStage.FirePractice: return "Using the extinguisher";
                case PracticeStage.Assembly: return "Reaching the assembly point";
                default: return "Your practice";
            }
        }
        public string BuildDebrief()
        {
            if (Stage != PracticeStage.Debrief) return string.Empty;
            var text = new StringBuilder();
            text.AppendLine("YOUR PRACTICE REVIEW");
            text.AppendLine("You reached the assembly point. Review your actions below.");
            text.AppendLine(PreviewUsed ? "Route preview: viewed." : "Route preview: skipped by choice.");
            if (DroppedRecords > 0) text.AppendLine("Earlier actions are no longer available. Only the latest " + RecordLimit + " entries are shown.");
            PracticeStage? previous = null;
            foreach (DecisionRecord record in records)
            {
                if (previous != record.Stage)
                {
                    text.AppendLine();
                    text.AppendLine(ReviewStageTitle(record.Stage));
                    previous = record.Stage;
                }
                text.AppendLine(record.ReviewText);
                if (!record.Accepted) text.AppendLine("Tip: " + record.Advice);
            }
            return text.ToString();
        }
    }
}
