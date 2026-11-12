using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CampusEmergency.Player
{
    /// <summary>Shared map artwork with live localized labels; no language is baked into the texture.</summary>
    public sealed class CampusRouteMapUI : MonoBehaviour
    {
        private sealed class Label
        {
            public Text text;public string english,simplified,traditional;
        }
        private readonly List<Label> labels=new List<Label>();
        public CampusLanguage DisplayLanguage { get; private set; }
        public Texture MapTexture => GetComponent<RawImage>().texture;
        public string LabelText(string name)
        {
            foreach(var item in labels)if(item.text.name==name)return item.text.text;
            return string.Empty;
        }
        public static CampusRouteMapUI Build(Transform parent,bool expanded=false)
        {
            var root=new GameObject("Localized Route A Map",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            root.transform.SetParent(parent,false);
            var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(1199,1312);rect.anchoredPosition=expanded?new Vector2(0,25):new Vector2(-280,0);rect.localScale=Vector3.one*(expanded?1f:.57f);
            var image=root.GetComponent<RawImage>();image.texture=Resources.Load<Texture2D>("CampusRouteMap/RouteA_Base");
            image.material=LanguageSelectionUI.OverlayMaterial;image.raycastTarget=false;
            var map=root.AddComponent<CampusRouteMapUI>();map.CreateLabels();map.RefreshLanguage();return map;
        }
        private void OnEnable()=>LanguageSelectionUI.LanguageChanged+=RefreshLanguage;
        private void OnDisable()=>LanguageSelectionUI.LanguageChanged-=RefreshLanguage;
        private void Add(string name,string english,string simplified,string traditional,float x,float y,float w,float h,int size,bool light=false)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
            var text=go.GetComponent<Text>();text.material=LanguageSelectionUI.OverlayMaterial;text.font=LanguageSelectionUI.CurrentFont;
            text.fontSize=size;text.fontStyle=FontStyle.Bold;text.color=light?Color.white:new Color(.055f,.12f,.2f);
            text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.Max(16,size-8);text.resizeTextMaxSize=size;
            labels.Add(new Label{text=text,english=english,simplified=simplified,traditional=traditional});
        }
        private void CreateLabels()
        {
            Add("Map Title","Campus Fire Evacuation","校园火灾疏散演练","校園火災疏散演練",100,8,1000,68,54);
            Add("Map Subtitle","Route A · Four-floor evacuation map","固定路线 A · 四层立体流程图","固定路線 A · 四層立體流程圖",140,81,920,52,34);
            Add("Stair A","Stair A · West","A楼梯 · 西侧","A樓梯 · 西側",113,165,178,39,25,true);
            Add("Stair B","Stair B · East","B楼梯 · 东侧","B樓梯 · 東側",736,165,178,39,25,true);
            Add("Classroom","Room 401","401 教室","401 教室",292,247,140,39,27);
            Add("Washroom","4F Washroom","四楼洗手间","四樓洗手間",231,375,118,37,22);
            Add("A Fire","A: fire below","A侧下行着火","A側下行著火",243,480,160,34,23,true);
            Add("B Fire","Extinguish → Return","灭火 → 归还灭火器","滅火 → 歸還滅火器",561,478,220,36,23,true);
            Add("Broken Stair","B: broken stair","B侧楼梯断裂","B側樓梯斷裂",650,836,152,33,22,true);
            Add("Main Entrance","Main entrance","学校正门","學校正門",431,1038,147,25,21,true);
            Add("Assembly","Assembly · 999","集合点 · 模拟999","集合點 · 模擬999",431,1171,218,39,24,true);
            Add("Step 1","Activate the alarm","启动火警","啟動火警",1008,299,180,73,31);
            Add("Step 2","Take, wet and raise the towel","拿取、打湿并举起毛巾","拿取、打濕並舉起毛巾",1008,428,180,98,31);
            Add("Step 3","Avoid fire; change route","避火改道","避火改道",1008,588,180,81,31);
            Add("Step 4","Extinguish, return, descend","灭火、归还后下楼","滅火、歸還後下樓",1008,711,180,112,31);
            Add("Step 5","Main exit, assembly, simulated call","经正门撤离，集合并模拟报警","經正門撤離，集合並模擬報警",1008,883,180,130,31);
            Add("Route Legend","Evacuation route","青色：演练路线","青色：演練路線",128,1250,225,43,24);
            Add("Hazard Legend","Fire / broken stairs","红色：火源或断裂楼梯","紅色：火源或斷裂樓梯",408,1250,313,43,24);
            Add("Map Note","Diagram of the current practice route","根据当前项目流程绘制的分层示意图","根據目前專案流程繪製的分層示意圖",781,1240,400,60,22);
        }
        public void RefreshLanguage()
        {
            DisplayLanguage=LanguageSelectionUI.Current;
            foreach(var item in labels)
            {
                item.text.font=LanguageSelectionUI.CurrentFont;
                item.text.text=LanguageSelectionUI.T(item.english,item.simplified,item.traditional);
            }
        }
    }
}
