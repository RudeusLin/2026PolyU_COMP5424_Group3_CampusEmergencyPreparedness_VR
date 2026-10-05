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
    public static class CampusWalkthroughValidation
    {
        [Serializable] private sealed class Layout { public Route[] routes; }
        [Serializable] private sealed class Route { public string name; public Point[] points; }
        [Serializable] private sealed class Point { public float[] position; }
        private const string Pending = "CampusWalkthroughValidation.Pending";

        static CampusWalkthroughValidation()
        {
            if (SessionState.GetBool(Pending, false))
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Begin()
        {
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

        private static Vector3 Position(Point p) => new Vector3(p.position[0], p.position[1], p.position[2]);

        private static void Validate()
        {
            SessionState.SetBool(Pending, false);
            try
            {
                PrototypeExplorer explorer = Object.FindObjectOfType<PrototypeExplorer>();
                explorer.enabled = false;
                CharacterController controller = explorer.GetComponent<CharacterController>();
                CampusStudentAvatar avatar = explorer.GetComponentInChildren<CampusStudentAvatar>();
                if (avatar == null || avatar.GetComponentsInChildren<Collider>().Length != 0)
                    throw new InvalidOperationException("Student must be visible geometry without extra collision bodies");
                foreach (PrototypeDoor door in Object.FindObjectsOfType<PrototypeDoor>())
                    door.Configure(door.OpenAngle, true);
                Physics.SyncTransforms();
                Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Environment/TeachingBuildingCampusLayout.json"));
                string report = "Unity " + Application.unityVersion + " actual CharacterController movement\n";
                foreach (Route route in layout.routes)
                {
                    Vector3[] points = route.points.Select(Position).ToArray();
                    foreach (float speed in new[] { 2.6f, 4.2f })
                    {
                        report += Walk(explorer, route.name + " descent", points, speed, 1f / 60f);
                        report += Walk(explorer, route.name + " ascent", points.Reverse().ToArray(), speed, 1f / 60f);
                    }
                    report += Walk(explorer, route.name + " low frame rate descent", points, 4.2f, 1f / 30f);
                }
                avatar.SetVisible(true);
                report += "Articulated student: present, no additional colliders; movement uses the same method as keyboard input PASS\n";
                if (Object.FindObjectsOfType<ReflectionProbe>().Length != 6
                    || Object.FindObjectsOfType<LightProbeGroup>().Length != 1)
                    throw new InvalidOperationException("Saved scene has duplicate generated light/reflection probes");
                foreach (Renderer surface in avatar.GetComponentsInChildren<Renderer>())
                    if (surface.lightmapIndex >= 0 || GameObjectUtility.AreStaticEditorFlagsSet(surface.gameObject, StaticEditorFlags.ContributeGI))
                        throw new InvalidOperationException("Animated student must use dynamic probe lighting");
                report += "Student uses dynamic lighting; one light probe group and six reflection probes PASS\n";
                CheckCameras(explorer, avatar);
                report += "First/third person visibility, wall-safe follow camera and reversible view toggle PASS\n";
                CaptureStudentViews(avatar);
                Directory.CreateDirectory("Docs/Previews/Campus");
                File.WriteAllText("Docs/Previews/Campus/CampusWalkthroughValidation.txt", report);
                Debug.Log("CAMPUS_WALKTHROUGH_VALIDATION_SUCCESS\n" + report);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureStudentPreviews()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/TeachingBuildingCampus.unity");
            CampusStudentAvatar avatar = Object.FindObjectOfType<CampusStudentAvatar>();
            avatar.SetVisible(true);
            CaptureStudentViews(avatar);
        }

        private static void CaptureStudentViews(CampusStudentAvatar avatar)
        {
            CaptureStudent(avatar, "Campus-Student.png", new Vector3(-6.15f, 12.1f, 4.5f));
            Transform student = avatar.transform.parent;
            Vector3 previousPosition = student.position;
            Quaternion previousRotation = student.rotation;
            try
            {
                // An outdoor rear view keeps the backpack unobstructed by classroom walls.
                student.SetPositionAndRotation(new Vector3(-4, .025f, -17), Quaternion.identity);
                CaptureStudent(avatar, "Campus-StudentBack.png", new Vector3(-5.5f, 1.4f, -19.2f));
            }
            finally { student.SetPositionAndRotation(previousPosition, previousRotation); }
        }

        private static void CheckCameras(PrototypeExplorer explorer, CampusStudentAvatar avatar)
        {
            Transform spawn = GameObject.Find("Spawn_F04_401").transform;
            explorer.SendMessage("EnterFloor", spawn);
            Camera camera = explorer.transform.Find("WalkCamera").GetComponent<Camera>();
            Renderer[] surfaces = avatar.GetComponentsInChildren<Renderer>();
            if (surfaces.Any(r => r.enabled) || Vector3.Distance(camera.transform.localPosition, new Vector3(0, 1.65f, 0)) > .001f)
                throw new InvalidOperationException("First-person view must hide its own model");
            explorer.ToggleCameraView();
            if (!surfaces.All(r => r.enabled) || Physics.CheckSphere(camera.transform.position, .06f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Third-person camera/model is obstructed");
            // Face a nearby classroom wall; the camera should retreat without clipping it.
            CharacterController controller = explorer.GetComponent<CharacterController>();
            controller.enabled = false;
            explorer.transform.position = new Vector3(-10.65f, spawn.position.y, spawn.position.z);
            explorer.transform.rotation = Quaternion.Euler(0, 90, 0);
            controller.enabled = true;
            Physics.SyncTransforms();
            explorer.ToggleCameraView();
            explorer.ToggleCameraView();
            if (Physics.CheckSphere(camera.transform.position, .06f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Follow camera clips geometry near a wall");
            explorer.ToggleCameraView();
            if (surfaces.Any(r => r.enabled)) throw new InvalidOperationException("View toggle did not restore first person");
            explorer.SendMessage("EnterFloor", spawn);
            explorer.transform.rotation = Quaternion.identity;
            avatar.SetVisible(true);
            avatar.Animate(Vector3.zero, true, 1);
            for (int frame = 0; frame < 5; frame++) explorer.MoveCharacter(Vector3.zero, 0, 1f / 60f);
            Renderer sole = avatar.GetComponentsInChildren<Renderer>().First(r => r.name == "Sole");
            Vector3 soleCenter = sole.bounds.center;
            if (!Physics.Raycast(soleCenter + Vector3.up * .2f, Vector3.down, out RaycastHit floor, .4f)
                || Mathf.Abs(sole.bounds.min.y - floor.point.y) > .04f)
                throw new InvalidOperationException("Student soles do not meet the supporting floor");
        }

        private static void CaptureStudent(CampusStudentAvatar avatar, string name, Vector3 eye)
        {
            GameObject holder = new GameObject("StudentCaptureCamera");
            Camera camera = holder.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(avatar.transform.position + Vector3.up * .9f - eye));
            camera.fieldOfView = 46;
            camera.nearClipPlane = .06f;
            camera.clearFlags = CameraClearFlags.Skybox;
            RenderTexture target = new RenderTexture(900, 1100, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = new Texture2D(900, 1100, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 900, 1100), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Docs/Previews/Campus/" + name, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(holder);
            }
        }

        private static string Walk(PrototypeExplorer explorer, string name, Vector3[] points, float speed, float dt)
        {
            CharacterController controller = explorer.GetComponent<CharacterController>();
            controller.enabled = false;
            controller.transform.position = points[0] + Vector3.up * .05f;
            controller.enabled = true;
            Physics.SyncTransforms();
            int moves = 0;
            foreach (Vector3 point in points.Skip(1))
            {
                int frame = 0;
                for (; frame < 900; frame++)
                {
                    Vector3 delta = point - controller.transform.position;
                    delta.y = 0;
                    if (delta.magnitude < .035f && Mathf.Abs(point.y - controller.transform.position.y) < .24f) break;
                    if (delta.sqrMagnitude > .0001f)
                        controller.transform.rotation = Quaternion.LookRotation(delta);
                    explorer.MoveCharacter(delta, Mathf.Min(speed, delta.magnitude / dt), dt);
                    moves++;
                }
                if (frame == 900)
                {
                    Vector3 feet = controller.transform.position;
                    string contacts = string.Join(", ", Physics.OverlapCapsule(feet + Vector3.up * .28f,
                        feet + Vector3.up * 1.52f, .3f).Select(c => c.name).Distinct());
                    throw new InvalidOperationException(name + " blocked at " + feet.ToString("F3")
                        + "; target " + point.ToString("F3") + "; nearby: " + contacts);
                }
            }
            return name + " at " + speed + " m/s, " + Mathf.RoundToInt(1f / dt) + " Hz: " + moves + " controller moves PASS\n";
        }
    }
}
