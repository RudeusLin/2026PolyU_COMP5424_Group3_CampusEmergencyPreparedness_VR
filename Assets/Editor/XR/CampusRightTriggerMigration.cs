using System;
using System.IO;
using System.Linq;
using CampusEmergency.Player.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusEmergency.Editor
{
    public static class CampusRightTriggerMigration
    {
        public static void Apply()
        {
            const string backup = "Tools/XRSetup/BeforeRightTriggerTeleport/";
            Directory.CreateDirectory(backup);
            foreach (string path in new[] { CampusXRSceneBuilder.RigPath, CampusXRSceneBuilder.XRScene })
            {
                string copy = backup + Path.GetFileName(path) + ".txt";
                if (!File.Exists(copy)) File.Copy(path, copy);
            }
            var root = PrefabUtility.LoadPrefabContents(CampusXRSceneBuilder.RigPath);
            try { SetRightHand(root); PrefabUtility.SaveAsPrefabAsset(root, CampusXRSceneBuilder.RigPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            var teleport = UnityEngine.Object.FindObjectOfType<CampusTeleportController>();
            SetRightHand(teleport.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(teleport);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            CampusXRSyncValidation.Validate();
            Debug.Log("CAMPUS_RIGHT_TRIGGER_MIGRATION_PASS");
        }
        static void SetRightHand(GameObject root)
        {
            var right = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Right Controller Aim");
            var serialized = new SerializedObject(root.GetComponent<CampusTeleportController>());
            serialized.FindProperty("rayOrigin").objectReferenceValue = right;
            serialized.FindProperty("rayDistance").floatValue = 12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
