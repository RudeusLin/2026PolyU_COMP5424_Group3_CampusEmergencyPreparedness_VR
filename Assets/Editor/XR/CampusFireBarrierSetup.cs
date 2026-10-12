using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampusEmergency.Player.XR;
using CampusEmergency.Interaction.FireSafety;

namespace CampusEmergency.Editor
{
    public static class CampusFireBarrierSetup
    {
        public static readonly string[] OutdoorSurfaces={"Ground","Forecourt","MainWalkway","SideExitPath"};
        [MenuItem("Campus Emergency/XR/Configure fire blockers and outdoor teleport")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play first");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var prefab=PrefabUtility.LoadPrefabContents(CampusXRSceneBuilder.RigPath);
            try { ConfigureRig(prefab); PrefabUtility.SaveAsPrefabAsset(prefab,CampusXRSceneBuilder.RigPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            ConfigureScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Validate();
        }
        public static void ConfigureRig(GameObject rig)
        { if(rig.GetComponent<CampusXRFireBarrierGuard>()==null) rig.AddComponent<CampusXRFireBarrierGuard>(); }
        public static void ConfigureScene()
        {
            Physics.SyncTransforms();
            foreach(var fire in UnityEngine.Object.FindObjectsOfType<CampusFireTarget>())
            {
                foreach(var sphere in fire.GetComponents<SphereCollider>()) UnityEngine.Object.DestroyImmediate(sphere);
                var box=fire.GetComponent<BoxCollider>();
                if(box==null) box=fire.gameObject.AddComponent<BoxCollider>();
                // 4.2m square footprint exceeds the 3m corridor width; no cylindrical edge gaps.
                float floorY=fire.transform.position.y;
                var support=Physics.RaycastAll(fire.transform.position+Vector3.up*.3f,Vector3.down,3f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.collider.GetComponent<CampusTeleportSurface>()!=null).OrderBy(h=>h.distance).ToArray();
                if(support.Length>0) floorY=support[0].point.y;
                box.isTrigger=false;
                // Main corridors are centered on z=0 and 3m wide. Center the barrier on the
                // passage rather than an offset flame so even a few-centimetre ray gap is closed.
                float centerZ=Mathf.Abs(fire.transform.position.x)<=13f ? 0f : fire.transform.position.z;
                float top=floorY+3.4f;
                foreach(var surface in UnityEngine.Object.FindObjectsOfType<CampusTeleportSurface>())
                {
                    if(surface.name!="FloorSlab" && surface.name!="Landing" && surface.name!="HalfLanding") continue;
                    var collider=surface.GetComponent<Collider>(); if(collider==null || collider.isTrigger) continue;
                    var bounds=collider.bounds;
                    if(bounds.min.y<=floorY+.5f || bounds.min.y-.05f>=top) continue;
                    if(bounds.max.x<fire.transform.position.x-2.1f || bounds.min.x>fire.transform.position.x+2.1f
                        || bounds.max.z<centerZ-2.1f || bounds.min.z>centerZ+2.1f) continue;
                    top=Mathf.Min(top,bounds.min.y-.05f);
                }
                float bottom=floorY-.05f;
                box.center=fire.transform.InverseTransformPoint(new Vector3(fire.transform.position.x,(bottom+top)*.5f,centerZ));
                box.size=new Vector3(4.2f,top-bottom,4.2f);
                if(fire.GetComponent<CampusFireBarrier>()==null) fire.gameObject.AddComponent<CampusFireBarrier>();
            }
            foreach(var t in UnityEngine.Object.FindObjectsOfType<Transform>().Where(t=>OutdoorSurfaces.Contains(t.name)))
            {
                var c=t.GetComponent<Collider>();
                if(c==null) c=t.gameObject.AddComponent<BoxCollider>();
                c.enabled=true; c.isTrigger=false;
                if(t.GetComponent<CampusTeleportSurface>()==null) t.gameObject.AddComponent<CampusTeleportSurface>();
            }
            ConfigureRig(UnityEngine.Object.FindObjectOfType<CampusXRInput>().gameObject);
        }
        public static void Validate()
        {
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene); Physics.SyncTransforms();
            var fires=UnityEngine.Object.FindObjectsOfType<CampusFireTarget>();
            if(fires.Length!=9 || fires.Any(f=>f.GetComponent<BoxCollider>()==null || f.GetComponent<BoxCollider>().isTrigger
                || f.GetComponent<BoxCollider>().bounds.size.x<4f || f.GetComponent<BoxCollider>().bounds.size.z<4f)) throw new Exception("Fire boxes incomplete");
            var lines=new System.Collections.Generic.List<string>{"PASS: nine solid fire boxes, 4.2m footprint, tops clipped below overhead floor slabs"};
            foreach(var fire in fires)
            {
                var fireBounds=fire.GetComponent<BoxCollider>().bounds;
                foreach(var surface in UnityEngine.Object.FindObjectsOfType<CampusTeleportSurface>())
                {
                    if(surface.name!="FloorSlab" && surface.name!="Landing" && surface.name!="HalfLanding") continue;
                    var ceiling=surface.GetComponent<Collider>().bounds;
                    if(ceiling.min.y<=fireBounds.min.y+.55f) continue;
                    bool overlap=ceiling.max.x>=fireBounds.min.x && ceiling.min.x<=fireBounds.max.x
                        && ceiling.max.z>=fireBounds.min.z && ceiling.min.z<=fireBounds.max.z;
                    if(overlap && fireBounds.max.y>ceiling.min.y-.04f) throw new Exception("Fire penetrates overhead slab: "+fire.name+" box="+fireBounds+" ceiling="+ceiling+" object="+surface.name);
                }
                lines.Add("PASS: "+fire.name+" vertical limits "+fireBounds.min.y.ToString("F2")+".."+fireBounds.max.y.ToString("F2")+" below overhead slabs");
            }
            var spawn=GameObject.Find("Spawn_F04_401").transform.position;
            if(CampusFireBarrier.BlocksTraversal(spawn,spawn,1.65f)) throw new Exception("Fire boxes cover initial spawn");
            lines.Add("PASS: initial spawn remains outside fire volumes");
            var validator=UnityEngine.Object.FindObjectOfType<CampusTeleportValidator>();
            foreach(var name in OutdoorSurfaces)
            {
                var go=GameObject.Find(name); if(go==null || go.GetComponent<CampusTeleportSurface>()==null) throw new Exception("Missing outdoor surface: "+name);
            }
            foreach(var point in new[]{new Vector3(0,2,-12),new Vector3(0,2,-19),new Vector3(5,2,-20),new Vector3(-21,2,-5)})
            {
                if(!Physics.Raycast(point,Vector3.down,out var hit,5f,~0,QueryTriggerInteraction.Ignore)
                    || !validator.IsValid(hit,hit.point+Vector3.back*2,1.65f,out _)) throw new Exception("Outdoor landing rejected: "+point);
                lines.Add("PASS: outdoor landing "+hit.point+" on "+hit.collider.name);
            }
            var target=fires.First(); var box=target.GetComponent<BoxCollider>(); var center=box.bounds.center;
            Vector3 start=center-Vector3.right*3, end=center+Vector3.right*3;
            if(!box.Raycast(new Ray(start,Vector3.right),out _,6f)) throw new Exception("Ray passed through fire box");
            if(!CampusFireBarrier.BlocksTraversal(start-Vector3.up,end-Vector3.up,1.65f)) throw new Exception("Body traversal crossed fire");
            lines.Add("PASS: box ray and swept player path are blocked");
            File.WriteAllLines("Docs/FireBarrierValidation.txt",lines);
        }
    }
}
