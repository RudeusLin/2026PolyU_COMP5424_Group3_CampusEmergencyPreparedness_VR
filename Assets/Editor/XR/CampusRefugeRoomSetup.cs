using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampusEmergency.Player.XR;
namespace CampusEmergency.Editor
{
    /// <summary>Small 2F refuge furnishings, kept separate from the escort and parking interaction.</summary>
    public static class CampusRefugeRoomSetup
    {
        const string Folder="Assets/Environment/RefugeRoom";
        [MenuItem("Campus Emergency/XR/Refresh 2F refuge furnishings")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Scenes/TeachingBuildingCampusXR.unity");
            var floor=GameObject.Find("TeachingBuilding/Building/Floor_02").transform;
            var previous=floor.Find("Refuge Room Furnishings");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            var root=new GameObject("Refuge Room Furnishings");root.transform.SetParent(floor,true);
            var cream=Mat("Cabinet Ivory",new Color(.87f,.89f,.86f));var green=Mat("Safety Teal",new Color(.035f,.39f,.36f));var white=Mat("Warm White",new Color(.97f,.96f,.9f));var metal=Mat("Bench Frame",new Color(.18f,.23f,.25f));var wood=Mat("Bench Timber",new Color(.54f,.39f,.24f));
            // Opposite side to the parking corner: the centre of the room stays clear.
            var cabinet=new GameObject("Emergency supplies cabinet");cabinet.transform.SetParent(root.transform,false);
            Box(cabinet.transform,"Cabinet body",new Vector3(.39f,4.85f,-4.4f),new Vector3(.48f,1.08f,1.05f),cream,true);
            Box(cabinet.transform,"Cabinet front",new Vector3(.64f,4.85f,-4.4f),new Vector3(.025f,1.01f,.98f),green,false);
            Box(cabinet.transform,"White cross vertical",new Vector3(.66f,4.94f,-4.4f),new Vector3(.016f,.35f,.11f),white,false);
            Box(cabinet.transform,"White cross horizontal",new Vector3(.66f,4.94f,-4.4f),new Vector3(.016f,.11f,.35f),white,false);
            Box(cabinet.transform,"Cabinet handle",new Vector3(.69f,4.65f,-4.73f),new Vector3(.06f,.18f,.035f),metal,false);
            var bench=new GameObject("Wall-side waiting bench");bench.transform.SetParent(root.transform,false);
            Box(bench.transform,"Bench seat",new Vector3(.46f,4.09f,-7.05f),new Vector3(.55f,.09f,1.55f),wood,true);
            Box(bench.transform,"Bench back",new Vector3(.2f,4.4f,-7.05f),new Vector3(.07f,.55f,1.55f),wood,false);
            foreach(float z in new[]{-7.63f,-6.47f})Box(bench.transform,"Bench support",new Vector3(.46f,3.85f,z),new Vector3(.42f,.44f,.06f),metal,true);
            Sign(root.transform,new Vector3(1.8f,5.5f,-1.62f),Quaternion.identity,"REFUGE AREA", "Keep entrance and wheelchair space clear",green);
            Sign(root.transform,new Vector3(.14f,5.88f,-4.4f),Quaternion.Euler(0,-90,0),"EMERGENCY SUPPLIES", "For emergency use",green);
            GameObject.Find("Refuge wheelchair parking").transform.position=CampusWheelchairRescue.ParkingPosition;
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(floor.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("REFUGE_FURNISHINGS_SAVED");
        }
        static Material Mat(string name,Color color)
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Environment","RefugeRoom");
            var path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
            material.color=color;material.SetFloat("_Glossiness",.22f);EditorUtility.SetDirty(material);return material;
        }
        static void Box(Transform parent,string name,Vector3 p,Vector3 size,Material material,bool solid)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        static void Sign(Transform parent,Vector3 p,Quaternion rotation,string title,string body,Material color)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Canvas),typeof(Image));go.transform.SetParent(parent,false);go.transform.position=p;go.transform.rotation=rotation;go.transform.localScale=Vector3.one*.001f;
            ((RectTransform)go.transform).sizeDelta=new Vector2(1200,340);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Image>().color=color.color;
            var text=new GameObject("Sign text",typeof(RectTransform),typeof(Text));text.transform.SetParent(go.transform,false);((RectTransform)text.transform).sizeDelta=new Vector2(1130,300);
            var label=text.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.alignment=TextAnchor.MiddleCenter;label.fontSize=45;label.color=Color.white;label.supportRichText=true;label.text="<b><size=57>"+title+"</size></b>\n"+body;
        }
    }
}
