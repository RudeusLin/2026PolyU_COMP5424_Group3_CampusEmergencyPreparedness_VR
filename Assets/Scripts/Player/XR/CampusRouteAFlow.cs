using System.Linq;
using UnityEngine;
using CampusEmergency.Prototype;
using CampusEmergency.Interaction.FireSafety;

namespace CampusEmergency.Player.XR
{
    /// <summary>Single source of truth for the five-node journey, HUD and navigation.</summary>
    public sealed class CampusRouteAFlow : MonoBehaviour
    {
        public int Node { get; private set; } = 1;
        public bool CanCall => Node == 5 && routeIndex >= Exit.Length;
        public string Instruction { get; private set; }
        public string TargetName { get; private set; }
        public Transform Target { get; private set; }
        private Transform lastExtinguishedFire;
        public Transform ReturnHintAnchor => Node==4 && fireDone && extinguisher!=null && extinguisher.Held!=null ? lastExtinguishedFire : null;
        public readonly bool[] Completed = new bool[5];
        private CampusXRInventory inventory;
        private CampusXRExtinguisherController extinguisher;
        private CampusXRInput input;
        private Transform anchor, head, assembly;
        private FacilityNotice alarm;
        private CampusTowel towel;
        private CampusFaucet tap;
        private CampusExtinguisher equipment;
        private CampusFireTarget[] fires;
        private int routeIndex;
        private Vector3[] activeWaypoints;
        private Vector3 previousFeet;
        private Vector3 Feet => head!=null ? new Vector3(head.position.x,transform.position.y,head.position.z) : transform.position;
        private bool alarmDone;
        private static readonly Vector3[] ToThird = {
            new Vector3(-12,10.8f,3), new Vector3(-14.42f,10.8f,1.8f),
            new Vector3(-14.42f,9,-3), new Vector3(-16.58f,9,-1.65f),
            new Vector3(-16.58f,7.2f,3), new Vector3(-12,7.2f,3), new Vector3(-10.5f,7.2f,0) };
        private static readonly Vector3[] ToB = { new Vector3(9.5f,7.2f,0) };
        private static readonly Vector3[] ToSecond = {
            new Vector3(12,7.2f,3), new Vector3(16.58f,7.2f,1.8f),
            new Vector3(16.58f,5.4f,-3), new Vector3(14.42f,5.4f,-1.65f),
            new Vector3(14.42f,3.6f,3), new Vector3(12,3.6f,3), new Vector3(10.5f,3.6f,0) };
        private static readonly Vector3[] Exit = {
            new Vector3(0,3.6f,0), new Vector3(-10.5f,3.6f,0), new Vector3(-12,3.6f,3),
            new Vector3(-14.42f,3.6f,1.8f), new Vector3(-14.42f,1.8f,-3),
            new Vector3(-16.58f,1.8f,-1.65f), new Vector3(-16.58f,0,3),
            new Vector3(-12,0,3), new Vector3(-10.5f,0,0), new Vector3(0,0,0),
            new Vector3(0,0,-8), new Vector3(0,0,-11) };
        private bool fireDone, extinguisherUsed, descendingB;

        public void Configure(Transform camera, Transform goal) { head = camera; assembly = goal; }
        private void Start()
        {
            inventory = GetComponent<CampusXRInventory>();
            extinguisher = GetComponent<CampusXRExtinguisherController>();
            input = GetComponent<CampusXRInput>();
            anchor = new GameObject("Route A Current Objective").transform;
            anchor.SetParent(transform, true);
            alarm = FindObjectsOfType<FacilityNotice>().Where(x => x.Kind == FacilityNotice.FacilityKind.Alarm)
                .OrderBy(x => Vector3.Distance(x.transform.position, new Vector3(-6.5f,12,1.65f))).FirstOrDefault();
            towel = FindObjectsOfType<CampusTowel>().OrderBy(x => Vector3.Distance(x.transform.position, new Vector3(-7,11,-3))).FirstOrDefault();
            tap = FindObjectsOfType<CampusFaucet>().Where(x => Mathf.Abs(x.transform.position.y-11.8f)<1.5f)
                .OrderBy(x => towel == null ? 0 : Vector3.Distance(x.transform.position,towel.transform.position)).FirstOrDefault();
            equipment = FindObjectsOfType<CampusExtinguisher>().Where(x => x.transform.position.x>0 && Mathf.Abs(x.transform.position.y-8.2f)<1.5f)
                .OrderBy(x => Vector3.Distance(x.transform.position,new Vector3(9.8f,8.2f,1.29f))).FirstOrDefault();
            EnsureBFire("Route A B Door Fire", new Vector3(12.2f,7.26f,2.9f));
            EnsureBFire("Route A B Corner Fire", new Vector3(12f,7.26f,.5f));
            fires = FindObjectsOfType<CampusFireTarget>().Where(x => x.transform.position.x>8 && Mathf.Abs(x.transform.position.y-7.2f)<1.5f).ToArray();
            extinguisher.RefreshTargets();
        }
        private static void EnsureBFire(string name,Vector3 position)
        {
            if (GameObject.Find(name)!=null) return;
            var item=new GameObject(name); item.transform.position=position;
            item.AddComponent<CampusForecourtFire>(); item.AddComponent<CampusFireTarget>();
            item.AddComponent<CampusFireBarrier>().Configure(new Vector3(0,.85f,0),new Vector3(1.5f,1.8f,1.7f));
        }
        private static string T(string en,string sc,string tc) => LanguageSelectionUI.T(en,sc,tc);
        private void Next() { Completed[Node-1] = true; Node++; routeIndex=0; activeWaypoints=null; }
        public void RecordCall() { if (CanCall) Completed[4] = true; }
        public void Tick()
        {
            if (anchor == null || head == null || LanguageSelectionUI.IsBlocking || (input != null && (!input.HeadTracked || !input.HasInteractionFocus))) return;
            Evaluate();
        }
        private void Evaluate()
        {
            var atmosphere = FindObjectOfType<CampusDrillAtmosphere>();
            alarmDone |= atmosphere != null && atmosphere.IsAlarmActive;
            if (Node == 1 && alarmDone) Next();
            if (Node == 2 && inventory.HeldTowel != null && inventory.HeldTowel.IsWet && inventory.IsTowelRaised) Next();
            if(Node==4)
            {
                extinguisherUsed |= extinguisher.Held==equipment && equipment!=null;
                fireDone=fires.Length>0 && fires.All(x=>x!=null && x.Extinguished);
                if(fireDone && lastExtinguishedFire==null)
                    lastExtinguishedFire=fires.OrderByDescending(x=>x.ExtinguishOrder).First().transform;
                // Returning the tool is the immediate next step after the final fire,
                // even if the towel was lowered during extinguishing.
                if(fireDone && extinguisher.Held!=null)
                {
                    Set(extinguisher.Held.transform,T("Fire cleared. Release trigger, press RIGHT GRIP to return extinguisher.","火已扑灭，松开扳机，按右握把归还灭火器。","火已撲滅，鬆開扳機，按右握把歸還滅火器。"));
                    return;
                }
            }
            // Recovery is based on what the player can do now, without erasing completed nodes.
            if (Node >= 3 && transform.position.y > 1.2f && (inventory.HeldTowel == null || !inventory.HeldTowel.IsWet || !inventory.IsTowelRaised))
            {
                if (extinguisher.Held != null && inventory.HeldTowel == null) { Set(extinguisher.Held.transform,T("Return extinguisher with RIGHT GRIP before retrieving a towel.","按右握把归还灭火器后拿取毛巾。","按右握把歸還滅火器後拿取毛巾。")); return; }
                TowelObjective(); return;
            }
            if (Node == 3 && Walk(ToThird,T("Avoid the A stair fire; enter the 3F corridor.","避开A侧下行火势，进入三楼走廊。","避開A側下行火勢，進入三樓走廊。"))) Next();
            if (Node == 4)
            {
                if (fireDone && extinguisherUsed && extinguisher.Held == null)
                {
                    if (!descendingB) { descendingB=true; routeIndex=0; activeWaypoints=null; }
                    if (Walk(ToSecond,T("Fire cleared. Open Stair B and descend to 2F.","火已扑灭，开B楼梯门下至二楼。","火已撲滅，開B樓梯門下至二樓。"))) Next();
                }
            }

            if (Node == 1) Set(alarm != null ? alarm.transform : null,T("FIRE ALARM\nPoint at this button with the right controller and press A.","启动火警\n用右手柄指向此按钮，按 A 键启动警报。","啟動火警\n用右手柄指向此按鈕，按 A 鍵啟動警報。"));
            else if (Node == 2) TowelObjective();
            else if (Node == 3) Walk(ToThird,T("WEST Stair A → 3F corridor; avoid the downward fire.","西侧A楼梯 → 三楼走廊，避开下行火势。","西側A樓梯 → 三樓走廊，避開下行火勢。"));
            else if (Node == 4)
            {
                if (!fireDone && routeIndex < ToB.Length && !Walk(ToB,T("Cross the 3F corridor to EAST Stair B.","沿三楼走廊前往东侧B楼梯。","沿三樓走廊前往東側B樓梯。"))) return;
                if (!fireDone)
                {
                    if (extinguisher.Held == null) Set(equipment != null ? equipment.transform:null,T("Press A to take the 3F B-side extinguisher.","按A拿取三楼B侧灭火器。","按A拿取三樓B側滅火器。"));
                    else if (!extinguisher.Held.PinRemoved) Set(extinguisher.Held.Pin,T("LEFT GRIP within 18cm of the yellow pin.","左手靠近黄色保险销18厘米内，按左握把拔销。","左手靠近黃色保險銷18厘米內，按左握把拔銷。"));
                    else { var fire=fires.Where(x=>x!=null&&!x.Extinguished).OrderBy(x=>Vector3.Distance(transform.position,x.transform.position)).FirstOrDefault(); Set(fire != null ? fire.transform:null,T("B: SPRAY mode; hold trigger on the fire BASE for 3 seconds.","按B切换喷射模式，对准火源根部连续喷射3秒。","按B切換噴射模式，對準火源根部連續噴射3秒。")); }
                }
                else if (extinguisher.Held != null) Set(extinguisher.Held.transform,T("Fire cleared. Release trigger, press RIGHT GRIP to return extinguisher.","火已扑灭，松开扳机，按右握把归还灭火器。","火已撲滅，鬆開扳機，按右握把歸還滅火器。"));
                else Walk(ToSecond,T("Open Stair B and descend to 2F; do not use the broken lower flight.","开B楼梯门下至二楼，勿走下方断裂楼梯。","開B樓梯門下至二樓，勿走下方斷裂樓梯。"));
            }
            else if (Node == 5)
            {
                if (!Walk(Exit,T("Stair A → 1F corridor → MAIN entrance.","A楼梯 → 一楼走廊 → 学校正门。","A樓梯 → 一樓走廊 → 學校正門。"))) return;
                Set(assembly,T("Reach the outdoor assembly point, then use the simulated phone to call 999.","前往室外集合点，使用模拟电话拨打999。","前往室外集合點，使用模擬電話撥打999。"));
            }
        }
        private void TowelObjective()
        {
            if (Node>=3 && inventory.HeldTowel==null)
            {
                // Recover on the player's floor; do not send a descending player back to 4F.
                towel=FindObjectsOfType<CampusTowel>().Where(x=>Mathf.Abs(x.transform.position.y-(transform.position.y+1))<1.4f)
                    .OrderBy(x=>Vector3.Distance(x.transform.position,transform.position)).FirstOrDefault();
            }
            if (inventory.HeldTowel!=null && !inventory.HeldTowel.IsWet)
                tap=FindObjectsOfType<CampusFaucet>().Where(x=>Mathf.Abs(x.transform.position.y-(transform.position.y+1))<1.4f)
                    .OrderBy(x=>Vector3.Distance(x.transform.position,transform.position)).FirstOrDefault();
            if (inventory.HeldTowel == null) Set(towel != null ? towel.transform:null,T("4F washroom opposite 401: press A to take the towel.","401对门四楼洗手间：按A拿毛巾。","401對門四樓洗手間：按A拿毛巾。"));
            else if (!inventory.HeldTowel.IsWet) Set(tap != null ? tap.transform:null,T("Press A at the tap to wet the towel.","对水龙头按A打湿毛巾。","對水龍頭按A打濕毛巾。"));
            else Set(inventory.HeldTowel.transform,T("Press LEFT TRIGGER once to raise the wet towel.","按一次左扳机举起湿毛巾。","按一次左扳機舉起濕毛巾。"));
        }
        private bool Walk(Vector3[] points,string text)
        {
            var feet=Feet;
            if(activeWaypoints!=points){activeWaypoints=points;previousFeet=feet;}
            // Entering a later leg means its preceding waypoints have been passed.
            // Height and lateral bounds prevent skipping to the flight across a stairwell.
            for(int i=points.Length-2;i>=routeIndex;i--)
                if(OnLeg(feet,points[i],points[i+1],false)){routeIndex=i+1;break;}
            while(routeIndex<points.Length && (At(points[routeIndex]) || SweptPast(previousFeet,feet,points[routeIndex])
                || (routeIndex==points.Length-1 && points.Length>1 && OnLeg(feet,points[routeIndex-1],points[routeIndex],true))))routeIndex++;
            // Stage hand-offs also accept arrival farther along the continuing corridor.
            // This covers a single teleport across both a terminal point and a turn.
            if((points==ToThird && OnLeg(feet,points[points.Length-1],ToB[0],false))
                || (points==ToSecond && OnLeg(feet,points[points.Length-1],Exit[0],false))
                || (points==ToB && OnLeg(feet,new Vector3(-10.5f,7.2f,0),points[0],true)))routeIndex=points.Length;
            previousFeet=feet;
            if (routeIndex >= points.Length) return true;
            SetPoint(points[routeIndex], text); return false;
        }
        private bool At(Vector3 p) { var d=Feet-p; return Mathf.Abs(d.y)<.65f && new Vector2(d.x,d.z).magnitude<1.25f; }
        private static bool SweptPast(Vector3 from,Vector3 to,Vector3 point)
        {
            var move=to-from;if(move.sqrMagnitude<.01f)return false;
            float t=Vector3.Dot(point-from,move)/move.sqrMagnitude;
            if(t<=0 || t>=1)return false;
            var closest=from+move*t;var delta=closest-point;
            return Mathf.Abs(delta.y)<.5f && new Vector2(delta.x,delta.z).magnitude<.8f;
        }
        private static bool OnLeg(Vector3 feet,Vector3 a,Vector3 b,bool beyondEnd)
        {
            var leg=b-a;float length=leg.magnitude;if(length<.1f)return false;
            float t=Vector3.Dot(feet-a,leg)/leg.sqrMagnitude;
            if(beyondEnd ? (t<1 || (t-1)*length>12f) : (t<.05f || t>1f))return false;
            var delta=feet-(a+leg*t);
            return Mathf.Abs(delta.y)<.45f && new Vector2(delta.x,delta.z).magnitude<.7f;
        }
        private void SetPoint(Vector3 point,string text) { anchor.position=point; Set(anchor,text); }
        private void Set(Transform value,string text)
        {
            Target=value; Instruction=text;
            if (value==null) { Instruction=T("Required scene object is missing. Restart after checking scene setup.","缺少流程对象，请检查场景配置后重新开始。","缺少流程物件，請檢查場景配置後重新開始。"); TargetName=Instruction; return; }
            TargetName=Instruction;
        }
        public string Debrief()
        {
            string[] names={T("401 alarm","401启动火警","401啟動火警"),T("Wet towel raised","湿毛巾举起","濕毛巾舉起"),T("A stair to 3F corridor","A楼梯下三楼并改道","A樓梯下三樓並改道"),T("B fire cleared / extinguisher returned / 2F reached","B侧灭火、归还并下二楼","B側滅火、歸還並下二樓"),T("Main entrance / assembly / simulated 999 call","正门撤离、集合及模拟999报警","正門撤離、集合及模擬999報警")};
            return string.Join("\n\n",names.Select((name,i)=>(i+1)+"  "+name+" — "+(Completed[i]?T("RECORDED","已记录","已記錄"):T("NOT RECORDED: repeat this step","未记录：请重新练习此步骤","未記錄：請重新練習此步驟"))))+"\n\n"+T("Records confirm interactions and checkpoints only; gaze, hazard recognition and real-world competence are not measured.","记录仅确认交互和位置节点，不推断注视、危险识别或真实应急能力。","記錄僅確認互動和位置節點，不推斷注視、危險識別或真實應急能力。");
        }
    }
}
