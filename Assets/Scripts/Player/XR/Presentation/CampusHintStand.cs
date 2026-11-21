using UnityEngine;
namespace CampusEmergency.Player.XR
{
    /// <summary>Noncolliding supports for outdoor instruction boards where no wall is available.</summary>
    public sealed class CampusHintStand : MonoBehaviour
    {
        private GameObject parts;
        private Transform backing,left,right;
        private Material material;
        private Transform Part(string name)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parts.transform,false);
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        public void Configure(bool visible)
        {
            if(!visible){if(parts!=null)parts.SetActive(false);return;}
            if(parts==null)
            {
                parts=new GameObject("Outdoor hint board supports");parts.transform.SetParent(transform,false);
                material=new Material(Shader.Find("Standard"));material.color=new Color(.075f,.15f,.17f);material.SetFloat("_Glossiness",.2f);
                backing=Part("Board backing");left=Part("Left post");right=Part("Right post");
            }
            parts.SetActive(true);
            var rect=(RectTransform)transform;float scale=transform.lossyScale.y;
            backing.localPosition=new Vector3(0,0,22);backing.localScale=new Vector3(rect.rect.width+12,rect.rect.height+12,30);
            float postHeight=Mathf.Max(.5f,transform.position.y-rect.rect.height*scale*.5f+.05f);
            foreach(var post in new[]{left,right})
            {
                float sign=post==left?-1:1;var world=transform.position+transform.right*sign*rect.rect.width*transform.lossyScale.x*.32f+transform.forward*.04f;world.y=postHeight*.5f;
                post.position=world;post.localRotation=Quaternion.identity;post.localScale=new Vector3(.035f/transform.lossyScale.x,postHeight/scale,.035f/transform.lossyScale.z);
            }
        }
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
