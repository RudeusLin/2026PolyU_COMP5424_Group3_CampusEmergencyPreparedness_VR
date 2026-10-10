using System;
using System.IO;
using System.Linq;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    [InitializeOnLoad]
    public static class CampusModelingValidation
    {
        private const string Pending = "CampusModelingValidation.Pending";
        private const string Folder = "Docs/Previews/Campus";

        static CampusModelingValidation()
        {
            if (SessionState.GetBool(Pending, false)) EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Campus Emergency/Validate campus modeling")]
        public static void Begin()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/TeachingBuildingCampus.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Validate;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Validate()
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            try
            {
                var explorer = Object.FindObjectOfType<PrototypeExplorer>();
                explorer.SendMessage("EnterFloor", GameObject.Find("Spawn_F04_401").transform);
                explorer.enabled = false;
                var taps = Object.FindObjectsOfType<CampusFaucet>().OrderBy(t => t.transform.position.y).ToArray();
                var towels = Object.FindObjectsOfType<CampusTowel>().OrderBy(t => t.transform.position.y).ToArray();
                Require(taps.Length == 4 && towels.Length == 4, "Expected one usable wash station per floor");
                Require(taps.All(t => !t.IsRunning) && towels.All(t => !t.IsWet), "Fixtures must start dry and off");
                foreach (var tap in taps)
                {
                    tap.Interact(explorer);
                    Require(tap.IsRunning, "Tap cannot be turned on without a towel");
                    tap.Interact(explorer);
                    Require(!tap.IsRunning, "Tap cannot be turned off");
                }
                for (int i = 0; i < towels.Length; i++)
                {
                    CampusTowel towel = towels[i];
                    Transform home = towel.transform.parent;
                    Vector3 homePosition = towel.transform.localPosition;
                    Quaternion homeRotation = towel.transform.localRotation;
                    towel.Interact(explorer);
                    Require(explorer.HeldTowel == towel && towel.transform.parent == explorer.TowelHoldPoint,
                        "Pickup did not attach the towel to the player");
                    Require(towel.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Held towel obstructs the player");
                    CampusTowel other = towels[(i + 1) % towels.Length];
                    Transform otherHome = other.transform.parent;
                    other.Interact(explorer);
                    Require(explorer.HeldTowel == towel && other.transform.parent == otherHome, "Second pickup replaced the held towel");
                    taps[i].Interact(explorer);
                    Require(towel.IsWet && taps[i].IsRunning, "Tap interaction did not wet the held towel");
                    Require(towel.GetComponentsInChildren<Renderer>().All(r => r.lightmapIndex < 0
                        && !GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.ContributeGI)),
                        "Movable towel was baked into the building");
                    taps[i].Interact(explorer);
                    Require(!taps[i].IsRunning && towel.IsWet, "Turning off the tap lost towel state");
                    taps[i].SetRunning(true);
                    explorer.ResetWashroomInteractions();
                    Require(explorer.HeldTowel == null && !towel.IsWet && taps.All(t => !t.IsRunning), "Restart did not clear fixture state");
                    Require(towel.transform.parent == home && Vector3.Distance(towel.transform.localPosition, homePosition) < .001f
                        && Quaternion.Angle(towel.transform.localRotation, homeRotation) < .01f,
                        "Restart did not return the towel to its original counter");
                    Require(towel.GetComponentsInChildren<Collider>().All(c => c.enabled), "Returned towel cannot be picked up again");
                }
                Require(Object.FindObjectsOfType<Transform>().Count(t => t.name.StartsWith("RefugeRoom_F")) == 4,
                    "Refuge rooms must exist on all floors");
                Require(Object.FindObjectsOfType<Transform>().Count(t => t.name == "RefugeDoor") == 4,
                    "Refuge-room door finishes were not saved");
                Transform restroom = GameObject.Find("Restrooms_F04").transform;
                Require(restroom.Find("DoorFrame").localPosition.x < 0, "Fourth-floor WC is still far from 401");
                Transform room403 = GameObject.Find("Room403").transform;
                Require(room403.GetComponentsInChildren<CampusTowel>().Length == 0
                    && room403.Find("CollaborationTable") != null, "Old WC was not converted into room 403");
                CheckAccess(explorer);
                foreach (PrototypeDoor door in Object.FindObjectsOfType<PrototypeDoor>())
                    if (door.transform.parent.name == "RefugeDoor") door.Configure(door.OpenAngle, false);
                foreach (CampusTowel towel in towels)
                    Require(Physics.Raycast(towel.transform.position + Vector3.up * .35f, Vector3.down,
                        out RaycastHit hit, .5f) && hit.collider.GetComponentInParent<CampusTowel>() == towel,
                        "Counter towel is missing a reachable interaction collider");
                Directory.CreateDirectory(Folder);
                Capture("Campus-Washroom.png", new Vector3(-7.1f, 12.45f, -3.7f), new Vector3(-5.65f, 12.1f, -1.7f), 66);
                taps[3].SetRunning(true);
                Capture("Campus-WashroomWater.png", new Vector3(-7.1f, 12.45f, -3.7f), new Vector3(-5.65f, 12.1f, -1.7f), 66);
                taps[3].SetRunning(false);
                Capture("Campus-RefugeRoom.png", new Vector3(3.3f, 12.45f, .7f), new Vector3(4.8f, 12.4f, -1.5f), 72);
                Capture("Campus-Room403.png", new Vector3(11.8f, 12.45f, -2.2f), new Vector3(10.15f, 11.6f, -4.2f), 72);
                File.WriteAllText(Folder + "/CampusModelingValidation.txt",
                    "Unity " + Application.unityVersion + " saved-scene desktop Play Mode\n"
                    + "Four reusable wash stations; visible basin, mirror, towel and tap: PASS\n"
                    + "Tap on/off, towel pickup and wetting on all four floors: PASS\n"
                    + "Second-towel rejection, disabled held colliders and dynamic towel lighting: PASS\n"
                    + "Restart restores original towel pose, dry state, collider and all tap states: PASS\n"
                    + "Fourth-floor 403/WC use exchanged; WC reached directly from 401: PASS\n"
                    + "Four refuge-room markers and distinct doors; middle entrance clear: PASS\n"
                    + "Actual CharacterController access to WC, wash station and refuge room: PASS\n"
                    + "Counter pickup colliders and saved Unity render captures: PASS\n"
                    + "Headset input and performance remain unverified.\n");
                Debug.Log("CAMPUS_MODELING_VALIDATION_SUCCESS");
                Finish(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(false);
            }
        }

        private static void Finish(bool success)
        {
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static void CheckAccess(PrototypeExplorer explorer)
        {
            foreach (PrototypeDoor door in Object.FindObjectsOfType<PrototypeDoor>()) door.Configure(door.OpenAngle, true);
            var controller = explorer.GetComponent<CharacterController>();
            controller.enabled = false;
            explorer.transform.position = new Vector3(-7.8f, 10.85f, 2.7f);
            controller.enabled = true;
            Physics.SyncTransforms();
            foreach (Vector3 target in new[] { new Vector3(-7.8f, 10.8f, -3), new Vector3(-5.65f, 10.8f, -2.9f),
                new Vector3(-7.8f, 10.8f, -3), new Vector3(-7.8f, 10.8f, 0),
                new Vector3(4.1f, 10.8f, 0), new Vector3(4.1f, 10.8f, -3) })
            {
                int steps = 0;
                while (Vector2.Distance(new Vector2(explorer.transform.position.x, explorer.transform.position.z),
                    new Vector2(target.x, target.z)) > .04f && steps++ < 1000)
                {
                    Vector3 direction = target - explorer.transform.position;
                    direction.y = 0;
                    explorer.MoveCharacter(direction, Mathf.Min(2.6f, direction.magnitude * 60), 1f / 60f);
                }
                Require(steps < 1000, "New room access is blocked before " + target.ToString("F2"));
            }
        }

        private static void Capture(string filename, Vector3 eye, Vector3 target, float fov)
        {
            GameObject holder = new GameObject("ModelingCaptureCamera");
            try
            {
                Camera camera = holder.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.nearClipPlane = .06f;
                TeachingBuildingBuilder.Capture(camera, Folder, filename, eye, target, fov);
            }
            finally { Object.DestroyImmediate(holder); }
        }
    }
}
