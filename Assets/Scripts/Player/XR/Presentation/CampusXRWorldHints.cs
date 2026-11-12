using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CampusEmergency.Prototype;
using CampusEmergency.Interaction.FireSafety;

namespace CampusEmergency.Player.XR
{
    /// <summary>Scene-anchored cards with fixed positions and viewer-facing orientation.</summary>
    public sealed class CampusXRWorldHints : MonoBehaviour
    {
        private struct Placement { public Vector3 position;public Quaternion rotation; }
        private sealed class Card { public GameObject root;public Text text; }
        private readonly Dictionary<Transform,Placement> authored=new Dictionary<Transform,Placement>();
        private Card objective,door;
        private CampusXRInput input;
        private PrototypeDoor[] doors;
        private Transform head;
        private Vector3 waypoint;
        public Transform InstructionPanel => objective!=null?objective.root.transform:null;
        public string InstructionText => objective!=null?objective.text.text:string.Empty;
        private static string T(string en,string sc,string tc)=>LanguageSelectionUI.T(en,sc,tc);
        private void Awake()
        {
            input=GetComponent<CampusXRInput>();var origin=GetComponent<Unity.XR.CoreUtils.XROrigin>();head=origin!=null&&origin.Camera!=null?origin.Camera.transform:null;
            objective=BuildCard("Objective World Hint");door=BuildCard("Door World Hint");
        }
        private void Start()
        {
            // Cache authored placements before items can be picked up or doors rotate.
            foreach(var x in FindObjectsOfType<FacilityNotice>())Remember(x.transform);
            foreach(var x in FindObjectsOfType<CampusTowel>())Remember(x.transform);
            foreach(var x in FindObjectsOfType<CampusFaucet>())Remember(x.transform);
            foreach(var x in FindObjectsOfType<CampusExtinguisher>())Remember(x.transform);
            doors=FindObjectsOfType<PrototypeDoor>();
            foreach(var x in doors)Remember(x.transform);
        }
        private Card BuildCard(string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(Image));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=head!=null?head.GetComponent<Camera>():null;
            var rt=(RectTransform)go.transform;rt.sizeDelta=new Vector2(680,240);rt.localScale=Vector3.one*.00125f;
            var background=go.GetComponent<Image>();background.color=new Color(.025f,.075f,.09f,.95f);background.raycastTarget=false;
            var textObject=new GameObject("Instruction",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));textObject.transform.SetParent(go.transform,false);
            var text=textObject.GetComponent<Text>();text.font=LanguageSelectionUI.CurrentFont;text.fontSize=27;text.lineSpacing=1.15f;
            text.alignment=TextAnchor.MiddleLeft;text.color=new Color(.83f,1f,.95f);text.supportRichText=false;text.raycastTarget=false;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=21;text.resizeTextMaxSize=27;
            var tr=(RectTransform)text.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(25,18);tr.offsetMax=new Vector2(-25,-18);
            go.SetActive(false);return new Card{root=go,text=text};
        }
        private Vector3 WallNormal(Transform anchor)
        {
            float nearest=.6f;Vector3 normal=Vector3.zero;
            foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
                foreach(var hit in Physics.RaycastAll(anchor.position,direction,.6f,~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.collider.transform.IsChildOf(anchor)||hit.collider.GetComponentInParent<PrototypeDoor>()!=null||Mathf.Abs(hit.normal.y)>.3f)continue;
                    if(hit.distance<nearest){nearest=hit.distance;normal=hit.normal;}
                }
            if(normal.sqrMagnitude<.1f)
            {
                var delta=head!=null?head.position-anchor.position:-anchor.forward;delta.y=0;
                normal=Mathf.Abs(delta.x)>Mathf.Abs(delta.z)?Vector3.right*Mathf.Sign(delta.x):Vector3.forward*Mathf.Sign(delta.z);
            }
            return normal;
        }
        private Placement Remember(Transform anchor)
        {
            if(authored.TryGetValue(anchor,out var cached))return cached;
            Vector3 normal,position;
            if(anchor.parent!=null&&anchor.parent.name=="WashStation")
            {
                normal=-anchor.parent.forward;position=anchor.parent.TransformPoint(new Vector3(0,1.65f,-.48f));
            }
            else if(anchor.GetComponent<FacilityNotice>() is FacilityNotice notice && notice.Kind==FacilityNotice.FacilityKind.Alarm)
            {
                // Alarm plate/sign are siblings; their colliders must not determine the wall normal.
                normal=anchor.parent!=null && anchor.parent.name.StartsWith("Alarm_") ? -anchor.parent.forward : -anchor.forward;
                position=anchor.position+normal*.55f+Vector3.up*.25f+Vector3.Cross(Vector3.up,normal)*.65f;
            }
            else
            {
                normal=WallNormal(anchor);
                bool onFloor=anchor.name=="Route A Current Objective"||anchor.GetComponent<CampusFireTarget>()!=null||anchor.name.Contains("Assembly");
                position=anchor.position+normal*.55f+Vector3.up*(onFloor?1.45f:.5f);
                if(!onFloor)position+=Vector3.Cross(Vector3.up,normal)*.65f;
            }
            var result=new Placement{position=position,rotation=Quaternion.LookRotation(-normal,Vector3.up)};
            authored[anchor]=result;return result;
        }
        private Transform ResolveAnchor(Transform target)
        {
            // A held extinguisher/pin and held towel retain the original station's card.
            for(var t=target;t!=null&&t!=transform;t=t.parent)if(authored.ContainsKey(t))return t;
            return target;
        }
        private void FaceUser(Card card,Placement placement)
        {
            var away=placement.position-head.position;
            var rotation=placement.rotation;
            if(away.sqrMagnitude>.0001f)
            {
                var up=Mathf.Abs(Vector3.Dot(away.normalized,Vector3.up))>.99f ? Vector3.forward : Vector3.up;
                // UI's readable front is its local -Z side.
                rotation=Quaternion.LookRotation(away,up);
            }
            card.root.transform.SetPositionAndRotation(placement.position,rotation);
        }
        public void Refresh(bool visible)
        {
            var flow=GetComponent<CampusRouteAFlow>();
            if(!visible||head==null||flow==null||flow.Target==null){objective.root.SetActive(false);door.root.SetActive(false);return;}
            var target=flow.ReturnHintAnchor!=null ? flow.ReturnHintAnchor : ResolveAnchor(flow.Target);
            if(target.name=="Route A Current Objective" && Vector3.Distance(waypoint,target.position)>.1f){authored.Remove(target);waypoint=target.position;}
            var placement=Remember(target);
            FaceUser(objective,placement);
            objective.text.font=LanguageSelectionUI.CurrentFont;objective.text.text=flow.Instruction;
            if(target.name=="Route A Current Objective")objective.text.text+="\n"+T("Right trigger: teleport · right stick: turn","右扳机：传送 · 右摇杆：转向","右扳機：傳送 · 右搖桿：轉向");
            objective.root.SetActive(true);
            PrototypeDoor nearest=null;float distance=3.2f;
            if(doors!=null)foreach(var candidate in doors)
            {
                if(candidate==null)continue;
                float d=Vector3.Distance(candidate.transform.position,head.position);
                if(!candidate.IsOpen&&d<distance){nearest=candidate;distance=d;}
            }
            door.root.SetActive(nearest!=null);
            if(nearest!=null)
            {
                var p=Remember(nearest.transform);FaceUser(door,p);
                door.text.font=LanguageSelectionUI.CurrentFont;door.text.text=T("DOOR\nPoint at the door and press right A to open.","开门\n右手指向门，按 A 键打开。","開門\n右手指向門，按 A 鍵打開。");
            }
        }
        private void LateUpdate()=>Refresh(input!=null&&input.HeadTracked&&input.HasSessionFocus&&!LanguageSelectionUI.IsBlocking);
        private void OnDisable(){if(objective!=null)objective.root.SetActive(false);if(door!=null)door.root.SetActive(false);}
        private void OnDestroy(){if(objective!=null)Destroy(objective.root);if(door!=null)Destroy(door.root);}
    }
}
