using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusEmergency.Prototype;
using CampusEmergency.Player.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    public static class CampusXRSyncValidation
    {
        // Additive migration: never rebuild the existing rig or replace its settings.
        public static void Integrate()
        {
            var root = PrefabUtility.LoadPrefabContents(CampusXRSceneBuilder.RigPath);
            try
            {
                CampusXRSceneBuilder.ConfigureInventory(root);
                PrefabUtility.SaveAsPrefabAsset(root, CampusXRSceneBuilder.RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            var origin = Object.FindObjectOfType<XROrigin>();
            if (origin == null) throw new InvalidOperationException("Saved XR rig missing.");
            if (origin.GetComponent<CampusXRInventory>() == null)
                throw new InvalidOperationException("Inventory did not propagate from the preserved prefab.");
            foreach (var collider in Object.FindObjectsOfType<Collider>())
                if (CampusXRSceneBuilder.IsWalkableName(collider.name) && !collider.isTrigger
                    && collider.GetComponent<CampusTeleportSurface>() == null)
                    collider.gameObject.AddComponent<CampusTeleportSurface>();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Validate();
        }

        public static void Validate()
        {
            CampusXRTeleportValidation.ValidateScene();
            var lines = new List<string> { "Campus1 -> Campus_XR sync validation", "UTC: " + DateTime.UtcNow.ToString("O") };
            Action<bool, string> check = (ok, message) =>
            {
                lines.Add((ok ? "PASS: " : "FAIL: ") + message);
                File.WriteAllLines("Docs/XRSyncValidation.txt", lines);
                if (!ok) throw new InvalidOperationException(message);
            };
            var taps = Object.FindObjectsOfType<CampusFaucet>().OrderBy(t => t.transform.position.y).ToArray();
            var towels = Object.FindObjectsOfType<CampusTowel>().OrderBy(t => t.transform.position.y).ToArray();
            var inventory = Object.FindObjectOfType<CampusXRInventory>();
            check(taps.Length == 4 && towels.Length == 4, "Four wash stations imported into XR scene");
            check(inventory != null && inventory.TowelHoldPoint != null, "XR towel anchor saved on tracked right controller");
            check(GameObject.Find("Room403").transform.Find("CollaborationTable") != null, "Room 403 remodeled");
            check(Object.FindObjectsOfType<Transform>().Count(t => t.name.StartsWith("RefugeRoom_F")) == 4,
                "Four refuge rooms retained");
            check(GameObject.Find("Restrooms_F04").transform.Find("DoorFrame").localPosition.x < 0,
                "Fourth-floor washroom relocated near room 401");
            foreach (var towel in towels) towel.SendMessage("Awake");
            try
            {
                for (int i = 0; i < towels.Length; i++)
                {
                    var towel = towels[i];
                    Transform home = towel.transform.parent;
                    Vector3 position = towel.transform.localPosition;
                    Quaternion rotation = towel.transform.localRotation;
                    Vector3 scale = towel.transform.localScale;
                    taps[i].Interact(inventory);
                    check(taps[i].IsRunning, "Floor " + (i+1) + ": empty-hand tap on");
                    taps[i].Interact(inventory);
                    check(!taps[i].IsRunning, "Floor " + (i+1) + ": empty-hand tap off");
                    ((IPrototypeInteractable)towel).Interact(inventory);
                    check(inventory.HeldTowel == towel && towel.transform.parent == inventory.TowelHoldPoint
                        && towel.GetComponentsInChildren<Collider>().All(c => !c.enabled), "XR pickup attaches towel; no held collision");
                    towels[(i+1)%4].Interact(inventory);
                    check(inventory.HeldTowel == towel, "Second towel cannot replace held towel");
                    ((IPrototypeInteractable)taps[i]).Interact(inventory);
                    check(towel.IsWet && taps[i].IsRunning, "XR interface wets held towel");
                    taps[i].Interact(inventory);
                    check(!taps[i].IsRunning && towel.IsWet, "Tap off preserves wet state");
                    inventory.ReturnHeldTowel();
                    check(inventory.HeldTowel == null && !towel.IsWet && towel.transform.parent == home
                        && Vector3.Distance(towel.transform.localPosition, position) < .001f
                        && Quaternion.Angle(towel.transform.localRotation, rotation) < .01f
                        && Vector3.Distance(towel.transform.localScale, scale) < .001f
                        && towel.GetComponentsInChildren<Collider>().All(c => c.enabled), "Return restores pose, dry state and pickup colliders");
                }
                var door = Object.FindObjectsOfType<PrototypeDoor>().First();
                var prompt = door.Prompt;
                ((IPrototypeInteractable)door).Interact(inventory);
                check(door.Prompt != prompt, "Original door interaction works with XR actor");
                ((IPrototypeInteractable)door).Interact(inventory);
            }
            finally
            {
                inventory.ReturnHeldTowel();
                foreach (var tap in taps) tap.SetRunning(false);
            }
            lines.Add("Editor API/physics checks only. Headset input and rendering: NOT RUN.");
            lines.Add("RESULT: PASS");
            File.WriteAllLines("Docs/XRSyncValidation.txt", lines);
            Debug.Log("CAMPUS_XR_SYNC_VALIDATION_PASS");
        }
    }
}
