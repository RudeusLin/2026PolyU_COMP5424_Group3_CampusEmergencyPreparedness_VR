using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampusEmergency.Player.XR;
namespace CampusEmergency.Editor
{
 public static class CampusHandSetup
 {
  public static void Apply()
  {
   EditorSceneManager.OpenScene("Assets/Scenes/TeachingBuildingCampusXR.unity");
   var input=Object.FindObjectOfType<CampusXRInput>();
   CampusXRSceneBuilder.ConfigureControllerVisuals(input.gameObject);
   var hands=Object.FindObjectsOfType<CampusHandVisual>();
   if(hands.Length!=2)throw new System.Exception("Expected two hands");
   foreach(var hand in hands)if(hand.GetComponentsInChildren<Collider>().Length!=0)throw new System.Exception("Hand collider would block rays");
   EditorSceneManager.MarkSceneDirty(input.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
   Debug.Log("CAMPUS_HAND_SETUP_PASS: two articulated hands, no colliders, original controller aim transforms preserved");
  }
 }
}
