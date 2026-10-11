using System;
using System.IO;
using CampusEmergency.Prototype;
using CampusEmergency.Player.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    public static class CampusSpawnValidation
    {
        public static void Validate()
        {
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.SourceScene);
            var player = Object.FindObjectOfType<PrototypeExplorer>();
            var serialized = new SerializedObject(player);
            var spawn = (Transform)serialized.FindProperty("spawnFourthFloor").objectReferenceValue;
            var walk = (Camera)serialized.FindProperty("walkCamera").objectReferenceValue;
            var overview = (Camera)serialized.FindProperty("overviewCamera").objectReferenceValue;
            // Invoke the actual startup path; no F4/EnterFloor call in the test.
            player.SendMessage("Awake");
            Require(Vector3.Distance(player.transform.position, spawn.position) < .001f, "Startup feet match F4 spawn");
            Require(Quaternion.Angle(player.transform.rotation, spawn.rotation) < .01f, "Startup faces F4 direction");
            Require(walk.enabled && !overview.enabled, "Walk camera active; overview inactive");
            Require(Vector3.Distance(walk.transform.position, spawn.position + Vector3.up * 1.65f) < .001f,
                "Initial eye is 1.65m above F4 feet");
            Require(player.GetComponent<CharacterController>().enabled, "Character movement enabled at startup");
            var feet = spawn.position;
            Physics.SyncTransforms();
            Require(Physics.Raycast(feet + Vector3.up * .1f, Vector3.down, out var floor, .4f),
                "Floor directly beneath spawn");
            File.WriteAllText("Docs/SpawnValidation.txt", "PASS: Actual desktop Awake starts at F4 without key press.\n"
                + "Feet: " + feet.ToString("F3") + "\nEye: " + walk.transform.position.ToString("F3")
                + "\nPASS: Walking camera, facing, controller and floor support.\n");
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            var xrSession = new SerializedObject(Object.FindObjectOfType<CampusXRSession>());
            var xrSpawn = (Transform)xrSession.FindProperty("spawn").objectReferenceValue;
            Require(xrSpawn != null && Vector3.Distance(xrSpawn.position, feet) < .001f, "XR uses same F4 anchor");
            var origin = Object.FindObjectOfType<XROrigin>();
            Require(Vector3.Distance(origin.transform.position, feet) < .001f, "Saved XR origin starts at F4 anchor");
            CampusXRTeleportValidation.ValidateScene();
            File.AppendAllText("Docs/SpawnValidation.txt", "PASS: XR spawn and saved origin use the same F4 point.\n"
                + "PASS: Existing XR teleport regression.\nEditor checks only; headset test NOT RUN.\nRESULT: PASS\n");
            Debug.Log("CAMPUS_SPAWN_VALIDATION_PASS");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
