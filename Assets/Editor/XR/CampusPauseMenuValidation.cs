using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CampusEmergency.Player;
using CampusEmergency.Player.XR;
using CampusEmergency.Prototype;
using CampusEmergency.Interaction.FireSafety;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    [InitializeOnLoad]
    public static class CampusPauseMenuValidation
    {
        const string Phase = "CampusPauseMenuValidation.Phase";
        const string Output = "E:/0_PolyU/COMP5424_XR/Project/output/PauseMenu";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static double readyAt;
        static CampusPauseMenuValidation() { EditorApplication.playModeStateChanged += OnPlay; }
        [MenuItem("Campus Emergency/XR/Validate in-exercise pause menu")]
        public static void Begin()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Validation.txt", "Pause menu: Editor Play Mode with simulated Oculus Touch B input, no headset.\n");
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            SessionState.SetString(Phase, "start");
            EditorApplication.isPlaying = true;
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Phase, "") == "start")
            {
                readyAt = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += WaitForScene;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(Phase, "") == "exit")
            {
                Check(!EditorApplication.isPlaying && Mathf.Approximately(Time.timeScale, 1) && !AudioListener.pause, "Confirmed exit stops Play Mode and restores time/audio");
                File.AppendAllText(Output + "/Validation.txt", "RESULT: PASS. Physical headset readability/input require device verification.\n");
                SessionState.SetString(Phase, "");
                Debug.Log("CAMPUS_PAUSE_MENU_VALIDATION_PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }
        static void WaitForScene()
        {
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= WaitForScene;
            try { Run(); } catch (Exception e) { Fail(e); }
        }
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Auto(object target, string name, object value) => Set(target, "<" + name + ">k__BackingField", value);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static void Check(bool ok, string message)
        {
            File.AppendAllText(Output + "/Validation.txt", (ok ? "PASS: " : "FAIL: ") + message + "\n");
            if (!ok) throw new Exception(message);
        }
        static void Fail(Exception error)
        {
            Time.timeScale = 1; AudioListener.pause = false;
            SessionState.SetString(Phase, "");
            File.AppendAllText(Output + "/Validation.txt", error + "\nRESULT: FAIL\n");
            Debug.LogException(error); EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
        static void Queue<T>(InputControl<T> control, T value) where T : struct
        {
            using (StateEvent.From(control.device, out var state))
            {
                control.WriteValueIntoEvent(value, state); InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }
        static void Capture(Camera camera, string name)
        {
            Canvas.ForceUpdateCanvases();
            var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
            var target = new RenderTexture(1920, 1080, 24);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = previous;
                Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            }
        }
        static void Layout(Transform root, string name)
        {
            Canvas.ForceUpdateCanvases();
            var card = root.GetComponentsInChildren<Image>().Single(x => x.name == "Completion Debrief Card").rectTransform;
            foreach (var text in root.GetComponentsInChildren<Text>())
            {
                Check(text.preferredHeight <= text.rectTransform.rect.height + 2, name + " text fits: " + text.name);
                var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                Check(corners.All(c => card.rect.Contains(card.InverseTransformPoint(c))), name + " text remains within panel: " + text.name);
            }
        }
        static void Run()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            var ui = Object.FindObjectOfType<LanguageSelectionUI>();
            var input = origin.GetComponent<CampusXRInput>();
            var flow = origin.GetComponent<CampusRouteAFlow>();
            Check(ui != null && origin != null && input != null && flow != null, "XR scene and startup loaded");
            ui.enabled = false; Invoke(ui, "OnEnable");
            origin.Camera.transform.position = origin.transform.position + Vector3.up * 1.65f;
            origin.Camera.transform.rotation = origin.transform.rotation;
            var previousSettings = InputSystem.settings;
            var settings = Object.Instantiate(previousSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings = settings;
            InputSystem.RegisterLayout<UnityEngine.XR.OpenXR.Input.HapticControl>("Haptic");
            InputSystem.RegisterLayout(typeof(OculusTouchControllerProfile.OculusTouchController).GetProperty("pointer").PropertyType, "Pose");
            InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>();
            var right = InputSystem.AddDevice<OculusTouchControllerProfile.OculusTouchController>();
            InputSystem.SetDeviceUsage(right, "RightHand");
            try
            {
                var toggle = (InputAction)Get(ui, "pauseToggle");
                Check(toggle.controls.Any(x => x == right.secondaryButton), "B binding resolves to right Oculus Touch secondary button");
                Queue(right.secondaryButton, 1f); Invoke(ui, "ProcessPauseInput", true);
                Check(!LanguageSelectionUI.IsPauseMenuOpen && LanguageSelectionUI.IsStartupBlocking, "B cannot bypass startup gates");
                Queue(right.secondaryButton, 0f);
                Invoke(ui, "BeginExperience");
                Queue(right.secondaryButton, 1f); Invoke(ui, "ProcessPauseInput", false);
                Check(!LanguageSelectionUI.IsPauseMenuOpen, "Focus/tracking gate rejects menu input");
                Queue(right.secondaryButton, 0f);
                flow.Completed[0] = true; flow.Completed[1] = true;
                var saved = (bool[])flow.Completed.Clone();
                var canvas = (Canvas)Get(ui, "canvas");
                for (int language = 0; language < 3; language++)
                {
                    string code = language == 0 ? "EN" : language == 1 ? "SC" : "TC";
                    typeof(LanguageSelectionUI).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, (CampusLanguage)language);
                    Queue(right.secondaryButton, 1f); Invoke(ui, "ProcessPauseInput", true);
                    File.AppendAllText(Output + "/Validation.txt", "DEBUG menu=" + LanguageSelectionUI.IsPauseMenuOpen + " block=" + LanguageSelectionUI.IsBlocking + " startup=" + LanguageSelectionUI.IsStartupBlocking + " scale=" + Time.timeScale + " audio=" + AudioListener.pause + " B=" + toggle.IsPressed() + " edge=" + toggle.WasPressedThisFrame() + " confirming=" + Get(ui,"confirming") + " complete=" + Get(ui,"selectionComplete") + " xr=" + Get(ui,"xrMode") + "\n");
                    Check(LanguageSelectionUI.IsPauseMenuOpen && LanguageSelectionUI.IsBlocking && !LanguageSelectionUI.IsStartupBlocking && Time.timeScale == 0 && AudioListener.pause, code + " B opens menu, blocks actions and pauses simulation/audio");
                    Check(!input.HasInteractionFocus, code + " shared input gate blocks teleport, turn, interactions and item switching");
                    Check(((RectTransform[])Get(ui, "choices")).Count(x => x != null) == 3, code + " menu has continue/restart/exit choices");
                    Vector3 anchor = canvas.transform.position; Quaternion facing = canvas.transform.rotation;
                    origin.Camera.transform.localRotation = Quaternion.Euler(0, 40, 0);
                    Invoke(ui, "LateUpdate");
                    origin.transform.position += Vector3.right;
                    Invoke(ui, "LateUpdate");
                    Check(Vector3.Distance(origin.transform.position, (Vector3)Get(ui, "fixedPlayerPosition")) < .001f, code + " B menu locks player position like startup");
                    Check(Mathf.Abs(canvas.transform.position.y - origin.transform.position.y - 1.65f) < .001f, code + " B menu matches startup centre height");
                    Check(Vector3.Distance(canvas.transform.position, anchor) < .001f && Quaternion.Angle(canvas.transform.rotation, facing) < .01f, code + " panel stays fixed while head turns");
                    origin.Camera.transform.rotation = origin.transform.rotation;
                    Layout(canvas.transform, code + " menu"); Capture(origin.Camera, "Menu_" + code);
                    Invoke(ui, "Select", 1);
                    Check((int)Get(ui, "pendingPauseAction") == 1 && LanguageSelectionUI.IsPauseMenuOpen && !GetBool(ui, "leavingDebrief"), code + " restart opens confirmation without restarting");
                    Layout(canvas.transform, code + " restart confirmation"); Capture(origin.Camera, "ConfirmRestart_" + code);
                    Invoke(ui, "Select", 0);
                    Check((int)Get(ui, "pendingPauseAction") == 0 && LanguageSelectionUI.IsPauseMenuOpen, code + " cancel returns to paused menu");
                    Invoke(ui, "Select", 2);
                    Check((int)Get(ui, "pendingPauseAction") == 2 && !GetBool(ui, "leavingDebrief"), code + " exit opens confirmation without quitting");
                    Layout(canvas.transform, code + " exit confirmation"); Capture(origin.Camera, "ConfirmExit_" + code);
                    Invoke(ui, "Select", 0);
                    Queue(right.secondaryButton, 0f); Queue(right.secondaryButton, 1f); Invoke(ui, "ProcessPauseInput", true);
                    Check(!LanguageSelectionUI.IsPauseMenuOpen && !LanguageSelectionUI.IsBlocking && !canvas.gameObject.activeSelf && Time.timeScale == 1 && !AudioListener.pause, code + " B resumes and hides all menu targets");
                    InputSystem.Update(); Invoke(ui, "ProcessPauseInput", true);
                    Check(!LanguageSelectionUI.IsPauseMenuOpen, code + " held B does not reopen menu");
                    Queue(right.secondaryButton, 0f);
                    Check(flow.Completed.SequenceEqual(saved), code + " cancel/resume preserves checkpoint progress");
                }
                var lifts = Object.FindObjectsOfType<FacilityNotice>().Where(x => x.Kind == FacilityNotice.FacilityKind.Lift).ToArray();
                Check(lifts.Length == 4, "All four elevator call panels are interactive");
                Check(lifts.Select(x => Mathf.FloorToInt(x.transform.position.y / 3.6f)).Distinct().Count() == 4, "Call panels cover all four floors");
                for (int language = 0; language < 3; language++)
                {
                    typeof(LanguageSelectionUI).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, (CampusLanguage)language);
                    string code = language == 0 ? "EN" : language == 1 ? "SC" : "TC";
                    for (int floor = 0; floor < lifts.Length; floor++)
                    {
                        lifts[floor].Interact();
                        Check(GetBool(ui, "elevatorWarningVisible") && LanguageSelectionUI.IsBlocking && Time.timeScale == 0, code + " lift " + floor + " opens blocking warning");
                        var pos = origin.transform.position; var rot = origin.transform.rotation;
                        var panel = canvas.transform.position; var facing = canvas.transform.rotation;
                        Check(Mathf.Abs(panel.y - pos.y - 1.65f) < .001f && Vector3.Dot(canvas.transform.up, Vector3.up) > .999f, code + " warning matches startup height and stays vertical");
                        origin.transform.position += Vector3.right; origin.transform.rotation = Quaternion.Euler(0, 45, 0);
                        origin.Camera.transform.localRotation = Quaternion.Euler(-30, 20, 0);
                        Invoke(ui, "LateUpdate");
                        Check(Vector3.Distance(origin.transform.position, pos) < .001f && Quaternion.Angle(origin.transform.rotation, rot) < .01f, code + " warning locks player pose");
                        Check(Vector3.Distance(canvas.transform.position, panel) < .001f && Quaternion.Angle(canvas.transform.rotation, facing) < .01f, code + " warning panel remains fixed during head motion");
                        origin.Camera.transform.rotation = origin.transform.rotation;
                        Layout(canvas.transform, code + " elevator warning");
                        if (floor == 0) Capture(origin.Camera, "ElevatorWarning_" + code);
                        Queue(right.secondaryButton, 1f); Invoke(ui, "ProcessPauseInput", true);
                        Check(GetBool(ui, "elevatorWarningVisible"), "B does not bypass elevator acknowledgement"); Queue(right.secondaryButton, 0f);
                        Invoke(ui, "Select", 0);
                        Check(!LanguageSelectionUI.IsBlocking && !LanguageSelectionUI.IsPauseMenuOpen && Time.timeScale == 1 && !AudioListener.pause, code + " acknowledgement restores gameplay");
                        Check(flow.Completed.SequenceEqual(saved), code + " warning preserves route progress");
                    }
                }
                // Menu-only input must not interfere with phone/debrief screens.
                foreach (string flag in new[] { "emergencyCallVisible", "completionVisible" })
                {
                    Set(ui, flag, true); Queue(right.secondaryButton, 1f); Invoke(ui, "ProcessPauseInput", true);
                    Check(!LanguageSelectionUI.IsPauseMenuOpen, "B leaves " + flag + " unchanged");
                    Queue(right.secondaryButton, 0f); Set(ui, flag, false);
                }
                var inventory = origin.GetComponent<CampusXRInventory>();
                var towel = Object.FindObjectsOfType<CampusTowel>().First();
                Check(inventory.TryHoldTowel(towel), "Seed acquired towel");
                Auto(towel, "IsWet", true); Auto(inventory, "IsTowelRaised", true); Set(inventory, "raisedBlend", .8f);
                Invoke(ui, "OpenPauseMenu"); Invoke(inventory, "ProcessInput", false);
                Check(inventory.OwnedTowel == towel && inventory.IsTowelRaised && towel.IsWet && inventory.Equipped == CampusXRInventory.EquipmentSlot.Towel, "Menu preserves held wet towel and raised state");
                Invoke(ui, "Select", 0);
                Check(inventory.IsTowelRaised && Mathf.Approximately((float)Get(inventory, "raisedBlend"), .8f), "Continue preserves towel cover pose state");
                inventory.Equip(CampusXRInventory.EquipmentSlot.Empty);
                var tool = origin.GetComponent<CampusXRExtinguisherController>();
                var item = Object.FindObjectsOfType<CampusExtinguisher>().First();
                Check(tool.TryPickup(item), "Seed acquired extinguisher"); tool.PullPin();
                var fire = Object.FindObjectsOfType<CampusFireTarget>().First(x => !x.Extinguished);
                tool.ApplyTarget(fire, 1.1f); Set(tool, "triggerReleased", true);
                Invoke(ui, "OpenPauseMenu");
                Check(tool.Owned == item && tool.Held == item && item.PinRemoved, "Menu preserves equipment and pin state");
                Check(fire.ContinuousSeconds == 0 && !GetBool(tool, "triggerReleased"), "Pause interrupts spray timer and requires a fresh trigger release");
                Invoke(ui, "ResumePractice");
                var rescue = Object.FindObjectOfType<CampusWheelchairRescue>();
                Auto(flow, "Node", 5); Auto(rescue, "Escorting", true);
                Set(rescue, "pending", true); Vector3 chair = rescue.transform.position;
                Invoke(ui, "OpenPauseMenu"); Invoke(rescue, "LateUpdate");
                Check(rescue.Escorting && GetBool(rescue, "pending") && rescue.transform.position == chair, "Menu preserves escort and pending chair position");
                Invoke(ui, "ResumePractice"); Set(rescue, "pending", false); Auto(rescue, "Escorting", false);
                Time.timeScale = .6f; Invoke(ui, "OpenPauseMenu"); Invoke(ui, "ResumePractice");
                Check(Mathf.Approximately(Time.timeScale, .6f), "Resume restores the prior time scale instead of forcing one"); Time.timeScale = 1;
                Invoke(ui, "OpenPauseMenu"); Invoke(ui, "Select", 2);
                Queue(right.secondaryButton, 1f); Invoke(ui, "ProcessPauseInput", true);
                Check(!LanguageSelectionUI.IsPauseMenuOpen && !GetBool(ui, "leavingDebrief"), "B dismisses exit confirmation without exiting"); Queue(right.secondaryButton, 0f);
                Invoke(ui, "OpenPauseMenu"); Invoke(ui, "OnDisable");
                Check(!LanguageSelectionUI.IsPauseMenuOpen && Time.timeScale == 1 && !AudioListener.pause, "Disabling menu owner restores time and audio"); Invoke(ui, "OnEnable");
            }
            finally
            {
                InputSystem.RemoveDevice(right); InputSystem.settings = previousSettings; Object.DestroyImmediate(settings);
            }
            SessionState.SetString(Phase, "restart"); readyAt = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += AwaitRestart;
            Invoke(ui, "OpenPauseMenu"); Invoke(ui, "Select", 1); Invoke(ui, "Select", 1);
        }
        static bool GetBool(object target, string name) => (bool)Get(target, name);
        static void AwaitRestart()
        {
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= AwaitRestart;
            try
            {
                var ui = Object.FindObjectOfType<LanguageSelectionUI>();
                Check(ui != null && LanguageSelectionUI.IsStartupBlocking && !LanguageSelectionUI.IsPauseMenuOpen, "Confirmed restart restores language selection");
                Check(Time.timeScale == 1 && !AudioListener.pause, "Restart restores simulation and audio");
                var flow = Object.FindObjectOfType<CampusRouteAFlow>();
                Check(flow.Node == 1 && flow.Completed.All(x => !x), "Restart clears node progress");
                var inventory = flow.GetComponent<CampusXRInventory>();
                Check(inventory.OwnedTowel == null && flow.GetComponent<CampusXRExtinguisherController>().Owned == null, "Restart clears acquired equipment");
                Check(!Object.FindObjectOfType<CampusWheelchairRescue>().Rescued, "Restart resets wheelchair rescue");
                Check(Vector3.Distance(ui.transform.position, GameObject.Find("Spawn_F04_401").transform.position) < .05f, "Restart returns to 401");
                ui.enabled = false;
                Invoke(ui, "Select", 1); Invoke(ui, "Select", 0);
                Check(GetBool(ui, "controlsGuideVisible") && LanguageSelectionUI.IsStartupBlocking, "Restart retains the controls guide gate");
                Invoke(ui, "Select", 0); Invoke(ui, "OpenPauseMenu"); Invoke(ui, "Select", 2);
                SessionState.SetString(Phase, "exit"); Invoke(ui, "Select", 1);
            }
            catch (Exception e) { Fail(e); }
        }
    }
}
