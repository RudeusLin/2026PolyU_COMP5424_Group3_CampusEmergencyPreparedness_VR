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
        private string rawInstruction;
        public string InstructionText => rawInstruction ?? string.Empty;
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
        private static int ExitFireStage(string source)
        {
            if (source.StartsWith("EXIT FIRE\n") || source.StartsWith("出口火焰\n")) return 1;
            if (source.StartsWith("REMOVE PIN AGAIN\n") || source.StartsWith("重新拔销\n") || source.StartsWith("重新拔銷\n")) return 2;
            if (source.StartsWith("CLEAR THE EXIT\n") || source.StartsWith("清理出口\n")) return 3;
            return 0;
        }
        private void StyleCard(Card card,string source)
        {
            string title=null,body=source;
            if(LanguageSelectionUI.Current==CampusLanguage.SimplifiedChinese)
            {
                if(source.StartsWith("启动火警\n")){title="01 启动火警";body=source.Substring(source.IndexOf('\n')+1);}
                else if(source.StartsWith("开门\n")){title="交互 · 打开房门";body=source.Substring(source.IndexOf('\n')+1);}
                else if(source.StartsWith("401对门四楼洗手间："))title="02 拿取毛巾";
                else if(source.StartsWith("对水龙头按A打湿毛巾。"))title="02 打湿毛巾";
                else if(source.StartsWith("按一次左扳机举起湿毛巾。"))title="02 举起湿毛巾";
                else if(source.StartsWith("西侧A楼梯")||source.StartsWith("避开A侧下行火势"))title="03 避火改道";
                else if(source.StartsWith("沿三楼走廊前往东侧B楼梯。"))title="04 前往B侧楼梯";
                else if(source.StartsWith("按X切换空手，再按A拾取灭火器。"))title="04 拾取灭火器";
                else if(source.StartsWith("按X切换至灭火器。"))title="04 装备灭火器";
                else if(source.StartsWith("右手射线对准黄色保险销"))title="04 拔出保险销";
                else if(source.StartsWith("左手对准火源根部"))title="04 喷射灭火";
                else if(source.StartsWith("火已扑灭")||source.StartsWith("开B楼梯门下至二楼"))title="04 前往二楼";
                else if(source.StartsWith("B侧下行楼梯已断裂"))title="05 护送轮椅人物";
                else if(source.StartsWith("A楼梯 →"))title="06 从正门撤离";
                else if(source.StartsWith("前往室外集合点"))title="06 集合并报警";
            }
            if(LanguageSelectionUI.Current==CampusLanguage.English)
            {
                if(source.StartsWith("FIRE ALARM\n")){title="01 Activate the alarm";body=source.Substring(source.IndexOf('\n')+1);}
                else if(source.StartsWith("DOOR\n")){title="Interact · Open the door";body=source.Substring(source.IndexOf('\n')+1);}
                else if(source.StartsWith("4F washroom opposite 401"))title="02 Pick up the towel";
                else if(source.StartsWith("Press A at the tap"))title="02 Wet the towel";
                else if(source.StartsWith("Press LEFT TRIGGER once"))title="02 Raise the wet towel";
                else if(source.StartsWith("WEST Stair A")||source.StartsWith("Avoid the A stair fire"))title="03 Avoid fire; change route";
                else if(source.StartsWith("Cross the 3F corridor"))title="04 Reach Stair B";
                else if(source.StartsWith("X: empty hands"))title="04 Pick up the extinguisher";
                else if(source.StartsWith("X: equip"))title="04 Equip the extinguisher";
                else if(source.StartsWith("Point RIGHT ray"))title="04 Remove the safety pin";
                else if(source.StartsWith("Hold LEFT trigger")){title="04 Extinguish the fire";body="Aim at the fire base and hold LEFT TRIGGER for 3 seconds.\nRIGHT TRIGGER: teleport.";}
                else if(source.StartsWith("Fire cleared.")||source.StartsWith("Open Stair B"))title="04 Descend to 2F";
                else if(source.StartsWith("B stairs below")){title="05 Assist the wheelchair user";body="The lower B stairs are broken.\nAim at the wheelchair and press A to start/pause escort.\nUse RIGHT TRIGGER to teleport; aim at the door and press A to open.\nInside the refuge, aim at the green marker and press A to confirm parking.";}
                else if(source.StartsWith("Stair A →"))title="06 Leave through the main entrance";
                else if(source.StartsWith("Reach the outdoor assembly"))title="06 Assemble and call";
            }
            if(LanguageSelectionUI.Current==CampusLanguage.TraditionalChinese)
            {
                if(source.StartsWith("啟動火警\n")){title="01 啟動火警";body=source.Substring(source.IndexOf('\n')+1);}
                else if(source.StartsWith("開門\n")){title="互動 · 打開房門";body=source.Substring(source.IndexOf('\n')+1);}
                else if(source.StartsWith("401對門四樓洗手間："))title="02 拿取毛巾";
                else if(source.StartsWith("對水龍頭按A打濕毛巾。"))title="02 打濕毛巾";
                else if(source.StartsWith("按一次左扳機舉起濕毛巾。"))title="02 舉起濕毛巾";
                else if(source.StartsWith("西側A樓梯")||source.StartsWith("避開A側下行火勢"))title="03 避火改道";
                else if(source.StartsWith("沿三樓走廊前往東側B樓梯。"))title="04 前往B側樓梯";
                else if(source.StartsWith("按X切換空手，再按A拾取滅火器。"))title="04 拾取滅火器";
                else if(source.StartsWith("按X切換至滅火器。"))title="04 裝備滅火器";
                else if(source.StartsWith("右手射線對準黃色保險銷"))title="04 拔出保險銷";
                else if(source.StartsWith("左手對準火源根部")){title="04 噴射滅火";body=body.Replace("；右扳機","；\n右扳機");}
                else if(source.StartsWith("火已撲滅")||source.StartsWith("開B樓梯門下至二樓"))title="04 前往二樓";
                else if(source.StartsWith("B側下行樓梯已斷裂")){title="05 護送輪椅人物";body="B側下行樓梯已斷裂。\n對準輪椅按A開始/暫停護送。\n使用右扳機傳送，按A開門進入安全房。\n對準綠色安置點按A確認。";}
                else if(source.StartsWith("A樓梯 →"))title="06 從正門撤離";
                else if(source.StartsWith("前往室外集合點"))title="06 集合並報警";
            }
            int exitStage = ExitFireStage(source);
            if (exitStage > 0)
            {
                title = exitStage == 1 ? T("06 Exit fire · Equip", "06 出口灭火 · 装备", "06 出口滅火 · 裝備")
                    : exitStage == 2 ? T("06 Exit fire · Remove pin", "06 出口灭火 · 拔销", "06 出口滅火 · 拔銷")
                    : T("06 Exit fire · Spray", "06 出口灭火 · 喷射", "06 出口滅火 · 噴射");
                body = exitStage == 1 ? T("Three fires block the main entrance.\nPress X to equip your extinguisher.", "正门被三簇火焰封堵。\n按X切换出灭火器。", "正門被三簇火焰封堵。\n按X切換出滅火器。")
                    : exitStage == 2 ? T("Re-equipping restores the safety pin.\nAim the RIGHT ray at the pin and press A.", "重新装备后需要再次拔销。\n右手射线对准保险销，按A拔出。", "重新裝備後需要再次拔銷。\n右手射線對準保險銷，按A拔出。")
                    : T("Aim at each fire BASE and hold LEFT TRIGGER\nfor 3 continuous seconds per fire.\nClear all three fires, then use RIGHT TRIGGER to leave.", "逐簇对准火焰根部，按住左扳机。\n每簇连续喷射3秒，扑灭全部三簇。\n通道清空后，按右扳机传送离开。", "逐簇對準火焰根部，按住左扳機。\n每簇連續噴射3秒，撲滅全部三簇。\n通道清空後，按右扳機傳送離開。");
            }
            if(title=="04 喷射灭火")body=body.Replace("；右扳机","；\n右扳机");
            if(title=="05 护送轮椅人物")body="B侧下行楼梯已断裂。\n对准轮椅按A开始/暂停护送。\n使用右扳机传送，按A开门进入安全房。\n对准绿色安置点按A确认。";
            var text=card.text;text.supportRichText=title!=null;text.font=LanguageSelectionUI.CurrentFont;
            text.fontSize=title!=null?(LanguageSelectionUI.Current==CampusLanguage.English?28:30):27;text.resizeTextForBestFit=title==null;text.resizeTextMinSize=21;text.resizeTextMaxSize=27;
            text.lineSpacing=title!=null?1.28f:1.15f;text.color=title!=null?new Color(.9f,.95f,.96f):new Color(.83f,1,.95f);
            text.text=title==null?source:"<b><color=#66E3D0><size=34>"+title+"</size></color></b>\n"+CampusUIKeycaps.Format(body,LanguageSelectionUI.Current==CampusLanguage.English?30:34);
            if(title!=null && text.GetComponent<CampusUIKeycaps>()==null)text.gameObject.AddComponent<CampusUIKeycaps>();
            ((RectTransform)card.root.transform).sizeDelta=new Vector2(LanguageSelectionUI.Current==CampusLanguage.English?920:760,title=="05 Assist the wheelchair user"?460:(title=="05 护送轮椅人物"||title=="05 護送輪椅人物")?400:LanguageSelectionUI.Current==CampusLanguage.English?300:240);
            card.root.GetComponent<Canvas>().sortingOrder = exitStage > 0 ? 520 : 0;
            var background = card.root.GetComponent<Image>();
            background.material = exitStage > 0 ? LanguageSelectionUI.OverlayMaterial : null;
            background.color = new Color(.025f, .075f, .09f, exitStage > 0 ? .98f : .95f);
            text.material = exitStage > 0 ? LanguageSelectionUI.OverlayMaterial : null;
            if (exitStage > 0) ((RectTransform)card.root.transform).sizeDelta = new Vector2(LanguageSelectionUI.Current == CampusLanguage.English ? 1060 : 860, LanguageSelectionUI.Current == CampusLanguage.English ? 380 : 310);
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
            var placement = ExitFireStage(flow.Instruction) > 0
                ? new Placement { position = new Vector3(-1.4f, 1.85f, -6.1f), rotation = Quaternion.identity }
                : Remember(target);
            FaceUser(objective,placement);
            objective.text.font=LanguageSelectionUI.CurrentFont;objective.text.text=flow.Instruction;
            if(target.name=="Route A Current Objective")objective.text.text+="\n"+T("Right trigger: teleport · right stick: turn","右扳机：传送 · 右摇杆：转向","右扳機：傳送 · 右搖桿：轉向");
            rawInstruction=objective.text.text;StyleCard(objective,rawInstruction);
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
                StyleCard(door,door.text.text);
            }
        }
        private void LateUpdate()=>Refresh(input!=null&&input.HeadTracked&&input.HasSessionFocus&&!LanguageSelectionUI.IsBlocking);
        private void OnDisable()
        {
            if(objective!=null && objective.root!=null)objective.root.SetActive(false);
            if(door!=null && door.root!=null)door.root.SetActive(false);
        }
        private void OnDestroy()
        {
            if(objective!=null && objective.root!=null)Destroy(objective.root);
            if(door!=null && door.root!=null)Destroy(door.root);
        }
    }
}
