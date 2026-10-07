using System;
using CampusEmergency.Session;

namespace CampusEmergency.Editor
{
    // Runs without Unity as well as from Tools > Campus Emergency > Validate Session Logic.
    public static class PracticeSessionChecks
    {
        private static int assertions;
        private static void Check(bool condition, string message)
        { assertions++; if (!condition) throw new InvalidOperationException(message); }
        private static PracticeSession New()
        { return new PracticeSession(); }
        private static void ToSmoke(PracticeSession s, bool preview = false)
        {
            Check(s.Report(PracticeAction.Consent), "Consent accepted");
            Check(s.Report(preview ? PracticeAction.PreviewRoute : PracticeAction.SkipPreview), "Preview choice accepted");
            Check(s.Report(PracticeAction.AlarmStarted), "Alarm accepted");
        }
        private static void FinishTowel(PracticeSession s)
        {
            foreach (var action in new[] { PracticeAction.TakeTowel, PracticeAction.WetTowel, PracticeAction.CoverMouth })
                Check(s.Report(action), "Ordered smoke action " + action);
        }
        private static void FinishSmoke(PracticeSession s)
        {
            FinishTowel(s);
            Check(s.Stage == PracticeStage.SmokeProtection && s.SmokeStep == 3, "Cover mouth waits for lower posture");
            Check(s.NextInstruction == "Use the lower-posture control.", "Posture instruction shown before route choice");
            Check(s.Report(PracticeAction.Crouch), "Required lower posture accepted");
        }
        private static void ToFire(PracticeSession s)
        { ToSmoke(s); FinishSmoke(s); Check(s.Report(PracticeAction.ChooseStairs), "Stairs accepted"); }
        private static void FinishFire(PracticeSession s)
        {
            foreach (var action in new[] { PracticeAction.TakeExtinguisher, PracticeAction.RemovePin, PracticeAction.AimAtBase, PracticeAction.Spray, PracticeAction.FireExtinguished })
                Check(s.Report(action), "Ordered fire action " + action);
        }
        private static void Finish(PracticeSession s)
        { s.SetAssemblyPresence(true); Check(s.Stage == PracticeStage.Debrief, "Entering assembly completes practice without a separate confirmation"); }
        private static void CheckSmokeActionsIgnored(PracticeSession s)
        {
            var stage = s.Stage; int count = s.Records.Count;
            bool covered = s.IsCovered, crouching = s.IsCrouching;
            string feedback = s.Feedback;
            foreach (var action in new[] { PracticeAction.Crouch, PracticeAction.StandUpright, PracticeAction.UncoverMouth })
                Check(!s.Report(action), "Smoke-only action ignored in " + stage + ": " + action);
            Check(s.Stage == stage && s.Records.Count == count && s.Feedback == feedback &&
                s.IsCovered == covered && s.IsCrouching == crouching, "No smoke judgement or side effects in " + stage);
        }

        public static string Run()
        {
            assertions = 0;
            var s = New();
            Check(!s.Report(PracticeAction.TakeTowel) && s.Records.Count == 0, "No activity stored before consent");
            ToSmoke(s, true);
            Check(s.PreviewUsed, "Preview choice retained");
            Check(s.NextInstruction == "Select the towel.", "Smoke begins with towel selection without bathroom entry");
            Check(!s.Report(PracticeAction.CoverMouth) && s.SmokeStep == 0, "Out-of-order covering rejected");
            Check(!s.Report(PracticeAction.WetTowel), "Cannot wet missing towel");
            Check(!s.Report(PracticeAction.Crouch) && s.SmokeStep == 0 && !s.IsCrouching, "Cannot lower posture before towel sequence");
            FinishSmoke(s);
            Check(s.Stage == PracticeStage.RouteChoice, "Four required actions complete smoke protection");
            CheckSmokeActionsIgnored(s);
            Check(!s.Report(PracticeAction.ChooseLift) && s.Stage == PracticeStage.RouteChoice, "Lift error recoverable");
            Check(s.Report(PracticeAction.ChooseStairs), "Recover via stairs");
            CheckSmokeActionsIgnored(s);
            int beforeRouteActions = s.Records.Count;
            string beforeRouteFeedback = s.Feedback;
            Check(!s.Report((PracticeAction)21), "Retired route blockage event ignored");
            Check(!s.Report((PracticeAction)22), "Retired alternative route event ignored");
            Check(s.Stage == PracticeStage.FirePractice && s.FireStep == 0 &&
                s.Records.Count == beforeRouteActions && s.Feedback == beforeRouteFeedback,
                "Route callbacks cannot affect fire practice or block later assembly");
            Check(!s.Report(PracticeAction.FireExtinguished), "Completion cannot skip extinguisher steps");
            Check(!s.Report(PracticeAction.Spray), "Spray before pin rejected");
            Check(s.Report(PracticeAction.TakeExtinguisher), "Take extinguisher");
            Check(s.Report(PracticeAction.RemovePin), "Remove pin");
            Check(!s.Report(PracticeAction.AimAtTop), "Top aim recorded as error");
            Check(!s.Report(PracticeAction.Spray), "Invalid aim spray rejected");
            Check(s.Report(PracticeAction.AimAtBase), "Correct aim");
            Check(s.Report(PracticeAction.Spray), "Valid spray");
            Check(!s.Report(PracticeAction.AimAtTop), "Lose valid aim");
            Check(!s.Report(PracticeAction.FireExtinguished), "Completion with lost aim rejected");
            Check(s.Report(PracticeAction.AimAtBase) && s.Report(PracticeAction.Spray), "Aim and spray can recover after first spray");
            Check(s.Report(PracticeAction.FireExtinguished), "Fire completion accepted");
            CheckSmokeActionsIgnored(s);
            Check(!s.Report(PracticeAction.ConfirmAssembly), "No remote assembly confirmation");
            s.SetAssemblyPresence(false);
            Check(s.Stage == PracticeStage.Assembly, "Outside presence does not complete assembly");
            beforeRouteActions = s.Records.Count;
            beforeRouteFeedback = s.Feedback;
            Check(!s.Report((PracticeAction)21) && !s.Report((PracticeAction)22) &&
                s.Stage == PracticeStage.Assembly && s.Records.Count == beforeRouteActions && s.Feedback == beforeRouteFeedback,
                "Retired route events have no effect on assembly");
            Finish(s);
            Check(s.BuildDebrief().Contains("Needs improvement: Choosing the lift") && s.BuildDebrief().Contains("Route preview: viewed."), "Debrief reflects actual mistakes and preview use");
            string debrief = s.BuildDebrief();
            Check(!debrief.Contains("Duration:") && !debrief.Contains("[") && debrief.Contains("Completed: Reaching the assembly point"),
                "Debrief retains action records without duration or timestamps");
            Check(debrief.Contains("Correct: Picking up the towel") && debrief.Contains("Correct: Wetting the towel at the tap") &&
                debrief.Contains("Correct: Covering your mouth with the towel"), "Successful towel actions have distinct readable descriptions");
            Check(!debrief.Contains("SmokeProtection") && !debrief.Contains("FirePractice") && !debrief.Contains(" / Recorded"),
                "Review hides internal stage names and log formatting");
            Check(debrief.IndexOf("Needs improvement: Choosing the lift") < debrief.IndexOf("Correct: Choosing the stairs"),
                "Mistake and subsequent correction remain in order");
            bool earlySprayTip = false, wrongAimTip = false;
            foreach (var record in s.Records)
            {
                if (!record.Accepted) Check(!string.IsNullOrEmpty(record.Advice), "Every rejected action has a tip");
                if (!record.Accepted && record.Action == PracticeAction.Spray)
                {
                    earlySprayTip |= record.Advice == "Select the training extinguisher.";
                    wrongAimTip |= record.Advice == "Aim at the base of the fire before spraying.";
                }
            }
            Check(earlySprayTip && wrongAimTip, "Tips preserve the different prerequisites missing when spraying was attempted");
            var count = s.Records.Count;
            s.SetAssemblyPresence(true); s.SetAssemblyPresence(false);
            Check(s.Stage == PracticeStage.Debrief && s.IsAtAssembly && s.Records.Count == count && s.BuildDebrief() == debrief,
                "Repeated arrival or exit callbacks cannot change the completed practice");
            Check(!s.Report(PracticeAction.ChooseLift) && s.Records.Count == count, "No mutations after completion");
            var liveRecords = s.Records;
            int oldId = s.SessionId;
            Check(s.Replay(), "Replay accepted after debrief");
            Check(s.Stage == PracticeStage.RoutePreview && liveRecords.Count == 0 && s.BuildDebrief() == "", "Replay clears previous records and debrief");
            Check(!s.Report(PracticeAction.FireExtinguished, oldId), "Stale completion callback ignored");
            Check(!s.PreviewUsed && !s.IsAtAssembly && !s.IsCovered && s.FireStep == 0, "Replay resets flags");
            s.Exit();
            Check(s.Stage == PracticeStage.Exited && s.Records.Count == 0 && s.BuildDebrief() == "", "Exit clears all session data");
            Check(!s.Report(PracticeAction.Consent), "Exited session requires explicit new session");
            s.Reset(); Check(s.Stage == PracticeStage.Consent, "Reset requires consent again");

            s = New(); ToSmoke(s); FinishTowel(s);
            Check(s.Stage == PracticeStage.SmokeProtection, "Smoke protection always waits for posture");
            Check(!s.Report(PracticeAction.ChooseStairs) && s.Stage == PracticeStage.SmokeProtection && s.SmokeStep == 3, "Stairs cannot skip required posture");
            Check(!s.Report(PracticeAction.UncoverMouth), "Cover removal before fourth action recorded");
            Check(!s.Report(PracticeAction.Crouch) && s.Stage == PracticeStage.SmokeProtection, "Fourth action cannot finish with covering removed");
            Check(!s.Report(PracticeAction.CoverMouth) && !s.IsCovered, "Repeated cover action cannot restore removed covering");
            s.Reset(); ToSmoke(s); FinishTowel(s);
            Check(s.Report(PracticeAction.Crouch) && s.Stage == PracticeStage.RouteChoice, "Required fourth action advances to route choice");
            CheckSmokeActionsIgnored(s);
            Check(!s.Report(PracticeAction.CoverMouth) && s.IsCovered, "Duplicate cover action leaves completed smoke state unchanged");
            Check(s.SmokeStep == 4, "Smoke protection counts four required actions");

            // Reset and exit at every core stage, including pending fire completion.
            var journey = new[] { PracticeAction.Consent, PracticeAction.SkipPreview, PracticeAction.AlarmStarted,
                    PracticeAction.TakeTowel, PracticeAction.WetTowel, PracticeAction.CoverMouth, PracticeAction.Crouch,
                    PracticeAction.ChooseStairs, PracticeAction.TakeExtinguisher, PracticeAction.RemovePin, PracticeAction.AimAtBase,
                    PracticeAction.Spray, PracticeAction.FireExtinguished, PracticeAction.UncoverMouth, PracticeAction.ConfirmAssembly };
            for (int stop = 0; stop <= journey.Length; stop++)
            {
                s = New();
                for (int i = 0; i < stop; i++)
                {
                    if (journey[i] == PracticeAction.ConfirmAssembly) s.SetAssemblyPresence(true);
                    else s.Report(journey[i]);
                }
                oldId = s.SessionId; s.Reset();
                Check(s.Stage == PracticeStage.Consent && s.Records.Count == 0 && s.SmokeStep == 0 && s.FireStep == 0, "Reset at journey index " + stop);
                Check(!s.Report(PracticeAction.FireExtinguished, oldId), "Stale event after reset " + stop);
                s.Exit(); Check(s.Records.Count == 0 && s.Stage == PracticeStage.Exited, "Exit after reset " + stop);
            }
            s = New(); ToFire(s);
            int before = s.Records.Count;
            s.Report(PracticeAction.TakeExtinguisher); s.Report(PracticeAction.TakeExtinguisher);
            Check(s.Records.Count == before + 1 && s.FireStep == 1, "Repeated accepted callbacks are idempotent");
            for (int i = 0; i < 1000; i++) { s.Report(PracticeAction.ChooseLift); s.Report(PracticeAction.AimAtTop); }
            Check(s.Records.Count == PracticeSession.RecordLimit && s.DroppedRecords > 0, "Temporary record memory is bounded");
            s.Reset(); Check(s.DroppedRecords == 0, "Discard counter reset");

            s = New(); ToFire(s); FinishFire(s); Finish(s);
            Check(!s.PreviewUsed && s.BuildDebrief().Contains("skipped"), "Skip branch completes");
            Check(!s.BuildDebrief().Contains("Needs improvement") && !s.BuildDebrief().Contains("Tip:"), "Error-free practice does not invent mistakes or advice");
            Check(s.BuildDebrief().Contains("Your choice: Choosing to skip the route preview"), "Skipping an optional preview is a neutral choice");
            Check(s.Replay() && s.Stage == PracticeStage.RoutePreview, "Replay stage invariant");
            s = New(); ToFire(s);
            s.Report(PracticeAction.Spray); s.Report(PracticeAction.Spray);
            FinishFire(s); Finish(s);
            Check(s.BuildDebrief().Contains("Needs improvement: Spraying the extinguisher (2 attempts)"),
                "Repeated mistakes keep their occurrence count");
            return "PASS: " + assertions + " assertions; ordered journey, both preview branches, errors/recovery, mandatory posture step, arrival guards, data clearing, replay, stale callbacks and bounded records.";
        }

        public static int Main()
        { try { Console.WriteLine(Run()); return 0; } catch (Exception e) { Console.Error.WriteLine(e); return 1; } }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Campus Emergency/Validate Session Logic")]
        public static void ValidateInEditor() { UnityEngine.Debug.Log(Run()); }
#endif
    }
}
