using UnityEngine;
using UnityEngine.UI;

namespace CampusEmergency.Player
{
    /// <summary>Shared display of the supplied, fully localized route images.</summary>
    public sealed class CampusRouteMapUI : MonoBehaviour
    {
        public CampusLanguage DisplayLanguage { get; private set; }
        public Texture MapTexture => GetComponent<RawImage>().texture;
        public static CampusRouteMapUI Build(Transform parent,bool expanded=false)
        {
            var root=new GameObject("Localized Route A Map",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            root.transform.SetParent(parent,false);
            var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(1199,1312);rect.anchoredPosition=expanded?new Vector2(0,25):new Vector2(-280,0);rect.localScale=Vector3.one*(expanded?1f:.57f);
            var image=root.GetComponent<RawImage>();image.material=LanguageSelectionUI.OverlayMaterial;image.raycastTarget=false;
            var map=root.AddComponent<CampusRouteMapUI>();map.RefreshLanguage();root.AddComponent<CampusEmergency.Player.XR.CampusHintAttention>().Configure(false,null,true);return map;
        }
        private void OnEnable()=>LanguageSelectionUI.LanguageChanged+=RefreshLanguage;
        private void OnDisable()=>LanguageSelectionUI.LanguageChanged-=RefreshLanguage;
        public void RefreshLanguage()
        {
            DisplayLanguage=LanguageSelectionUI.Current;
            string resource=DisplayLanguage==CampusLanguage.SimplifiedChinese?"RouteA_SC":DisplayLanguage==CampusLanguage.TraditionalChinese?"RouteA_TC":"RouteA_Base";
            GetComponent<RawImage>().texture=Resources.Load<Texture2D>("CampusRouteMap/"+resource);
        }
    }
}
