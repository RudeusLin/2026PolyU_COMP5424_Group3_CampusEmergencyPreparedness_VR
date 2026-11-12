using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    public static class CampusScenePolish
    {
        private const string ScenePath = "Assets/Scenes/TeachingBuildingCampus.unity";
        private const string Folder = "Docs/Previews/Campus/Polish";

        [MenuItem("Campus Emergency/Apply campus scene polish")]
        public static void ApplyAndCapture()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            if (Object.FindObjectsOfType<Light>().Any(l => l.name == "StairApproachLight"))
            {
                ValidateSavedScene();
                CaptureViews("After");
                Debug.Log("CAMPUS_SCENE_POLISH_SUCCESS: existing polish verified; no duplicate objects added.");
                return;
            }
            CaptureViews("Before");
            if (!CampusModelingBuilder.ApplyLayoutChanges(".utmp/TeachingBuildingCampusBeforePolish.json")) return;
            EditorSceneManager.OpenScene(ScenePath);
            ValidateSavedScene();
            CaptureViews("After");
            Debug.Log("CAMPUS_SCENE_POLISH_SUCCESS: saved scene, lighting, floor signs and route physics verified.");
        }

        private static void ValidateSavedScene()
        {
            Light[] lights = Object.FindObjectsOfType<Light>();
            Require(lights.Count(l => l.name == "StairApproachLight") == 8, "Eight baked stair-approach sources required");
            Require(lights.Where(l => l.name == "StairApproachLight")
                .All(l => l.lightmapBakeType == LightmapBakeType.Baked), "Approach lights must add no realtime light cost");
            Require(lights.Where(l => l.name == "InteriorLight" && l.transform.parent.name.StartsWith("Stair_"))
                .All(l => Mathf.Abs(l.intensity - 1.45f) < .001f), "Stair lighting update missing");
            MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>(true);
            Require(renderers.All(r => r.sharedMaterial != null), "Missing scene material");
            Require(renderers.Count(r => r.name == "PrintedLettering"
                && r.transform.parent.name == "Label_EXIT AT LEVEL 01") == 8, "Eight stair landing captions required");
            for (int floor = 1; floor <= 4; floor++)
                Require(renderers.Any(r => r.sharedMaterial.name == "FloorAccent" + floor.ToString("00")),
                    "Floor accent missing at level " + floor);
            Require(LightmapSettings.lightmaps.Length > 0 && LightmapSettings.lightmaps.All(l => l.lightmapColor != null),
                "Saved baked lighting missing");
            Require(Object.FindObjectsOfType<ReflectionProbe>().All(p => p.bakedTexture != null), "Saved reflection missing");
            var layout = JsonUtility.FromJson<TeachingBuildingBuilder.Layout>(
                File.ReadAllText("Assets/Environment/TeachingBuildingCampusLayout.json"));
            var errors = TeachingBuildingBuilder.ValidateRoutes(layout.routes);
            Require(errors.Count == 0, string.Join("; ", errors));
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/Validation.txt", "Unity " + Application.unityVersion
                + "\nSaved scene and material references: PASS\nFour floor accents and eight stair captions: PASS"
                + "\nEight added sources are baked: PASS\nStair lighting: PASS\nA/B route physics: PASS"
                + "\nSaved lightmaps and reflections: PASS\nMeshRenderers: " + renderers.Length
                + "\nHeadset frame time and stereo readability: NOT RUN\n");
        }

        private static void CaptureViews(string prefix)
        {
            Directory.CreateDirectory(Folder);
            GameObject holder = new GameObject("ScenePolishCapture");
            try
            {
                Camera camera = holder.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.nearClipPlane = .06f;
                camera.allowHDR = false;
                TeachingBuildingBuilder.Capture(camera, Folder, prefix + "-Classroom.png",
                    new Vector3(-3.8f, 12.45f, 7.3f), new Vector3(-10.9f, 12.5f, 5.9f), 75);
                TeachingBuildingBuilder.Capture(camera, Folder, prefix + "-Corridor.png",
                    new Vector3(-10.9f, 12.45f, -.35f), new Vector3(8, 12.3f, 0), 70);
                TeachingBuildingBuilder.Capture(camera, Folder, prefix + "-Stairwell.png",
                    new Vector3(-15.5f, 12.42f, 3.7f), new Vector3(-14.42f, 11.6f, -.8f), 80);
                TeachingBuildingBuilder.Capture(camera, Folder, prefix + "-FloorIdentity.png",
                    new Vector3(-15.5f, 12.42f, 1.5f), new Vector3(-15.5f, 12.95f, 4.16f), 65);
            }
            finally { Object.DestroyImmediate(holder); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
