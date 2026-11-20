using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace CampusEmergency.Player
{
 public static class CampusClassroomRouteBoard
 {
  public static void Show()
  {
   if(GameObject.Find("401 Evacuation Route Board")!=null)return;
   var room=Object.FindObjectsOfType<Transform>().FirstOrDefault(t=>t.name=="Classroom_Detailed" && t.position.y>10 && t.position.x<0);
   if(room==null)return;
   foreach(var text in room.GetComponentsInChildren<TextMesh>(true))
    if(text.transform.position.x<room.position.x-4)text.gameObject.SetActive(false);
   var go=new GameObject("401 Evacuation Route Board",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(room,false);
   go.transform.localPosition=new Vector3(-4.37f,1.94f,1);go.transform.localRotation=Quaternion.Euler(0,-90,0);go.transform.localScale=Vector3.one*.001f;
   var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
   var map=CampusRouteMapUI.Build(go.transform,true);var rect=(RectTransform)map.transform;
   rect.anchoredPosition=Vector2.zero;rect.localScale=Vector3.one;
   foreach(var graphic in go.GetComponentsInChildren<Graphic>()){graphic.material=null;graphic.raycastTarget=false;}
  }
 }
}
