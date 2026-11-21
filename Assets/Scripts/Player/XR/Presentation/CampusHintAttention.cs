using UnityEngine;
using UnityEngine.UI;

namespace CampusEmergency.Player.XR
{
    /// <summary>A quiet frame and an angle-dependent information badge for wall-mounted hints.</summary>
    public sealed class CampusHintAttention : MonoBehaviour
    {
        private RectTransform panel,frame,badge;
        private Image[] edges;
        private Image badgeBackground;
        private Text glyph;
        private Transform viewer;
        private bool mounted,forceFrame;
        public bool AttentionVisible => badge!=null && badge.gameObject.activeInHierarchy;
        public bool FrameVisible => frame!=null && frame.gameObject.activeInHierarchy;
        public Transform AttentionMarker => badge;
        private void Awake()
        {
            panel=(RectTransform)transform;
            frame=new GameObject("Thin teal frame",typeof(RectTransform)).GetComponent<RectTransform>();frame.SetParent(transform,false);frame.anchorMin=Vector2.zero;frame.anchorMax=Vector2.one;frame.offsetMin=frame.offsetMax=Vector2.zero;
            edges=new[]{Edge("Top",new Vector2(0,1),Vector2.one,new Vector2(0,-4),Vector2.zero),Edge("Bottom",Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,4)),Edge("Left",Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(4,0)),Edge("Right",new Vector2(1,0),Vector2.one,new Vector2(-4,0),Vector2.zero)};
            badge=new GameObject("Side-view information cue",typeof(RectTransform),typeof(Canvas),typeof(Image)).GetComponent<RectTransform>();badge.SetParent(transform,false);badge.sizeDelta=new Vector2(110,110);badge.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            badgeBackground=badge.GetComponent<Image>();badgeBackground.raycastTarget=false;badgeBackground.color=new Color(.035f,.24f,.25f,.95f);
            var outline=badge.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.4f,.95f,.85f,.9f);outline.effectDistance=new Vector2(3,-3);outline.useGraphicAlpha=true;
            var icon=new GameObject("Information glyph",typeof(RectTransform),typeof(Text));var rect=icon.GetComponent<RectTransform>();rect.SetParent(badge,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            glyph=icon.GetComponent<Text>();glyph.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");glyph.text="i";glyph.fontStyle=FontStyle.Bold;glyph.fontSize=85;glyph.alignment=TextAnchor.MiddleCenter;glyph.raycastTarget=false;
            frame.gameObject.SetActive(false);badge.gameObject.SetActive(false);
        }
        private Image Edge(string name,Vector2 min,Vector2 max,Vector2 insetMin,Vector2 insetMax)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=go.GetComponent<RectTransform>();rect.SetParent(frame,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=insetMin;rect.offsetMax=insetMax;
            var image=go.GetComponent<Image>();image.raycastTarget=false;var background=GetComponent<Graphic>();if(background!=null)image.material=background.material;return image;
        }
        public void Configure(bool surfaceMounted,Transform head,bool frameOnly=false){mounted=surfaceMounted;forceFrame=frameOnly;viewer=head;RefreshAttention();}
        public void RefreshAttention()
        {
            frame.gameObject.SetActive(mounted||forceFrame);
            var background=GetComponent<Graphic>();if(background!=null){var material=background.material;foreach(var edge in edges)if(edge.material!=material)edge.material=material;if(badgeBackground.material!=material)badgeBackground.material=material;if(glyph.material!=material)glyph.material=material;}
            if(!mounted || viewer==null || !isActiveAndEnabled){foreach(var edge in edges)edge.color=new Color(.25f,.73f,.67f,.9f);badge.gameObject.SetActive(false);return;}
            Vector3 toViewer=viewer.position-transform.position;
            float distance=toViewer.magnitude;
            float front=distance>.001f?Vector3.Dot(-transform.forward,toViewer/distance):1f;
            float gaze=distance>.001f?Vector3.Dot(viewer.forward,-toViewer/distance):1f;
            bool oblique=(front<.6f || gaze<.45f) && front>.02f && gaze>-.15f && distance<5.5f;
            // A small projection from the wall gives the cue visible area when the text is edge-on.
            badge.localPosition=new Vector3(panel.rect.width*.5f-55f,panel.rect.height*.5f+65f,-128f);
            if(oblique){Vector3 ray=badge.position-viewer.position;foreach(var hit in Physics.RaycastAll(viewer.position,ray.normalized,ray.magnitude,~0,QueryTriggerInteraction.Ignore))if(!hit.collider.transform.IsChildOf(viewer.root)){oblique=false;break;}}
            badge.gameObject.SetActive(oblique);
            float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*Mathf.PI*2f*.65f);
            var frameColor=Color.Lerp(new Color(.18f,.59f,.56f,.8f),new Color(.4f,.92f,.82f,1f),oblique?.3f+.5f*pulse:.3f);
            foreach(var edge in edges)edge.color=frameColor;
            if(!oblique)return;
            var facing=Vector3.ProjectOnPlane(badge.position-viewer.position,Vector3.up);if(facing.sqrMagnitude>.001f)badge.rotation=Quaternion.LookRotation(facing);
            badge.localScale=Vector3.one*(1f+.08f*pulse);glyph.color=new Color(1f,.85f,.4f,.8f+.2f*pulse);
        }
        private void LateUpdate()=>RefreshAttention();
        private void OnDisable(){if(badge!=null)badge.gameObject.SetActive(false);}
    }
}
