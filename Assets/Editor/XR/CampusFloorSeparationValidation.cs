using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using CampusEmergency.Player.XR;
using CampusEmergency.Interaction.FireSafety;

namespace CampusEmergency.Editor
{
    public static class CampusFloorSeparationValidation
    {
        static void Check(bool value,string message)
        { File.AppendAllText("Docs/FloorSeparationValidation.txt",(value?"PASS: ":"FAIL: ")+message+"\n"); if(!value)throw new Exception(message); }
        static GameObject Box(string name,Vector3 position,Vector3 scale,List<GameObject> list)
        { var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;list.Add(go);go.AddComponent<CampusTeleportSurface>();return go; }
        public static void Run(CampusXRInput input)
        {
            File.WriteAllText("Docs/FloorSeparationValidation.txt","Collider-free fire ray / cross-floor regression\n");
            var objects=new List<GameObject>(); var origin=input.GetComponent<XROrigin>();
            Vector3 oldPosition=origin.transform.position; Quaternion oldRotation=origin.transform.rotation;
            var movement=input.GetComponent<CampusTeleportController>();
            var hand=(Transform)typeof(CampusTeleportController).GetField("rayOrigin",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(movement);
            Vector3 handPosition=hand.localPosition, headPosition=origin.Camera.transform.localPosition;
            Quaternion handRotation=hand.localRotation;
            Vector3 center=new Vector3(2000,0,2000);
            try
            {
                Box("Lower floor",center+Vector3.down*.1f,new Vector3(16,.2f,12),objects);
                Box("Upper floor",center+Vector3.up*3.5f,new Vector3(10,.2f,10),objects);
                var obstacle=new GameObject("Lower fire without colliders"); objects.Add(obstacle); obstacle.transform.position=center;
                var fire=obstacle.AddComponent<CampusFireTarget>();
                obstacle.AddComponent<CampusFireBarrier>().Configure(Vector3.up*1.65f,new Vector3(4.2f,3.3f,4.2f));
                origin.transform.SetPositionAndRotation(center+Vector3.left*3,Quaternion.identity);
                origin.Camera.transform.position=center+new Vector3(-3,1.6f,0);
                hand.position=center+new Vector3(-3,1.4f,0);
                Vector3 destination=center+Vector3.right*3;
                hand.rotation=Quaternion.LookRotation(destination-hand.position);
                Physics.SyncTransforms();
                Check(obstacle.GetComponents<Collider>().Length==0,"Fire has no collider, including triggers");
                Check(Physics.Raycast(hand.position,hand.forward,out var physical,12f,~0,QueryTriggerInteraction.Ignore)
                    && physical.collider.name=="Lower floor","Physics ray passes through fire and reaches floor");
                Check(!movement.Trace(out _,out _,out var reason) && reason.Contains("fire"),"Real teleport Trace rejects a floor reached through fire");
                Check(CampusFireBarrier.TryIntersect(center+Vector3.up,center+Vector3.right*4,out _),"Ray starting inside fire cannot bypass detection");
                Check(CampusFireBarrier.TryIntersect(center+new Vector3(-3,1,2.1f),center+new Vector3(3,1,2.1f),out _),"Ray touching region edge is rejected");
                Check(!CampusFireBarrier.TryIntersect(center+new Vector3(-3,1,3),center+new Vector3(3,1,3),out _),"Ray outside fire region stays clear");
                // A solid wall ends the ray before it reaches fire; do not report fire through walls.
                var wall=Box("Wall before fire",center+new Vector3(-2.6f,1.5f,0),new Vector3(.1f,2f,2f),objects);
                Physics.SyncTransforms();
                Check(!movement.Trace(out _,out _,out reason) && !reason.Contains("fire"),"Fire behind first physical hit is ignored");
                UnityEngine.Object.DestroyImmediate(wall); objects.Remove(wall);
                hand.position=center+new Vector3(-3,1.4f,4);
                origin.Camera.transform.position=center+new Vector3(-3,1.6f,4);
                hand.rotation=Quaternion.LookRotation(center+new Vector3(3,0,4)-hand.position);
                Physics.SyncTransforms();
                Check(movement.Trace(out _,out _,out _),"Clear ray around fire still permits teleport");
                origin.Camera.transform.position=center+new Vector3(-3,4.5f,0);
                hand.position=center+new Vector3(-3,4.2f,0);
                hand.rotation=Quaternion.LookRotation(center+Vector3.up*3.6f-hand.position);
                Physics.SyncTransforms();
                Check(movement.Trace(out var upper,out _,out _),"Upper floor above active lower fire is a valid ray destination");
                var provider=input.GetComponent<TeleportationProvider>();
                Check(provider.QueueTeleportRequest(new TeleportRequest{destinationPosition=upper.point+Vector3.up*.035f,matchOrientation=MatchOrientation.WorldSpaceUp}),"Upper-floor teleport is queued");
                typeof(TeleportationProvider).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(provider,null);
                Check(Mathf.Abs(origin.Camera.transform.position.x-upper.point.x)<.02f && Mathf.Abs(origin.Camera.transform.position.z-upper.point.z)<.02f,"Provider actually relocates camera to upper floor");
                Check(Array.TrueForAll(input.GetComponents<MonoBehaviour>(), c=>c!=null && c.GetType().Name!="CampusXRFireBarrierGuard"),"No legacy physical-movement guard can undo teleport");
                Check(input.GetComponent<CampusTeleportValidator>().HasClearance(center,1.65f,out _),"Fire no longer occupies physical standing clearance");
                fire.ApplySpray(2.9f);
                Check(CampusFireBarrier.TryIntersect(center+Vector3.left*3+Vector3.up,center+Vector3.right*3+Vector3.up,out _),"Live fire still rejects rays at 2.9 seconds");
                fire.ApplySpray(.1f);
                Check(!CampusFireBarrier.TryIntersect(center+Vector3.left*3+Vector3.up,center+Vector3.right*3+Vector3.up,out _),"Extinguished fire immediately releases ray restriction");
                origin.transform.SetPositionAndRotation(center+Vector3.left*3,Quaternion.identity);
                origin.Camera.transform.position=center+new Vector3(-3,1.6f,0);
                hand.position=center+new Vector3(-3,1.4f,0); hand.rotation=Quaternion.LookRotation(destination-hand.position);
                Physics.SyncTransforms();
                Check(movement.Trace(out _,out _,out _),"Formerly blocked teleport ray becomes valid after extinguishing");
                File.AppendAllText("Docs/FloorSeparationValidation.txt","RESULT: PASS. Simulated headset; hardware NOT RUN.\n");
            }
            finally
            {
                origin.transform.SetPositionAndRotation(oldPosition,oldRotation);
                hand.SetLocalPositionAndRotation(handPosition,handRotation); origin.Camera.transform.localPosition=headPosition;
                foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);
                Physics.SyncTransforms();
            }
        }
    }
}
