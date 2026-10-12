using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Unity.XR.CoreUtils;
using CampusEmergency.Player.XR;
using CampusEmergency.Prototype;

namespace CampusEmergency.Editor
{
    public static class CampusXRTowelValidation
    {
        static void Check(bool value,string message)
        { File.AppendAllText("Docs/XRTowelValidation.txt",(value?"PASS: ":"FAIL: ")+message+"\n"); if(!value)throw new Exception(message); }
        static void Queue<T>(InputControl<T> control,T value) where T:struct
        { using(var e=StateEvent.From(control.device,out var state)){control.WriteValueIntoEvent(value,state);InputSystem.QueueEvent(state);}InputSystem.Update(); }
        static void Tick(object obj,string method,params object[] args)
        { obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args); }
        static void Pose(CampusXRInventory inventory)
        { typeof(CampusXRInventory).GetField("raisedBlend",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(inventory,1f); Tick(inventory,"UpdatePose"); }
        public static void Run(CampusXRInput input,OculusTouchControllerProfile.OculusTouchController right,OculusTouchControllerProfile.OculusTouchController left)
        {
            File.WriteAllText("Docs/XRTowelValidation.txt","Left-hand towel + hold-to-raise simulated input\n");
            var inventory=input.GetComponent<CampusXRInventory>();
            var towel=UnityEngine.Object.FindObjectsOfType<CampusTowel>().First();
            var head=input.GetComponent<XROrigin>().Camera.transform;
            var rotation=head.localRotation;
            var home=towel.transform.parent; var homePosition=towel.transform.localPosition;
            var resting=inventory.TowelHoldPoint.localPosition;
            try
            {
                Queue(left.isTracked,1f);Queue(right.isTracked,1f);Queue(right.triggerPressed,0f);Queue(left.triggerPressed,1f);
                towel.Interact(inventory); Tick(inventory,"ProcessInput",true);
                Check(towel.transform.parent==inventory.TowelHoldPoint && inventory.TowelHoldPoint.parent.name=="Left Controller Aim","Pickup uses left hand and disables held collision");
                Check(!inventory.IsTowelRaised,"Pickup with trigger already held requires release");
                Queue(left.triggerPressed,0f);Tick(inventory,"ProcessInput",true);
                Queue(left.triggerPressed,1f);Tick(inventory,"ProcessInput",true);Pose(inventory);
                Check(inventory.IsTowelRaised,"Actual left trigger raises towel");
                foreach(float pitch in new[]{0f,60f,-60f})
                {
                    head.localRotation=Quaternion.Euler(pitch,20f,15f);Pose(inventory);
                    float top=float.NegativeInfinity;
                    foreach(var renderer in towel.GetComponentsInChildren<Renderer>())
                    {
                        var b=renderer.bounds;
                        for(int i=0;i<8;i++)top=Mathf.Max(top,head.InverseTransformPoint(b.center+Vector3.Scale(b.extents,
                            new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))).y);
                    }
                    Check(top<=-.119f,"Rendered towel remains below central view at head pitch "+pitch);
                }
                Check(towel.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Raised towel never blocks ray or standing space");
                towel.Wet();Queue(left.triggerPressed,0f);Tick(inventory,"ProcessInput",true);
                Check(!inventory.IsTowelRaised && towel.IsWet,"Release lowers towel without losing wet state");
                Queue(left.triggerPressed,1f);Tick(inventory,"ProcessInput",true);Tick(inventory,"ProcessInput",false);
                Check(!inventory.IsTowelRaised && towel.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"Focus loss lowers and hides held towel");
                Tick(inventory,"ProcessInput",true);Check(!inventory.IsTowelRaised,"Focus recovery while trigger held does not auto-raise");
                Queue(left.triggerPressed,0f);Tick(inventory,"ProcessInput",true);
                Queue(left.triggerPressed,1f);Tick(inventory,"ProcessInput",true);
                Queue(left.isTracked,0f);Tick(inventory,"ProcessInput",true);
                Check(!inventory.IsTowelRaised && towel.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"Left tracking loss hides towel");
                Queue(left.isTracked,1f);Queue(left.triggerPressed,0f);Tick(inventory,"ProcessInput",true);
                Queue(left.triggerPressed,1f);Tick(inventory,"ProcessInput",true);Pose(inventory);
                Queue(right.gripPressed,1f);Tick(inventory,"ProcessInput",true);
                Check(inventory.HeldTowel==null && !inventory.IsTowelRaised && towel.transform.parent==home
                    && Vector3.Distance(towel.transform.localPosition,homePosition)<.001f && !towel.IsWet
                    && towel.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"Existing right grip returns raised towel and restores dry visible model");
                Check(Vector3.Distance(inventory.TowelHoldPoint.localPosition,resting)<.001f,"Return restores resting anchor for next pickup");
                File.AppendAllText("Docs/XRTowelValidation.txt","RESULT: PASS. Synthetic controllers; headset comfort NOT RUN.\n");
            }
            finally
            { inventory.ReturnHeldTowel();head.localRotation=rotation;Queue(left.isTracked,1f);Queue(left.triggerPressed,0f);Queue(right.gripPressed,0f); }
        }
    }
}
