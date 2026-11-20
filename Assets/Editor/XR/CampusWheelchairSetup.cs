using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampusEmergency.Player.XR;
namespace CampusEmergency.Editor
{
 public static class CampusWheelchairSetup
 {
  public static void Apply()
  {
   EditorSceneManager.OpenScene("Assets/Scenes/TeachingBuildingCampusXR.unity");
   var flow=Object.FindObjectOfType<CampusRouteAFlow>();

   var rescue=CampusWheelchairRescue.Create(flow,flow!=null ? flow.transform : null);
   GameObject.Find("Refuge wheelchair parking").transform.position=CampusWheelchairRescue.ParkingPosition;
   // Persist generated materials as assets so the saved model survives editor reload.
   string folder="Assets/Environment/Wheelchair";
   if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Environment","Wheelchair");
   int i=0;
   foreach(var renderer in rescue.GetComponentsInChildren<Renderer>())
   {var material=renderer.sharedMaterial;if(!AssetDatabase.Contains(material))AssetDatabase.CreateAsset(material,folder+"/Part"+(i++)+".mat");}
   EditorSceneManager.MarkSceneDirty(rescue.gameObject.scene);
   EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
   Debug.Log("WHEELCHAIR_SETUP_PASS: model persisted at 2F corridor centre");
  }
  public static void ApplyAndValidate() {Apply();CampusRouteAValidation.Begin();}
  public static void ValidateRoute() {EditorSceneManager.OpenScene("Assets/Scenes/TeachingBuildingCampusXR.unity");CampusRouteAValidation.Begin();}
 }
}
