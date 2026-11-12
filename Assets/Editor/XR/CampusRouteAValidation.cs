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
                Check(map.DisplayLanguage==CampusLanguage.English && map.LabelText("Stair A")=="Stair A · West" && map.LabelText("Assembly").Contains("Assembly"),"English selection localizes map labels including assembly and stairs");
                typeof(LanguageSelectionUI).GetMethod("Select",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{1});
                var enlargedMap=ui.GetComponentsInChildren<CampusRouteMapUI>().Single();
                Check(enlargedMap.MapTexture==sharedMapTexture && enlargedMap.transform.localScale.x>.9f && LanguageSelectionUI.IsBlocking,"Map enlarges without starting gameplay or changing language");
                Capture(origin.Camera,"route-map-enlarged-en.png");
                typeof(LanguageSelectionUI).GetMethod("Select",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{1});
                Capture(origin.Camera,"route-a-intro-en.png");
                typeof(LanguageSelectionUI).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,CampusLanguage.SimplifiedChinese);
                Invoke(ui,"BuildBriefing");Capture(origin.Camera,"route-a-intro-zh.png");
                map=ui.GetComponentsInChildren<CampusRouteMapUI>().Single();
                Check(map.DisplayLanguage==CampusLanguage.SimplifiedChinese && map.LabelText("Stair B")=="B楼梯 · 东侧" && map.MapTexture==sharedMapTexture,"Simplified Chinese map uses localized text and identical route artwork");
                typeof(LanguageSelectionUI).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,CampusLanguage.TraditionalChinese);
                map.RefreshLanguage();
                Check(map.LabelText("Stair B")=="B樓梯 · 東側" && map.LabelText("Main Entrance")=="學校正門","Existing map switches to Traditional Chinese without replacing artwork");
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
                Check(Vector3.Dot(-hints.InstructionPanel.forward,(origin.Camera.transform.position-hints.InstructionPanel.position).normalized)>.999f,"Instruction card faces the user after movement");
                Check(Quaternion.Angle(fixedHintRotation,hints.InstructionPanel.rotation)>.01f,"Instruction card rotates toward the new viewer position");
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
                Set(flow,"alarmDone",true); Step(flow);Check(flow.Node==2,"Alarm advances to towel node");
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
                Check(extinguisher.TryPickup(item),"Raised wet towel permits simultaneous extinguisher pickup");
                Step(flow);Check(flow.Target==item.Pin,"Guidance selects the held safety pin");
                Check(item.TryPullPin(item.Pin.position),"Pin removal succeeds at valid distance");
                Step(flow);Check(flow.Target!=null,"Fire-base objective resolves after pin removal");
                var progress=origin.GetComponent<CampusFireProgress>();
                Check(progress!=null,"World-space circular fire progress is installed");
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
                extinguisher.SwitchMode();
                Check(!progress.IsVisible && fires[1].ContinuousSeconds==0,"Mode switch cancels progress");
                origin.Camera.transform.localPosition=oldHeadPosition;origin.Camera.transform.localRotation=oldHeadRotation;
                for(int i=fires.Length-1;i>=0;i--)fires[i].ApplySpray(3);
                progress.Show(fires[0]);Check(progress.IsVisible && progress.NormalizedProgress==1 && progress.SecondsRemaining==0,"Completed fire displays full ring and zero remaining time");progress.Hide();
                Set(inventory,"<IsTowelRaised>k__BackingField",false);
                Step(flow);Check(flow.Node==4 && flow.Instruction.Contains("GRIP"),"Must return extinguisher before descending");
                Check(flow.Instruction.Contains("GRIP"),"Return instruction takes priority even when towel is lowered");
                Set(inventory,"<IsTowelRaised>k__BackingField",true);
                Check(flow.ReturnHintAnchor==fires[0].transform,"Return hint uses last extinguished fire, not scene enumeration order");
                hints.Refresh(true);
                Check(Vector3.Distance(hints.InstructionPanel.position,fires[0].transform.position)<1.8f && hints.InstructionText.Contains("GRIP"),"Return instructions appear immediately beside the final fire");
                var returnHintPosition=hints.InstructionPanel.position;
                origin.Camera.transform.position=origin.transform.position+Vector3.up*1.65f;
                origin.Camera.transform.rotation=Quaternion.LookRotation(returnHintPosition-origin.Camera.transform.position);
                hints.Refresh(true);Capture(origin.Camera,"return-extinguisher-world-hint.png");
                origin.Camera.transform.localPosition+=Vector3.right*.3f;hints.Refresh(true);
                Check(Vector3.Distance(returnHintPosition,hints.InstructionPanel.position)<.001f && Vector3.Dot(-hints.InstructionPanel.forward,(origin.Camera.transform.position-returnHintPosition).normalized)>.999f,"Return card stays beside fire and faces moving user");
                origin.Camera.transform.localPosition=oldHeadPosition;origin.Camera.transform.localRotation=oldHeadRotation;
                extinguisher.ReturnHeld(); Walk(flow,"ToSecond");Check(flow.Node==5,"B route reaches 2F corridor");
                Check(flow.ReturnHintAnchor==null,"Return hint clears after extinguisher is returned");
                Check(GameObject.Find("Route A Wheelchair NPC")==null && GameObject.Find("Refuge wheelchair parking")==null,"No rescue objects are created; 2F leads directly to Stair A");
                inventory.ReturnHeldTowel(); origin.transform.position=new Vector3(0,0,0);Step(flow);
                Check(flow.Node==5 && !flow.Instruction.Contains("towel"),"Ground-floor guidance never regresses to towel pickup");
                towel.Interact(inventory);towel.Wet();Set(inventory,"<IsTowelRaised>k__BackingField",true);
                var exit=(Vector3[])typeof(CampusRouteAFlow).GetField("Exit",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                for(int i=0;i<exit.Length-1;i++){origin.transform.position=exit[i];Step(flow);}
                origin.transform.position=new Vector3(0,0,-14);Step(flow);
                Check(flow.CanCall,"Teleport beyond main-entrance endpoint advances to assembly");
                Check(!flow.Completed[4],"Final node incomplete before successful call");
                flow.RecordCall();Check(flow.Completed.All(x=>x),"All five recorded nodes included in debrief");
                Check(flow.Debrief().Contains("RECORDED"),"Debrief uses actual records");
                File.AppendAllText(Report,"RESULT: PASS. UI layout and full headset journey still require manual verification.\n");
                Debug.Log("CAMPUS_ROUTE_A_VALIDATION_PASS");
            }
            catch(Exception e){File.AppendAllText(Report,e+"\nRESULT: FAIL\n");Debug.LogException(e);}
            finally { EditorApplication.isPlaying=false; }
        }
    }
}
