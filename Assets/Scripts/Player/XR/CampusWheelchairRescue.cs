using UnityEngine;
using CampusEmergency.Prototype;

namespace CampusEmergency.Player.XR
{
    /// <summary>Accessible assisted escort: teleport stays available while the chair follows the corridor.</summary>
    public sealed class CampusWheelchairRescue : MonoBehaviour, IPrototypeInteractable
    {
        public bool Escorting { get; private set; }
        public bool Rescued { get; private set; }
        private CampusRouteAFlow flow;
        private Transform player;
        public static CampusWheelchairRescue Active { get; private set; }
        public static bool IsPushing => Active!=null && Active.Escorting;
        public string Blocker { get; private set; }
        private Collider[] chairColliders;
        private bool[] colliderStates;
        private bool pending,started;
        public CampusRefugeCheckpoint Checkpoint { get; private set; }
        private Vector3 pendingFeet,pendingChair;
        private CampusXRInventory inventory;
        private CampusXRInventory.EquipmentSlot savedSlot;
        private Vector3 heading;

        public static Vector3 ParkingPosition => new Vector3(3.75f,3.65f,-5.5f);
        private const float Separation=1.05f;
        public string Prompt => Rescued ? T("Person safe in refuge room", "已送达安全房") : T("[A button] Start / pause wheelchair assistance", "[A] 开始 / 暂停轮椅护送");
        private static string T(string en,string zh) => LanguageSelectionUI.T(en,zh,zh);
        private void OnDisable(){StopEscort();}
        public string Interact(ICampusInteractor interactor = null)
        {
            if(Rescued)return Prompt;
            if(flow==null || flow.Node!=5)return T("Complete the preceding route steps first.","请先完成前面的疏散节点。");
            if(Escorting){StopEscort();return T("Assistance paused. Teleport restored.","护送已暂停，恢复传送。");}
            var feet=Feet;
            heading=started?Vector3.ProjectOnPlane(transform.position-feet,Vector3.up).normalized:Vector3.right;
            if(heading.sqrMagnitude<.5f)heading=Vector3.forward;
            Vector3 stand=transform.position-heading*Separation;
            if(Mathf.Abs(feet.y-transform.position.y)>.45f || !Supported(stand) || !Clear(feet,stand,.23f,1.7f))
                return T("Approach on the same floor with clear space behind the chair.","请从同层靠近，确保轮椅后方有足够推行空间。");
            player.position+=stand-feet;transform.rotation=Quaternion.LookRotation(heading);
            var origin=player.GetComponent<Unity.XR.CoreUtils.XROrigin>();
            if(origin!=null)origin.MatchOriginUpCameraForward(Vector3.up,heading);
            inventory=player.GetComponent<CampusXRInventory>();savedSlot=inventory.Equipped;
            inventory.Equip(CampusXRInventory.EquipmentSlot.Empty);
            started=true;Active=this;Escorting=true;
            chairColliders=GetComponentsInChildren<Collider>(true);colliderStates=new bool[chairColliders.Length];
            for(int i=0;i<chairColliders.Length;i++){colliderStates[i]=chairColliders[i].enabled;chairColliders[i].enabled=false;}
            return T("RIGHT TRIGGER: teleport with wheelchair. A pauses; aim at doors and A to open.","右扳机射线传送，轮椅同步移动；A暂停，对准门按A开门。");
        }
        public void StopEscort()
        {
            bool restore=Escorting;Escorting=false;pending=false;if(Active==this)Active=null;
            if(chairColliders!=null)for(int i=0;i<chairColliders.Length;i++)if(chairColliders[i]!=null)chairColliders[i].enabled=colliderStates[i];
            chairColliders=null;
            if(restore && inventory!=null)inventory.Equip(savedSlot);
        }
        public void Configure(CampusRouteAFlow route,Transform feet)
        {
            flow=route;player=feet;
            if(!started&&!Rescued)transform.rotation=Quaternion.LookRotation(Vector3.right);
            if(Application.isPlaying && player!=null && Checkpoint==null)Checkpoint=CampusRefugeCheckpoint.Create(this);
        }
        public bool PlayerInRefuge => player!=null && Mathf.Abs(Feet.y-3.65f)<.5f && Feet.z< -1.8f && Feet.z> -5.8f && Feet.x>2.8f && Feet.x<5.4f;
        public bool CanConfirmParking(out string reason)
        {
            var goal = ParkingPosition;
            if (Rescued) { reason = T("Wheelchair user safely parked.", "轮椅人物已安全安置。"); return false; }
            if (flow == null || flow.Node != 5 || !Escorting || !PlayerInRefuge || pending
                || Vector3.Distance(transform.position, goal) > 3.5f)
            { reason = T("Escort the wheelchair into the refuge, then aim at the marker and press A.", "请先护送进入安全房，再对准安置点按A确认。"); return false; }
            if (!Supported(goal) || !Clear(transform.position, goal, .43f, 1.65f))
            { reason = T("Clear the route to the parking marker first.", "请先确保轮椅至安置点的通路畅通。"); return false; }
            if (Vector2.Distance(new Vector2(Feet.x, Feet.z), new Vector2(goal.x, goal.z)) < .85f)
            { reason = T("Stand back from the marker, then press A again.", "请离安置点稍远一些，再按A确认。"); return false; }
            reason = string.Empty;
            return true;
        }
        public string ConfirmParking()
        {
            if (!CanConfirmParking(out string reason)) return reason;
            transform.position = ParkingPosition;
            // The refuge doorway is north of the back wall, toward the corridor.
            transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Rescued = true;
            StopEscort();
            Checkpoint?.ShowCompleted();
            return T("CONFIRMED. Person safe. Continue to Stair A.", "安置已确认，轮椅人物已安全停放。请前往A侧楼梯。");
        }
        private Vector3 Feet
        {
            get {var origin=player.GetComponent<Unity.XR.CoreUtils.XROrigin>();var p=origin!=null&&origin.Camera!=null?origin.Camera.transform.position:player.position;return new Vector3(p.x,player.position.y,p.z);}
        }
        private void LateUpdate()
        {
            if(!Escorting || player==null)return;
            if(flow.Node!=5 || Rescued){StopEscort();return;}
            if(pending && Vector3.Distance(Feet,pendingFeet)<.15f)
            {
                transform.position=pendingChair;
                heading=(pendingChair-pendingFeet).normalized;transform.rotation=Quaternion.LookRotation(heading);
                pending=false;

            }
        }
        public bool CanTeleport(Vector3 destination,out Vector3 chair)
        {
            chair=transform.position;
            if(!Escorting || pending || Mathf.Abs(destination.y-3.65f)>.15f)return false;
            // Keep the chair ahead of teleport travel, independent of sideways head gaze.
            Vector3 travel = Vector3.ProjectOnPlane(destination - Feet, Vector3.up);
            Vector3 direction = travel.sqrMagnitude > .01f ? travel.normalized : heading;
            if (direction.sqrMagnitude < .1f) direction = Vector3.forward;
            Blocker = null;
            if (TryEscortPair(destination, direction, out chair)) return true;
            // A landing near a corridor wall may need the existing chair orientation.
            return heading.sqrMagnitude > .1f && TryEscortPair(destination, heading.normalized, out chair);
        }
        private bool TryEscortPair(Vector3 destination, Vector3 direction, out Vector3 chair)
        {
            chair = destination + direction * Separation; chair.y = 3.65f;
            return Supported(destination) && Supported(chair) && ClearEscortPath(Feet, destination, .23f, 1.7f)
                && ClearEscortPath(transform.position, chair, .43f, 1.65f) && Clear(destination, chair, .2f, 1.65f);
        }
        private bool ClearEscortPath(Vector3 from, Vector3 to, float radius, float height)
        {
            if (Clear(from, to, radius, height)) return true;
            // A diagonal sweep clips the refuge jamb even when both end poses are safe.
            // Route only crossings of this doorway through its real centre; every leg
            // still checks solid doors, walls, fire and supported floor.
            const float doorZ = -1.5f;
            if ((from.z - doorZ) * (to.z - doorZ) >= 0f) return false;
            Vector3 outside = new Vector3(4.1f, 3.65f, -.6f);
            Vector3 inside = new Vector3(4.1f, 3.65f, -2.4f);
            Vector3 entry = from.z > doorZ ? outside : inside;
            Vector3 exit = from.z > doorZ ? inside : outside;
            bool clear = Supported(entry) && Supported(exit)
                && Clear(from, entry, radius, height) && Clear(entry, exit, radius, height)
                && Clear(exit, to, radius, height);
            if (clear) Blocker = null;
            return clear;
        }
        public void QueueEscortTeleport(Vector3 destination)
        {
            if(!CanTeleport(destination,out var chair))return;
            pendingFeet=destination;pendingChair=chair;pending=true;
        }
        private bool Ignore(Collider c)=>c.transform.IsChildOf(transform)||c.transform.IsChildOf(player);
        private bool Supported(Vector3 p)
        {
            foreach(var hit in Physics.RaycastAll(p+Vector3.up*.25f,Vector3.down,.6f,~0,QueryTriggerInteraction.Ignore))
                if(!Ignore(hit.collider)&&Vector3.Dot(hit.normal,Vector3.up)>.85f && Mathf.Abs(hit.point.y-3.6f)<.15f)return true;
            return false;
        }
        private bool Clear(Vector3 from,Vector3 to,float radius,float height)
        {
            Vector3 offset=to-from;
            if(offset.magnitude>.001f)
                foreach(var hit in Physics.CapsuleCastAll(from+Vector3.up*(radius+.08f),from+Vector3.up*(height-radius),radius,offset.normalized,offset.magnitude,~0,QueryTriggerInteraction.Ignore))
                    if(!Ignore(hit.collider)){Blocker=hit.collider.name;return false;}
            foreach(var c in Physics.OverlapCapsule(to+Vector3.up*(radius+.08f),to+Vector3.up*(height-radius),radius,~0,QueryTriggerInteraction.Ignore))
                if(!Ignore(c)){Blocker=c.name;return false;}
            return !CampusEmergency.Interaction.FireSafety.CampusFireBarrier.TryIntersect(from+Vector3.up*.5f,to+Vector3.up*.5f,out _);
        }
        public static CampusWheelchairRescue Create(CampusRouteAFlow route,Transform feet)
        {
            var existing=FindObjectOfType<CampusWheelchairRescue>();
            if(existing!=null){existing.Configure(route,feet);return existing;}
            var root=new GameObject("Route A Wheelchair NPC");root.transform.position=new Vector3(0,3.65f,0);
            var rescue=root.AddComponent<CampusWheelchairRescue>();rescue.Configure(route,feet);
            Part(root,"Seat",PrimitiveType.Cube,new Vector3(0,.52f,0),new Vector3(.58f,.12f,.6f),new Color(.12f,.25f,.4f));
            Part(root,"Backrest",PrimitiveType.Cube,new Vector3(0,.85f,-.26f),new Vector3(.58f,.65f,.1f),new Color(.12f,.25f,.4f));
            for(int side=-1;side<=1;side+=2)
            {
                var wheel=Part(root,"Rear wheel",PrimitiveType.Cylinder,new Vector3(side*.36f,.34f,-.15f),new Vector3(.66f,.045f,.66f),Color.black);wheel.localRotation=Quaternion.Euler(0,0,90);
                var caster=Part(root,"Front caster",PrimitiveType.Cylinder,new Vector3(side*.3f,.13f,.38f),new Vector3(.23f,.035f,.23f),Color.gray);caster.localRotation=Quaternion.Euler(0,0,90);
                Part(root,"Armrest",PrimitiveType.Cube,new Vector3(side*.32f,.76f,0),new Vector3(.07f,.07f,.6f),Color.gray);
                Part(root,"Thigh",PrimitiveType.Cube,new Vector3(side*.14f,.65f,.14f),new Vector3(.2f,.2f,.45f),new Color(.17f,.18f,.22f));
                Part(root,"Lower leg",PrimitiveType.Cube,new Vector3(side*.14f,.36f,.35f),new Vector3(.18f,.48f,.18f),new Color(.17f,.18f,.22f));
            }
            Part(root,"Student torso",PrimitiveType.Capsule,new Vector3(0,1.02f,-.05f),new Vector3(.42f,.32f,.28f),new Color(.25f,.48f,.6f));
            Part(root,"Student head",PrimitiveType.Sphere,new Vector3(0,1.49f,-.04f),new Vector3(.26f,.3f,.26f),new Color(.7f,.5f,.36f));
            var parking=new GameObject("Refuge wheelchair parking");parking.transform.position=ParkingPosition;
            return rescue;
        }
        private static Transform Part(GameObject root,string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
        {
            var part=GameObject.CreatePrimitive(type);part.name=name;part.transform.SetParent(root.transform,false);part.transform.localPosition=position;part.transform.localScale=scale;
            var material=new Material(Shader.Find("Standard"));material.color=color;part.GetComponent<Renderer>().sharedMaterial=material;
            return part.transform;
        }
    }
}
