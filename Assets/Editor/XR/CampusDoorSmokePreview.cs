using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CampusEmergency.Player;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace CampusEmergency.Editor
{
    [InitializeOnLoad]
    public static class CampusDoorSmokePreview
    {
        const string Pending="CampusDoorSmokePreview.Pending";
        const string Output="E:/0_PolyU/COMP5424_XR/Project/output/DoorSmoke";
        static double ready;
        static CampusDoorSmokePreview(){EditorApplication.playModeStateChanged+=OnPlay;}
        public static void Begin(){EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){ready=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Wait;}}
        static void Wait(){if(EditorApplication.timeSinceStartup<ready)return;EditorApplication.update-=Wait;Run();}
        static void Capture(Camera camera,string name)
        {
            var old=RenderTexture.active;var oldTarget=camera.targetTexture;var target=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
            camera.targetTexture=oldTarget;RenderTexture.active=old;Object.DestroyImmediate(image);Object.DestroyImmediate(target);
        }
        static void Run()
        {
            int result=0;SessionState.SetBool(Pending,false);Directory.CreateDirectory(Output);
            try
            {
                var ui=Object.FindObjectOfType<LanguageSelectionUI>();
                ui.GetType().GetMethod("BeginExperience",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                var all=Object.FindObjectsOfType<CampusCorridorSmoke>();
                var haze=all.SelectMany(x=>x.GetComponentsInChildren<ParticleSystem>()).Where(x=>x.name=="401 Doorway Light Haze").ToArray();
                if(haze.Length!=1)throw new Exception("Expected one local haze emitter");
                var properties=new MaterialPropertyBlock();haze[0].GetComponent<ParticleSystemRenderer>().GetPropertyBlock(properties);
                var min=properties.GetVector("_ClipMin");var max=properties.GetVector("_ClipMax");
                if(min.y<11.9f||max.y>13.4f||min.x< -12.3f||max.x> -9.49f)throw new Exception("Haze exceeds local 4F passage bounds");
                if(haze[0].GetComponentInParent<CampusCorridorSmoke>().IsStairProfile)throw new Exception("Stair profile changed");
                foreach(var ps in all.SelectMany(x=>x.GetComponentsInChildren<ParticleSystem>())){ps.useAutoRandomSeed=false;ps.randomSeed=17;ps.Simulate(5,true,true,true);ps.Pause();}
                Debug.Log("DOOR_HAZE_PARTICLES="+haze[0].particleCount);
                var camera=Camera.main;
                camera.transform.position=new Vector3(-8.9f,12.45f,0);
                camera.transform.rotation=Quaternion.LookRotation(new Vector3(-1,-.04f,0));
                haze[0].GetComponent<ParticleSystemRenderer>().enabled=false;Capture(camera,"CeilingOnly");
                haze[0].GetComponent<ParticleSystemRenderer>().enabled=true;Capture(camera,"DoorwayHaze");
                camera.transform.position=new Vector3(-10.7f,12.45f,.1f);Capture(camera,"InsideHaze");
                var sign=Object.FindObjectOfType<CampusEmergency.Player.XR.CampusSmokeWarningSign>();
                if(sign==null || sign.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Missing sign or colliding sign");
                if(Vector3.Dot(-sign.transform.forward,Vector3.right)<.999f || Mathf.Abs(sign.transform.position.x+10.875f)>.01f)throw new Exception("Sign position or Block B orientation incorrect");
                camera.transform.position=new Vector3(-8.5f,12.45f,0);camera.transform.rotation=Quaternion.LookRotation(Vector3.left);
                for(int language=0;language<3;language++){
                    typeof(LanguageSelectionUI).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,(CampusLanguage)language);
                    sign.RefreshLanguage();
                    sign.GetType().GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sign,null);
                    Canvas.ForceUpdateCanvases();
                    foreach(var keys in sign.GetComponentsInChildren<CampusUIKeycaps>())keys.Refresh();
                    foreach(var text in sign.GetComponentsInChildren<UnityEngine.UI.Text>())if(text.preferredHeight>text.rectTransform.rect.height+2)throw new Exception("Warning text overflow: "+text.name);
                    Capture(camera,"SmokeWarning_"+(language==0?"EN":language==1?"SC":"TC"));
                }

                File.WriteAllText(Output+"/Validation.txt","PASS: one XR doorway haze emitter.\nPASS: local clipping stays on 4F and inside protected passage.\nPASS: stair profiles unchanged.\nCaptures: same seeded particles; ceiling-only versus added local veil.\nRESULT: PASS. Headset comfort remains to be checked on device.\n");
                Debug.Log("CAMPUS_DOOR_SMOKE_PREVIEW_PASS");
            }
            catch(Exception e){result=1;File.WriteAllText(Output+"/Validation.txt",e+"\nRESULT: FAIL\n");Debug.LogException(e);}
            EditorApplication.isPlaying=false;if(Application.isBatchMode)EditorApplication.Exit(result);
        }
    }
}

