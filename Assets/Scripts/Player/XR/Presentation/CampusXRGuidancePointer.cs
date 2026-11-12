using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CampusEmergency.Interaction.FireSafety;
using CampusEmergency.Prototype;

namespace CampusEmergency.Player.XR
{
    /// <summary>Depth-tested floor trail from tracked feet to the current objective.</summary>
    [DisallowMultipleComponent]
    public sealed class CampusXRGuidancePointer : MonoBehaviour
    {
        [SerializeField] private Transform head, assemblyPoint;
        private CampusRouteAFlow flow;
        private CampusXRInput input;
        private LineRenderer trail, destination;
        private Material material;
        private Coroutine search;
        private Transform target, plannedTarget;
        private Vector3 plannedStart, plannedEnd;
        private float nextPlan;
        private readonly List<Vector3> path = new List<Vector3>();
        public Transform CurrentTarget => target;
        public int TrailPointCount => trail != null && trail.enabled ? trail.positionCount : 0;
        public void Configure(Transform cameraTransform, Transform assembly) { head=cameraTransform;assemblyPoint=assembly; }
        private void Awake()
        {
            input=GetComponent<CampusXRInput>();
            if(head==null){var origin=GetComponent<Unity.XR.CoreUtils.XROrigin>();if(origin!=null&&origin.Camera!=null)head=origin.Camera.transform;}
            material=new Material(Shader.Find("Sprites/Default"));
            trail=CreateLine("Floor Guidance Trail",.075f);
            destination=CreateLine("Floor Guidance Destination",.035f); destination.loop=true;
        }
        private LineRenderer CreateLine(string name,float width)
        {
            var item=new GameObject(name);item.transform.SetParent(transform,false);
            var line=item.AddComponent<LineRenderer>();line.sharedMaterial=material;
            line.useWorldSpace=true;line.widthMultiplier=width;line.numCornerVertices=3;line.numCapVertices=3;
            line.alignment=LineAlignment.TransformZ;item.transform.rotation=Quaternion.Euler(90,0,0);
            line.startColor=new Color(.12f,1,.68f,1);line.endColor=new Color(.4f,1,.8f,1);
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            line.enabled=false;return line;
        }
        private Vector3 Feet => new Vector3(head.position.x,transform.position.y,head.position.z);
        private void LateUpdate()
        {
            if(head==null)return;
            if(flow==null){flow=GetComponent<CampusRouteAFlow>();if(flow==null)flow=gameObject.AddComponent<CampusRouteAFlow>();flow.Configure(head,assemblyPoint);}
            flow.Tick();target=flow.Target;
            if(target==null || LanguageSelectionUI.IsBlocking || input==null || !input.HeadTracked || !input.HasInteractionFocus){Hide();return;}
            // Hand actions use the existing HUD, never a path leading toward the player's own hand.
            if(target.IsChildOf(transform) && target.name!="Route A Current Objective"){Hide();return;}
            bool changed=target!=plannedTarget || Vector3.Distance(target.position,plannedEnd)>.3f;
            if(changed || (search!=null && Vector3.Distance(Feet,plannedStart)>1f)){CancelSearch();path.Clear();trail.enabled=destination.enabled=false;}
            if(search==null && (changed || Time.unscaledTime>=nextPlan || Vector3.Distance(Feet,plannedStart)>.6f))
            {
                plannedTarget=target;plannedStart=Feet;plannedEnd=target.position;nextPlan=Time.unscaledTime+2f;
                search=StartCoroutine(Plan(plannedStart,plannedEnd));
            }
            Draw();
        }
        private void Draw()
        {
            // Consume already travelled portions of a cached path after a teleport.
            int consumed=0;float nearest=.7f;
            for(int i=1;i<path.Count;i++)
            {
                float distance=Vector3.Distance(Feet,path[i]);
                if(distance<nearest && Clear(Feet,path[i])){nearest=distance;consumed=i;}
            }
            if(consumed>0)path.RemoveRange(0,consumed);
            if(path.Count<2 || Vector3.Distance(Feet,path[0])>1.2f || !Clear(Feet,path[0])){trail.enabled=destination.enabled=false;return;}
            trail.positionCount=path.Count+1;trail.SetPosition(0,Feet+Vector3.up*.045f);
            for(int i=0;i<path.Count;i++)trail.SetPosition(i+1,path[i]+Vector3.up*.045f);
            trail.enabled=true;Vector3 end=path[path.Count-1];destination.positionCount=32;
            for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32;destination.SetPosition(i,end+new Vector3(Mathf.Cos(a)*.18f,.05f,Mathf.Sin(a)*.18f));}
            destination.enabled=true;
        }
        private sealed class Node { public Vector3 p;public Node parent;public float g,f; }
        private static Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.RoundToInt(p.x/.1f),Mathf.RoundToInt(p.y/.15f),Mathf.RoundToInt(p.z/.1f));
        private static bool Project(Vector3 seed,float up,float down,out Vector3 point)
        {
            point=default;float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(seed+Vector3.up*up,Vector3.down,down,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.GetComponent<CampusTeleportSurface>()==null || hit.normal.y<.7f)continue;
                float d=Mathf.Abs(hit.point.y-seed.y);if(d>=nearest)continue;nearest=d;point=hit.point;
            }
            return nearest<float.PositiveInfinity;
        }
        private static bool Standable(Vector3 p)
        {
            // A door is an intended passage: show the route through it, while the HUD
            // prompts opening it and the independent teleport validator still blocks it.
            foreach(var collider in Physics.OverlapCapsule(p+Vector3.up*.55f,p+Vector3.up*1.3f,.18f,~0,QueryTriggerInteraction.Ignore))
                if(collider.GetComponentInParent<PrototypeDoor>()==null)return false;
            return !CampusFireBarrier.TryIntersect(p+Vector3.up*.15f,p+Vector3.up*.15f,out _);
        }
        private static bool Clear(Vector3 a,Vector3 b)
        {
            if(Mathf.Abs(a.y-b.y)>.55f)return false;
            var start=a+Vector3.up*.65f;var delta=b-a;
            foreach(var hit in Physics.RaycastAll(start,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(hit.collider.GetComponentInParent<PrototypeDoor>()==null)return false;
            return !CampusFireBarrier.TryIntersect(a+Vector3.up*.15f,b+Vector3.up*.15f,out _);
        }
        private bool WashStationGoal(Vector3 end,out Vector3 goal)
        {
            goal=default;
            if(target==null || (target.GetComponent<CampusTowel>()==null && target.GetComponent<CampusFaucet>()==null))return false;
            var station=target.parent;
            if(station==null || station.name!="WashStation")return false;
            var local=station.InverseTransformPoint(end);
            var approach=station.TransformPoint(new Vector3(local.x,0,-.95f));
            // Stay on the room-facing side of this counter; never fall back across its wall.
            for(int i=0;i<5;i++)
            {
                var seed=approach+station.right*((i==0?0:(i%2==0?1:-1))*((i+1)/2)*.15f);
                if(Project(seed,.35f,.8f,out var p) && Standable(p)){goal=p;return true;}
            }
            return false;
        }
        private bool Goal(Vector3 start,Vector3 end,out Vector3 goal)
        {
            if(target!=null && target.parent!=null && target.parent.name=="WashStation"
                && (target.GetComponent<CampusTowel>()!=null || target.GetComponent<CampusFaucet>()!=null))
                return WashStationGoal(end,out goal);
            goal=default;float best=float.PositiveInfinity;
            for(int ring=0;ring<5;ring++)for(int i=0;i<(ring==0?1:16);i++)
            {
                float a=i*Mathf.PI/8;var seed=end+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(ring*.45f);
                if(!Project(seed,.2f,3,out var p)||!Standable(p))continue;
                float cost=Vector3.Distance(start,p)+ring*.7f;if(cost<best){best=cost;goal=p;}
            }
            return best<float.PositiveInfinity;
        }
        private IEnumerator Plan(Vector3 start,Vector3 end)
        {
            yield return null;
            if(!Project(start,.4f,1,out start)||!Goal(start,end,out var goal)){search=null;yield break;}
            var first=new Node{p=start};var open=new List<Node>{first};
            var nodes=new Dictionary<Vector3Int,Node>{{Key(start),first}};var closed=new HashSet<Vector3Int>();
            Node found=null;int expanded=0;float frameStart=Time.realtimeSinceStartup;
            while(open.Count>0 && expanded++<6500)
            {
                int best=0;for(int i=1;i<open.Count;i++)if(open[i].f<open[best].f)best=i;
                var n=open[best];open.RemoveAt(best);closed.Add(Key(n.p));
                if(Vector3.Distance(n.p,goal)<.55f && Clear(n.p,goal)){found=n;break;}
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {
                    if(x==0&&z==0)continue;
                    if(!Project(n.p+new Vector3(x*.4f,0,z*.4f),.45f,1,out var p)||Mathf.Abs(p.y-n.p.y)>.4f)continue;
                    var key=Key(p);if(closed.Contains(key)||!Standable(p)||!Clear(n.p,p))continue;
                    if(x!=0&&z!=0 && (!Project(n.p+new Vector3(x*.4f,0,0),.45f,1,out var sa)||!Standable(sa)
                        ||!Project(n.p+new Vector3(0,0,z*.4f),.45f,1,out var sb)||!Standable(sb)))continue;
                    float g=n.g+Vector3.Distance(n.p,p);if(nodes.TryGetValue(key,out var old)&&old.g<=g)continue;
                    var next=old??new Node();next.p=p;next.parent=n;next.g=g;next.f=g+Vector3.Distance(p,goal);
                    if(old==null){nodes.Add(key,next);open.Add(next);}
                }
                if(Time.realtimeSinceStartup-frameStart>.002f){yield return null;frameStart=Time.realtimeSinceStartup;}
            }
            path.Clear();if(found!=null){path.Add(goal);for(var n=found;n!=null;n=n.parent)path.Add(n.p);path.Reverse();}
            search=null;
        }
        private void CancelSearch(){if(search!=null)StopCoroutine(search);search=null;}
        private void Hide(){CancelSearch();path.Clear();plannedTarget=null;if(trail!=null)trail.enabled=false;if(destination!=null)destination.enabled=false;}
        private void OnDisable()=>Hide();
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
