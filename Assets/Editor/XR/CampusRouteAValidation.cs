using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CampusEmergency.Player;
using CampusEmergency.Player.XR;
using CampusEmergency.Prototype;
using CampusEmergency.Interaction.FireSafety;
using UnityEditor;
using UnityEngine;
using Unity.XR.CoreUtils;
using Object=UnityEngine.Object;

namespace CampusEmergency.Editor
{
    [InitializeOnLoad]
    public static class CampusRouteAValidation
    {
        const string Pending="CampusRouteAValidation.Pending";
        const string Report=".utmp/RouteAValidation.txt";
        static CampusRouteAValidation() { EditorApplication.playModeStateChanged+=OnPlay; }
        [MenuItem("Campus Emergency/XR/Validate fixed Route A")]
        public static void Begin()
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path!=CampusXRSceneBuilder.XRScene)
                throw new InvalidOperationException("Open the XR scene before validation.");
            SessionState.SetBool(Pending,true);
            if(EditorApplication.isPlaying) EditorApplication.delayCall+=Run;
            else EditorApplication.isPlaying=true;
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false)) EditorApplication.delayCall+=Run;
        }
        static object Get(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(o);
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(o,value);
        static void Invoke(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
        static void Check(bool ok,string text) { File.AppendAllText(Report,(ok?"PASS: ":"FAIL: ")+text+"\n"); if(!ok)throw new Exception(text); }
        static void Step(CampusRouteAFlow flow)=>Invoke(flow,"Evaluate");
        static void EyeHeight(XROrigin origin)
        {
            Check(origin.RequestedTrackingOriginMode==XROrigin.TrackingOriginMode.Device,"Session requests device origin instead of runtime floor height");
            Check(Mathf.Abs(origin.CameraYOffset-1.65f)<.001f,"Standing eye-height offset is 1.65m");
            var offset=origin.CameraFloorOffsetObject.transform;var camera=origin.Camera.transform;
            var oldOffset=offset.localPosition;var oldHead=camera.localPosition;
            var oldPosition=origin.transform.position;var oldRotation=origin.transform.rotation;
            try
            {
                // Simulate the recentered Device-space poses supplied by OpenXR.
                offset.localPosition=new Vector3(oldOffset.x,origin.CameraYOffset,oldOffset.z);
                camera.localPosition=Vector3.zero;
                Check(Mathf.Abs(origin.CameraInOriginSpaceHeight-1.65f)<.001f,"Recentered headset stands 1.65m above feet");
                camera.localPosition=Vector3.down*.45f;
                Check(Mathf.Abs(origin.CameraInOriginSpaceHeight-1.2f)<.001f,"Physical crouching still lowers eyes naturally");
                camera.localPosition=new Vector3(.3f,0,.2f);
                var provider=origin.GetComponent<UnityEngine.XR.Interaction.Toolkit.TeleportationProvider>();
                var landing=new Vector3(-10,3.65f,0);
                Check(provider.QueueTeleportRequest(new UnityEngine.XR.Interaction.Toolkit.TeleportRequest {
                    destinationPosition=landing,matchOrientation=UnityEngine.XR.Interaction.Toolkit.MatchOrientation.WorldSpaceUp}),"Height regression queues real XRI teleport");
                Invoke(provider,"Update");
                Check(Mathf.Abs(origin.Camera.transform.position.y-landing.y-1.65f)<.001f,"Teleport preserves standing eye height on another floor");
                Check(Vector2.Distance(new Vector2(camera.position.x,camera.position.z),new Vector2(landing.x,landing.z))<.001f,"Teleport compensates physical head offset");
            }
            finally
            {
                offset.localPosition=oldOffset;camera.localPosition=oldHead;
                origin.transform.SetPositionAndRotation(oldPosition,oldRotation);
            }
        }
        static void Trail(CampusXRGuidancePointer guide,Vector3 start,Vector3 end,string title)
        {
            var plan=(IEnumerator)guide.GetType().GetMethod("Plan",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(guide,new object[]{start,end});
            int iterations=0;while(plan.MoveNext() && iterations++<10000) { }
            var points=(List<Vector3>)Get(guide,"path");
            Check(points.Count>1,title+" has a grounded route");
            Check(Vector3.Distance(points[0],start)<.5f,title+" starts at player feet");
            var clear=guide.GetType().GetMethod("Clear",BindingFlags.NonPublic|BindingFlags.Static);
            for(int i=1;i<points.Count;i++)Check((bool)clear.Invoke(null,new object[]{points[i-1],points[i]}),title+" segment "+i+" avoids walls, fire and floor jumps");
        }
        static void Capture(Camera camera,string filename)
        {
            Directory.CreateDirectory(".utmp");
            Canvas.ForceUpdateCanvases();
            var previous=RenderTexture.active; var target=new RenderTexture(1600,1000,24);
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            File.WriteAllBytes(".utmp/"+filename,image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(image);Object.DestroyImmediate(target);
        }
        static void Walk(CampusRouteAFlow flow,string field)
        {
            var points=(Vector3[])typeof(CampusRouteAFlow).GetField(field,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            foreach(var p in points){flow.transform.position=p; Step(flow);}
        }
        static void Run()
        {
            SessionState.SetBool(Pending,false);
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(Report,"Fixed Route A integration checks (Editor Play Mode, no headset).\n");
            try
            {
                var origin=Object.FindObjectOfType<XROrigin>();
                Check(origin!=null,"XR scene loaded");
                EyeHeight(origin);
                CampusClassroomRouteBoard.Show();
                Check(GameObject.Find("401 Evacuation Route Board")!=null,"401 blackboard displays shared localized map");
                var ui=Object.FindObjectOfType<LanguageSelectionUI>();
                // A connected controller may select the language before the validation callback.
                ui.enabled=false;
                Set(ui,"selectionComplete",false);Set(ui,"briefingVisible",false);Set(ui,"mapExpanded",false);
                Set(ui,"emergencyCallVisible",false);Set(ui,"completionVisible",false);
                ((Canvas)Get(ui,"canvas")).gameObject.SetActive(true);
                typeof(LanguageSelectionUI).GetMethod("Select",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{0});
                var map=ui.GetComponentsInChildren<CampusRouteMapUI>().Single();
                var sharedMapTexture=map.MapTexture;
                Check(sharedMapTexture!=null,"Localized route map texture is bundled in Resources");
                Check(map.DisplayLanguage==CampusLanguage.English && map.MapTexture==Resources.Load<Texture2D>("CampusRouteMap/RouteA_Base"),"English selection uses supplied English route image");
                typeof(LanguageSelectionUI).GetMethod("Select",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{1});
                var enlargedMap=ui.GetComponentsInChildren<CampusRouteMapUI>().Single();
                Check(enlargedMap.MapTexture==sharedMapTexture && Mathf.Abs(enlargedMap.transform.localScale.x-.9f)<.001f && LanguageSelectionUI.IsBlocking,"Map enlarges without starting gameplay or changing language");
                Capture(origin.Camera,"route-map-enlarged-en.png");
                typeof(LanguageSelectionUI).GetMethod("Select",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{1});
                Capture(origin.Camera,"route-a-intro-en.png");
                typeof(LanguageSelectionUI).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,CampusLanguage.SimplifiedChinese);
                Invoke(ui,"BuildBriefing");Capture(origin.Camera,"route-a-intro-zh.png");
                map=ui.GetComponentsInChildren<CampusRouteMapUI>().Single();
                Check(map.DisplayLanguage==CampusLanguage.SimplifiedChinese && map.MapTexture==Resources.Load<Texture2D>("CampusRouteMap/RouteA_SC") && map.MapTexture!=sharedMapTexture,"Simplified Chinese map uses supplied Simplified Chinese image");
                typeof(LanguageSelectionUI).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,CampusLanguage.TraditionalChinese);
                map.RefreshLanguage();
                Check(map.DisplayLanguage==CampusLanguage.TraditionalChinese && map.MapTexture==Resources.Load<Texture2D>("CampusRouteMap/RouteA_TC"),"Existing map switches to supplied Traditional Chinese image");
                Invoke(ui,"BuildBriefing");Capture(origin.Camera,"route-a-intro-tc.png");
                typeof(LanguageSelectionUI).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,CampusLanguage.English);
                Invoke(ui,"BuildBriefing");
                Check(((RectTransform[])Get(ui,"choices")).Count(x=>x!=null)==2,"Introduction provides Begin and Enlarge Map buttons without a route selector");
                var guide=origin.GetComponent<CampusXRGuidancePointer>(); guide.enabled=false;
                var flow=origin.GetComponent<CampusRouteAFlow>();
                if(flow==null){flow=origin.gameObject.AddComponent<CampusRouteAFlow>();flow.Configure(origin.Camera.transform,Object.FindObjectsOfType<Transform>().First(x=>x.name=="AssemblyPoint"));Invoke(flow,"Start");}
                var inventory=origin.GetComponent<CampusXRInventory>();
                var extinguisher=origin.GetComponent<CampusXRExtinguisherController>();
                Check(LanguageSelectionUI.SelectedRoute==0,"Route A is fixed");
                Step(flow); Check(flow.Target!=null && flow.Node==1,"Alarm target resolves");
                Invoke(ui,"BeginExperience");
                var hints=origin.GetComponent<CampusXRWorldHints>();
                Check(hints!=null,"Scene-anchored world hints installed");
                hints.Refresh(true);
                Check(!hints.InstructionPanel.IsChildOf(origin.Camera.transform),"Instruction card is not parented to the headset");
                var fixedHintPosition=hints.InstructionPanel.position;var fixedHintRotation=hints.InstructionPanel.rotation;
                var oldHeadPosition=origin.Camera.transform.localPosition;var oldHeadRotation=origin.Camera.transform.localRotation;
                origin.Camera.transform.localPosition+=Vector3.right*.2f;origin.Camera.transform.localRotation=Quaternion.Euler(0,90,0);
                hints.Refresh(true);
                Check(Vector3.Distance(fixedHintPosition,hints.InstructionPanel.position)<.001f,"Head movement does not change the instruction card position");
                Check(Vector3.Dot(hints.InstructionPanel.up,Vector3.up)>.999f,"Mounted alarm card stays vertical after head movement");
                Check(Quaternion.Angle(fixedHintRotation,hints.InstructionPanel.rotation)<.01f,"Mounted alarm card retains its wall alignment after head movement");
                origin.Camera.transform.localPosition=oldHeadPosition;origin.Camera.transform.localRotation=oldHeadRotation;
                var feedback=origin.GetComponentInChildren<CampusXRFeedback>(true);
                feedback.Notify("Press A to activate alarm");feedback.SetAim("TELEPORT instructions");feedback.SetPrompt("Press A");Invoke(feedback,"LateUpdate");
                Check(!feedback.StatusText.Contains("Press A") && !feedback.StatusText.Contains("TELEPORT") && !feedback.StatusText.Contains("NEXT"),"Head-relative HUD contains status only, not action instructions");
                origin.Camera.transform.position=origin.transform.position+Vector3.up*1.65f;
                origin.Camera.transform.rotation=Quaternion.LookRotation(fixedHintPosition-origin.Camera.transform.position);
                Capture(origin.Camera,"alarm-world-hint.png");
                File.AppendAllText(Report,"Alarm: "+((FacilityNotice)Get(flow,"alarm")).transform.position+"\n");
                var towel=(CampusTowel)Get(flow,"towel"); var tap=(CampusFaucet)Get(flow,"tap");
                var item=(CampusExtinguisher)Get(flow,"equipment"); var fires=(CampusFireTarget[])Get(flow,"fires");
                Check(towel!=null && towel.transform.position.y>10,"Towel is on 4F");
                Check(tap!=null && tap.transform.position.y>10,"Tap is on 4F");
                Check(item!=null && item.transform.position.x>0,"Extinguisher binds to 3F B side");
                Check(fires.Length>0,"3F B-side fire targets present");
                Check(origin.GetComponentsInChildren<UnityEngine.UI.Image>(true).All(x=>x.name!="Direction Arrow"),"Central direction arrow removed");
                Invoke(ui,"BeginExperience");
                Trail(guide,new Vector3(-10.5f,7.2f,0),new Vector3(9.5f,7.2f,0),"3F corridor trail");
                origin.transform.position=new Vector3(-10.5f,7.2f,0);
                origin.Camera.transform.position=origin.transform.position+Vector3.up*1.65f;
                Invoke(guide,"Draw");
                origin.Camera.transform.rotation=Quaternion.LookRotation(new Vector3(5,-1.3f,0));
                Capture(origin.Camera,"floor-guidance-corridor.png");
                Trail(guide,new Vector3(-14.42f,10.8f,1.8f),new Vector3(-14.42f,9,-3),"A stair trail");
                origin.transform.position=new Vector3(-14.42f,10.8f,1.8f);
                origin.Camera.transform.position=origin.transform.position+Vector3.up*1.65f;
                origin.Camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,-3,-4.8f));
                Invoke(guide,"Draw");Capture(origin.Camera,"floor-guidance-stairs.png");
                Trail(guide,new Vector3(9.5f,7.2f,0),fires[0].transform.position,"Fire approach trail");
                origin.transform.position=new Vector3(-7.8f,10.85f,2.7f);
                foreach(var fire in fires)File.AppendAllText(Report,"Target fire "+fire.name+" "+fire.transform.position+"\n");
                Check(!flow.CanCall,"Assembly cannot complete before earlier nodes");
                var atmosphere=Object.FindObjectOfType<CampusDrillAtmosphere>();
                atmosphere.ActivateAlarm();
                var alarms=atmosphere.GetComponentsInChildren<AudioSource>();
                Check(alarms.Length==12 && alarms.All(x=>x.clip!=null && !x.loop),"Effects: twelve spatial one-shot alarm sources");
                foreach(var source in alarms)source.Stop();
                Check(!atmosphere.IsAlarmActive && atmosphere.HasAlarmBeenActivated,"Alarm event persists after one-shot audio ends");
                Check(Object.FindObjectsOfType<Transform>().Count(x=>x.name.StartsWith("CorridorCeilingSmoke_F"))==4,"Effects: ceiling smoke on all four floors");
                Check(Object.FindObjectsOfType<Transform>().Count(x=>x.name=="Route A B Door Fire")==1 && Object.FindObjectsOfType<Transform>().Count(x=>x.name=="Route A B Corner Fire")==1,"Effects: scene and runtime fire creation do not duplicate B fires");
                tap.SetRunning(true);
                Check(tap.GetComponentsInChildren<AudioSource>().Any(x=>x.clip!=null && x.isPlaying),"Effects: tap starts water sound");
                tap.SetRunning(false);Check(tap.GetComponentsInChildren<AudioSource>().All(x=>!x.isPlaying),"Effects: tap stops water sound");
                Step(flow);Check(flow.Node==2,"Alarm advances to towel node");
                hints.Refresh(true);Check(hints.InstructionText==flow.Instruction,"Towel instructions replace alarm instructions at the wash station");
                var station=towel.transform.parent;
                Check(Vector3.Distance(hints.InstructionPanel.position,station.position)<2.5f,"Towel card is located at the wash station");
                origin.Camera.transform.position=station.TransformPoint(new Vector3(0,1.65f,-2));
                origin.Camera.transform.rotation=Quaternion.LookRotation(hints.InstructionPanel.position-origin.Camera.transform.position);
                Capture(origin.Camera,"washstation-world-hint.png");
                origin.Camera.transform.localPosition=oldHeadPosition;origin.Camera.transform.localRotation=oldHeadRotation;
                Check(flow.Target==towel.transform,"Closed doors keep towel as final objective");
                Set(guide,"target",towel.transform);
                var approach=towel.transform.parent.TransformPoint(new Vector3(towel.transform.localPosition.x,0,-.95f));
                File.AppendAllText(Report,"Towel "+towel.transform.position+" station "+towel.transform.parent.position+" approach "+approach+"\n");
                foreach(var obstacle in Physics.OverlapCapsule(approach+Vector3.up*.55f,approach+Vector3.up*1.3f,.18f,~0,QueryTriggerInteraction.Ignore))
                    File.AppendAllText(Report,"Approach obstacle: "+obstacle.name+" parent "+obstacle.transform.parent.name+"\n");
                Trail(guide,origin.transform.position,towel.transform.position,"401 to washbasin trail");
                var washPath=(List<Vector3>)Get(guide,"path");
                var washEnd=towel.transform.parent.InverseTransformPoint(washPath[washPath.Count-1]);
                Check(washEnd.z<-.6f && Mathf.Abs(washEnd.x-towel.transform.localPosition.x)<.4f,"Towel trail ends inside WC in front of counter, not outside wall");
                origin.transform.position=washPath[0];origin.Camera.transform.position=origin.transform.position+Vector3.up*1.65f;
                origin.Camera.transform.rotation=Quaternion.LookRotation(new Vector3(1,-.8f,-2));
                Invoke(guide,"Draw");Capture(origin.Camera,"floor-guidance-washbasin.png");
                Set(guide,"target",tap.transform);
                Trail(guide,washPath[washPath.Count-1],tap.transform.position,"Towel to tap trail");
                var tapPath=(List<Vector3>)Get(guide,"path");
                Check(tap.transform.parent.InverseTransformPoint(tapPath[tapPath.Count-1]).z<-.6f,"Tap trail also ends on room-facing side");
                Set(guide,"target",null);
                towel.Interact(inventory); Step(flow); Check(flow.Node==2,"Dry towel does not complete node 2");
                tap.Interact(inventory); Step(flow); Check(flow.Node==2,"Wet but lowered towel does not complete node 2");
                Set(inventory,"<IsTowelRaised>k__BackingField",true); Step(flow);Check(flow.Node==3,"Raised wet towel completes node 2");
                origin.transform.position=new Vector3(-14.42f,10.2f,.2f);Step(flow);
                Check((int)Get(flow,"routeIndex")>=2,"Teleport beyond stair-entry waypoint advances to next leg");
                Walk(flow,"ToThird");Check(flow.Node==4,"A stair route enters 3F corridor");
                Walk(flow,"ToB");
                origin.transform.position=new Vector3(10.9f,7.2f,0);
                Set(flow,"routeIndex",0);Step(flow);
                Check((int)Get(flow,"routeIndex")==1,"Single-point corridor leg advances when its endpoint is already behind player");
                var legTest=typeof(CampusRouteAFlow).GetMethod("OnLeg",BindingFlags.NonPublic|BindingFlags.Static);
                Check(!(bool)legTest.Invoke(null,new object[]{new Vector3(10.9f,3.6f,0),new Vector3(-10.5f,7.2f,0),new Vector3(9.5f,7.2f,0),true}),"Being on a different floor never counts as passing corridor endpoint");
                Check(!extinguisher.TryPickup(item),"Towel-equipped state rejects extinguisher pickup");
                inventory.CycleEquipment();Check(inventory.IsEmpty,"X cycles towel to empty when no extinguisher owned");
                Check(extinguisher.TryPickup(item),"Empty hands acquire extinguisher");
                Check(item.transform.parent.name=="Left Controller Aim" && !extinguisher.BlocksTeleport,"Extinguisher uses left hand and never blocks right teleport");
                Step(flow);Check(flow.Target==item.Pin,"Guidance selects the held safety pin");
                var ray=new Ray(item.Pin.position-Vector3.forward*.2f,Vector3.forward);
                Physics.SyncTransforms();
                Check(extinguisher.TryPointAtPin(ray,2.6f,out var pinHit),"Right ray acquires held pin without held colliders");
                extinguisher.PullPin();Check(item.PinRemoved,"A pin interaction removes pin");
                Step(flow);Check(flow.Target!=null,"Fire-base objective resolves after pin removal");
                var progress=origin.GetComponent<CampusFireProgress>();
                Check(progress!=null,"World-space circular fire progress is installed");
                var agent=(ParticleSystem)Get(extinguisher,"spray");
                extinguisher.UpdateSprayVisual(true,item.Nozzle.position+item.Nozzle.forward*2);
                Check(agent.isPlaying && agent.particleCount>0,"FIRST spray emits visible particles immediately");
                extinguisher.UpdateSprayVisual(false,item.Nozzle.position);
                Check(agent.particleCount==0,"Spray release clears particles");
                extinguisher.UpdateSprayVisual(true,item.Nozzle.position+item.Nozzle.forward*2);
                Check(agent.isPlaying && agent.particleCount>0,"SECOND spray emits particles equally");
                extinguisher.UpdateSprayVisual(false,item.Nozzle.position);

                extinguisher.ApplyTarget(fires[0],1.2f);
                Check(progress.IsVisible && Mathf.Abs(progress.NormalizedProgress-.4f)<.001f && Mathf.Abs(progress.SecondsRemaining-1.8f)<.001f,"Ring and countdown read real continuous suppression timer");
                Check(Vector3.Distance(progress.Display.position,fires[0].transform.position+Vector3.up*1.95f)<.001f,"Progress ring is visibly above the targeted fire");
                origin.Camera.transform.position=origin.transform.position+Vector3.up*1.65f;
                origin.Camera.transform.rotation=Quaternion.LookRotation(progress.Display.position-origin.Camera.transform.position);
                progress.Refresh();Capture(origin.Camera,"fire-circular-progress.png");
                Check(Vector3.Dot(-progress.Display.forward,(origin.Camera.transform.position-progress.Display.position).normalized)>.999f,"Fire progress faces the user");
                extinguisher.ApplyTarget(null,0);
                Check(fires[0].ContinuousSeconds==0 && !progress.IsVisible,"Releasing or missing resets progress and hides the active countdown");
                extinguisher.ApplyTarget(fires[0],.8f);extinguisher.ApplyTarget(fires[1],.3f);
                Check(fires[0].ContinuousSeconds==0 && Mathf.Abs(progress.SecondsRemaining-2.7f)<.001f,"Changing fires resets the old target and uses the new target's timer");
                inventory.CycleEquipment();
                Check(!progress.IsVisible && fires[1].ContinuousSeconds==0,"Equipment switch cancels progress");
                origin.Camera.transform.localPosition=oldHeadPosition;origin.Camera.transform.localRotation=oldHeadRotation;
                for(int i=fires.Length-1;i>=0;i--)fires[i].ApplySpray(3);
                progress.Show(fires[0]);Check(progress.IsVisible && progress.NormalizedProgress==1 && progress.SecondsRemaining==0,"Completed fire displays full ring and zero remaining time");progress.Hide();
                inventory.CycleEquipment();Check(inventory.TowelEquipped && inventory.HeldTowel.IsWet && !inventory.IsTowelRaised,"Stored towel stays wet and re-equips lowered");
                inventory.CycleEquipment();Check(extinguisher.Held==item && !item.PinRemoved,"Re-equipped extinguisher requires pin removal again");
                Step(flow);Check(flow.Node==4 && !flow.Instruction.Contains("GRIP"),"Extinguished fire unlocks descent without returning item");
                Walk(flow,"ToSecond");Check(flow.Node==5,"B route reaches 2F with extinguisher equipped");
                var rescue=Object.FindObjectOfType<CampusWheelchairRescue>();
                Check(rescue!=null && !rescue.Rescued,"Wheelchair rescue exists and is required on 2F");
                Step(flow);Check(flow.Node==5 && !flow.CanCall,"Cannot bypass rescue before calling");
                origin.transform.position=new Vector3(-1.05f,3.65f,0);origin.Camera.transform.position=origin.transform.position+Vector3.up*1.65f;
                rescue.Interact(inventory);Check(rescue.Escorting,"A interaction starts escort");
                Check(CampusWheelchairRescue.IsPushing && inventory.IsEmpty,"Escort stows equipment");
                Check(Vector3.Dot(origin.Camera.transform.forward,Vector3.right)>.99f && Vector3.Dot(rescue.transform.forward,Vector3.right)>.99f,"First escort places player behind chair facing B");
                rescue.Interact(inventory);Check(!rescue.Escorting,"A interaction pauses escort");
                rescue.Interact(inventory);
                foreach(var door in Object.FindObjectsOfType<PrototypeDoor>()) door.Configure(door.OpenAngle,true);
                Physics.SyncTransforms();
                Check(rescue.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Active wheelchair colliders cannot block teleport or interaction rays");
                Check(!rescue.CanTeleport(new Vector3(4.1f,.05f,0),out _),"Escort rejects another floor");
                var destinations=new[]{new Vector3(3.05f,3.635f,0),new Vector3(4.1f,3.635f,.9f),new Vector3(4.1f,3.635f,-1.85f),new Vector3(4.8f,3.635f,-4.8f),new Vector3(5.5f,3.635f,-5.9f)};
                for(int i=0;i<destinations.Length;i++)
                {
                    origin.Camera.transform.rotation=Quaternion.LookRotation(i==0?Vector3.right:Vector3.back);
                    Check(rescue.CanTeleport(destinations[i],out _),"Safe paired teleport "+i+" blocker="+rescue.Blocker);
                    rescue.QueueEscortTeleport(destinations[i]);
                    origin.transform.position=destinations[i];origin.Camera.transform.position=destinations[i]+Vector3.up*1.65f;
                    Invoke(rescue,"LateUpdate");Physics.SyncTransforms();
                    Check(Vector3.Distance(rescue.transform.position,destinations[i])>1,"Wheelchair and player never overlap after teleport");
                }
                Check(!rescue.Rescued && rescue.Escorting,"Entering refuge alone does not silently complete rescue");
                Check(rescue.Checkpoint!=null,"Visible A confirmation checkpoint exists");
                rescue.Checkpoint.Interact(inventory);
                Check(rescue.Rescued && !rescue.Escorting,"Checkpoint A confirms parking and restores locomotion");
                Check(Vector3.Dot(rescue.transform.forward,Vector3.forward)>.99f,"Parked wheelchair faces refuge doorway");
                Step(flow);
                Check(flow.Node==6 && flow.Completed[4],"Refuge arrival records rescue and unlocks exit");
                inventory.Equip(CampusXRInventory.EquipmentSlot.Empty); origin.transform.position=new Vector3(0,0,0);Step(flow);
                Check(flow.Node==6 && !flow.Instruction.Contains("towel"),"Ground-floor guidance never regresses to towel pickup");
                towel.Interact(inventory);towel.Wet();Set(inventory,"<IsTowelRaised>k__BackingField",true);
                var exit=(Vector3[])typeof(CampusRouteAFlow).GetField("Exit",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                var exitFires=CampusExitFireEncounter.Ensure();
                Check(exitFires!=null && !exitFires.IsCleared,"Exit has three live fire targets");
                origin.transform.position=new Vector3(0,0,-4.5f);Step(flow);
                Check(!flow.CanCall && flow.Instruction.Contains("X"),"Exit guidance first prompts equipment switching");
                inventory.Equip(CampusXRInventory.EquipmentSlot.Extinguisher);Step(flow);
                Check(!item.PinRemoved && flow.Target==item.Pin,"Exit guidance then targets the restored safety pin");
                foreach(var exitFire in exitFires.Targets)exitFire.ApplySpray(3f);
                Check(exitFires.IsCleared,"Exit opens after all three fires are extinguished");
                for(int i=0;i<exit.Length-1;i++){origin.transform.position=exit[i];Step(flow);}
                origin.transform.position=new Vector3(0,0,-14);Step(flow);
                Check(flow.CanCall,"Teleport beyond main-entrance endpoint advances to assembly");
                Check(!flow.Completed[5],"Final node incomplete before successful call");
                flow.RecordCall();Check(flow.Completed.All(x=>x),"All six recorded nodes included in debrief");
                Check(flow.Debrief().Contains("RECORDED"),"Debrief uses actual records");
                File.AppendAllText(Report,"RESULT: PASS. UI layout and full headset journey still require manual verification.\n");
                Debug.Log("CAMPUS_ROUTE_A_VALIDATION_PASS");
            }
            catch(Exception e){File.AppendAllText(Report,e+"\nRESULT: FAIL\n");Debug.LogException(e);}
            finally { EditorApplication.isPlaying=false; }
        }
    }
}
