using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampusEmergency.Prototype;
using CampusEmergency.Player.XR;
using CampusEmergency.Interaction.FireSafety;

namespace CampusEmergency.Editor
{
    public static class CampusExtinguisherSetup
    {
        [MenuItem("Campus Emergency/XR/Install extinguisher interactions")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var prefab = PrefabUtility.LoadPrefabContents(CampusXRSceneBuilder.RigPath);
            try { ConfigureRig(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, CampusXRSceneBuilder.RigPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            ConfigureScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Validate();
        }
        public static void ConfigureScene()
        {
            foreach (var body in UnityEngine.Object.FindObjectsOfType<Transform>().Where(t => t.name == "Extinguisher").ToArray())
            {
                if (body.GetComponentInParent<CampusExtinguisher>() != null) continue;
                var parent = body.parent;
                var handle = parent.Find("ExtinguisherHandle");
                if (handle == null) throw new InvalidOperationException("Extinguisher handle missing");
                var pieces = parent.Cast<Transform>().Where(t => t.name.StartsWith("Extinguisher") || t.name == "PressureGauge" || t.name == "Sign_ABC").ToArray();
                var group = new GameObject("Interactive Extinguisher");
                group.transform.SetParent(parent, false); group.transform.position = handle.position;
                foreach (var part in pieces)
                {
                    part.SetParent(group.transform, true);
                    foreach (var child in part.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                }
                var pin = Part(group.transform,"Safety Pin",new Vector3(-.1f,0f,0f),new Vector3(.12f,.025f,.025f),Color.yellow);
                var nozzle = Part(group.transform,"Spray Nozzle",new Vector3(.08f,0f,.18f),new Vector3(.045f,.045f,.14f),new Color(.08f,.08f,.09f));
                // Place the ray at the forward opening rather than inside the nozzle mesh.
                var outlet = new GameObject("Nozzle Outlet").transform; outlet.SetParent(group.transform,false); outlet.localPosition=new Vector3(.08f,0f,.26f);
                group.AddComponent<CampusExtinguisher>().Configure(pin,outlet);
            }
            foreach (var fire in UnityEngine.Object.FindObjectsOfType<CampusForecourtFire>())
            {
                if (fire.GetComponent<CampusFireTarget>() == null) fire.gameObject.AddComponent<CampusFireTarget>();
                if (fire.GetComponent<SphereCollider>() == null && fire.GetComponent<BoxCollider>() == null)
                { var trigger=fire.gameObject.AddComponent<SphereCollider>(); trigger.isTrigger=true; trigger.radius=.5f; trigger.center=Vector3.up*.12f; }
            }
            ConfigureRig(UnityEngine.Object.FindObjectOfType<CampusXRInput>().gameObject);
            CampusFireBarrierSetup.ConfigureScene();
        }
        public static void ConfigureRig(GameObject rig)
        {
            var controller = rig.GetComponent<CampusXRExtinguisherController>() ?? rig.AddComponent<CampusXRExtinguisherController>();
            foreach(var duplicate in rig.GetComponents<CampusXRExtinguisherController>().Skip(1).ToArray()) UnityEngine.Object.DestroyImmediate(duplicate);
            var transforms=rig.GetComponentsInChildren<Transform>(true);
            controller.Configure(rig.GetComponent<CampusXRInput>(),rig.GetComponent<CampusXRInventory>(),rig.GetComponentInChildren<CampusXRFeedback>(true),
                transforms.Single(t=>t.name=="Right Controller Aim"),transforms.Single(t=>t.name=="Left Controller Aim"),transforms.Single(t=>t.name=="XR Head Camera"));
            EditorUtility.SetDirty(controller);
        }
        private static Transform Part(Transform parent,string name,Vector3 position,Vector3 scale,Color color)
        {
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube); item.name=name; item.transform.SetParent(parent,false);
            item.transform.localPosition=position; item.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            string path="Assets/XR/Campus/Materials/"+(name=="Safety Pin"?"SafetyPin":"SprayNozzle")+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Standard")); material.color=color; AssetDatabase.CreateAsset(material,path); }
            item.GetComponent<Renderer>().sharedMaterial=material;
            return item.transform;
        }
        public static void Validate()
        {
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            var items=UnityEngine.Object.FindObjectsOfType<CampusExtinguisher>();
            var bodies=UnityEngine.Object.FindObjectsOfType<Transform>().Where(t=>t.name=="Extinguisher").ToArray();
            if(items.Length==0 || items.Length!=bodies.Length || bodies.Any(t=>t.GetComponentInParent<CampusExtinguisher>()==null)) throw new Exception("Not all extinguishers interactive");
            var fires=UnityEngine.Object.FindObjectsOfType<CampusForecourtFire>();
            if(fires.Length!=9 || fires.Any(f=>f.GetComponent<CampusFireTarget>()==null)) throw new Exception("Not all fires interactive");
            if(items.Any(i=>i.Pin==null || i.Nozzle==null)) throw new Exception("Missing pin or nozzle");
            System.IO.File.WriteAllText("Docs/ExtinguisherSceneValidation.txt","PASS: "+items.Length+" complete extinguishers and "+fires.Length+" fire targets.\n");
            CampusXRSyncValidation.Validate();
        }
    }
}
