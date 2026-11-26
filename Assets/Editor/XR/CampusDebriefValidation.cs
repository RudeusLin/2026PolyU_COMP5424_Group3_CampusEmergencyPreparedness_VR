using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CampusEmergency.Player;
using CampusEmergency.Player.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    [InitializeOnLoad]
    public static class CampusDebriefValidation
    {
        const string Phase = "CampusDebriefValidation.Phase";
        const string Output = "E:/0_PolyU/COMP5424_XR/Project/output/DebriefEducation";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static double readyAt;
        static CampusDebriefValidation() { EditorApplication.playModeStateChanged += OnPlay; }
        [MenuItem("Campus Emergency/XR/Validate educational debrief")]
        public static void Begin()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Validation.txt", "Educational debrief: Editor Play Mode checks without headset.\n");
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            SessionState.SetString(Phase, "start");
            EditorApplication.isPlaying = true;
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Phase, "") == "start")
            {
                readyAt = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += WaitForScene;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(Phase, "") == "exit")
            {
                Check(!EditorApplication.isPlaying, "Exit action stops Editor Play Mode");
                File.AppendAllText(Output + "/Validation.txt", "RESULT: PASS. Headset readability and physical input remain to be verified on device.\n");
                SessionState.SetString(Phase, "");
                Debug.Log("CAMPUS_EDUCATIONAL_DEBRIEF_VALIDATION_PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }
        static void WaitForScene()
        {
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= WaitForScene;
            try { Run(); } catch (Exception e) { Fail(e); }
        }
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static void Check(bool ok, string message)
        {
            File.AppendAllText(Output + "/Validation.txt", (ok ? "PASS: " : "FAIL: ") + message + "\n");
            if (!ok) throw new Exception(message);
        }
        static void Fail(Exception error)
        {
            SessionState.SetString(Phase, "");
            File.AppendAllText(Output + "/Validation.txt", error + "\nRESULT: FAIL\n");
            Debug.LogException(error);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
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
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = previous;
                Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            }
        }
        static void Layout(Transform root, string label)
        {
            Canvas.ForceUpdateCanvases();
            var card = root.GetComponentsInChildren<Image>().Single(x => x.name == "Completion Debrief Card").rectTransform;
            foreach (var text in root.GetComponentsInChildren<Text>())
            {
                Check(text.preferredHeight <= text.rectTransform.rect.height + 2, label + " text fits: " + text.name);
                var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                    Check(card.rect.Contains(card.InverseTransformPoint(corner)), label + " text stays inside panel: " + text.name);
            }
        }
        static void Run()
        {
            var origin = Object.FindObjectOfType<XROrigin>();
            var ui = Object.FindObjectOfType<LanguageSelectionUI>();
            Check(origin != null && ui != null, "Live XR scene and language gate loaded");
            ui.enabled = false;
            var camera = origin.Camera;
            camera.transform.position = origin.transform.position + Vector3.up * 1.65f;
            camera.transform.rotation = origin.transform.rotation;
            Invoke(ui, "BeginExperience");
            var flow = origin.GetComponent<CampusRouteAFlow>();
            flow.Completed[0] = true; flow.Completed[3] = true;
            bool[] savedRecords = (bool[])flow.Completed.Clone();
            for (int language = 0; language < 3; language++)
            {
                string code = language == 0 ? "EN" : language == 1 ? "SC" : "TC";
                typeof(LanguageSelectionUI).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, (CampusLanguage)language);
                if (!(bool)Get(ui, "completionVisible")) LanguageSelectionUI.ShowCompletionDebrief();
                else Invoke(ui, "BuildCompletionDebrief");
                var canvas = (Canvas)Get(ui, "canvas");
                Vector3 fixedFeet = ui.transform.position;
                var fixedCanvas = canvas.transform.position;
                var fixedFacing = canvas.transform.rotation;
                Check(!canvas.transform.IsChildOf(camera.transform), code + " review is independent of headset transform");
                Check(Mathf.Abs(fixedCanvas.y - fixedFeet.y - 1.65f) < .001f, code + " centre matches startup 1.65m height");
                Check(Vector3.Dot(canvas.transform.up, Vector3.up) > .999f, code + " panel is vertical without head pitch");
                ui.transform.position += Vector3.right;
                camera.transform.localRotation = Quaternion.Euler(-20, 35, 0);
                Invoke(ui, "LateUpdate");
                Check(Vector3.Distance(ui.transform.position, fixedFeet) < .001f, code + " review locks player origin");
                Check(Vector3.Distance(canvas.transform.position, fixedCanvas) < .001f && Quaternion.Angle(canvas.transform.rotation, fixedFacing) < .01f, code + " head movement leaves review anchored");
                Check(Quaternion.Angle(camera.transform.localRotation, Quaternion.Euler(-20, 35, 0)) < .01f, code + " natural head rotation remains available");
                camera.transform.rotation = origin.transform.rotation;

                var choices = (RectTransform[])Get(ui, "choices");
                Check(choices.Count(x => x != null) == 8, code + " overview has six detail rows plus restart and exit");
                Check(canvas.GetComponentsInChildren<Text>().All(t => !t.text.Contains("RECORDED") && !t.text.Contains("已记录") && !t.text.Contains("未记录") && !t.text.Contains("已記錄") && !t.text.Contains("未記錄")), code + " no completion record labels displayed");
                Check(canvas.GetComponentsInChildren<Text>().Count(t => t.text == "→") == 6, code + " every node has a detail arrow");
                Set(ui, "hover", 2); Invoke(ui, "UpdateChoiceVisuals");
                Check(choices[2].localScale.x > 1f, code + " row hover feedback retained");
                Set(ui, "hover", -1); Set(ui, "confirmedChoice", 2); Invoke(ui, "UpdateChoiceVisuals");
                Check(choices[2].localScale.x < 1f, code + " row confirmation feedback retained");
                Set(ui, "confirmedChoice", -1); Invoke(ui, "UpdateChoiceVisuals");
                Layout(canvas.transform, code + " overview");
                Capture(camera, "Overview_" + code);
                for (int node = 0; node < 6; node++)
                {
                    Invoke(ui, "Select", node + 2);
                    Check((int)Get(ui, "debriefDetail") == node && LanguageSelectionUI.IsBlocking, code + " node " + (node + 1) + " opens while gameplay blocked");
                    var picture = canvas.GetComponentsInChildren<RawImage>().Single(x => x.name == "Debrief Illustration");
                    Check(picture.texture != null && picture.texture == Resources.Load<Texture2D>("CampusDebrief/Node" + (node + 1).ToString("00") + "_" + code), code + " node " + (node + 1) + " uses correct localized image");
                    Check(Mathf.Abs(picture.rectTransform.rect.width / picture.rectTransform.rect.height - (float)picture.texture.width / picture.texture.height) < .001f, code + " illustration aspect preserved");
                    choices = (RectTransform[])Get(ui, "choices");
                    Check(choices.Count(x => x != null) == 1 && choices[8].GetComponent<Button>().onClick.GetPersistentEventCount() == 0, code + " detail exposes one runtime back button");
                    Layout(canvas.transform, code + " node " + (node + 1));
                    Capture(camera, "Node" + (node + 1).ToString("00") + "_" + code);
                    Invoke(ui, "Select", 8);
                    Check((int)Get(ui, "debriefDetail") == -1 && choices[8] == null && LanguageSelectionUI.IsBlocking, code + " back returns to overview and clears stale detail target");
                }
                Check(flow.Completed.SequenceEqual(savedRecords), code + " browsing does not change internal route records");
            }
            SessionState.SetString(Phase, "restart");
            readyAt = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += AwaitRestart;
            Invoke(ui, "Select", 0);
        }
        static void AwaitRestart()
        {
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= AwaitRestart;
            try
            {
                var ui = Object.FindObjectOfType<LanguageSelectionUI>();
                Check(ui != null && LanguageSelectionUI.IsStartupBlocking, "Restart reinstalls language/startup gate");
                Check(LanguageSelectionUI.Current == CampusLanguage.English, "Restart clears previous language");
                var flow = Object.FindObjectOfType<CampusRouteAFlow>();
                Check(flow != null && flow.Node == 1 && flow.Completed.All(x => !x), "Restart clears checkpoint records and returns to first node");
                var spawn = GameObject.Find("Spawn_F04_401").transform;
                Check(Vector3.Distance(ui.transform.position, spawn.position) < .05f, "Restart returns player to 401 spawn");
                ui.enabled = false;
                Invoke(ui, "Select", 1); Invoke(ui, "Select", 0);
                Check((bool)Get(ui, "controlsGuideVisible") && LanguageSelectionUI.IsStartupBlocking, "Restart still requires controls guide confirmation");
                Invoke(ui, "Select", 0);
                LanguageSelectionUI.ShowCompletionDebrief();
                SessionState.SetString(Phase, "exit");
                Invoke(ui, "Select", 1);
            }
            catch (Exception error) { Fail(error); }
        }
    }
}
