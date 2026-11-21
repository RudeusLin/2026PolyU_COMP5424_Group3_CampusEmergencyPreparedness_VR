using UnityEngine;
using System.Linq;
using CampusEmergency.Prototype;

namespace CampusEmergency.Player.XR
{
    /// <summary>Finds fixed upright surfaces for campus route and door instruction cards.</summary>
    public static class CampusHintSurfacePlacement
    {
        public struct Mount { public Vector3 position;public Quaternion rotation;public bool compact; }
        public static bool IsSupportedFloor(float y)=>y>=-.2f && y<11.3f;
        private static Renderer[] plaques;
        private static int plaqueSceneHandle=-1;
        private static bool CoversPlaque(Vector3 centre,Vector3 right,Vector3 normal,float half,float halfHeight)
        {
            int sceneHandle=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if(plaques==null || plaqueSceneHandle!=sceneHandle){plaqueSceneHandle=sceneHandle;plaques=Object.FindObjectsOfType<Renderer>().Where(x=>x.name=="SignPanel"||x.GetComponent<TextMesh>()!=null).ToArray();}
            foreach(var renderer in plaques)
            {
                if(renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
                if(renderer.name!="SignPanel" && renderer.GetComponent<TextMesh>()==null)continue;
                var bounds=renderer.bounds;var delta=bounds.center-centre;float depth=Vector3.Dot(delta,normal);
                if(depth<-.08f || depth>.5f)continue;
                float extent=Mathf.Abs(right.x)*bounds.extents.x+Mathf.Abs(right.z)*bounds.extents.z;
                if(Mathf.Abs(Vector3.Dot(delta,right))<half+extent+.04f && Mathf.Abs(delta.y)<halfHeight+bounds.extents.y+.04f)return true;
            }
            return false;
        }
        private static bool IsWall(Collider c)
        {
            string n=c.name.ToLowerInvariant();
            return (n.Contains("wall")||n.Contains("pier")||n.Contains("column")||n.Contains("doorheader")) && c.GetComponentInParent<PrototypeDoor>()==null;
        }
        public static bool TryFind(Vector3 anchor,float floorY,Transform viewer,bool door,Vector3 avoid,out Mount mount)
        {
            Physics.SyncTransforms();
            if(TryAtHeight(anchor,floorY,viewer,door,avoid,1.6f,out mount))return true;
            if(viewer!=null && TryAtHeight(viewer.position,floorY,viewer,door,avoid,1.6f,out mount))return true;
            // A glazed stair enclosure may only have solid wall on the doorway header.
            if(TryAtHeight(anchor,floorY,viewer,door,avoid,2.78f,out mount))return true;
            return viewer!=null && TryAtHeight(viewer.position,floorY,viewer,door,avoid,2.78f,out mount);
        }
        private static bool TryAtHeight(Vector3 anchor,float floorY,Transform viewer,bool door,Vector3 avoid,float height,out Mount mount)
        {
            mount=default;if(!IsSupportedFloor(floorY))return false;
            float best=float.PositiveInfinity;bool found=false;
            // Full-width first; compact cards also fit the narrow solid strips beside stair doors.
            foreach(bool compact in new[]{false,true})
            {
                float half=compact?.26f:.61f;
                foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
                foreach(float slide in new[]{0f,.4f,-.4f,.8f,-.8f,1.2f,-1.2f,1.6f,-1.6f,1.8f,-1.8f,2.3f,-2.3f,2.8f,-2.8f,3.2f,-3.2f})
                {
                    Vector3 tangent=Vector3.Cross(Vector3.up,direction);
                    Vector3 start=new Vector3(anchor.x,floorY+height,anchor.z)+tangent*slide-direction*.4f;
                    foreach(var hit in Physics.RaycastAll(start,direction,4.6f,~0,QueryTriggerInteraction.Ignore))
                    {
                        if(!IsWall(hit.collider)||Mathf.Abs(hit.normal.y)>.1f)continue;
                        Vector3 normal=hit.normal;var candidate=hit.point+normal*.035f;
                        if(viewer!=null && Mathf.Abs(viewer.position.y-(floorY+1.65f))<2.1f && Vector3.Dot(viewer.position-candidate,normal)<.05f)continue;
                        if(!float.IsPositiveInfinity(avoid.x) && Vector3.Distance(candidate,avoid)<1.1f)continue;
                        var right=Vector3.Cross(Vector3.up,normal);
                        bool fits=true;
                        foreach(float x in new[]{-half,half})foreach(float y in new[]{compact?-.33f:-.26f,compact?.33f:.26f})
                        {
                            var sample=candidate+right*x+Vector3.up*y;
                            if(!Physics.Raycast(sample+normal*.1f,-normal,out var backing,.25f,~0,QueryTriggerInteraction.Ignore)||!IsWall(backing.collider)||Vector3.Dot(backing.normal,normal)<.99f)fits=false;
                            if(viewer!=null){var sight=sample-viewer.position;foreach(var occluder in Physics.RaycastAll(viewer.position,sight.normalized,Mathf.Max(0,sight.magnitude-.06f),~0,QueryTriggerInteraction.Ignore))if(!occluder.collider.transform.IsChildOf(viewer.root)){fits=false;break;}}
                        }
                        if(!fits)continue;
                        if(floorY<5.3f && CoversPlaque(candidate,right,normal,half,compact?.33f:.26f))continue;
                        var rotation=Quaternion.LookRotation(-normal);
                        bool blocked=false;
                        foreach(var collider in Physics.OverlapBox(candidate+normal*.1f,new Vector3(half,.25f,.06f),rotation,~0,QueryTriggerInteraction.Ignore))
                            if(collider!=hit.collider && !IsWall(collider))blocked=true;
                        if(blocked)continue;
                        if(viewer!=null){var line=candidate-viewer.position;foreach(var occluder in Physics.RaycastAll(viewer.position,line.normalized,Mathf.Max(0,line.magnitude-.06f),~0,QueryTriggerInteraction.Ignore))if(!occluder.collider.transform.IsChildOf(viewer.root)){blocked=true;break;}}
                        if(blocked)continue;
                        float cost=Vector3.Distance(candidate,new Vector3(anchor.x,floorY+height,anchor.z))+(compact?.7f:0f);
                        if(viewer!=null){var view=candidate-viewer.position;cost+=view.magnitude*.12f+(1f-Vector3.Dot(viewer.forward,view.normalized))*1.2f;}
                        if(door && Mathf.Abs(slide)<.8f)cost+=1f;
                        if(cost>=best)continue;best=cost;mount=new Mount{position=candidate,rotation=rotation,compact=compact};found=true;
                    }
                }
                
            }
            return found;
        }
    }
}
