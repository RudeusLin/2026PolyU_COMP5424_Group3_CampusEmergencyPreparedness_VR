using UnityEngine;
using Unity.XR.CoreUtils;
using CampusEmergency.Interaction.FireSafety;

namespace CampusEmergency.Player.XR
{
    /// <summary>Colliders alone cannot stop tracked headset motion. Keep the virtual rig on the safe side.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class CampusXRFireBarrierGuard : MonoBehaviour
    {
        private XROrigin origin;
        private CampusXRInput input;
        private Vector3 safeFeet;
        private bool initialized;
        private void Awake() { origin=GetComponent<XROrigin>(); input=GetComponent<CampusXRInput>(); }
        private void OnEnable() { initialized=false; Application.onBeforeRender+=Constrain; }
        private void OnDisable() { Application.onBeforeRender-=Constrain; initialized=false; }
        private void LateUpdate() => Constrain();
        private void Constrain()
        {
            if(origin==null || origin.Camera==null || input==null || !input.HeadTracked) return;
            Vector3 feet=origin.Camera.transform.position; feet.y=origin.transform.position.y;
            if(!initialized) { safeFeet=feet; initialized=true; return; }
            if(CampusFireBarrier.BlocksTraversal(safeFeet,feet,origin.CameraInOriginSpaceHeight))
            {
                origin.transform.position+=safeFeet-feet;
                return;
            }
            safeFeet=feet;
        }
    }
}
