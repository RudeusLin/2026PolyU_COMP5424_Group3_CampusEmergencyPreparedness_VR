using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CampusEmergency.Player.XR;
using CampusEmergency.Interaction;
using CampusEmergency.Prototype;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    [InitializeOnLoad]
    public static class CampusControllerValidation
    {
        static void Queue<T>(InputControl<T> control, T value) where T : struct
        {
            using (StateEvent.From(control.device, out var state))
            {
                control.WriteValueIntoEvent(value, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }
        const string Pending = "CampusControllerValidation.Pending";
        static CampusControllerValidation()
        {
            if (SessionState.GetBool(Pending, false)) EditorApplication.playModeStateChanged += OnPlay;
        }
        public static void Begin()
        {
            if (!Application.isPlaying) EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            SessionState.SetBool(Pending, true);
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.isPlaying = true;
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.delayCall += () =>
            {
                SessionState.SetBool(Pending, false);
                EditorApplication.playModeStateChanged -= OnPlay;
                try { Validate(); EditorApplication.Exit(0); }
                catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
            };
        }
        static void Check(bool ok, string message)
        {
            File.AppendAllText("Docs/ControllerValidation.txt", (ok ? "PASS: " : "FAIL: ") + message + "\n");
            if (!ok) throw new InvalidOperationException(message);
        }
        static void Tick(object target, string method, params object[] args)
        {
            var type = target.GetType();
            MethodInfo info = null;
            while (type != null && info == null)
            { info = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance); type = type.BaseType; }
            info.Invoke(target, args);
        }
        public static void InstallAndValidate()
        {
            var root = PrefabUtility.LoadPrefabContents(CampusXRSceneBuilder.RigPath);
            try
            {
                CampusXRSceneBuilder.ConfigureControllerVisuals(root);
                PrefabUtility.SaveAsPrefabAsset(root, CampusXRSceneBuilder.RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Validate();
        }
        public static void Validate()
        {
            if (!Application.isPlaying) EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            File.WriteAllText("Docs/ControllerValidation.txt", "Controller input and teleport integration test\n");
            Check(CampusXRInput.ResolveInteractionFocus(true, true, false), "Headset focus works without desktop focus");
            Check(!CampusXRInput.ResolveInteractionFocus(true, false, true), "System menu blocks input despite desktop focus");
            Check(!CampusXRInput.ResolveInteractionFocus(false, false, false), "Desktop fallback respects focus");
            var origin = Object.FindObjectOfType<XROrigin>();
            var input = origin.GetComponent<CampusXRInput>();
            var teleport = origin.GetComponent<CampusTeleportController>();
            var interaction = origin.GetComponent<CampusXRInteractionRay>();
            var provider = origin.GetComponent<TeleportationProvider>();
            var visuals = origin.GetComponentsInChildren<CampusControllerVisual>(true);
            Check(visuals.Length == 2 && visuals.All(v => v.GetComponentsInChildren<Collider>(true).Length == 0),
                "Two saved controller visuals; no colliders obstructing rays");
            var originalSettings = InputSystem.settings;
            var testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings = testSettings;
            InputSystem.RegisterLayout<UnityEngine.XR.OpenXR.Input.HapticControl>("Haptic");
            InputSystem.RegisterLayout(typeof(OculusTouchControllerProfile.OculusTouchController).GetProperty("pointer").PropertyType, "Pose");
            InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>();
            var head = InputSystem.AddDevice<XRHMD>();
            var left = InputSystem.AddDevice<OculusTouchControllerProfile.OculusTouchController>();
            var right = InputSystem.AddDevice<OculusTouchControllerProfile.OculusTouchController>();
            InputSystem.SetDeviceUsage(left, "LeftHand");
            InputSystem.SetDeviceUsage(right, "RightHand");
            GameObject floor = null, doorObject = null;
            try
            {
                input.Actions.Disable();
                Queue(head.isTracked, 1f);
                Queue(left.isTracked, 1f);
                Queue(right.isTracked, 1f);
                InputSystem.Update();
                Tick(input, "OnEnable");
                InputSystem.Update();
                Debug.Log("INPUT_DEBUG raw=" + head.isTracked.ReadValue() + "," + left.isTracked.ReadValue() + "," + right.isTracked.ReadValue()
                    + " actions=" + input.HeadTracked + "," + input.LeftTracked + "," + input.RightTracked
                    + " controls=" + input.Actions.FindAction("Campus/HeadTracked").controls.Count + "," + input.Actions.FindAction("Campus/LeftTracked").controls.Count);
                Check(input.HeadTracked && input.LeftTracked && input.RightTracked,
                    "Already-tracked headset and controllers read immediately after actions enabled");
                Check(input.Actions.FindAction("Campus/LeftPosition").controls.Any(c => c.device == left)
                    && input.Actions.FindAction("Campus/RightPosition").controls.Any(c => c.device == right),
                    "Saved aim bindings resolve to actual Oculus Touch layout");
                Queue(head.centerEyePosition, new Vector3(.2f, 1.65f, .1f));
                Queue(head.centerEyeRotation, Quaternion.identity);
                Queue(head.trackingState, 3);
                Queue(left.pointerPosition, new Vector3(-.2f, 1.2f, .3f));
                Queue(left.pointerRotation, Quaternion.Euler(45, 0, 0));
                Queue(left.trackingState, 3);
                Queue(right.pointerPosition, new Vector3(.3f, 1.2f, .3f));
                Queue(right.pointerRotation, Quaternion.Euler(45, 0, 0));
                Queue(right.trackingState, 3);
                InputSystem.Update();
                foreach (var visual in visuals) Tick(visual, "LateUpdate");
                Check(visuals.All(v => v.GetComponentsInChildren<Renderer>().All(r => r.enabled)),
                    "Tracked controllers become visible");
                origin.transform.SetPositionAndRotation(new Vector3(1000, 0, 1000), Quaternion.identity);
                origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
                origin.Camera.transform.localPosition = input.Actions.FindAction("Campus/HeadPosition").ReadValue<Vector3>();
                origin.Camera.transform.localRotation = Quaternion.identity;
                foreach (string side in new[] { "Left", "Right" })
                {
                    var hand = origin.GetComponentsInChildren<Transform>().Single(t => t.name == side + " Controller Aim");
                    hand.localPosition = input.Actions.FindAction("Campus/" + side + "Position").ReadValue<Vector3>();
                    hand.localRotation = input.Actions.FindAction("Campus/" + side + "Rotation").ReadValue<Quaternion>();
                }
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = new Vector3(1000, -.1f, 1000);
                floor.transform.localScale = new Vector3(20, .2f, 20);
                floor.AddComponent<CampusTeleportSurface>();
                Physics.SyncTransforms();
                var serializedTeleport = new SerializedObject(teleport);
                Check(((Transform)serializedTeleport.FindProperty("rayOrigin").objectReferenceValue).name == "Right Controller Aim",
                    "Saved teleport ray uses RIGHT controller");
                Tick(teleport, "ProcessInput", true);
                Tick(interaction, "ProcessInput", true);
                var ray = origin.GetComponentsInChildren<LineRenderer>().Single(l => l.name == "Interaction Ray");
                var arc = origin.GetComponentsInChildren<LineRenderer>().Single(l => l.name == "Teleport Arc");
                Check(teleport.IsAiming && arc.enabled && arc.positionCount == 2 && !ray.enabled,
                    "Right teleport ray is visible without touching either stick; no overlapping interaction ray");
                Check(teleport.Trace(out var hit, out _, out _), "Right ray selects valid floor");
                var destination = hit.point + Vector3.up * .035f;
                Vector3 before = origin.Camera.transform.position;
                Queue(left.thumbstick, Vector2.up);
                Tick(teleport, "ProcessInput", true);
                Tick(provider, "Update");
                Check(Vector3.Distance(before, origin.Camera.transform.position) < .001f, "Left stick does not teleport");
                Queue(left.isTracked, 0f);
                Queue(right.triggerPressed, 1f);
                Tick(teleport, "ProcessInput", true);
                Tick(provider, "Update");
                var eye = origin.Camera.transform.position;
                Check(Vector2.Distance(new Vector2(eye.x, eye.z), new Vector2(destination.x, destination.z)) < .01f,
                    "RIGHT trigger press moves actual camera to right-ray destination, even without left tracking");
                Check(Vector3.Distance(before, eye) > .1f, "Teleport actually changes camera position");
                typeof(CampusTeleportController).GetField("lastTeleport", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(teleport, -10f);
                Tick(teleport, "ProcessInput", true);
                Tick(provider, "Update");
                Check(Vector3.Distance(eye, origin.Camera.transform.position) < .001f, "Holding trigger cannot repeat teleport after cooldown");
                Queue(right.triggerPressed, 0f);
                Tick(teleport, "ProcessInput", true);
                Tick(provider, "Update");
                Check(Vector3.Distance(eye, origin.Camera.transform.position) < .001f, "Trigger RELEASE does not teleport");

                var rightHand = origin.GetComponentsInChildren<Transform>().Single(t => t.name == "Right Controller Aim");
                doorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                doorObject.transform.position = rightHand.position + rightHand.forward * .8f;
                doorObject.transform.localScale = Vector3.one * .3f;
                var door = doorObject.AddComponent<PrototypeDoor>();
                door.Configure(-100, false);
                Physics.SyncTransforms();
                string closedPrompt = door.Prompt;
                Queue(right.primaryButton, 1f);
                Tick(teleport, "ProcessInput", true);
                Tick(interaction, "ProcessInput", true);
                Tick(provider, "Update");
                Check(door.Prompt != closedPrompt && !teleport.IsAiming && ray.enabled,
                    "A button switches to interaction ray and opens door");
                Check(Vector3.Distance(eye, origin.Camera.transform.position) < .001f, "A interaction never teleports");
                Queue(right.primaryButton, 0f);
                Tick(teleport, "ProcessInput", true);
                Queue(right.triggerPressed, 1f);
                Tick(teleport, "ProcessInput", true);
                Tick(interaction, "ProcessInput", true);
                Tick(provider, "Update");
                Check(door.Prompt != closedPrompt && Vector3.Distance(eye, origin.Camera.transform.position) < .001f,
                    "Trigger aimed at door neither interacts nor teleports through obstacle");
                Object.DestroyImmediate(doorObject);
                doorObject = null;
                Physics.SyncTransforms();
                Tick(teleport, "ProcessInput", false);
                Tick(teleport, "ProcessInput", true);
                Tick(provider, "Update");
                Check(Vector3.Distance(eye, origin.Camera.transform.position) < .001f,
                    "Regaining focus with held trigger cannot teleport");
                CampusExtinguisherValidation.Run(input, right, left);
                CampusFloorSeparationValidation.Run(input);
                CampusXRTowelValidation.Run(input, right, left);
                var hud = Object.FindObjectOfType<CampusXRFeedback>();
                hud.SetVisible(true);
                Queue(left.isTracked, 1f); Queue(left.primaryButton, 0f);
                Queue(left.primaryButton, 1f); Tick(hud, "ProcessToggle", true);
                Check(!hud.IsVisible && hud.gameObject.activeInHierarchy, "Left X hides HUD without disabling its input host");
                Queue(left.primaryButton, 0f); Tick(hud, "ProcessToggle", true);
                Queue(left.primaryButton, 1f); Tick(hud, "ProcessToggle", true);
                Check(hud.IsVisible, "Left X restores hidden HUD");
                Queue(left.primaryButton, 0f);
                var hudKeyboard = InputSystem.AddDevice<Keyboard>();
                try
                {
                    Queue(hudKeyboard.hKey, 1f); Tick(hud, "ProcessToggle", true);
                    Check(!hud.IsVisible, "Keyboard H hides HUD");
                    Queue(hudKeyboard.hKey, 0f); Tick(hud, "ProcessToggle", true);
                    Queue(hudKeyboard.hKey, 1f); Tick(hud, "ProcessToggle", true);
                    Check(hud.IsVisible, "Keyboard H restores HUD");
                }
                finally { InputSystem.RemoveDevice(hudKeyboard); }
                Queue(left.isTracked, 0f);
                Queue(right.isTracked, 0f);
                InputSystem.Update();
                Tick(teleport, "ProcessInput", true);
                Tick(interaction, "ProcessInput", true);
                foreach (var visual in visuals) Tick(visual, "LateUpdate");
                Check(!ray.enabled && !arc.enabled && visuals.All(v => v.GetComponentsInChildren<Renderer>().All(r => !r.enabled)),
                    "Tracking loss hides controllers and rays safely");
            }
            finally
            {
                input.Actions.Disable();
                if (floor != null) Object.DestroyImmediate(floor);
                if (doorObject != null) Object.DestroyImmediate(doorObject);
                InputSystem.RemoveDevice(head); InputSystem.RemoveDevice(left); InputSystem.RemoveDevice(right);
                InputSystem.settings = originalSettings;
                Object.DestroyImmediate(testSettings);
                // Discard synthetic device poses; never save the test scene.
                if (!Application.isPlaying) EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            }
            if (!Application.isPlaying) CampusXRTeleportValidation.ValidateScene();
            File.AppendAllText("Docs/ControllerValidation.txt", "Simulated Input System devices + real teleport provider. Hardware NOT RUN.\nRESULT: PASS\n");
            Debug.Log("CAMPUS_CONTROLLER_VALIDATION_PASS");
        }
    }
}
