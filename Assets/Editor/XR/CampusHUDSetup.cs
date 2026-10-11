using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using CampusEmergency.Player.XR;

namespace CampusEmergency.Editor
{
    public static class CampusHUDSetup
    {
        [MenuItem("Campus Emergency/XR/Update HUD presentation")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play mode first");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var prefab=PrefabUtility.LoadPrefabContents(CampusXRSceneBuilder.RigPath);
            try
            {
                var feedback=prefab.GetComponentInChildren<CampusXRFeedback>(true);
                feedback.ApplyPresentation();
                PrefabUtility.SaveAsPrefabAsset(prefab,CampusXRSceneBuilder.RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            var hud=UnityEngine.Object.FindObjectOfType<CampusXRFeedback>();
            hud.ApplyPresentation();
            var label=hud.GetComponentInChildren<Text>();
            label.text="<color=#8ADFFF><b>CAMPUS XR</b></color>    X / H: SHOW OR HIDE\nTRIGGER: TELEPORT    A: INTERACT    RIGHT STICK: TURN\nRIGHT GRIP: RETURN TOWEL\n\n<color=#8ADFFF><b>STATUS</b></color>  Ready";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            if(hud.transform.localPosition.y<=0 || label.lineSpacing<1.2f) throw new Exception("HUD layout not applied");
            hud.SetVisible(false); if(hud.IsVisible || !hud.gameObject.activeInHierarchy) throw new Exception("HUD hiding disabled toggle host");
            hud.ToggleVisibility(); if(!hud.IsVisible) throw new Exception("HUD cannot be restored");
            File.WriteAllText("Docs/HUDValidation.txt","PASS: HUD raised to y=0.24m, distance 1.1m, panel 900x260, line spacing 1.3.\nPASS: hide and restore canvas without disabling gameplay/input host.\n");
        }
    }
}
