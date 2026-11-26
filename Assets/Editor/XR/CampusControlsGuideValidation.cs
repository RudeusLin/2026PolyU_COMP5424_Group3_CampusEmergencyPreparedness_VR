using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CampusEmergency.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    /// <summary>Play-mode startup transition checks and real scene-camera captures; no scene saves.</summary>
    [InitializeOnLoad]
    public static class CampusControlsGuideValidation
    {
        const string Pending = "CampusControlsGuideValidation.Pending";
        const string Output = "E:/0_PolyU/COMP5424_XR/Project/output/ControlsGuideIntegration";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static double readyAt;
        static CampusControlsGuideValidation() { EditorApplication.playModeStateChanged += OnPlay; }
        [MenuItem("Campus Emergency/XR/Validate startup controls guide")]
        public static void Begin()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Validation.txt", "Editor Play Mode startup controls validation (no headset).\n");
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            readyAt = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += WaitForScene;
        }
        static void WaitForScene()
        {
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= WaitForScene;
            Run();
        }
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static void Check(bool ok, string message)
        {
            File.AppendAllText(Output + "/Validation.txt", (ok ? "PASS: " : "FAIL: ") + message + "\n");
            if (!ok) throw new Exception(message);
        }
        static void Capture(Camera camera, string name)
        {
            Canvas.ForceUpdateCanvases();
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            var target = new RenderTexture(1920, 1080, 24);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }
        static void Run()
        {
            SessionState.SetBool(Pending, false);
            int exitCode = 0;
            try
            {
                var origin = Object.FindObjectOfType<XROrigin>();
                var ui = Object.FindObjectOfType<LanguageSelectionUI>();
                Check(origin != null && ui != null, "XR scene and startup gate are running");
                ui.enabled = false; // Keep hardware input from racing deterministic transition checks.
                var camera = origin.Camera;
                camera.transform.position = origin.transform.position + Vector3.up * 1.65f;
                camera.transform.rotation = origin.transform.rotation;
                var canvas = (Canvas)Get(ui, "canvas");
                Vector3 anchor = canvas.transform.position;
                Quaternion rotation = canvas.transform.rotation;

                for (int language = 0; language < 3; language++)
                {
                    string code = language == 0 ? "EN" : language == 1 ? "SC" : "TC";
                    Set(ui, "selectionComplete", false);
                    Set(ui, "briefingVisible", false);
                    Set(ui, "controlsGuideVisible", false);
                    Set(ui, "mapExpanded", false);
                    canvas.gameObject.SetActive(true);
                    Invoke(ui, "Select", language);
                    Check(LanguageSelectionUI.IsStartupBlocking && (bool)Get(ui, "briefingVisible"), code + " language opens route overview with movement blocked");
                    Invoke(ui, "Select", 1);
                    Check((bool)Get(ui, "mapExpanded") && !(bool)Get(ui, "controlsGuideVisible"), code + " enlarge map does not skip to guide or start practice");
                    Invoke(ui, "Select", 1);
                    Invoke(ui, "Select", 0);
                    Check(LanguageSelectionUI.IsBlocking && LanguageSelectionUI.IsStartupBlocking && (bool)Get(ui, "controlsGuideVisible"), code + " route confirmation opens guide while gameplay stays blocked");
                    var guide = canvas.GetComponentsInChildren<RawImage>().Single(x => x.name == "Localized Controls Guide");
                    Check(guide.texture == Resources.Load<Texture2D>("CampusControls/Controls_" + code) && guide.texture != null, code + " guide loads matching language texture");
                    var rect = guide.rectTransform;
                    Check(Mathf.Abs(rect.rect.width / rect.rect.height - (float)guide.texture.width / guide.texture.height) < .001f, code + " image aspect ratio preserved");
                    var choices = (RectTransform[])Get(ui, "choices");
                    Check(choices.Count(x => x != null) == 2 && choices[0].GetComponent<Button>() != null && choices[1].GetComponent<Button>() != null, code + " begin and back use existing ray-compatible buttons");
                    origin.transform.position += Vector3.right;
                    Invoke(ui, "LateUpdate");
                    Check(Vector3.Distance(origin.transform.position, (Vector3)Get(ui, "startupPosition")) < .001f, code + " startup position remains locked");
                    Check(Vector3.Distance(canvas.transform.position, anchor) < .001f && Quaternion.Angle(canvas.transform.rotation, rotation) < .01f, code + " guide stays on fixed world anchor");
                    camera.transform.position = origin.transform.position + Vector3.up * 1.65f;
                    camera.transform.rotation = origin.transform.rotation;
                    Capture(camera, "ControlsGuide_" + code);
                    Invoke(ui, "Select", 1);
                    Check((bool)Get(ui, "briefingVisible") && !(bool)Get(ui, "controlsGuideVisible") && LanguageSelectionUI.IsStartupBlocking, code + " back returns to route overview without releasing movement");
                    Invoke(ui, "Select", 0);
                    Invoke(ui, "Select", 0);
                    Check(!LanguageSelectionUI.IsBlocking && !LanguageSelectionUI.IsStartupBlocking && !canvas.gameObject.activeSelf, code + " guide confirmation hides startup UI and unlocks practice");
                }
                File.AppendAllText(Output + "/Validation.txt", "RESULT: PASS. Captures use the live XR scene camera in Editor Play Mode; headset verification remains separate.\n");
                Debug.Log("CAMPUS_CONTROLS_GUIDE_VALIDATION_PASS");
            }
            catch (Exception error)
            {
                exitCode = 1;
                File.AppendAllText(Output + "/Validation.txt", error + "\nRESULT: FAIL\n");
                Debug.LogException(error);
            }
            finally
            {
                EditorApplication.isPlaying = false;
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
        }
    }
}
