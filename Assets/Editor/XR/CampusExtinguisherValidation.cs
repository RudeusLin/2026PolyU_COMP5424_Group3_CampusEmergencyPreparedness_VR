using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.OpenXR.Features.Interactions;
using CampusEmergency.Player.XR;
using CampusEmergency.Interaction.FireSafety;
using CampusEmergency.Prototype;

namespace CampusEmergency.Editor
{
    public static class CampusExtinguisherValidation
    {
        private static void Check(bool ok,string text)
        { File.AppendAllText("Docs/ExtinguisherValidation.txt",(ok?"PASS: ":"FAIL: ")+text+"\n"); if(!ok) throw new Exception(text); }
        private static void Queue<T>(InputControl<T> control,T value) where T:struct
        { using(var e=StateEvent.From(control.device,out var state)) { control.WriteValueIntoEvent(value,state); InputSystem.QueueEvent(state); } InputSystem.Update(); }
        private static void Tick(object obj,string method,params object[] args)
        { obj.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args); }
        public static void Run(CampusXRInput input,OculusTouchControllerProfile.OculusTouchController right,OculusTouchControllerProfile.OculusTouchController left)
        {
            File.WriteAllText("Docs/ExtinguisherValidation.txt","Extinguisher Play Mode integration\n");
            var c=input.GetComponent<CampusXRExtinguisherController>();
            var inv=input.GetComponent<CampusXRInventory>();
            var movement=input.GetComponent<CampusTeleportController>();
            var item=UnityEngine.Object.FindObjectsOfType<CampusExtinguisher>().First();
            var fire=UnityEngine.Object.FindObjectsOfType<CampusFireTarget>().First();
            var other=UnityEngine.Object.FindObjectsOfType<CampusFireTarget>().First(f=>f!=fire);
            var home=item.transform.position;
            Queue(right.triggerPressed,0f); Queue(right.primaryButton,0f); Queue(left.isTracked,1f);
            Check(c.TryPickup(item) && item.IsHeld && !c.SprayMode,"Pickup complete model starts in ACTION mode");
            Check(!inv.TryHoldTowel(UnityEngine.Object.FindObjectOfType<CampusTowel>()),"Cannot carry towel and extinguisher together");
            Tick(c,"ProcessInput",true); Tick(movement,"ProcessInput",true);
            Check(movement.IsAiming && !c.BlocksTeleport,"Held extinguisher permits teleport aiming in ACTION mode");
            var origin=input.GetComponent<Unity.XR.CoreUtils.XROrigin>();
            var before=origin.Camera.transform.position;
            typeof(CampusTeleportController).GetField("lastTeleport",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(movement,-10f);
            Queue(right.triggerPressed,1f); Tick(c,"ProcessInput",true); Tick(movement,"ProcessInput",true);
            Tick(input.GetComponent<UnityEngine.XR.Interaction.Toolkit.TeleportationProvider>(),"Update");
            Check(Vector3.Distance(before,origin.Camera.transform.position)>.1f && item.IsHeld,"Right trigger actually moves player while holding extinguisher");
            Queue(right.triggerPressed,0f); Tick(c,"ProcessInput",true); Tick(movement,"ProcessInput",true);
            Queue(right.secondaryButton,1f); Tick(c,"ProcessInput",true); Tick(movement,"ProcessInput",true);
            Check(c.SprayMode && !movement.IsAiming && c.BlocksTeleport,"Actual B button switches to SPRAY and blocks teleport");
            Queue(right.secondaryButton,0f); Tick(c,"ProcessInput",true);
            Queue(right.triggerPressed,1f); Tick(c,"ProcessInput",true);
            Check(fire.ContinuousSeconds==0f,"Trigger before pin removal cannot extinguish");
            Check(!item.TryPullPin(item.Pin.position+Vector3.one),"Pin cannot be removed remotely");
            var leftHand=input.GetComponentsInChildren<Transform>().Single(t=>t.name=="Left Controller Aim");
            Queue(left.gripPressed,1f); leftHand.position=item.Pin.position; Tick(c,"ProcessInput",true);
            Check(item.PinRemoved && !item.Pin.gameObject.activeSelf,"Tracked LEFT grip near pin removes pin");
            Queue(left.gripPressed,0f); Queue(right.triggerPressed,0f); Tick(c,"ProcessInput",true);
            var rightHand=item.transform.parent; rightHand.rotation=Quaternion.identity;
            fire.transform.position=item.Nozzle.position+Vector3.forward*1.5f-Vector3.up*.12f;
            Physics.SyncTransforms();
            Check(c.FindTarget(item.Nozzle.position,Vector3.forward,out _)==fire,"Spray nozzle ray identifies fire base");
            Check(c.FindTarget(item.Nozzle.position,Vector3.up,out _)==null,"Aiming away misses fire base");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position=item.Nozzle.position+Vector3.forward*.65f; wall.transform.localScale=new Vector3(1,1,.1f); Physics.SyncTransforms();
            Check(c.FindTarget(item.Nozzle.position,Vector3.forward,out _)==null,"Wall blocks spray");
            UnityEngine.Object.DestroyImmediate(wall); Physics.SyncTransforms();
            c.ApplyTarget(fire,2f); c.ApplyTarget(null,0f);
            Check(!fire.Extinguished && fire.ContinuousSeconds==0f,"Interrupted two-second hit resets progress");
            c.ApplyTarget(fire,1f); c.ApplyTarget(other,.1f);
            Check(fire.ContinuousSeconds==0f,"Changing targets resets previous progress");
            c.ApplyTarget(fire,1f); Tick(c,"ProcessInput",false);
            Check(fire.ContinuousSeconds==0f,"Focus loss resets spray progress");
            c.ApplyTarget(fire,2.9f);
            Check(!fire.Extinguished,"2.9 seconds does not extinguish");
            c.ApplyTarget(fire,.1f);
            Check(fire.Extinguished && fire.GetComponentsInChildren<ParticleSystem>().All(p=>!p.IsAlive())
                && fire.GetComponentsInChildren<Light>().All(l=>!l.enabled),"Three continuous seconds clears particles and light immediately");
            Queue(right.triggerPressed,1f); Queue(right.secondaryButton,1f); Tick(c,"ProcessInput",true); Tick(movement,"ProcessInput",true);
            Check(!c.SprayMode && c.BlocksTeleport && !movement.IsAiming,"B switch with held trigger cannot teleport");
            Queue(right.secondaryButton,0f); Queue(right.triggerPressed,0f); Tick(c,"ProcessInput",true); Tick(movement,"ProcessInput",true);
            Check(!c.BlocksTeleport && movement.IsAiming,"Release rearms ACTION mode");
            Queue(right.gripPressed,1f); Tick(c,"ProcessInput",true);
            Check(c.Held==null && !item.PinRemoved && Vector3.Distance(home,item.transform.position)<.001f && fire.Extinguished,"Right grip returns model, resets pin and preserves extinguished fire");
            Queue(right.gripPressed,0f);
            inv.TryHoldTowel(UnityEngine.Object.FindObjectOfType<CampusTowel>());
            Check(!c.TryPickup(item),"Cannot pick up extinguisher while carrying towel");
            inv.ReleaseTowel(inv.HeldTowel);
            File.AppendAllText("Docs/ExtinguisherValidation.txt","RESULT: PASS. Synthetic controllers; headset visuals NOT RUN.\n");
        }
    }
}
