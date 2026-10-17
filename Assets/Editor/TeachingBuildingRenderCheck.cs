using System;
using System.IO;
using System.Linq;
using CampusEmergency.Editor;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TeachingBuildingRenderCheck
{
    private static void OpenLegacyRefinedScene()
    {
        const string scene = "Assets/Scenes/TeachingBuildingRefined.unity";
        if (!File.Exists(scene))
            throw new FileNotFoundException("Legacy Refined scene was archived. Restore it from Git or use Campus Emergency/Legacy/Rebuild refined scene before running this legacy check.", scene);
        EditorSceneManager.OpenScene(scene);
    }

    public static void Run()
    {
        TeachingBuildingBuilder.BuildRefinedAndCapture();
        ValidateSavedScene();
    }

    public static void ValidateSavedScene()
    {
        OpenLegacyRefinedScene();
        const string folder = "Assets/Environment/TeachingBuildingRefined";
        PrototypeSceneBootstrap bootstrap = Object.FindObjectOfType<PrototypeSceneBootstrap>();
        TextMesh[] labels = bootstrap.GetComponentsInChildren<TextMesh>(true);
        Require(labels.Length > 100, "Refined scene labels exist");
        float[] sizes = labels.Select(label => label.characterSize).ToArray();
        Material originalTextMaterial = labels[0].GetComponent<MeshRenderer>().sharedMaterial;
        bootstrap.enabled = false;
        Require(originalTextMaterial != null && labels.All(label => label.GetComponent<MeshRenderer>().sharedMaterial == originalTextMaterial),
            "Disabling initializer preserves rendered labels");
        bootstrap.enabled = true;
        bootstrap.InitializeText();
        Require(labels.Select((label, i) => Mathf.Abs(label.characterSize - sizes[i]) < .00001f).All(value => value),
            "Repeated initialization preserves refined label sizes");
        foreach (TextMesh label in labels)
        {
            Require(label.GetComponent<MeshRenderer>().sharedMaterial.shader.name == "CampusEmergency/WorldSpaceText",
                "World text depth material");
            Transform panel = label.transform.parent.Find("SignPanel");
            if (panel == null) continue;
            Vector3 size = label.GetComponent<MeshRenderer>().localBounds.size;
            Require(size.x <= panel.localScale.x * 1.02f && size.y <= panel.localScale.y * 1.02f,
                "Sign fit: " + label.text + " / " + size + " / " + panel.localScale);
        }
        string[] meshes = AssetDatabase.FindAssets("t:Mesh", new[] { folder + "/Meshes" });
        Require(meshes.Length > 10, "Reusable beveled meshes generated");
        foreach (string guid in meshes)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
            if (!mesh.name.StartsWith("Bevel_")) continue;
            Require(mesh.vertexCount >= 96 && mesh.triangles.Length == 324, "Beveled box topology");
            Require(mesh.uv2.Length == mesh.vertexCount && mesh.tangents.Length == mesh.vertexCount, "Lightmap UVs and surface tangents");
            Require(mesh.normals.All(normal => Mathf.Abs(normal.magnitude - 1) < .001f), "Bevel normals");
            Require(mesh.vertices.All(v => Mathf.Abs(v.x) <= .5001f && Mathf.Abs(v.y) <= .5001f && Mathf.Abs(v.z) <= .5001f),
                "Bevel stays inside collider bounds");
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Require(Enumerable.Range(0, triangles.Length / 3).All(i => Vector3.Dot(
                Vector3.Cross(vertices[triangles[i * 3 + 1]] - vertices[triangles[i * 3]],
                    vertices[triangles[i * 3 + 2]] - vertices[triangles[i * 3]]),
                mesh.normals[triangles[i * 3]]) > 0), "Bevel triangles face outward");
        }
        string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder + "/Prefabs" });
        Require(prefabs.Length == 7, "Seven reusable environment prefabs");
        foreach (string guid in prefabs)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GUIDToAssetPath(guid));
            try
            {
                PrototypeSceneBootstrap prefabBootstrap = contents.GetComponent<PrototypeSceneBootstrap>();
                if (prefabBootstrap == null) continue;
                TextMesh label = contents.GetComponentInChildren<TextMesh>();
                float size = label.characterSize;
                prefabBootstrap.InitializeText();
                Require(Mathf.Abs(label.characterSize - size) < .00001f, "Prefab text remains normalized");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        OpenLegacyRefinedScene();
        bootstrap = Object.FindObjectOfType<PrototypeSceneBootstrap>();
        bootstrap.InitializeText();
        TextMesh[] loadedLabels = bootstrap.GetComponentsInChildren<TextMesh>(true);
        Require(loadedLabels.Length == labels.Length, "Saved refined scene reload");
        Require(loadedLabels.Select((label, i) => Mathf.Abs(label.characterSize - sizes[i]) < .00001f).All(value => value),
            "Saved scene preserves text sizes");
        Require(Object.FindObjectsOfType<MeshRenderer>().All(renderer => renderer.sharedMaterial != null),
            "All visible meshes have materials after scene reload");
        Material leaf = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Materials/Leaf.mat");
        Require(leaf.GetFloat("_Glossiness") < .1f, "Foliage has matte shading");
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Materials/Glass.mat");
        Require(glass.renderQueue == 3000 && glass.GetInt("_ZWrite") == 0, "Transparent glass depth state");
        Require(LightmapSettings.lightmaps.Length > 0 && LightmapSettings.lightmaps.All(map => map.lightmapColor != null),
            "Saved baked lighting exists");
        Require(Object.FindObjectsOfType<ReflectionProbe>().All(probe => probe.bakedTexture != null), "Reflection assets survive reload");
        foreach (string shaderName in new[] { "ArchitecturalSurface", "WorldSpaceText" })
            Require(!ShaderUtil.ShaderHasError(Resources.Load<Shader>(shaderName)), "Shader compilation: " + shaderName);
        Require(Object.FindObjectsOfType<MeshRenderer>().Where(r => GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.ContributeGI)
            && r.receiveGI == ReceiveGI.Lightmaps && r.scaleInLightmap > 0)
            .All(r => r.lightmapIndex >= 0 && r.lightmapIndex < LightmapSettings.lightmaps.Length), "Static surfaces have baked lightmaps");
        int lightmapCount = LightmapSettings.lightmaps.Length;
        CheckTextPixels();
        File.WriteAllText("Docs/Previews/Refined/ModelValidation.txt", "Unity " + Application.unityVersion
            + "\nRefined scene build and route physics: PASS\nWorld text, sign fit and reload: PASS\n"
            + "Beveled meshes, winding and bounds: PASS\nSeven prefab assets and prefab text: PASS\n"
            + "Baked lighting, reflection reload, shaders and material state: PASS\nText color, disabled initializer and opaque-wall pixel checks: PASS\n"
            + "Lightmap atlases: " + lightmapCount + "\nLabels: " + loadedLabels.Length + "\nShared environment meshes: " + meshes.Length
            + "\nVR headset performance and locomotion: NOT RUN\n");
        Debug.Log("TEACHING_RENDER_CHECK_SUCCESS");
    }

    private static void CheckTextPixels()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cameraObject = new GameObject("TextPixelCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -4);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 1;
        camera.allowHDR = false;
        GameObject root = new GameObject("TextPixelCheck");
        GameObject labelObject = new GameObject("EXIT");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = Vector3.forward;
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = "EXIT";
        label.characterSize = .45f;
        label.anchor = TextAnchor.MiddleCenter;
        label.color = new Color(.2f, .4f, .6f);
        PrototypeSceneBootstrap bootstrap = root.AddComponent<PrototypeSceneBootstrap>();
        bootstrap.InitializeText();
        Material fontMaterial = label.GetComponent<MeshRenderer>().sharedMaterial;
        label.font.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 256);
        Require(fontMaterial.mainTexture == label.font.material.mainTexture, "Font atlas rebuild");
        Color32[] visible = RenderPixels(camera);
        Require(Mathf.Abs(visible.Max(pixel => pixel.r) - 51) <= 4
            && Mathf.Abs(visible.Max(pixel => pixel.g) - 102) <= 4
            && Mathf.Abs(visible.Max(pixel => pixel.b) - 153) <= 4, "World text has the intended display color");
        bootstrap.enabled = false;
        Require(visible.SequenceEqual(RenderPixels(camera)), "Disabling text initializer preserves visible pixels");
        bootstrap.enabled = true;
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.localScale = new Vector3(3, 2, .2f);
        Material wallMaterial = new Material(Shader.Find("Unlit/Color"));
        wallMaterial.color = new Color(.12f, .2f, .3f);
        wall.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;
        Color32[] blocked = RenderPixels(camera);
        labelObject.SetActive(false);
        Require(blocked.SequenceEqual(RenderPixels(camera)), "A label behind an opaque wall contributes zero pixels");
        Object.DestroyImmediate(wallMaterial);
        OpenLegacyRefinedScene();
    }

    private static Color32[] RenderPixels(Camera camera)
    {
        RenderTexture target = new RenderTexture(256, 256, 24);
        Texture2D image = new Texture2D(256, 256, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            image.Apply();
            return image.GetPixels32();
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
