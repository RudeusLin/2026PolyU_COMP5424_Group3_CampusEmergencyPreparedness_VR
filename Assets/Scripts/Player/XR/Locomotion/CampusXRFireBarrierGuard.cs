using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
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
        private TeleportationProvider teleport;
        private void Awake() { origin=GetComponent<XROrigin>(); input=GetComponent<CampusXRInput>(); }
        private void OnEnable() { initialized=false; teleport=GetComponent<TeleportationProvider>(); if(teleport!=null) teleport.endLocomotion+=OnTeleport; Application.onBeforeRender+=Constrain; }
        private void OnDisable() { if(teleport!=null) teleport.endLocomotion-=OnTeleport; Application.onBeforeRender-=Constrain; initialized=false; }
        private void OnTeleport(LocomotionSystem system)
        {
            // The provider has already validated and completed an instantaneous relocation.
            if(origin==null || origin.Camera==null) return;
            safeFeet=origin.Camera.transform.position; safeFeet.y=origin.transform.position.y; initialized=true;
        }
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
