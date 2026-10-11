using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using CampusEmergency.Prototype;
using CampusEmergency.Player.XR;
using CampusEmergency.Interaction;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    public static class CampusXRTeleportValidation
    {
        [MenuItem("Campus Emergency/XR/Validate campus teleport scene")]
        public static void ValidateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if (EditorSceneManager.GetActiveScene().path != CampusXRSceneBuilder.XRScene)
                EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            var lines = new List<string> { "Campus XR teleport validation", "UTC: " + DateTime.UtcNow.ToString("O") };
            Action<bool, string> check = (ok, message) => {
                lines.Add((ok ? "PASS: " : "FAIL: ") + message);
                if (!ok) { File.WriteAllLines("Docs/XRTeleportValidation.txt", lines); throw new InvalidOperationException(message); }
            };
            var origin = Object.FindObjectOfType<XROrigin>();
            check(origin != null, "XR Origin is active");
            check(Object.FindObjectsOfType<Camera>().Count(c => c.enabled) == 1, "Exactly one enabled camera");
            check(Object.FindObjectsOfType<AudioListener>().Count(c => c.enabled) == 1, "Exactly one enabled audio listener");
            check(Object.FindObjectsOfType<PrototypeExplorer>().Length == 0, "Desktop player inactive in XR scene");
            check(Object.FindObjectsOfType<TrackedPoseDriver>().Length == 3, "Tracked HMD and both controller poses");
            check(Object.FindObjectsOfType<TrackedPoseDriver>().All(d => d.positionInput.action != null
                && d.rotationInput.action != null && d.trackingStateInput.action != null), "Saved tracking input references resolve");
            check(Object.FindObjectOfType<CampusXRInteractionRay>() != null, "Right controller interaction adapter present");
            var input = Object.FindObjectOfType<CampusXRInput>();
            check(input != null && input.Actions != null && input.Actions.FindAction("Campus/Teleport") != null
                && input.Actions.FindAction("Campus/Interact") != null, "Persistent teleport and interaction input references");
            var serializedSession = new SerializedObject(Object.FindObjectOfType<CampusXRSession>());
            check(serializedSession.FindProperty("spawn").objectReferenceValue != null
                && serializedSession.FindProperty("assembly").objectReferenceValue != null, "Scene spawn and assembly references saved");
            var surfaces = Object.FindObjectsOfType<CampusTeleportSurface>();
            check(surfaces.Length > 150 && surfaces.All(s => CampusXRSceneBuilder.IsWalkableName(s.name)),
                "Only structural walkable colliders marked (" + surfaces.Length + ")");
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                check(go.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
                    "No missing scripts in " + go.name);
            var avatar = origin.GetComponentInChildren<CampusStudentAvatar>(true);
            check(avatar != null && avatar.GetComponentsInChildren<Collider>(true).Length == 0, "Original visual avatar retained without extra collision");
            var validator = origin.GetComponent<CampusTeleportValidator>();
            Physics.SyncTransforms();
            int validCampusPoints = 0;
            foreach (var surface in surfaces.Where(s => s.name == "FloorSlab" || s.name == "Landing"))
            {
                var bounds = surface.GetComponent<Collider>().bounds;
                if (Physics.Raycast(new Vector3(bounds.center.x, bounds.max.y + .15f, bounds.center.z), Vector3.down, out var hit, .3f)
                    && validator.IsValid(hit, hit.point, 1.65f, out _)) validCampusPoints++;
            }
            check(validCampusPoints >= 5, "Clear destinations available in campus floors/landings (" + validCampusPoints + ")");
            TestGeometry(validator, origin, check);
            TestProvider(origin, check);
            lines.Add("Headset/controller hardware test: NOT RUN. These are editor physics and real XRI provider tick checks.");
            lines.Add("RESULT: PASS");
            File.WriteAllLines("Docs/XRTeleportValidation.txt", lines);
            Debug.Log("CAMPUS_XR_TELEPORT_VALIDATION_PASS\n" + string.Join("\n", lines));
        }

        static GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;
            return go;
        }

        static void TestGeometry(CampusTeleportValidator validator, XROrigin origin, Action<bool,string> check)
        {
            var objects = new List<GameObject>();
            Vector3 center = new Vector3(1000, 0, 1000);
            Vector3 previousEye = origin.Camera.transform.position;
            try
            {
                var floor = Box("Validation Floor", center + Vector3.down * .1f, new Vector3(6, .2f, 6));
                objects.Add(floor);
                floor.AddComponent<CampusTeleportSurface>();
                Physics.SyncTransforms();
                Physics.Raycast(center + Vector3.up, Vector3.down, out var hit, 2);
                check(validator.IsValid(hit, center, 1.65f, out _), "Clear floor accepted");
                check(!validator.IsValid(hit, center + Vector3.right * 20, 1.65f, out _), "Out-of-range target rejected");
                check(!validator.IsValid(hit, center + Vector3.up * 5, 1.65f, out _), "Excessive floor-height change rejected");
                var obstacle = Box("Validation Obstacle", center + Vector3.up, new Vector3(.5f, 2, .5f));
                objects.Add(obstacle);
                Physics.SyncTransforms();
                check(!validator.HasClearance(center, 1.65f, out _), "Occupied destination rejected");
                obstacle.transform.position = center + Vector3.up * 1.2f;
                obstacle.transform.localScale = new Vector3(1, .1f, 1);
                Physics.SyncTransforms();
                check(!validator.HasClearance(center, 1.65f, out _), "Low overhead clearance rejected");
                obstacle.SetActive(false);
                Physics.SyncTransforms();
                check(!validator.HasClearance(center + Vector3.right * 2.97f, 1.65f, out _), "Unsupported floor edge rejected");
                var table = Box("Validation Table", center + Vector3.up * .8f, new Vector3(1, .1f, 1));
                objects.Add(table);
                Physics.SyncTransforms();
                Physics.Raycast(center + Vector3.up * 2, Vector3.down, out var tableHit, 3);
                check(!validator.IsValid(tableHit, center, 1.65f, out _), "Furniture not in whitelist rejected");
                table.SetActive(false);
                var wall = Box("Validation Closed Door", center + new Vector3(0, 1, 1.8f), new Vector3(3, 2, .1f));
                objects.Add(wall);
                var ray = new GameObject("Validation Ray"); objects.Add(ray);
                ray.transform.position = center + Vector3.up * 1.3f;
                origin.Camera.transform.position = center + new Vector3(0, 1.6f, -.2f);
                var tester = new GameObject("Validation Teleport"); objects.Add(tester);
                var controller = tester.AddComponent<CampusTeleportController>();
                controller.Configure(null, origin, ray.transform, null, validator, null, null);
                Physics.SyncTransforms();
                check(!controller.Trace(out var blockedHit, out _, out _) && blockedHit.collider == wall.GetComponent<Collider>(),
                    "Ray stops at first wall/closed door, never selects floor behind it");
                ray.transform.position = center + new Vector3(0, 1.3f, 2f);
                check(!controller.Trace(out _, out _, out _), "Controller reaching through a wall cannot teleport behind it");
                ray.transform.position = center + new Vector3(0, 1.3f, 1.8f);
                check(!controller.Trace(out _, out _, out _), "Ray starting inside a collider rejected");
            }
            finally { origin.Camera.transform.position = previousEye; foreach (var go in objects) Object.DestroyImmediate(go); Physics.SyncTransforms(); }
        }

        static void TestProvider(XROrigin origin, Action<bool,string> check)
        {
            var provider = origin.GetComponent<TeleportationProvider>();
            var head = origin.Camera.transform;
            Vector3 oldPosition = origin.transform.position, oldHead = head.localPosition;
            Quaternion oldRotation = origin.transform.rotation;
            try
            {
                origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                head.localPosition = new Vector3(.45f, 1.65f, -.3f);
                Vector3 target = new Vector3(7, 3.6f, -5);
                float height = origin.CameraInOriginSpaceHeight;
                provider.QueueTeleportRequest(new TeleportRequest {
                    destinationPosition = target, destinationRotation = Quaternion.identity,
                    matchOrientation = MatchOrientation.WorldSpaceUp });
                typeof(TeleportationProvider).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(provider, null);
                check(Vector3.Distance(head.position, target + Vector3.up * height) < .001f,
                    "Real XRI provider compensates room-scale head offset and preserves eye height");
                check(!provider.system.busy, "Locomotion lock released after teleport");
            }
            finally { origin.transform.SetPositionAndRotation(oldPosition, oldRotation); head.localPosition = oldHead; }
        }
    }
}
