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
    public static class CampusMovementValidation
    {
        private const string Pending = "CampusMovementValidation.Pending";
        private const string Folder = "Docs/Previews/Campus";

        static CampusMovementValidation()
        {
            if (SessionState.GetBool(Pending, false)) EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Campus Emergency/Validate campus movement and hands")]
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
                var avatar = explorer.GetComponentInChildren<CampusStudentAvatar>();
                var arms = explorer.GetComponentInChildren<CampusFirstPersonArms>();
                explorer.enabled = false;
                GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = "MovementValidationPlatform";
                platform.transform.position = new Vector3(0, 29.9f, -30);
                platform.transform.localScale = new Vector3(30, .2f, 30);
                Physics.SyncTransforms();
                Vector3 origin = new Vector3(0, 30.04f, -30);
                Place(explorer, origin);
                Vector3 previous = explorer.transform.position;
                explorer.MoveWithInput(Vector2.up, false, false, 1f / 60);
                float firstSpeed = (explorer.transform.position - previous).magnitude * 60;
                Require(firstSpeed < 1, "Movement starts instantly without acceleration");
                float walking = StepSpeed(explorer, Vector2.up, false);
                float sprint = StepSpeed(explorer, Vector2.up, true);
                float diagonal = StepSpeed(explorer, Vector2.one, true);
                Require(Mathf.Abs(walking - 2.6f) < .08f && Mathf.Abs(sprint - 4.2f) < .08f,
                    "Walk or sprint speed is incorrect");
                Require(Mathf.Abs(diagonal - sprint) < .08f, "Diagonal movement gains extra speed");
                Require(explorer.IsSprinting, "Sprint state is not reported");
                float stopped = StepSpeed(explorer, Vector2.zero, false);
                Require(stopped < .02f && !explorer.IsSprinting, "Releasing input does not brake to a stop");

                string report = "Unity " + Application.unityVersion + " saved-scene desktop Play Mode\n"
                    + "Keyboard movement path: acceleration, walk/sprint speeds, normalized diagonal input and braking PASS\n";
                foreach (int rate in new[] { 30, 60, 120 })
                {
                    Place(explorer, origin);
                    float floor = explorer.transform.position.y;
                    float apex = floor;
                    bool airborne = false, landed = false;
                    explorer.RequestJump();
                    for (int frame = 0; frame < rate * 2; frame++)
                    {
                        explorer.MoveWithInput(Vector2.zero, false, false, 1f / rate);
                        apex = Mathf.Max(apex, explorer.transform.position.y);
                        airborne |= !explorer.IsGrounded;
                        // A second press during ascent must not grant an airborne jump.
                        if (frame == rate / 5) explorer.RequestJump();
                        if (airborne && explorer.IsGrounded) { landed = true; break; }
                    }
                    Require(airborne && landed && apex - floor > .6f && apex - floor < .82f,
                        "Jump failed, doubled in midair, or did not land at " + rate + " Hz");
                    report += "Jump/landing and airborne second-jump rejection at " + rate + " Hz: "
                        + (apex - floor).ToString("F3") + " m apex PASS\n";
                }

                Place(explorer, new Vector3(14.8f, 30.04f, -30));
                explorer.MoveCharacter(Vector3.right, 60, 1f / 60);
                Require(!explorer.IsGrounded, "Coyote test did not leave the ledge");
                float ledgeHeight = explorer.transform.position.y;
                explorer.RequestJump();
                explorer.MoveCharacter(Vector3.zero, 0, 1f / 60);
                Require(explorer.transform.position.y > ledgeHeight + .05f, "Brief ledge grace cannot jump");
                report += "Brief jump grace after leaving a ledge PASS\n";

                Place(explorer, origin);
                explorer.MoveWithInput(Vector2.zero, false, true, 1f / 60);
                bool buffered = false, touchedGround = false, jumpedAgain = false;
                for (int frame = 0; frame < 100; frame++)
                {
                    if (!buffered && frame > 15 && explorer.transform.position.y < 30.14f && !explorer.IsGrounded)
                    {
                        explorer.RequestJump();
                        buffered = true;
                    }
                    explorer.MoveWithInput(Vector2.zero, false, false, 1f / 60);
                    if (buffered && explorer.IsGrounded) touchedGround = true;
                    if (touchedGround && !explorer.IsGrounded) { jumpedAgain = true; break; }
                }
                Require(buffered && touchedGround && jumpedAgain, "Jump pressed just before landing is discarded");
                report += "Jump input just before landing is buffered without an airborne extra jump PASS\n";

                Place(explorer, origin);
                float ground = explorer.transform.position.y;
                GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ceiling.transform.position = new Vector3(0, 32.02f, -30);
                ceiling.transform.localScale = new Vector3(4, .2f, 4);
                Physics.SyncTransforms();
                explorer.RequestJump();
                float highest = ground;
                for (int frame = 0; frame < 90; frame++)
                {
                    explorer.MoveCharacter(Vector3.zero, 0, 1f / 60);
                    highest = Mathf.Max(highest, explorer.transform.position.y);
                }
                Require(highest - ground < .22f && explorer.IsGrounded, "Jump penetrates a low ceiling or fails to fall");
                Object.DestroyImmediate(ceiling);
                Object.DestroyImmediate(platform);
                report += "Low ceiling collision cancels ascent and returns to the floor PASS\n";

                explorer.SendMessage("EnterFloor", GameObject.Find("Spawn_F04_401").transform);
                Transform hands = arms.transform.Find("FirstPersonArms");
                Require(hands.gameObject.activeSelf && avatar.GetComponentsInChildren<Renderer>().All(r => !r.enabled),
                    "First-person view does not separate hands from the hidden body");
                Require(hands.GetComponentsInChildren<Collider>().Length == 0
                    && hands.GetComponentsInChildren<Renderer>().All(r => r.enabled && r.lightmapIndex < 0),
                    "First-person hands are obstructive or baked into the building");
                foreach (string side in new[] { "LeftHandView", "RightHandView" })
                {
                    Transform view = hands.Find(side);
                    if (avatar.UsesImportedModel)
                        Require(view.GetComponent<MeshFilter>()?.sharedMesh != null
                            && view.GetComponent<MeshFilter>().sharedMesh.vertexCount > 100
                            && view.Find("Wrist/HandHoldPoint") != null, "Imported hand surface or towel anchor is missing");
                    else Require(view.Find("Wrist/HandDetails")?.childCount > 0, "Hand finger detail is missing");
                }
                Camera camera = arms.GetComponent<Camera>();
                Directory.CreateDirectory(Folder);
                arms.SetVisible(false);
                CaptureView(camera, "Campus-HandsBaseline.png");
                arms.SetVisible(true);
                CaptureView(camera, "Campus-Hands.png");
                int handPixels = ChangedPixels("Campus-HandsBaseline.png", "Campus-Hands.png");
                Require(handPixels > 1000, "First-person hands are enabled but not visibly rendered");
                report += "First-person hands visibly render: " + handPixels + " changed pixels PASS\n";
                var towel = Object.FindObjectsOfType<CampusTowel>().OrderBy(t => t.transform.position.y).Last();
                Transform home = towel.transform.parent;
                Vector3 homeScale = towel.transform.localScale;
                towel.Interact(explorer);
                explorer.MoveCharacter(Vector3.zero, 0, 1f / 60);
                Require(towel.transform.parent == arms.HoldPoint, "First-person towel is not attached to the hand");
                CaptureView(camera, "Campus-HandsTowel.png");
                explorer.ToggleCameraView();
                Require(!hands.gameObject.activeSelf && towel.transform.parent == avatar.RightHandHoldPoint,
                    "Third-person view retains floating hands or loses the held towel");
                explorer.ToggleCameraView();
                Require(hands.gameObject.activeSelf && towel.transform.parent == arms.HoldPoint,
                    "Camera toggle did not restore first-person hand/towel visibility");
                explorer.ResetWashroomInteractions();
                Require(towel.transform.parent == home && towel.transform.localScale == homeScale,
                    "Restart did not restore the original towel scale");
                report += "First-person sleeves/palms/fingers, hidden head/body, dynamic lighting and no hand colliders PASS\n"
                    + "Camera toggles transfer the held towel between hands; restart restores its original scale PASS\n";

                explorer.ToggleCameraView();
                avatar.SetVisible(true);
                avatar.ResetPose();
                Transform elbow = avatar.UsesImportedModel
                    ? avatar.GetComponentsInChildren<Transform>().Single(t => t.name == "Bip01 L Forearm")
                    : avatar.transform.Find("BodyRoot/LeftArm/LeftForearm");
                Transform knee = avatar.UsesImportedModel
                    ? avatar.GetComponentsInChildren<Transform>().Single(t => t.name == "Bip01 L Calf")
                    : avatar.transform.Find("BodyRoot/LeftLeg/LeftShin");
                Quaternion idle = elbow.localRotation;
                Quaternion idleKnee = knee.localRotation;
                avatar.Animate(Vector3.forward * 4.2f, true, 1);
                Require(Quaternion.Angle(idle, elbow.localRotation) > 35, "Run animation still uses the idle arm pose");
                avatar.Animate(Vector3.up * 3, false, .2f);
                Require(Quaternion.Angle(knee.localRotation, idleKnee) > 20, "Airborne knees do not flex");
                avatar.ResetPose();
                Require(Quaternion.Angle(idle, elbow.localRotation) < .1f, "Restart does not restore the idle pose");
                Place(explorer, new Vector3(-4, .06f, -17));
                avatar.SetVisible(true);
                avatar.Animate(Vector3.forward * 4.2f, true, 1);
                CaptureStudent(avatar, "Campus-StudentRun.png");
                explorer.RequestJump();
                for (int frame = 0; frame < 12; frame++) explorer.MoveCharacter(Vector3.zero, 0, 1f / 60);
                CaptureStudent(avatar, "Campus-StudentJump.png");
                report += "Distinct run arm/knee pose, airborne tuck, landing recovery and idle reset PASS\n"
                    + "Saved Unity first-person hands, towel, sprint and jump render captures PASS\n"
                    + "Headset input, stereo comfort and performance remain unverified.\n";
                File.WriteAllText(Folder + "/CampusMovementValidation.txt", report);
                Debug.Log("CAMPUS_MOVEMENT_VALIDATION_SUCCESS\n" + report);
                Finish(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(false);
            }
        }

        private static float StepSpeed(PrototypeExplorer explorer, Vector2 input, bool sprint)
        {
            Vector3 before = Vector3.zero;
            for (int frame = 0; frame < 45; frame++)
            {
                before = explorer.transform.position;
                explorer.MoveWithInput(input, sprint, false, 1f / 60);
            }
            Vector3 delta = explorer.transform.position - before;
            delta.y = 0;
            return delta.magnitude * 60;
        }

        private static void Place(PrototypeExplorer explorer, Vector3 position)
        {
            explorer.SendMessage("EnterFloor", GameObject.Find("Spawn_F04_401").transform);
            CharacterController controller = explorer.GetComponent<CharacterController>();
            controller.enabled = false;
            explorer.transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            Physics.SyncTransforms();
            for (int frame = 0; frame < 8; frame++) explorer.MoveCharacter(Vector3.zero, 0, 1f / 60);
        }

        private static void CaptureView(Camera camera, string name) => TeachingBuildingBuilder.Capture(camera,
            Folder, name, camera.transform.position, camera.transform.position + camera.transform.forward, camera.fieldOfView);

        private static int ChangedPixels(string baseline, string capture)
        {
            var before = new Texture2D(2, 2);
            var after = new Texture2D(2, 2);
            try
            {
                before.LoadImage(File.ReadAllBytes(Folder + "/" + baseline));
                after.LoadImage(File.ReadAllBytes(Folder + "/" + capture));
                Color32[] a = before.GetPixels32(), b = after.GetPixels32();
                Require(a.Length == b.Length, "Hand comparison capture dimensions differ");
                int changed = 0;
                for (int i = 0; i < a.Length; i++)
                    if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 24)
                        changed++;
                return changed;
            }
            finally { Object.DestroyImmediate(before); Object.DestroyImmediate(after); }
        }

        private static void CaptureStudent(CampusStudentAvatar avatar, string name)
        {
            GameObject holder = new GameObject("MovementCaptureCamera");
            Camera camera = holder.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .06f;
            Vector3 feet = avatar.transform.position;
            Vector3 eye = feet + new Vector3(-1.5f, 1.4f, -2.2f);
            Vector3 target = feet + Vector3.up * .9f;
            Require(!Physics.Linecast(eye, target, out RaycastHit hit)
                || hit.collider.GetComponent<PrototypeExplorer>() != null, "Student capture is blocked by " + hit.collider?.name);
            TeachingBuildingBuilder.Capture(camera, Folder, name, eye, target, 46);
            Object.DestroyImmediate(holder);
        }

        private static void Finish(bool success)
        {
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
