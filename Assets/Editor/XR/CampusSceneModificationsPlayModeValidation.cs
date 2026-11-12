using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CampusEmergency.Interaction;
using CampusEmergency.Player.XR;
using CampusEmergency.Prototype;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    [InitializeOnLoad]
    public static class CampusSceneModificationsPlayModeValidation
    {
        private const string Pending = "CampusSceneModifications.InteractionChecks";

        static CampusSceneModificationsPlayModeValidation()
        {
            if (SessionState.GetBool(Pending, false)) EditorApplication.playModeStateChanged += OnPlay;
        }

        [MenuItem("Campus Emergency/XR/Validate modified scene interactions in Play mode")]
        public static void Begin()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            SessionState.SetBool(Pending, true);
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.delayCall += () =>
            {
                SessionState.SetBool(Pending, false);
                EditorApplication.playModeStateChanged -= OnPlay;
                bool success = false;
                try { Run(); success = true; }
                catch (Exception exception) { Debug.LogException(exception); }
                if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
                else EditorApplication.isPlaying = false;
            };
        }

        private static void Run()
        {
            var lines = new List<string> { "Saved modified XR scene - Play mode interaction checks", "Unity " + Application.unityVersion };
            Action<bool, string> check = (ok, message) =>
            {
                lines.Add((ok ? "PASS: " : "FAIL: ") + message);
                File.WriteAllLines(CampusSceneModifications.PreviewFolder + "/InteractionValidation.txt", lines);
                if (!ok) throw new InvalidOperationException(message);
            };
            var inventory = Object.FindObjectOfType<CampusXRInventory>();
            foreach (var faucet in Object.FindObjectsOfType<CampusFaucet>().OrderBy(f => f.transform.position.y))
            {
                var towel = faucet.transform.parent.GetComponentInChildren<CampusTowel>();
                var home = towel.transform.parent;
                Vector3 homePosition = towel.transform.localPosition;
                string floor = "F" + (1 + Mathf.RoundToInt(faucet.transform.position.y / 3.6f));
                towel.Interact(inventory);
                check(inventory.HeldTowel == towel && towel.transform.parent == inventory.TowelHoldPoint
                    && towel.GetComponentsInChildren<Collider>().All(c => !c.enabled), floor + " towel pickup attaches to the existing XR hand and disables held collision");
                faucet.Interact(inventory);
                check(towel.IsWet && faucet.IsRunning, floor + " actual faucet interaction wets the held towel");
                faucet.Interact(inventory);
                inventory.ReturnHeldTowel();
                check(!faucet.IsRunning && !towel.IsWet && inventory.HeldTowel == null && towel.transform.parent == home
                    && Vector3.Distance(towel.transform.localPosition, homePosition) < .001f
                    && towel.GetComponentsInChildren<Collider>().All(c => c.enabled), floor + " tap stops and towel returns dry to its original position");
            }
            TestRightAAlarm(check);
            lines.Add("RESULT: PASS. Synthetic controllers in Unity Play mode; physical headset testing NOT RUN.");
            File.WriteAllLines(CampusSceneModifications.PreviewFolder + "/InteractionValidation.txt", lines);
            Debug.Log("CAMPUS_MODIFIED_INTERACTIONS_PASS\n" + string.Join("\n", lines));
        }

        private static void Queue<T>(InputControl<T> control, T value) where T : struct
        {
            using (StateEvent.From(control.device, out var state))
            {
                control.WriteValueIntoEvent(value, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }

        private static void TestRightAAlarm(Action<bool, string> check)
        {
            var originalSettings = InputSystem.settings;
            var settings = Object.Instantiate(originalSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings = settings;
            InputSystem.RegisterLayout<UnityEngine.XR.OpenXR.Input.HapticControl>("Haptic");
            InputSystem.RegisterLayout(typeof(OculusTouchControllerProfile.OculusTouchController).GetProperty("pointer").PropertyType, "Pose");
            InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>();
            var head = InputSystem.AddDevice<XRHMD>();
            var right = InputSystem.AddDevice<OculusTouchControllerProfile.OculusTouchController>();
            InputSystem.SetDeviceUsage(right, "RightHand");
            try
            {
                var input = Object.FindObjectOfType<CampusXRInput>();
                input.Actions.Disable();
                Queue(head.isTracked, 1f);
                Queue(right.isTracked, 1f);
                typeof(CampusXRInput).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, null);
                InputSystem.Update();
                check(input.RightTracked, "Saved input asset resolves the simulated right Quest controller");
                var origin = input.GetComponent<XROrigin>();
                var alarm = GameObject.Find("Floor_04").transform.Find("Alarm_401").GetComponentInChildren<FacilityNotice>();
                origin.transform.position = alarm.transform.position + new Vector3(0, -1.3f, 1.2f);
                origin.Camera.transform.position = origin.transform.position + Vector3.up * 1.65f;
                var interaction = input.GetComponent<CampusXRInteractionRay>();
                Transform ray = (Transform)new SerializedObject(interaction).FindProperty("rayOrigin").objectReferenceValue;
                ray.position = alarm.transform.position + Vector3.forward * 1.2f;
                ray.rotation = Quaternion.LookRotation(alarm.transform.position - ray.position);
                Physics.SyncTransforms();
                input.GetComponent<CampusTeleportController>().Cancel();
                var process = typeof(CampusXRInteractionRay).GetMethod("ProcessInput", BindingFlags.Instance | BindingFlags.NonPublic);
                Queue(right.primaryButton, 0f);
                process.Invoke(interaction, new object[] { true });
                Queue(right.primaryButton, 1f);
                check(input.InteractPressed, "Right A produces the saved interaction action");
                process.Invoke(interaction, new object[] { true });
                var atmosphere = Object.FindObjectOfType<CampusDrillAtmosphere>(true);
                check(atmosphere != null && atmosphere.IsAlarmActive, "Right A ray hits the relocated 401 button and starts the existing alarm audio");
            }
            finally
            {
                InputSystem.RemoveDevice(right);
                InputSystem.RemoveDevice(head);
                InputSystem.settings = originalSettings;
                Object.DestroyImmediate(settings);
            }
        }
    }
}
