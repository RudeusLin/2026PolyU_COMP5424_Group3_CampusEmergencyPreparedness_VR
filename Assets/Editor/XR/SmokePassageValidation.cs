using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampusEmergency.Player.XR;
using CampusEmergency.Prototype;
namespace CampusEmergency.Editor {
public static class SmokePassageValidation {
static int count;static System.Text.StringBuilder report=new System.Text.StringBuilder();
static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;report.AppendLine("PASS "+label);}
static void Set(object o,string p,object v){o.GetType().GetField("<"+p+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);}
public static void Run(){try{
EditorSceneManager.OpenScene("Assets/Scenes/TeachingBuildingCampusXR.unity");
var gate=new CampusSmokePassageGate();var b=gate.Passage;
Vector3 east=new Vector3(-8.5f,10.835f,0),west=new Vector3(-13,10.835f,0),inside=new Vector3(-10.5f,10.835f,0);
Check(!gate.Allows(east,inside,false),"Unprotected entry blocked");
Check(!gate.Allows(east,west,false),"Full segment crossing cannot skip smoke");
Check(gate.Allows(east,west,true),"Raised wet towel permits full crossing");
Check(gate.Allows(new Vector3(-7,10.835f,0),new Vector3(-7,10.835f,-3),false),"Washroom accessible before towel");
Check(gate.Allows(new Vector3(-8.5f,7.235f,0),new Vector3(-13,7.235f,0),false),"Third floor unaffected");
gate.ObserveFeet(east);gate.ObserveFeet(inside);
Check(gate.Allows(inside,east,false),"Lowered towel allows retreat to entry side");
Check(!gate.Allows(inside,west,false),"Lowered towel blocks opposite exit");
Check(!gate.Allows(inside,new Vector3(-11,10.835f,0),false),"Lowered towel blocks deeper movement");
Check(gate.Allows(inside,west,true),"Re-raising wet towel restores movement");
gate.ObserveFeet(west);Check(gate.Allows(inside,west,false)&&!gate.Allows(inside,east,false),"Reverse entry permits only reverse retreat");
var inventory=UnityEngine.Object.FindObjectOfType<CampusXRInventory>();var towel=UnityEngine.Object.FindObjectOfType<CampusTowel>();
Check(inventory!=null&&towel!=null,"XR scene has inventory and towel");
Set(inventory,"HeldTowel",towel);Set(inventory,"Equipped",CampusXRInventory.EquipmentSlot.Towel);Set(inventory,"IsTowelRaised",true);
Check(!inventory.IsProtectedFromSmoke,"Raised dry towel rejected");
Set(towel,"IsWet",true);Check(inventory.IsProtectedFromSmoke,"Equipped wet raised towel accepted");
Set(inventory,"IsTowelRaised",false);Check(!inventory.IsProtectedFromSmoke,"Wet lowered towel rejected");
Set(inventory,"IsTowelRaised",true);Set(inventory,"Equipped",CampusXRInventory.EquipmentSlot.Empty);Check(!inventory.IsProtectedFromSmoke,"Stored wet towel rejected");
Set(inventory,"Equipped",CampusXRInventory.EquipmentSlot.Extinguisher);Check(!inventory.IsProtectedFromSmoke,"Extinguisher-equipped state rejected");
report.AppendLine("PASS "+count+" checks. Scene was not saved. Headset testing remains required.");File.WriteAllText("Docs/SmokePassageValidation.txt",report.ToString());Debug.Log(report.ToString());EditorApplication.Exit(0);
}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}}
