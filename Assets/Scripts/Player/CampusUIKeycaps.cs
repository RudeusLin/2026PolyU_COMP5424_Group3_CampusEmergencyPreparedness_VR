using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
namespace CampusEmergency.Player {
 [RequireComponent(typeof(Text))] public sealed class CampusUIKeycaps:MonoBehaviour {
 private Text label;private string last;private Vector2 lastSize;private readonly List<GameObject> badges=new List<GameObject>();private static Sprite rounded;
 public static string Format(string source,int size)=>Regex.Replace(source,@"LEFT TRIGGER|RIGHT TRIGGER|RIGHT STICK|left trigger|right trigger|right stick|左扳机|右扳机|右摇杆|左扳機|右扳機|右搖桿|(?<![A-Za-z0-9])(?<!Stair )A(?!楼|侧|樓|側|[A-Za-z0-9]| stair| Stair)|(?<![A-Za-z0-9])X(?![A-Za-z0-9])",m=>"\u2002<b><color=#F4D58A><size="+size+">"+m.Value.Replace(" ","\u00A0")+"</size></color></b>\u2002");
 public static void Apply(Text text,string source,int size){text.supportRichText=true;text.text=Format(source,size);if(text.GetComponent<CampusUIKeycaps>()==null)text.gameObject.AddComponent<CampusUIKeycaps>();}
 private void LateUpdate()=>Refresh();
 public void Refresh(){
 if(label==null)label=GetComponent<Text>();if(!label.isActiveAndEnabled)return;
 if(last==label.text && lastSize==label.rectTransform.rect.size)return;last=label.text;lastSize=label.rectTransform.rect.size;
 foreach(var b in badges)if(b!=null){b.SetActive(false);Destroy(b);}badges.Clear();
 var generator=new TextGenerator();generator.Populate(label.text,label.GetGenerationSettings(lastSize));var verts=generator.verts;var groups=new List<Rect>();float units=label.pixelsPerUnit;
 for(int i=0;i+3<verts.Count;i+=4){var color=verts[i].color;if(Mathf.Abs(color.r-244)>3||Mathf.Abs(color.g-213)>3||Mathf.Abs(color.b-138)>3)continue;
 Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
 for(int j=0;j<4;j++){Vector2 v=verts[i+j].position;min=Vector2.Min(min,v/units);max=Vector2.Max(max,v/units);}if(max.x-min.x<.1f)continue;
 var rect=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
 if(groups.Count>0){var old=groups[groups.Count-1];if(rect.xMin-old.xMax<8 && Mathf.Abs(rect.center.y-old.center.y)<14){groups[groups.Count-1]=Rect.MinMaxRect(old.xMin,Mathf.Min(old.yMin,rect.yMin),rect.xMax,Mathf.Max(old.yMax,rect.yMax));continue;}}
 groups.Add(rect);
 }
 foreach(var rect in groups){var outer=Badge("Keycap border",label.rectTransform.parent,new Color(.957f,.835f,.541f));outer.GetComponent<Image>().material=label.material;outer.localPosition=label.rectTransform.localPosition+(Vector3)rect.center;outer.sizeDelta=rect.size+new Vector2(14,10);outer.SetSiblingIndex(label.transform.GetSiblingIndex());badges.Add(outer.gameObject);
 var inner=Badge("Keycap fill",outer,new Color(.025f,.065f,.09f,1));inner.GetComponent<Image>().material=label.material;inner.sizeDelta=outer.sizeDelta-Vector2.one*3;}
 }
 private static RectTransform Badge(string name,Transform parent,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;image.sprite=Rounded();image.type=Image.Type.Sliced;return (RectTransform)go.transform;}
 private static Sprite Rounded(){if(rounded!=null)return rounded;var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);for(int y=0;y<32;y++)for(int x=0;x<32;x++){float dx=Mathf.Max(7-x,Mathf.Max(x-24,0)),dy=Mathf.Max(7-y,Mathf.Max(y-24,0));texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(7.5f-Mathf.Sqrt(dx*dx+dy*dy))));}texture.Apply();rounded=Sprite.Create(texture,new Rect(0,0,32,32),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));return rounded;}
 private void OnDisable(){foreach(var b in badges)if(b!=null)b.SetActive(false);last=null;}
 private void OnDestroy(){foreach(var b in badges)if(b!=null)Destroy(b);}
 }
}
