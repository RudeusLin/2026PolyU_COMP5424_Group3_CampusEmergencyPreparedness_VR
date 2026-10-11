using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    public static class CampusModelingBuilder
    {
        private const string LayoutPath = "Assets/Environment/TeachingBuildingCampusLayout.json";
        private const string BeforePath = ".utmp/TeachingBuildingCampusBeforeModeling.json";

        public static void ApplyAndCapture()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var layout = JsonUtility.FromJson<TeachingBuildingBuilder.Layout>(File.ReadAllText(LayoutPath));
            var before = JsonUtility.FromJson<TeachingBuildingBuilder.Layout>(File.ReadAllText(BeforePath));
            EditorSceneManager.OpenScene(layout.scenePath);
            Dictionary<int, TeachingBuildingBuilder.NodeSpec> oldSpecs = before.nodes.ToDictionary(n => n.id);
            Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();
            var changed = layout.nodes.Where(n => !oldSpecs.TryGetValue(n.id, out var old)
                || JsonUtility.ToJson(n) != JsonUtility.ToJson(old)).ToArray();

            GameObject Resolve(int id)
            {
                if (objects.TryGetValue(id, out GameObject cached)) return cached;
                var spec = oldSpecs[id];
                IEnumerable<Transform> siblings = spec.parent == 0
                    ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.transform)
                    : Resolve(spec.parent).transform.Cast<Transform>();
                int ordinal = before.nodes.Where(n => n.parent == spec.parent && n.name == spec.name)
                    .TakeWhile(n => n.id != id).Count();
                Transform found = siblings.Where(t => t.name == spec.name).Skip(ordinal).FirstOrDefault();
                if (found == null) throw new InvalidOperationException("Saved scene is missing layout node " + id + " " + spec.name);
                objects.Add(id, found.gameObject);
                return found.gameObject;
            }

            // Resolve against the old hierarchy before changing names or reparenting.
            foreach (var spec in changed)
            {
                if (oldSpecs.ContainsKey(spec.id)) Resolve(spec.id);
                if (spec.parent != 0 && oldSpecs.ContainsKey(spec.parent)) Resolve(spec.parent);
            }
            string materialFolder = layout.assetFolder + "/Materials";
            string meshFolder = layout.assetFolder + "/Meshes";
            Directory.CreateDirectory(meshFolder);
            AssetDatabase.Refresh();
            var usedNames = new HashSet<string>(layout.nodes.Where(IsGeometry).Select(n => n.material));
            var materials = new Dictionary<string, Material>();
            var missing = new List<TeachingBuildingBuilder.MaterialSpec>();
            foreach (var spec in layout.materials.Where(m => usedNames.Contains(m.name)))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialFolder + "/" + spec.name + ".mat");
                if (material == null) missing.Add(spec);
                else materials.Add(spec.name, material);
            }
            foreach (var pair in TeachingBuildingBuilder.CreateMaterials(missing.ToArray(), materialFolder)) materials.Add(pair.Key, pair.Value);
            var meshes = new Dictionary<string, Mesh>();
            foreach (var spec in changed)
            {
                if (!objects.TryGetValue(spec.id, out GameObject item))
                {
                    item = IsGeometry(spec)
                        ? TeachingBuildingBuilder.CreateGeometry(spec, true, meshFolder, meshes, materials)
                        : new GameObject(spec.name);
                    objects.Add(spec.id, item);
                }
                item.name = spec.name;
                if (spec.parent != 0) item.transform.SetParent(objects[spec.parent].transform, false);
                item.transform.localPosition = new Vector3(spec.position[0], spec.position[1], spec.position[2]);
                item.transform.localRotation = new Quaternion(spec.rotation[0], spec.rotation[1], spec.rotation[2], spec.rotation[3]);
                item.transform.localScale = new Vector3(spec.scale[0], spec.scale[1], spec.scale[2]);
                if (IsGeometry(spec))
                {
                    item.GetComponent<Renderer>().sharedMaterial = materials[spec.material];
                    Collider collider = item.GetComponent<Collider>();
                    if (collider != null) collider.enabled = spec.collision;
                }
                if (spec.kind == "text" && !string.IsNullOrEmpty(spec.printTexture))
                    TeachingBuildingBuilder.ApplyPrintedLabel(item, spec, materialFolder);
                if (spec.kind == "light") TeachingBuildingBuilder.ConfigureLight(item, spec, layout);
                TeachingBuildingBuilder.ConfigureInteraction(item, spec);
                item.SetActive(spec.active);
                if (spec.roof) item.transform.SetParent(GameObject.Find("Roof_CutawayToggle").transform, true);
            }
            TeachingBuildingBuilder.ConfigureFaucets(changed, objects);
            Physics.SyncTransforms();
            var errors = TeachingBuildingBuilder.ValidateRoutes(layout.routes);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            foreach (string name in new[] { "WashStation", "RefugeDoor" })
                TeachingBuildingBuilder.SaveReusablePrefab(name, objects, layout.assetFolder + "/Prefabs");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), layout.scenePath);
            TeachingBuildingLighting.Bake(layout.assetFolder, layout.scenePath, layout.skyTexture);
            Debug.Log("CAMPUS_MODELING_UPDATE_SUCCESS: " + changed.Length + " changed/new layout nodes; original scene IDs retained.");
            CampusModelingValidation.Begin();
        }

        private static bool IsGeometry(TeachingBuildingBuilder.NodeSpec spec) =>
            spec.kind == "box" || spec.kind == "sphere" || spec.kind == "cylinder";
    }
}
