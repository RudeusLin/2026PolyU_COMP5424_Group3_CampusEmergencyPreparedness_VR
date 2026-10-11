using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using CampusEmergency.Prototype;
using CampusEmergency.Player.XR;
using CampusEmergency.Interaction;

namespace CampusEmergency.Editor
{
    /// <summary>Rebuilds only the XR derivative; source campus geometry and lighting stay unchanged.</summary>
    public static class CampusXRSceneBuilder
    {
        public const string SourceScene = "Assets/Scenes/TeachingBuildingCampus.unity";
        public const string XRScene = "Assets/Scenes/TeachingBuildingCampusXR.unity";
        public const string RigPath = "Assets/XR/Campus/Prefabs/CampusXROrigin.prefab";
        public const string InputPath = "Assets/XR/Campus/Input/CampusXR.inputactions";

        [MenuItem("Campus Emergency/XR/Build campus XR scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(SourceScene);
            EnsureFolder("Assets/XR/Campus/Input");
            EnsureFolder("Assets/XR/Campus/Prefabs");
            EnsureFolder("Assets/XR/Campus/Materials");
            var spawn = GameObject.Find("Spawn_F04_401").transform;
            var desktop = UnityEngine.Object.FindObjectOfType<PrototypeExplorer>();
            if (desktop == null) throw new InvalidOperationException("Desktop source player not found.");
            var serialized = new SerializedObject(desktop);
            var assembly = (Transform)serialized.FindProperty("assemblyPoint").objectReferenceValue;
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) camera.enabled = false;
            foreach (var listener in UnityEngine.Object.FindObjectsOfType<AudioListener>()) listener.enabled = false;
            desktop.gameObject.SetActive(false);
            foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>())
                if (IsWalkableName(collider.name) && !collider.isTrigger && collider.GetComponent<CampusTeleportSurface>() == null)
                    collider.gameObject.AddComponent<CampusTeleportSurface>();

            var inputAsset = BuildInput();
            var root = new GameObject("Campus XR Origin");
            root.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var origin = root.AddComponent<XROrigin>();
            origin.Origin = root;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.65f;
            var offset = Child(root.transform, "Camera Floor Offset");
            origin.CameraFloorOffsetObject = offset;
            var head = Child(offset.transform, "XR Head Camera");
            head.tag = "MainCamera";
            var xrCamera = head.AddComponent<Camera>();
            xrCamera.nearClipPlane = .05f;
            xrCamera.farClipPlane = 180;
            xrCamera.clearFlags = CameraClearFlags.Skybox;
            head.AddComponent<AudioListener>();
            origin.Camera = xrCamera;
            AddPose(head, inputAsset, "Head");
            var left = Child(offset.transform, "Left Controller Aim");
            AddPose(left, inputAsset, "Left");
            var right = Child(offset.transform, "Right Controller Aim");
            AddPose(right, inputAsset, "Right");
            var input = root.AddComponent<CampusXRInput>();
            input.Configure(inputAsset);
            var locomotion = root.AddComponent<LocomotionSystem>();
            locomotion.xrOrigin = origin;
            var provider = root.AddComponent<TeleportationProvider>();
            provider.system = locomotion;
            provider.delayTime = 0;
            var validator = root.AddComponent<CampusTeleportValidator>();
            var feedback = BuildHUD(head.transform);
            var visual = root.AddComponent<CampusTeleportVisual>();
            visual.Configure(Line(root.transform, "Teleport Arc", .012f), Line(root.transform, "Teleport Landing Ring", .015f));
            var teleport = root.AddComponent<CampusTeleportController>();
            teleport.Configure(input, origin, right.transform, provider, validator, visual, feedback);
            var turn = root.AddComponent<CampusSnapTurn>();
            turn.system = locomotion;
            turn.Configure(input, teleport);
            var interact = root.AddComponent<CampusXRInteractionRay>();
            interact.Configure(input, teleport, right.transform, Line(root.transform, "Interaction Ray", .006f), feedback);

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/TeachingBuildingCampus/Prefabs/CampusStudent.prefab");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
            var avatar = model.GetComponent<CampusStudentAvatar>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            root.AddComponent<CampusXRAvatar>().Configure(origin, avatar);
            ConfigureInventory(root);
            ConfigureControllerVisuals(root);
            CampusExtinguisherSetup.ConfigureScene();
            var session = root.AddComponent<CampusXRSession>();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, RigPath, InteractionMode.AutomatedAction);
            // Scene anchors deliberately live on the instance, never inside the reusable rig prefab.
            session.Configure(origin, input, spawn, assembly, feedback);
            EditorUtility.SetDirty(session);
            PrefabUtility.RecordPrefabInstancePropertyModifications(session);
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), XRScene))
                throw new InvalidOperationException("Could not save XR scene.");
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != XRScene).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(XRScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            CampusXRTeleportValidation.ValidateScene();
            Debug.Log("CAMPUS_XR_SCENE_BUILD_PASS");
        }

        public static void ConfigureInventory(GameObject root)
        {
            var hand = root.GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == "Right Controller Aim");
            var point = hand.Find("Towel Hold Point");
            if (point == null)
            {
                point = new GameObject("Towel Hold Point").transform;
                point.SetParent(hand, false);
                point.localPosition = new Vector3(0, -.04f, .12f);
                point.localRotation = Quaternion.Euler(90, 0, 0);
            }
            var inventory = root.GetComponent<CampusXRInventory>();
            if (inventory == null) inventory = root.AddComponent<CampusXRInventory>();
            inventory.Configure(root.GetComponent<CampusXRInput>(), root.GetComponent<CampusTeleportController>(),
                point, root.GetComponentInChildren<CampusXRFeedback>(true));
        }

        public static void ConfigureControllerVisuals(GameObject root)
        {
            foreach (bool left in new[] { true, false })
            {
                var hand = root.GetComponentsInChildren<Transform>(true)
                    .Single(t => t.name == (left ? "Left Controller Aim" : "Right Controller Aim"));
                if (hand.Find("Controller Visual") != null) continue;
                string materialPath = "Assets/XR/Campus/Materials/" + (left ? "LeftController" : "RightController") + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Unlit/Color"));
                    material.color = left ? new Color(.12f, .8f, 1f) : new Color(1f, .65f, .15f);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                var holder = Child(hand, "Controller Visual");
                foreach (bool tip in new[] { false, true })
                {
                    var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    part.name = tip ? "Aim Tip" : "Controller Body";
                    part.transform.SetParent(holder.transform, false);
                    part.transform.localPosition = tip ? new Vector3(0, 0, .07f) : new Vector3(0, -.025f, -.02f);
                    part.transform.localScale = tip ? Vector3.one * .022f : new Vector3(.045f, .075f, .11f);
                    UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                    part.GetComponent<Renderer>().sharedMaterial = material;
                    part.GetComponent<Renderer>().enabled = false;
                }
                holder.AddComponent<CampusControllerVisual>().Configure(root.GetComponent<CampusXRInput>(), left);
            }
        }

        public static bool IsWalkableName(string name) =>
            name == "FloorSlab" || name == "Ground" || name == "SideExitPath"
            || name == "Landing" || name == "HalfLanding"
            || name.StartsWith("Step_First_", StringComparison.Ordinal)
            || name.StartsWith("Step_Second_", StringComparison.Ordinal);

        static InputActionAsset BuildInput()
        {
            // Keep action GUIDs stable once created; rebuilding a scene must not invalidate references.
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "CampusXR";
            var map = new InputActionMap("Campus");
            asset.AddActionMap(map);
            Add(map, "HeadPosition", "<XRHMD>/centerEyePosition", "Vector3");
            Add(map, "HeadRotation", "<XRHMD>/centerEyeRotation", "Quaternion");
            Add(map, "HeadTrackingState", "<XRHMD>/trackingState", "Integer");
            Add(map, "HeadTracked", "<XRHMD>/isTracked", "Button", InputActionType.Button);
            foreach (string side in new[] { "Left", "Right" })
            {
                string path = "<XRController>{" + side + "Hand}/";
                Add(map, side + "Position", path + "pointerPosition", "Vector3");
                Add(map, side + "Rotation", path + "pointerRotation", "Quaternion");
                Add(map, side + "TrackingState", path + "trackingState", "Integer");
                Add(map, side + "Tracked", path + "isTracked", "Button", InputActionType.Button);
            }
            Add(map, "Teleport", "<XRController>{RightHand}/triggerPressed", "Button", InputActionType.Button);
            Add(map, "CancelTeleport", "<XRController>{LeftHand}/gripPressed", "Button", InputActionType.Button);
            Add(map, "Turn", "<XRController>{RightHand}/primary2DAxis", "Vector2", InputActionType.Value);
            Add(map, "Interact", "<XRController>{RightHand}/primaryButton", "Button", InputActionType.Button);
            File.WriteAllText(InputPath, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        }

        static void Add(InputActionMap map, string name, string binding, string control, InputActionType type = InputActionType.PassThrough) =>
            map.AddAction(name, type, binding).expectedControlType = control;

        static void AddPose(GameObject go, InputActionAsset asset, string prefix)
        {
            var driver = go.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.positionInput = new InputActionProperty(Ref(asset, prefix + "Position"));
            driver.rotationInput = new InputActionProperty(Ref(asset, prefix + "Rotation"));
            driver.trackingStateInput = new InputActionProperty(Ref(asset, prefix + "TrackingState"));
        }

        static InputActionReference Ref(InputActionAsset asset, string name)
        {
            var action = asset.FindAction("Campus/" + name, true);
            var reference = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.id == action.id);
            if (reference == null) throw new InvalidOperationException("Input action reference missing: " + name);
            return reference;
        }

        static CampusXRFeedback BuildHUD(Transform head)
        {
            var go = new GameObject("XR Status", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0, -.23f, .85f);
            go.transform.localScale = Vector3.one * .001f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = head.GetComponent<Camera>();
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(780, 150);
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(go.transform, false);
            Stretch((RectTransform)panel.transform);
            panel.GetComponent<Image>().color = new Color(.015f, .035f, .055f, .82f);
            panel.GetComponent<Image>().raycastTarget = false;
            var label = new GameObject("Instructions", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            Stretch((RectTransform)label.transform);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 23;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            var feedback = go.AddComponent<CampusXRFeedback>();
            feedback.Configure(text);
            return feedback;
        }

        static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(12, 8); rect.offsetMax = new Vector2(-12, -8); }

        static LineRenderer Line(Transform parent, string name, float width)
        {
            const string path = "Assets/XR/Campus/Materials/Ray.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }
            var line = Child(parent, name).AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        static GameObject Child(Transform parent, string name)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
