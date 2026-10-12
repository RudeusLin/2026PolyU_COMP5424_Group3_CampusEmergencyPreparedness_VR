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
        static GameObject Box(string name,Vector3 position,Vector3 scale,List<GameObject> list,bool walkable)
        { var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;list.Add(go);if(walkable)go.AddComponent<CampusTeleportSurface>();return go; }
        public static void Run(CampusXRInput input)
        {
            File.WriteAllText("Docs/FloorSeparationValidation.txt","Cross-floor live-fire regression\n");
            var objects=new List<GameObject>(); var origin=input.GetComponent<XROrigin>();
            Vector3 oldPosition=origin.transform.position;
            Quaternion oldRotation=origin.transform.rotation;
            var validator=input.GetComponent<CampusTeleportValidator>();
            var provider=input.GetComponent<TeleportationProvider>();
            var guard=input.GetComponent<CampusXRFireBarrierGuard>();
            Vector3 center=new Vector3(2000,0,2000);
            try
            {
                Box("Lower floor",center+Vector3.down*.1f,new Vector3(16,.2f,12),objects,true);
                Box("Upper floor",center+Vector3.up*3.5f,new Vector3(10,.2f,10),objects,true);
                var obstacle=Box("Lower fire",center+Vector3.up*1.65f,new Vector3(4.2f,3.3f,4.2f),objects,false);
                obstacle.AddComponent<CampusFireTarget>();obstacle.AddComponent<CampusFireBarrier>();
                Physics.SyncTransforms();
                Physics.Raycast(center+Vector3.up*4.5f,Vector3.down,out var upper,2f,~0,QueryTriggerInteraction.Ignore);
                Vector3 approach=center+Vector3.left*3;
                Check(CampusFireBarrier.BlocksTraversal(approach,upper.point,1.65f),"Fixture reproduces false diagonal sweep through lower fire");
                Check(validator.IsValid(upper,approach,1.65f,out _),"Upper floor above live lower fire is a valid destination");
                Check(!CampusFireBarrier.BlocksTraversal(upper.point,upper.point+Vector3.right,1.65f),"Walking on upper slab is independent of lower fire");
                Physics.Raycast(center+Vector3.right*6+Vector3.up,Vector3.down,out var lower,2f,~0,QueryTriggerInteraction.Ignore);
                Check(validator.IsValid(lower,upper.point,1.65f,out _),"Downstairs destination outside fire is valid");
                Check(!validator.IsValid(lower,center+Vector3.left*6,1.65f,out _),"Same-level teleport still cannot bypass live fire");
                Check(CampusFireBarrier.BlocksTraversal(approach,center+Vector3.right*3+Vector3.up*.6f,1.65f,true),"A small step-height change cannot bypass fire");
                Check(Physics.Raycast(center+Vector3.left*3+Vector3.up,Vector3.right,out var block,6f,~0,QueryTriggerInteraction.Ignore)
                    && block.collider.gameObject==obstacle,"Direct ray still stops on fire box");
                Check(!validator.HasClearance(center,1.65f,out _),"Landing inside live fire remains forbidden");
                typeof(CampusXRFireBarrierGuard).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(guard,false);
                typeof(CampusXRFireBarrierGuard).GetMethod("Constrain",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(guard,null);
                Check(provider.QueueTeleportRequest(new TeleportRequest{destinationPosition=upper.point+Vector3.up*.035f,matchOrientation=MatchOrientation.WorldSpaceUp}),"Upper-floor teleport request accepted");
                typeof(TeleportationProvider).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(provider,null);
                var arrived=origin.Camera.transform.position;
                Check(Mathf.Abs(arrived.x-upper.point.x)<.02f && Mathf.Abs(arrived.z-upper.point.z)<.02f,"Real provider moves camera to upper floor");
                typeof(CampusXRFireBarrierGuard).GetMethod("Constrain",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(guard,null);
                Check(Vector3.Distance(arrived,origin.Camera.transform.position)<.001f,"Guard does not undo completed teleport as physical walking");
                File.AppendAllText("Docs/FloorSeparationValidation.txt","RESULT: PASS. Simulated headset; hardware NOT RUN.\n");
            }
            finally
            {
                origin.transform.SetPositionAndRotation(oldPosition,oldRotation);
                foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);
                typeof(CampusXRFireBarrierGuard).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(guard,false);
            }
        }
    }
}
