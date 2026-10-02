using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    public static class TeachingBuildingLighting
    {
        public static void Bake(string assetFolder, string scenePath)
        {
            string folder = assetFolder + "/Lighting";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(folder + "/BuildingLighting.asset");
            if (settings == null)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, folder + "/BuildingLighting.asset");
            }
            settings.realtimeGI = false;
            settings.bakedGI = true;
            settings.lightmapper = SystemInfo.supportsComputeShaders
                ? LightingSettings.Lightmapper.ProgressiveGPU : LightingSettings.Lightmapper.ProgressiveCPU;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.directionalityMode = LightmapsMode.CombinedDirectional;
            settings.lightmapResolution = 10;
            settings.lightmapMaxSize = 1024;
            settings.lightmapPadding = 4;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 64;
            settings.environmentSampleCount = 32;
            settings.maxBounces = 2;
            settings.lightProbeSampleCountMultiplier = 2;
            settings.ao = true;
            settings.aoMaxDistance = .4f;
            settings.aoExponentIndirect = 1;
            settings.aoExponentDirect = .65f;
            Lightmapping.lightingSettings = settings;
            EditorUtility.SetDirty(settings);

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(folder + "/CampusSky.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, folder + "/CampusSky.mat");
            }
            sky.SetFloat("_Exposure", 1.05f);
            sky.SetColor("_SkyTint", new Color(.5f, .5f, .5f));
            sky.SetColor("_GroundColor", new Color(.36f, .37f, .38f));
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = .75f;
            RenderSettings.reflectionIntensity = .65f;
            EditorUtility.SetDirty(sky);

            foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (renderer.GetComponent<TextMesh>() != null || renderer.sharedMaterial.renderQueue >= 3000) continue;
                if (renderer.GetComponentInParent<PrototypeDoor>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI);
                Vector3 size = renderer.bounds.size;
                float area = 2 * (size.x * size.y + size.x * size.z + size.y * size.z);
                Vector3 localSize = renderer.transform.localScale;
                int thinAxes = (localSize.x < .5f ? 1 : 0) + (localSize.y < .5f ? 1 : 0) + (localSize.z < .5f ? 1 : 0);
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                bool detail = !mesh.name.StartsWith("Box_");
                bool narrowSurface = renderer.name == "WallDado" || renderer.name == "Mirror"
                    || renderer.name == "LobbyBench" || renderer.name == "TopFloorGuard"
                    || renderer.name.StartsWith("Step_") || renderer.name.EndsWith("Header");
                // These surfaces still cast baked shadows; probes avoid subpixel charts on their edges.
                if (area < 2.5f || thinAxes >= 2 || detail || narrowSurface || renderer.name == "SignPanel")
                {
                    renderer.receiveGI = ReceiveGI.LightProbes;
                    renderer.scaleInLightmap = 0;
                    continue;
                }
                renderer.receiveGI = ReceiveGI.Lightmaps;
                // Preserve the authored separation; automatic chart repacking can collapse narrow edges.
                SerializedObject rendererSettings = new SerializedObject(renderer);
                rendererSettings.FindProperty("m_PreserveUVs").boolValue = true;
                rendererSettings.FindProperty("m_MinimumChartSize").intValue = 16;
                rendererSettings.ApplyModifiedPropertiesWithoutUndo();
                renderer.scaleInLightmap = area > 2f ? 1 : 1.5f;
                if (renderer.name == "Ground") renderer.scaleInLightmap = .06f;
                if (renderer.name == "Forecourt") renderer.scaleInLightmap = .25f;
            }

            GameObject lighting = new GameObject("BakedLightingAndProbes");
            List<Vector3> points = new List<Vector3>();
            for (int floor = 0; floor < 4; floor++)
                foreach (float x in new[] { -15.5f, -10.7f, -5.5f, 0, 5.5f, 10.7f, 15.5f })
                    foreach (float z in new[] { -6f, -3f, 0f, 3f, 6f })
                        foreach (float height in new[] { .65f, 2.5f })
                            points.Add(new Vector3(x, floor * 3.6f + height, z));
            lighting.AddComponent<LightProbeGroup>().probePositions = points.ToArray();
            List<ReflectionProbe> reflections = new List<ReflectionProbe>();
            for (int floor = 0; floor < 4; floor++)
                reflections.Add(CreateReflection(lighting.transform, "Reflection_F" + (floor + 1),
                    new Vector3(0, floor * 3.6f + 1.8f, 2.6f), new Vector3(36, 3.6f, 18)));
            reflections.Add(CreateReflection(lighting.transform, "Reflection_Exterior", new Vector3(0, 4, -16), new Vector3(78, 25, 72)));

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("TEACHING_LIGHTING_BAKE_START");
            BakeLightmaps(scenePath);
            if (LightmapSettings.lightmaps.Length == 0) throw new InvalidOperationException("Bake produced no lightmaps.");
            foreach (ReflectionProbe probe in reflections)
            {
                string path = folder + "/" + probe.name + ".exr";
                if (!Lightmapping.BakeReflectionProbe(probe, path))
                    throw new InvalidOperationException("Reflection bake failed: " + probe.name);
                AssetDatabase.ImportAsset(path);
                probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                if (probe.bakedTexture == null) throw new InvalidOperationException("Missing baked cubemap: " + path);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);
            Debug.Log("TEACHING_LIGHTING_BAKE_SUCCESS: " + LightmapSettings.lightmaps.Length + " lightmaps, five reflection probes.");
        }

        private static void BakeLightmaps(string scenePath)
        {
            // Unity 2022 exposes this diagnostic internally; it must be read before scene reload.
            MethodInfo overlapCheck = typeof(Lightmapping).GetMethod("HasUVOverlaps",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (overlapCheck == null) throw new InvalidOperationException("Missing Unity UV-overlap diagnostic.");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (!Lightmapping.Bake()) throw new InvalidOperationException("Teaching-building lightmap bake failed.");
                List<MeshRenderer> overlaps = new List<MeshRenderer>();
                foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
                    if (renderer.receiveGI == ReceiveGI.Lightmaps && renderer.scaleInLightmap > 0
                        && (bool)overlapCheck.Invoke(null, new object[] { renderer })) overlaps.Add(renderer);
                if (overlaps.Count == 0)
                {
                    Debug.Log("TEACHING_UV_CHECK_SUCCESS: no renderer UV overlaps after bake.");
                    return;
                }
                foreach (MeshRenderer renderer in overlaps)
                {
                    Debug.Log("TEACHING_UV_RESAMPLE: " + renderer.name + " at " + renderer.transform.position);
                    renderer.scaleInLightmap *= 2;
                }
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);
            }
            throw new InvalidOperationException("UV overlap remains after increasing the affected surfaces' lightmap density.");
        }

        private static ReflectionProbe CreateReflection(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            ReflectionProbe probe = item.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.resolution = 128;
            probe.boxProjection = true;
            probe.size = size;
            probe.center = new Vector3(0, 0, -2.6f);
            probe.blendDistance = .3f;
            probe.intensity = .65f;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.nearClipPlane = .08f;
            return probe;
        }
    }
}
