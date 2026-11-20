using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
namespace CampusEmergency.Player.XR
{
    /// <summary>Controller-driven articulated hands. Visuals have no colliders or interaction ownership.</summary>
    public sealed class CampusHandVisual : MonoBehaviour
    {
        [SerializeField] private bool left;
        [SerializeField] private Transform[] joints;
        private InputAction trigger,grip;
        private CampusXRInventory inventory;
        private CampusXRExtinguisherController extinguisher;
        private void OnEnable()
        {
            string device="<XRController>{"+(left?"LeftHand":"RightHand")+"}/";
            trigger=new InputAction("Hand trigger",InputActionType.Value,device+"trigger");
            grip=new InputAction("Hand grip",InputActionType.Value,device+"grip");
            trigger.Enable();grip.Enable();
            inventory=GetComponentInParent<CampusXRInventory>();extinguisher=GetComponentInParent<CampusXRExtinguisherController>();
        }
        private void OnDisable(){trigger?.Dispose();grip?.Dispose();trigger=null;grip=null;}
        private void LateUpdate()
        {
            if(joints==null)return;
            bool holding=left ? inventory!=null && (inventory.TowelEquipped || (extinguisher!=null && extinguisher.Held!=null)) : false;
            float pull=trigger!=null?trigger.ReadValue<float>():0;
            float squeeze=grip!=null?grip.ReadValue<float>():0;
            for(int i=0;i<joints.Length;i++)
            {
                int finger=i/3;
                float amount=finger==0 ? Mathf.Max(pull,holding?.55f:.08f) : Mathf.Max(squeeze,holding?.8f:.15f);
                var pose=Quaternion.Euler(amount*(i%3==0?65:85),0,0);
                joints[i].localRotation=Quaternion.Slerp(joints[i].localRotation,pose,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            }
        }
        public static CampusHandVisual Build(Transform holder,bool isLeft,Material skin,Material cuff)
        {
            var root=new GameObject("Articulated Hand").transform;root.SetParent(holder,false);
            root.localPosition=new Vector3(0,-.025f,-.035f);
            var hand=root.gameObject.AddComponent<CampusHandVisual>();hand.enabled=false;hand.left=isLeft;
            Part(root,"Palm",new Vector3(0,0,.025f),new Vector3(.082f,.036f,.09f),skin);
            Part(root,"Wrist cuff",new Vector3(0,0,-.045f),new Vector3(.062f,.045f,.055f),cuff);
            hand.joints=new Transform[12];
            for(int f=0;f<4;f++)
            {
                Transform parent=root;
                float length=f==0?.03f:f==3?.025f:.034f;
                for(int j=0;j<3;j++)
                {
                    var joint=new GameObject("Finger "+f+" Joint "+j).transform;joint.SetParent(parent,false);
                    joint.localPosition=j==0?new Vector3((f-1.5f)*.021f*(isLeft?-1:1),0,.063f):new Vector3(0,0,length);
                    Part(joint,"Phalanx",new Vector3(0,0,length*.5f),new Vector3(.018f-j*.002f,.018f-j*.002f,length+.008f),skin);
                    hand.joints[f*3+j]=joint;parent=joint;
                }
            }
            var thumb=new GameObject("Thumb").transform;thumb.SetParent(root,false);
            thumb.localPosition=new Vector3((isLeft?1:-1)*.043f,-.007f,.012f);thumb.localRotation=Quaternion.Euler(25,isLeft?48:-48,0);
            Part(thumb,"Thumb base",new Vector3(0,0,.02f),new Vector3(.026f,.025f,.05f),skin);
            Part(thumb,"Thumb tip",new Vector3(0,-.01f,.048f),new Vector3(.022f,.022f,.035f),skin);
            hand.enabled=true;return hand;
        }
        private static void Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Sphere);part.name=name;part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer=part.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
}
