using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

namespace CampusEmergency.Editor
{
    // Explicit menu/batch entry points only; never changes a scene or runs on load.
    public static class CampusXRProjectSetup
    {
        const BuildTargetGroup Group = BuildTargetGroup.Standalone;
        const string ReportPath = "Docs/XRDependencyValidation.txt";
        const string SamplePath = "Assets/Samples/XR Interaction Toolkit/2.6.4/Starter Assets";

        [MenuItem("Campus Emergency/XR/Configure PC VR dependencies")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before configuring XR.");
            XRGeneralSettingsPerBuildTarget targets;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out targets) || targets == null)
            {
                EnsureFolder("Assets/XR");
                targets = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(targets, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, targets, true);
            }
            if (!targets.HasSettingsForBuildTarget(Group)) targets.CreateDefaultSettingsForBuildTarget(Group);
            if (!targets.HasManagerSettingsForBuildTarget(Group)) targets.CreateDefaultManagerSettingsForBuildTarget(Group);
            var general = targets.SettingsForBuildTarget(Group);
            general.InitManagerOnStart = true;
            general.Manager.automaticLoading = true;
            general.Manager.automaticRunning = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, typeof(OpenXRLoader).FullName, Group))
                throw new InvalidOperationException("Could not assign Windows OpenXR loader.");

            // OpenXR 1.14 exposes feature APIs, but its settings factory is internal.
            var packageType = typeof(FeatureHelpers).Assembly.GetType("UnityEditor.XR.OpenXR.OpenXRPackageSettings", true);
            packageType.GetMethod("GetOrCreateInstance", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            FeatureHelpers.RefreshFeatures(Group);
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(Group);
            var touch = xr.GetFeature<OculusTouchControllerProfile>();
            if (touch == null) throw new InvalidOperationException("Oculus Touch profile missing.");
            touch.enabled = true;
            // Preserve custom Built-in shaders until stereo instancing has been evaluated.
            xr.renderMode = OpenXRSettings.RenderMode.MultiPass;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.runInBackground = true;
            SetTeleportLayer();
            EditorUtility.SetDirty(touch);
            EditorUtility.SetDirty(xr);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(targets);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("CAMPUS_XR_CONFIGURATION_COMPLETE");
        }

        static void SetTeleportLayer()
        {
            // Starter Assets reserves interaction layer 31 (not a physics layer).
            var type = typeof(XRInteractionManager).Assembly.GetType("UnityEngine.XR.Interaction.Toolkit.InteractionLayerSettings", true);
            var instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var settings = (UnityEngine.Object)instanceProperty.GetValue(null);
            var serialized = new SerializedObject(settings);
            var names = serialized.FindProperty("m_LayerNames");
            if (names.arraySize != 32) throw new InvalidOperationException("Unexpected interaction layer schema.");
            var layer = names.GetArrayElementAtIndex(31);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != "Teleport")
                throw new InvalidOperationException("Interaction layer 31 is already in use.");
            layer.stringValue = "Teleport";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        [MenuItem("Campus Emergency/XR/Validate PC VR dependencies")]
        public static void Validate()
        {
            var lines = new List<string> { "Campus_XR PC VR dependency validation", "UTC: " + DateTime.UtcNow.ToString("O"), "Unity: " + Application.unityVersion };
            var failures = new List<string>();
            Action<bool, string> check = (ok, label) => { lines.Add((ok ? "PASS: " : "FAIL: ") + label); if (!ok) failures.Add(label); };
            check(Application.unityVersion == "2022.3.62f3", "Unity version preserved");
            var expected = new Dictionary<string,string> {
                {"com.unity.xr.openxr", "1.14.3"}, {"com.unity.xr.interaction.toolkit", "2.6.4"},
                {"com.unity.xr.management", "4.7.0"}, {"com.unity.inputsystem", "1.14.0"},
                {"com.unity.xr.core-utils", "2.5.2"}, {"com.unity.xr.legacyinputhelpers", "2.1.12"}, {"com.unity.mathematics", "1.2.6"} };
            var installed = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            foreach (var pair in expected) check(installed.Any(p => p.name == pair.Key && p.version == pair.Value), pair.Key + " " + pair.Value);
            check(EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64, "Windows x86_64 build target");
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(Group);
            check(general != null && general.InitManagerOnStart && general.Manager != null && general.Manager.activeLoaders.OfType<OpenXRLoader>().Any(), "OpenXR loader enabled at startup");
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(Group);
            check(xr != null && xr.GetFeature<OculusTouchControllerProfile>() != null && xr.GetFeature<OculusTouchControllerProfile>().enabled, "Oculus Touch controller profile");
            check(xr != null && xr.renderMode == OpenXRSettings.RenderMode.MultiPass, "Multi Pass for existing custom shaders");
            check(PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64).SequenceEqual(new[] { GraphicsDeviceType.Direct3D11 }), "Direct3D11");
#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            check(true, "Both input backends compiled and enabled");
#else
            check(false, "Both input backends compiled and enabled; restart required");
#endif
            var inputs = AssetDatabase.FindAssets("t:InputActionAsset", new[] { SamplePath });
            check(inputs.Any(g => AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(g)) != null), "Starter Assets input actions imported");
            check(typeof(UnityEngine.XR.Interaction.Toolkit.TeleportationProvider) != null, "TeleportationProvider assembly available");
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, Group);
            foreach (var issue in issues) {
                lines.Add((issue.error || issue.errorEnteringPlaymode ? "ERROR: " : "WARNING: ") + issue.message);
                if (issue.error || issue.errorEnteringPlaymode) failures.Add(issue.message);
            }
            lines.Add("Headset/Link runtime test: NOT RUN; scene XR rig and teleportation not implemented in this step.");
            lines.Add(failures.Count == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            Directory.CreateDirectory("Docs");
            File.WriteAllLines(ReportPath, lines);
            if (failures.Count != 0) throw new InvalidOperationException(string.Join("\n", failures));
            Debug.Log("CAMPUS_XR_VALIDATION_PASS\n" + string.Join("\n", lines));
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}