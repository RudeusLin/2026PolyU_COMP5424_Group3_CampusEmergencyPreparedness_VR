using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    public static class CampusStudentAppearanceBuilder
    {
        [MenuItem("Campus Emergency/Update campus student appearance")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var layout = JsonUtility.FromJson<TeachingBuildingBuilder.Layout>(
                File.ReadAllText("Assets/Environment/TeachingBuildingCampusLayout.json"));
            EditorSceneManager.OpenScene(layout.scenePath);
            ReplaceVisualRig(layout);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),layout.scenePath);
            Debug.Log("CAMPUS_STUDENT_APPEARANCE_SUCCESS: visual rig and prefab updated; existing controller and building lighting retained.");
        }

        internal static void ReplaceVisualRig(TeachingBuildingBuilder.Layout layout)
        {
            NodePaths(layout.nodes, out var paths);
            var root = layout.nodes.Single(n => n.component == "studentAvatar");
            GameObject parentObject = GameObject.Find(paths[root.parent]);
            if (parentObject == null) throw new InvalidOperationException("Missing student controller: " + paths[root.parent]);
            Transform parent = parentObject.transform;
            GameObject previous = parent.Find(root.name)?.gameObject;
            if (!string.IsNullOrEmpty(root.model))
            {
                GameObject imported = CampusImportedStudentAssets.Create(root.model, parent, root.name);
                if (previous != null) Object.DestroyImmediate(previous);
                TeachingBuildingBuilder.SaveReusablePrefab("CampusStudent",
                    new Dictionary<int, GameObject> { { root.id, imported } }, layout.assetFolder + "/Prefabs");
                return;
            }
            Directory.CreateDirectory(layout.assetFolder + "/Meshes");
            var materials = TeachingBuildingBuilder.CreateMaterials(layout.materials.Where(m => m.name.StartsWith("Student")).ToArray(),
                layout.assetFolder + "/Materials");
            var meshes = new Dictionary<string, Mesh>();
            var objects = new Dictionary<int, GameObject>();
            // Build beside the existing avatar, then replace only this generated visual rig.
            foreach (var spec in layout.nodes)
            {
                if (spec.id != root.id && !objects.ContainsKey(spec.parent)) continue;
                GameObject item = spec.kind == "box" || spec.kind == "sphere" || spec.kind == "cylinder"
                    ? TeachingBuildingBuilder.CreateGeometry(spec, true, layout.assetFolder + "/Meshes", meshes, materials)
                    : new GameObject(spec.name);
                item.name = spec.name;
                item.transform.SetParent(spec.id == root.id ? parent : objects[spec.parent].transform, false);
                item.transform.localPosition = new Vector3(spec.position[0],spec.position[1],spec.position[2]);
                item.transform.localRotation = new Quaternion(spec.rotation[0],spec.rotation[1],spec.rotation[2],spec.rotation[3]);
                item.transform.localScale = new Vector3(spec.scale[0],spec.scale[1],spec.scale[2]);
                item.SetActive(spec.active);
                objects.Add(spec.id,item);
            }
            GameObject avatarObject = objects[root.id];
            CampusStudentAvatar avatar = avatarObject.AddComponent<CampusStudentAvatar>();
            avatar.InitializeRig();
            foreach (MeshRenderer renderer in avatarObject.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                renderer.receiveGI = ReceiveGI.LightProbes;
                renderer.lightmapIndex = -1;
            }
            if (avatarObject.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("The student visual rig must not own colliders.");
            if (previous != null) Object.DestroyImmediate(previous);
            TeachingBuildingBuilder.SaveReusablePrefab("CampusStudent", objects, layout.assetFolder + "/Prefabs");
        }

        private static void NodePaths(TeachingBuildingBuilder.NodeSpec[] nodes, out Dictionary<int,string> paths)
        {
            paths = new Dictionary<int,string>();
            foreach (var node in nodes) paths.Add(node.id,node.parent == 0 ? node.name : paths[node.parent] + "/" + node.name);
        }
    }
}
