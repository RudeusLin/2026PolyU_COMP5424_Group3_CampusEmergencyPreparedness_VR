using UnityEngine;
using UnityEngine.UI;
using CampusEmergency.Interaction.FireSafety;

namespace CampusEmergency.Player.XR
{
    /// <summary>World-space radial countdown, reading the same continuous timer used to extinguish a fire.</summary>
    public sealed class CampusFireProgress : MonoBehaviour
    {
        private Transform head;
        private GameObject root;
        private Image fill;
        private Text countdown,caption;
        private Sprite ringSprite,discSprite;
        private Texture2D ringTexture,discTexture;
        private CampusFireTarget fire;
        private float completedUntil;
        public bool IsVisible => root!=null && root.activeSelf;
        public Transform Display => root!=null?root.transform:null;
        public float NormalizedProgress => fire!=null?Mathf.Clamp01(fire.ContinuousSeconds/CampusFireTarget.RequiredSeconds):0;
        public float SecondsRemaining => fire!=null?Mathf.Max(0,CampusFireTarget.RequiredSeconds-fire.ContinuousSeconds):CampusFireTarget.RequiredSeconds;
        public void Configure(Transform camera) { head=camera; }
        private void Awake()
        {
            root=new GameObject("Fire Suppression Progress",typeof(RectTransform),typeof(Canvas));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=40;
            var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(240,250);rect.localScale=Vector3.one*.002f;
            ringTexture=MakeTexture(true);discTexture=MakeTexture(false);
            ringSprite=Sprite.Create(ringTexture,new Rect(0,0,128,128),Vector2.one*.5f,100);
            discSprite=Sprite.Create(discTexture,new Rect(0,0,128,128),Vector2.one*.5f,100);
            var backdrop=Picture("Contrast disc",discSprite,184);backdrop.color=new Color(.015f,.035f,.05f,.92f);
            var track=Picture("Progress track",ringSprite,184);track.color=new Color(.38f,.48f,.52f,.8f);
            fill=Picture("Progress ring",ringSprite,184);fill.color=new Color(.15f,1f,.68f);
            fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Radial360;fill.fillOrigin=(int)Image.Origin360.Top;fill.fillClockwise=true;
            countdown=Label("Seconds remaining",0,46,150,65);
            caption=Label("Progress caption",-113,21,235,40);
            root.SetActive(false);
        }
        private static Texture2D MakeTexture(bool ring)
        {
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float radius=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*64);
                float alpha=Mathf.Clamp01(63-radius)*(ring?Mathf.Clamp01(radius-49):1);
                texture.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            texture.Apply();return texture;
        }
        private Image Picture(string name,Sprite sprite,float size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(root.transform,false);
            ((RectTransform)go.transform).sizeDelta=Vector2.one*size;
            var image=go.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;return image;
        }
        private Text Label(string name,float y,int size,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(root.transform,false);
            var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(0,y);
            var text=go.GetComponent<Text>();text.font=LanguageSelectionUI.CurrentFont;text.fontSize=size;text.fontStyle=FontStyle.Bold;
            text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;return text;
        }
        public void Show(CampusFireTarget target)
        {
            if(target==null)
            {
                if(fire!=null && fire.Extinguished && Time.unscaledTime<completedUntil)return;
                Hide();return;
            }
            if(fire!=target)completedUntil=0;
            fire=target;
            if(target.Extinguished)completedUntil=Time.unscaledTime+.5f;
            root.SetActive(true);Refresh();
        }
        public void Refresh()
        {
            if(!IsVisible || fire==null)return;
            if(!fire.gameObject.activeInHierarchy || (fire.Extinguished && Time.unscaledTime>=completedUntil)){Hide();return;}
            root.transform.position=fire.transform.position+Vector3.up*1.95f;
            if(head!=null)
            {
                var away=root.transform.position-head.position;
                if(away.sqrMagnitude>.0001f)root.transform.rotation=Quaternion.LookRotation(away,Mathf.Abs(Vector3.Dot(away.normalized,Vector3.up))>.99f?Vector3.forward:Vector3.up);
            }
            fill.fillAmount=NormalizedProgress;
            countdown.font=caption.font=LanguageSelectionUI.CurrentFont;
            countdown.text=fire.Extinguished?"✓":(Mathf.Ceil(SecondsRemaining*10)/10).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"s";
            caption.text=fire.Extinguished?LanguageSelectionUI.T("EXTINGUISHED","已扑灭","已撲滅"):LanguageSelectionUI.T("KEEP SPRAYING","持续喷射","持續噴射");
        }
        public void Hide(){if(root!=null)root.SetActive(false);fire=null;completedUntil=0;}
        private void LateUpdate()=>Refresh();
        private void OnDisable()=>Hide();
        private void OnDestroy()
        {
            if(root!=null)Destroy(root);
            if(ringSprite!=null)Destroy(ringSprite);if(discSprite!=null)Destroy(discSprite);
            if(ringTexture!=null)Destroy(ringTexture);if(discTexture!=null)Destroy(discTexture);
        }
    }
}
