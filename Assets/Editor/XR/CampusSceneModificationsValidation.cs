using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusEmergency.Interaction.FireSafety;
using CampusEmergency.Player.XR;
using CampusEmergency.Prototype;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    public static class CampusSceneModificationsValidation
    {
        [MenuItem("Campus Emergency/XR/Validate scene modeling modifications")]
        public static void ValidateSavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            Validate(true);
        }

        internal static void Validate(bool allClassrooms)
        {
            var lines = new List<string> { "October scene modifications", "Unity " + Application.unityVersion,
                "Scene: " + CampusXRSceneBuilder.XRScene, "Classrooms: " + (allClassrooms ? "all" : "401 sample") };
            Action<bool, string> check = (ok, message) =>
            {
                lines.Add((ok ? "PASS: " : "FAIL: ") + message);
                File.WriteAllLines(CampusSceneModifications.PreviewFolder + "/Validation.txt", lines);
                if (!ok) throw new InvalidOperationException(message);
            };
            Physics.SyncTransforms();
            var validator = Object.FindObjectOfType<CampusTeleportValidator>();
            var origin = Object.FindObjectOfType<XROrigin>();
            var controller = Object.FindObjectOfType<CampusTeleportController>();
            check(validator != null && origin != null && controller != null, "Existing XR rig and teleport components retained");
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            check(roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts");
            foreach (int floor in new[] { 1, 2, 3, 4 })
            {
                Transform level = GameObject.Find("Floor_" + floor.ToString("00")).transform;
                foreach (string side in new[] { "A", "B" })
                {
                    var label = level.Find("CorridorEnd_" + side);
                    var renderer = label.GetComponentInChildren<Renderer>();
                    check(renderer.sharedMaterial.mainTexture.name == "Corridor-" + side && renderer.bounds.size.y > 1.2f,
                        "F" + floor + " " + side + " large readable end letter (height " + renderer.bounds.size.y + ")");
                    Vector3 eye = new Vector3(0, level.position.y + 1.65f, 0);
                    check(Vector3.Dot(-label.forward, eye - label.position) > 0, "F" + floor + " " + side + " letter faces corridor");
                    check(!Physics.Linecast(eye, label.position, validator.EnvironmentMask, QueryTriggerInteraction.Ignore),
                        "F" + floor + " " + side + " letter visible from corridor center");
                }
                var equipment = level.Cast<Transform>().Where(t => t.name == "FireEquipmentPoint").ToArray();
                check(equipment.Length == (floor >= 3 ? 1 : 2) && equipment.Any(t => t.localPosition.x > 0)
                    && (floor < 3 || equipment.All(t => t.localPosition.x > 0)), "F" + floor + " correct A/B fire equipment distribution");
                var room = level.Find(floor == 4 ? "Restrooms_F04" : "Restrooms");
                var station = room.Find("WashStation");
                var towel = station.GetComponentInChildren<CampusTowel>();
                var faucet = station.GetComponentInChildren<CampusFaucet>();
                check(towel != null && faucet != null && towel.GetComponentsInChildren<Collider>().Any(c => c.enabled),
                    "F" + floor + " original towel/tap interactions and pickup collider retained");
                check(Vector3.Distance(towel.transform.localPosition, new Vector3(-.65f, .974f, -.015f)) < .001f
                    && Vector3.Distance(faucet.transform.localPosition, new Vector3(.25f, .93f, .24f)) < .001f,
                    "F" + floor + " towel/tap interaction positions unchanged");
                check(new SerializedObject(faucet).FindProperty("waterFlow").objectReferenceValue != null,
                    "F" + floor + " tap water reference resolves");
                check(station.Find("WashStation_FinishDetails") != null && room.Cast<Transform>().Count(t => t.name.StartsWith("CubicleFinish_")) == 2,
                    "F" + floor + " wash station and both cubicles refined");
            }
            check(Object.FindObjectsOfType<CampusExtinguisher>().Length == 6, "Six complete usable extinguishers remain");
            check(Object.FindObjectsOfType<FacilityNotice>().Count(n => n.Kind == FacilityNotice.FacilityKind.Alarm) == 8,
                "All eight alarms retained");
            ValidateAlarm(check);
            ValidateClassrooms(allClassrooms, validator, check);
            ValidateStairs(validator, check);
            // Real controller traces use the saved ray mask, obstruction checks and validator.
            ValidateTraces(origin, controller, check);
            lines.Add("Hardware headset readability/controller input: NOT RUN.");
            lines.Add("Structural trace checks temporarily disabled existing fire exclusion volumes and restored their original states.");
            lines.Add("B stair F02-F01 is intentionally impassable. Existing static route guidance is unchanged.");
            lines.Add("RESULT: PASS");
            File.WriteAllLines(CampusSceneModifications.PreviewFolder + "/Validation.txt", lines);
            Debug.Log("CAMPUS_SCENE_MODIFICATIONS_VALIDATION_PASS\n" + string.Join("\n", lines));
        }

        private static void ValidateAlarm(Action<bool, string> check)
        {
            Transform level = GameObject.Find("Floor_04").transform;
            var group = level.Find("Alarm_401");
            check(group != null && group.GetComponentInChildren<FacilityNotice>().Kind == FacilityNotice.FacilityKind.Alarm
                && group.Find("AlarmPressPanel") != null && group.Find("Sign_FIRE ALARM") != null,
                "401 contains the original complete alarm assembly");
            check(group.GetComponentInChildren<Collider>() != null && Mathf.Abs(group.position.z - 1.66f) < .001f
                && group.position.x > -7.05f, "401 alarm on interior wall to the left when facing the exit");
            var doors = Object.FindObjectsOfType<PrototypeDoor>();
            var rotations = doors.Select(d => d.transform.localRotation).ToArray();
            try
            {
                for (int i = 0; i < doors.Length; i++) doors[i].transform.localRotation = Quaternion.Euler(0, doors[i].OpenAngle, 0);
                Physics.SyncTransforms();
                Vector3 eye = group.position + new Vector3(0, .15f, 1.2f);
                check(Physics.Raycast(eye, (group.position - eye).normalized, out var hit, 1.5f)
                    && hit.collider.GetComponentInParent<FacilityNotice>()?.Kind == FacilityNotice.FacilityKind.Alarm,
                    "Open doors do not obscure the 401 alarm interaction collider");
            }
            finally
            {
                for (int i = 0; i < doors.Length; i++) doors[i].transform.localRotation = rotations[i];
                Physics.SyncTransforms();
            }
        }

        private static void ValidateClassrooms(bool all, CampusTeleportValidator validator, Action<bool, string> check)
        {
            var rooms = Object.FindObjectsOfType<Transform>().Where(t => t.name.StartsWith("Classroom_")
                && (all || (t.parent.name == "Floor_04" && t.localPosition.x < 0))).ToArray();
            check(rooms.Length == (all ? 8 : 1), "Expected classroom template scope");
            foreach (var room in rooms)
            {
                string name = room.parent.name + "/" + room.name + (room.localPosition.x < 0 ? " west" : " east");
                var desks = room.Cast<Transform>().Where(t => t.name == "DeskAndChair").ToArray();
                check(desks.Length == 6 && desks.All(d => d.Find("ChairSeat") != null && d.Find("Desktop") != null), name + " six complete desk/chair groups");
                check(desks.Select(t => Mathf.Round(t.localPosition.x * 100)).Distinct().Count() == 3
                    && desks.Select(t => Mathf.Round(t.localPosition.z * 100)).Distinct().Count() == 2, name + " two columns and three rows");
                for (int i = 0; i <= 12; i++)
                {
                    Vector3 point = room.TransformPoint(new Vector3(-3.2f + i * .5f, .025f, .375f));
                    check(validator.HasClearance(point, 1.65f, out _), name + " center aisle clear at sample " + i);
                }
            }
        }

        private static void ValidateStairs(CampusTeleportValidator validator, Action<bool, string> check)
        {
            var broken = GameObject.Find("Stair_B_F01").transform;
            check(Enumerable.Range(4, 6).All(i => broken.Find("Step_Second_" + i) == null), "Six broken treads and their colliders removed");
            var guard = broken.Find("BrokenFlight_TeleportBarrier").GetComponent<BoxCollider>();
            check(guard.enabled && !guard.isTrigger && guard.GetComponent<Renderer>() == null
                && guard.GetComponent<CampusTeleportSurface>() == null && (validator.EnvironmentMask.value & (1 << guard.gameObject.layer)) != 0,
                "Broken-flight barrier is invisible, solid, ray-visible and not a teleport destination");
            check(!Physics.Raycast(new Vector3(16.58f, 3.2f, 0), Vector3.down, out var gap, .8f)
                || gap.collider.GetComponent<CampusTeleportSurface>() == null, "No invisible walking surface across the broken span");
            foreach (string side in new[] { "A", "B" })
            {
                var stairs = GameObject.Find("Stairwell_" + side).transform;
                var guards = stairs.Find("Stairwell_InvisibleGuards").GetComponentsInChildren<BoxCollider>();
                check(guards.Length == 3 && guards.All(c => c.enabled && !c.isTrigger && c.GetComponent<Renderer>() == null
                    && c.GetComponent<CampusTeleportSurface>() == null && (validator.EnvironmentMask.value & (1 << c.gameObject.layer)) != 0),
                    side + " guards block the actual teleport mask without visible geometry");
                // Check turning space at every full and half landing on both stairs.
                foreach (Transform landing in stairs.GetComponentsInChildren<Transform>().Where(t => t.name == "Landing" || t.name == "HalfLanding"))
                {
                    float top = landing.GetComponent<Collider>().bounds.max.y;
                    Vector3 point = new Vector3(landing.position.x, top + .025f, landing.position.z);
                    check(validator.HasClearance(point, 1.65f, out _), side + " " + landing.parent.name + "/" + landing.name + " turning space remains clear");
                }
            }
        }

        private static void ValidateTraces(XROrigin origin, CampusTeleportController controller, Action<bool, string> check)
        {
            Transform head = origin.Camera.transform;
            Transform ray = (Transform)new SerializedObject(controller).FindProperty("rayOrigin").objectReferenceValue;
            Vector3 oldRoot = origin.transform.position, oldHead = head.position, oldRay = ray.position;
            Quaternion oldRayRotation = ray.rotation;
            var fireBarriers = Object.FindObjectsOfType<CampusFireBarrier>();
            bool[] fireStates = fireBarriers.Select(f => f.enabled).ToArray();
            bool Trace(Vector3 feet, Vector3 destination)
            {
                origin.transform.position = feet;
                head.position = feet + Vector3.up * 1.65f;
                ray.position = feet + new Vector3(0, 1.25f, 0);
                ray.rotation = Quaternion.LookRotation(destination - ray.position);
                Physics.SyncTransforms();
                bool valid = controller.Trace(out var hit, out _, out string reason);
                if (!valid) Debug.Log("MODELING_TRACE_REJECT: " + feet + " -> " + destination + ": " + reason + "; hit " + (hit.collider == null ? "none" : hit.collider.name));
                return valid;
            }
            try
            {
                // Isolate structural accessibility from the existing, intentional fire hazard.
                // The fire volumes and their original enabled states are restored below.
                foreach (var fire in fireBarriers) fire.enabled = false;
                foreach (string side in new[] { "A", "B" })
                {
                    float center = side == "A" ? -15.5f : 15.5f;
                    for (int floor = 0; floor < 3; floor++)
                    {
                        float y = floor * 3.6f;
                        // Reproduce the actual old shortcut, then prove the new geometry stops it.
                        var separator = GameObject.Find("Stairwell_" + side).transform
                            .Find("Stairwell_InvisibleGuards/BetweenFlights").GetComponent<BoxCollider>();
                        Vector3 start = new Vector3(center, y + 3.635f, 3f);
                        Vector3 finish = new Vector3(center, y + 1.8f, -3f);
                        separator.enabled = false;
                        try
                        {
                            check(Trace(start, finish), side + " F" + (floor + 1) + " baseline central-void shortcut reproduced with separator disabled");
                        }
                        finally { separator.enabled = true; Physics.SyncTransforms(); }
                        check(!Trace(start, finish), side + " F" + (floor + 1) + " central-void shortcut rejected with separator enabled");
                        foreach (float offset in new[] { -2.1f, 2.1f })
                        {
                            var edgeGuard = GameObject.Find("Stairwell_" + side).transform
                                .Find("Stairwell_InvisibleGuards/OuterFlight_" + (offset < 0 ? "West" : "East")).GetComponent<BoxCollider>();
                            Vector3 edgeStart = new Vector3(center + offset, y + 3.635f, 3f);
                            Vector3 edgeFinish = new Vector3(center + offset, y + 1.8f, -3f);
                            check(Physics.Linecast(edgeStart + Vector3.up * 1.25f, edgeFinish, out var edgeHit,
                                controller.Validator.EnvironmentMask, QueryTriggerInteraction.Ignore) && edgeHit.collider == edgeGuard,
                                side + " F" + (floor + 1) + " side-void ray stops at its guard at " + offset);
                            check(!Trace(edgeStart, edgeFinish), side + " F" + (floor + 1) + " side-void shortcut blocked at " + offset);
                        }
                        if (side == "B" && floor == 0) continue;
                        check(Trace(new Vector3(center - 1.08f, y + .035f, 2.7f), new Vector3(center - 1.08f, y + .9f, .2f)),
                            side + " F" + (floor + 1) + " normal first-flight ascent accepted");
                        check(Trace(new Vector3(center + 1.08f, y + 3.635f, 2.7f), new Vector3(center + 1.08f, y + 2.7f, -.1f)),
                            side + " F" + (floor + 1) + " normal second-flight descent accepted");
                    }
                }
                check(!Trace(new Vector3(16.58f, 3.635f, 2.7f), new Vector3(16.58f, 1.8f, -2.7f)),
                    "B F02 cannot teleport across the broken span to the lower half landing");
                check(!Trace(new Vector3(16.58f, 1.835f, -2.7f), new Vector3(16.58f, 3.6f, 2.7f)),
                    "Broken span also rejects uphill teleport");
            }
            finally
            {
                for (int i = 0; i < fireBarriers.Length; i++) fireBarriers[i].enabled = fireStates[i];
                origin.transform.position = oldRoot;
                head.position = oldHead;
                ray.SetPositionAndRotation(oldRay, oldRayRotation);
                Physics.SyncTransforms();
            }
        }
    }
}
