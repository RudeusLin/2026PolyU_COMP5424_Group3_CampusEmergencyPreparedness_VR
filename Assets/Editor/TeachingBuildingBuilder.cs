using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    public static class TeachingBuildingBuilder
    {
        private const string LayoutPath = "Assets/Environment/TeachingBuildingLayout.json";
        private const string ScenePath = "Assets/Scenes/TeachingBuilding.unity";
        private const string PreviewFolder = "Docs/Previews";

        [Serializable] private sealed class Layout
        {
            public string scenePath, assetFolder, previewFolder, previewPrefix, skyTexture;
            public string[] prefabNames;
            public bool bakeLighting;
            public MaterialSpec[] materials;
            public NodeSpec[] nodes;
            public RouteSpec[] routes;
        }

        [Serializable] private sealed class MaterialSpec
        {
            public string name, texture, detailTexture;
            public float[] color;
            public float tileMeters, metallic, smoothness, bumpMeters;
        }

        [Serializable] private sealed class RouteSpec
        {
            public string name;
            public RoutePoint[] points;
        }

        [Serializable] private sealed class RoutePoint { public float[] position; }

        [Serializable] private sealed class NodeSpec
        {
            public int id, parent;
            public string name, kind, material, text, component, tag, printTexture;
            public float[] position, rotation, scale, textColor;
            public bool active, collision, enabled, audio, startsOpen, roof;
            public float textSize, lightRange, lightIntensity, openAngle, bevel, printWidth, printHeight;
            public int noticeKind, spawnSecond, spawnFourth, assembly, roofNode, smokeNode, walkCamera, overviewCamera;
        }

        [MenuItem("Campus Emergency/Build teaching building prototype")]
        public static void BuildAndCapture()
        {
            Build(LayoutPath);
        }

        [MenuItem("Campus Emergency/Build refined teaching building")]
        public static void BuildRefinedAndCapture()
        {
            Build("Assets/Environment/TeachingBuildingRefinedLayout.json");
        }

        [MenuItem("Campus Emergency/Build campus teaching building")]
        public static void BuildCampusAndCapture()
        {
            Build("Assets/Environment/TeachingBuildingCampusLayout.json");
        }

        private static void Build(string layoutPath)
        {
            Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(layoutPath));
            if (layout == null || layout.nodes == null || layout.nodes.Length == 0)
                throw new InvalidOperationException("Missing teaching-building layout.");
            string scenePath = string.IsNullOrEmpty(layout.scenePath) ? ScenePath : layout.scenePath;
            string previewFolder = string.IsNullOrEmpty(layout.previewFolder) ? PreviewFolder : layout.previewFolder;
            string prefix = string.IsNullOrEmpty(layout.previewPrefix) ? "Unity" : layout.previewPrefix;
            string materialFolder = string.IsNullOrEmpty(layout.assetFolder)
                ? "Assets/Materials/Environment/TeachingBuilding" : layout.assetFolder + "/Materials";
            string prefabFolder = string.IsNullOrEmpty(layout.assetFolder)
                ? "Assets/Prefabs/Environment/TeachingBuilding" : layout.assetFolder + "/Prefabs";
            string meshFolder = layout.assetFolder + "/Meshes";
            if (!Application.isBatchMode)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (File.Exists(scenePath) && !EditorUtility.DisplayDialog("Rebuild prototype",
                    "Rebuild replaces the generated scene and prototype assets. Save a copy before keeping manual edits.",
                    "Rebuild", "Cancel")) return;
            }

            Directory.CreateDirectory(previewFolder);
            Directory.CreateDirectory(materialFolder);
            Directory.CreateDirectory(prefabFolder);
            if (!string.IsNullOrEmpty(layout.assetFolder)) Directory.CreateDirectory(meshFolder);
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Dictionary<string, Material> materials = CreateMaterials(layout.materials, materialFolder);
            Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
            Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();
            Font font;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (ArgumentException) { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }

            foreach (NodeSpec spec in layout.nodes)
            {
                GameObject item;
                if (spec.kind == "box" || spec.kind == "sphere" || spec.kind == "cylinder")
                {
                    PrimitiveType primitive = spec.kind == "box" ? PrimitiveType.Cube
                        : spec.kind == "sphere" ? PrimitiveType.Sphere : PrimitiveType.Cylinder;
                    item = GameObject.CreatePrimitive(primitive);
                    if (spec.kind == "box" && spec.bevel > 0)
                        item.GetComponent<MeshFilter>().sharedMesh = BeveledBox(Vector(spec.scale), spec.bevel, meshFolder, meshes);
                    else if (spec.kind == "box" && layout.bakeLighting)
                        item.GetComponent<MeshFilter>().sharedMesh = LightmapBox(Vector(spec.scale),
                            item.GetComponent<MeshFilter>().sharedMesh, meshFolder, meshes);
                    Object.DestroyImmediate(item.GetComponent<Collider>());
                    if (spec.collision)
                        item.AddComponent<BoxCollider>().size = spec.kind == "cylinder" ? new Vector3(1, 2, 1) : Vector3.one;
                    Renderer renderer = item.GetComponent<Renderer>();
                    renderer.sharedMaterial = materials[spec.material];
                    if (spec.material == "Glass" || spec.material == "Smoke")
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                else item = new GameObject(spec.name);
                item.name = spec.name;
                objects.Add(spec.id, item);
                if (spec.parent != 0) item.transform.SetParent(objects[spec.parent].transform, false);
                item.transform.localPosition = Vector(spec.position);
                item.transform.localRotation = new Quaternion(spec.rotation[0], spec.rotation[1], spec.rotation[2], spec.rotation[3]);
                item.transform.localScale = Vector(spec.scale);
                if (!string.IsNullOrEmpty(spec.tag)) item.tag = spec.tag;

                if (spec.kind == "text" && !string.IsNullOrEmpty(spec.printTexture))
                {
                    // Campus printed lettering receives scene lighting and uses fixed UVs.
                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.name = "PrintedLettering";
                    quad.transform.SetParent(item.transform, false);
                    quad.transform.localScale = new Vector3(spec.printWidth, spec.printHeight, 1);
                    string key = Path.GetFileNameWithoutExtension(spec.printTexture) + "_"
                        + ColorUtility.ToHtmlStringRGBA(ColorValue(spec.textColor));
                    string path = materialFolder + "/Lettering_" + key + ".mat";
                    Material ink = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (ink == null)
                    {
                        ink = new Material(Shader.Find("Standard"));
                        AssetDatabase.CreateAsset(ink, path);
                    }
                    ink.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.printTexture);
                    if (ink.mainTexture == null) throw new InvalidOperationException("Missing printed sign: " + spec.printTexture);
                    ink.color = ColorValue(spec.textColor);
                    ConfigurePrintedInk(ink);
                    MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = ink;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveGI = ReceiveGI.LightProbes;
                }
                else if (spec.kind == "text")
                {
                    TextMesh text = item.AddComponent<TextMesh>();
                    text.font = font;
                    text.text = spec.text;
                    text.fontSize = PrototypeSceneBootstrap.LabelFontSize;
                    text.characterSize = spec.textSize;
                    text.anchor = TextAnchor.MiddleCenter;
                    text.alignment = TextAlignment.Center;
                    text.color = ColorValue(spec.textColor);
                }
                if (spec.kind == "light" || spec.kind == "sun")
                {
                    Light light = item.AddComponent<Light>();
                    light.type = spec.kind == "sun" ? LightType.Directional : LightType.Point;
                    light.range = spec.kind == "sun" ? 10f : spec.lightRange;
                    light.intensity = spec.kind == "sun" ? 1.15f : spec.lightIntensity;
                    light.color = spec.kind == "sun" ? new Color(1, .95f, .86f) : new Color(.98f, .985f, 1);
                    light.shadows = spec.kind == "sun" || layout.bakeLighting ? LightShadows.Soft : LightShadows.None;
                    light.shadowRadius = .12f;
                    light.shadowStrength = .85f;
                    // Broad exterior surfaces need more offset to avoid self-shadowing in the overview.
                    light.shadowBias = spec.kind == "sun" ? .05f : .035f;
                    light.shadowNormalBias = spec.kind == "sun" ? .4f : .12f;
                    if (!string.IsNullOrEmpty(layout.skyTexture) && spec.kind == "sun")
                    {
                        light.intensity = 1.5f;
                        light.shadowBias = .025f;
                        light.shadowNormalBias = .08f;
                        light.shadowCustomResolution = 2048;
                    }
                    light.lightmapBakeType = layout.bakeLighting ? LightmapBakeType.Mixed : LightmapBakeType.Realtime;
                    if (layout.bakeLighting && spec.kind == "light") light.lightmapBakeType = LightmapBakeType.Baked;
                    if (spec.kind == "sun") RenderSettings.sun = light;
                }
                if (spec.kind == "camera")
                {
                    Camera camera = item.AddComponent<Camera>();
                    camera.enabled = spec.enabled;
                    camera.clearFlags = layout.bakeLighting ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.7f, .82f, .87f);
                    camera.nearClipPlane = .08f;
                    camera.farClipPlane = 180f;
                    camera.fieldOfView = 55f;
                    camera.allowHDR = false;
                    if (spec.audio) item.AddComponent<AudioListener>();
                }
                if (spec.kind == "player")
                {
                    CharacterController controller = item.AddComponent<CharacterController>();
                    controller.height = 1.8f;
                    controller.radius = .28f;
                    controller.center = new Vector3(0, .9f, 0);
                    controller.stepOffset = .28f;
                    controller.slopeLimit = 46f;
                    controller.skinWidth = .025f;
                    controller.enabled = false;
                }
                if (spec.component == "door") item.AddComponent<PrototypeDoor>().Configure(spec.openAngle, spec.startsOpen);
                if (spec.component == "notice") item.AddComponent<FacilityNotice>().Configure((FacilityNotice.FacilityKind)spec.noticeKind);
                if (spec.component == "bootstrap") item.AddComponent<PrototypeSceneBootstrap>();
                if (spec.component == "campusAtmosphere")
                    CampusAtmosphereAssets.Configure(item, layout.assetFolder);
                item.SetActive(spec.active);
            }
            foreach (PrototypeSceneBootstrap bootstrap in Object.FindObjectsOfType<PrototypeSceneBootstrap>())
                bootstrap.InitializeText();
            foreach (NodeSpec spec in layout.nodes)
            {
                if (spec.component != "explorer") continue;
                objects[spec.id].AddComponent<PrototypeExplorer>().Configure(
                    objects[spec.walkCamera].GetComponent<Camera>(), objects[spec.overviewCamera].GetComponent<Camera>(),
                    objects[spec.spawnSecond].transform, objects[spec.spawnFourth].transform,
                    objects[spec.assembly].transform, objects[spec.roofNode], objects[spec.smokeNode]);
                objects[spec.overviewCamera].transform.LookAt(new Vector3(0, 6, -3));
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.6f, .7f, .76f);
            RenderSettings.ambientEquatorColor = new Color(.42f, .49f, .51f);
            RenderSettings.ambientGroundColor = new Color(.3f, .34f, .32f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            foreach (string prefabName in layout.prefabNames ?? new[] { "DeskAndChair", "FireEquipmentPoint" })
                SaveReusablePrefab(prefabName, objects, prefabFolder);
            GroupCeilingDetails(layout.nodes, GameObject.Find("Roof_CutawayToggle"));
            Physics.SyncTransforms();
            List<string> errors = ValidateRoutes(layout.routes);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);
            // The lightmapper exports persisted mesh/material assets, including their UV layout.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (layout.bakeLighting) TeachingBuildingLighting.Bake(layout.assetFolder, scenePath, layout.skyTexture);
            AssetDatabase.SaveAssets();
            CapturePreviews(objects, layout.nodes, previewFolder, prefix);
            File.WriteAllText(Path.Combine(previewFolder, prefix + "Validation.txt"),
                "Unity " + Application.unityVersion + "\nBoth evacuation routes: " + (errors.Count == 0 ? "PASS" : "FAIL")
                + "\nBaked lightmap atlases: " + LightmapSettings.lightmaps.Length
                + "\n" + string.Join("\n", errors) + "\nHeadset performance and VR locomotion remain unverified.\n");
            Debug.Log("TEACHING_BUILDING_SUCCESS: shared layout imported, routes checked, Unity previews captured.");
        }

        private static Vector3 Vector(float[] value) => new Vector3(value[0], value[1], value[2]);
        private static Color ColorValue(float[] value) => new Color(value[0], value[1], value[2], value[3]);

        private static void ConfigurePrintedInk(Material ink)
        {
            // Smooth alpha preserves fine strokes at a distance while receiving Standard lighting.
            ink.SetFloat("_Mode", 2);
            ink.SetFloat("_Glossiness", .08f);
            ink.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            ink.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            ink.SetInt("_ZWrite", 0);
            ink.DisableKeyword("_ALPHATEST_ON");
            ink.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            ink.EnableKeyword("_ALPHABLEND_ON");
            ink.SetOverrideTag("RenderType", "Transparent");
            ink.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(ink);
        }

        public static void RefreshCampusLetteringAndCapture()
        {
            Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Environment/TeachingBuildingCampusLayout.json"));
            EditorSceneManager.OpenScene(layout.scenePath);
            foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>(true))
            {
                if (renderer.name != "PrintedLettering") continue;
                ConfigurePrintedInk(renderer.sharedMaterial);
                renderer.receiveGI = ReceiveGI.LightProbes;
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), layout.scenePath);
            AssetDatabase.SaveAssets();
            GameObject roof = GameObject.Find("Roof_CutawayToggle");
            Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();
            foreach (NodeSpec spec in layout.nodes) if (spec.name == roof.name) objects.Add(spec.id, roof);
            CapturePreviews(objects, layout.nodes, layout.previewFolder, layout.previewPrefix);
            Debug.Log("CAMPUS_LETTERING_REFRESH_SUCCESS");
        }

        public static void RefreshRefinedPreviews()
        {
            Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Environment/TeachingBuildingRefinedLayout.json"));
            EditorSceneManager.OpenScene(layout.scenePath);
            GameObject roof = GameObject.Find("Roof_CutawayToggle");
            GroupCeilingDetails(layout.nodes, roof);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), layout.scenePath);
            Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();
            foreach (NodeSpec spec in layout.nodes) if (spec.name == roof.name) objects.Add(spec.id, roof);
            CapturePreviews(objects, layout.nodes, layout.previewFolder, layout.previewPrefix);
        }

        private static void GroupCeilingDetails(NodeSpec[] nodes, GameObject roof)
        {
            HashSet<string> names = new HashSet<string>();
            foreach (NodeSpec spec in nodes) if (spec.roof) names.Add(spec.name);
            foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!names.Contains(renderer.name) || renderer.transform.IsChildOf(roof.transform)) continue;
                Matrix4x4 before = renderer.localToWorldMatrix;
                renderer.transform.SetParent(roof.transform, true);
                for (int i = 0; i < 16; i++)
                    if (Mathf.Abs(before[i] - renderer.localToWorldMatrix[i]) > .00001f)
                        throw new InvalidOperationException("Ceiling regrouping changed world geometry: " + renderer.name);
            }
        }

        private static Mesh BeveledBox(Vector3 size, float radius, string folder, Dictionary<string, Mesh> cache)
        {
            radius = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .45f);
            string name = string.Format(CultureInfo.InvariantCulture, "Bevel_{0:F3}_{1:F3}_{2:F3}_{3:F3}",
                size.x, size.y, size.z, radius).Replace('.', '_');
            if (cache.TryGetValue(name, out Mesh cached)) return cached;
            string path = folder + "/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                EnsureMeshUvs(mesh, size);
                cache.Add(name, mesh);
                return mesh;
            }
            Vector3 half = size * .5f, core = half - Vector3.one * radius;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                float[] us = { -half[u], -core[u], core[u], half[u] };
                float[] vs = { -half[v], -core[v], core[v], half[v] };
                foreach (int side in new[] { -1, 1 })
                {
                    int first = vertices.Count;
                    for (int row = 0; row < 4; row++)
                    {
                        for (int column = 0; column < 4; column++)
                        {
                            Vector3 point = Vector3.zero;
                            point[axis] = side * half[axis];
                            point[u] = us[column]; point[v] = vs[row];
                            Vector3 closest = new Vector3(Mathf.Clamp(point.x, -core.x, core.x),
                                Mathf.Clamp(point.y, -core.y, core.y), Mathf.Clamp(point.z, -core.z, core.z));
                            Vector3 normal = (point - closest).normalized;
                            Vector3 rounded = closest + normal * radius;
                            vertices.Add(new Vector3(rounded.x / size.x, rounded.y / size.y, rounded.z / size.z));
                            normals.Add(Vector3.Scale(normal, size).normalized);
                        }
                    }
                    for (int row = 0; row < 3; row++)
                    {
                        for (int column = 0; column < 3; column++)
                        {
                            int a = first + row * 4 + column, b = a + 1, c = a + 5, d = a + 4;
                            if (side > 0) triangles.AddRange(new[] { a, b, c, a, c, d });
                            else triangles.AddRange(new[] { a, c, b, a, d, c });
                        }
                    }
                }
            }
            mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EnsureMeshUvs(mesh, size);
            AssetDatabase.CreateAsset(mesh, path);
            cache.Add(name, mesh);
            return mesh;
        }

        private static Mesh LightmapBox(Vector3 size, Mesh source, string folder, Dictionary<string, Mesh> cache)
        {
            string name = string.Format(CultureInfo.InvariantCulture, "Box_{0:F3}_{1:F3}_{2:F3}",
                size.x, size.y, size.z).Replace('.', '_');
            if (cache.TryGetValue(name, out Mesh cached)) return cached;
            string path = folder + "/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Object.Instantiate(source);
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, path);
            }
            EnsureMeshUvs(mesh, size);
            cache.Add(name, mesh);
            return mesh;
        }

        private static void EnsureMeshUvs(Mesh mesh, Vector3 size)
        {
            if (mesh.uv.Length != mesh.vertexCount)
            {
                // The six box faces occupy consecutive groups of sixteen vertices.
                Vector3[] vertices = mesh.vertices;
                Vector2[] uv = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    int axis = i / 32;
                    uv[i] = new Vector2(vertices[i][(axis + 1) % 3] + .5f, vertices[i][(axis + 2) % 3] + .5f);
                }
                mesh.uv = uv;
                mesh.RecalculateTangents();
            }
            if (mesh.name.StartsWith("Box_") && !mesh.name.EndsWith("_SafeChartsUV2"))
            {
                // Reserve separate charts for all six faces, including very thin slab edges.
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Vector2[] uv = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 normal = normals[i];
                    int axis = Mathf.Abs(normal.x) > .5f ? 0 : Mathf.Abs(normal.y) > .5f ? 1 : 2;
                    int face = axis * 2 + (normal[axis] > 0 ? 1 : 0);
                    uv[i] = new Vector2(face % 3 / 3f + .035f + (vertices[i][(axis + 1) % 3] + .5f) * (.3333333f - .07f),
                        face / 3 / 2f + .04f + (vertices[i][(axis + 2) % 3] + .5f) * .42f);
                }
                mesh.uv2 = uv;
                mesh.name += "_SafeChartsUV2";
            }
            else if (mesh.name.StartsWith("Bevel_") && !mesh.name.EndsWith("_PaddedUV2"))
            {
                // Unwrap at actual dimensions, so thin walls do not receive six equal square charts.
                // Keep thin edge charts large enough for the lightmapper's texel dilation.
                float minimum = Mathf.Min(.6f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * .12f);
                Vector3 uvSize = new Vector3(Mathf.Max(size.x, minimum), Mathf.Max(size.y, minimum), Mathf.Max(size.z, minimum));
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i], uvSize);
                mesh.vertices = vertices;
                UnwrapParam.SetDefaults(out UnwrapParam unwrap);
                unwrap.packMargin = .08f;
                Unwrapping.GenerateSecondaryUVSet(mesh, unwrap);
                vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = new Vector3(vertices[i].x / uvSize.x, vertices[i].y / uvSize.y, vertices[i].z / uvSize.z);
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                mesh.name += "_PaddedUV2";
            }
            EditorUtility.SetDirty(mesh);
        }

        private static Dictionary<string, Material> CreateMaterials(MaterialSpec[] specs, string folder)
        {
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            foreach (MaterialSpec spec in specs)
            {
                string path = folder + "/" + spec.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Standard"));
                    AssetDatabase.CreateAsset(material, path);
                }
                bool textured = !string.IsNullOrEmpty(spec.texture);
                material.shader = textured ? Resources.Load<Shader>("ArchitecturalSurface") : Shader.Find("Standard");
                if (material.shader == null) throw new InvalidOperationException("Missing surface shader.");
                material.color = ColorValue(spec.color);
                material.SetFloat("_Metallic", spec.metallic);
                material.SetFloat("_Glossiness", spec.smoothness);
                material.mainTexture = null;
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_EMISSION");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
                if (material.HasProperty("_Mode"))
                {
                    material.SetFloat("_Mode", 0);
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                    material.SetInt("_ZWrite", 1);
                    material.SetColor("_EmissionColor", Color.black);
                }
                if (!string.IsNullOrEmpty(spec.texture))
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.texture);
                    if (texture == null) throw new InvalidOperationException("Missing texture: " + spec.texture);
                    material.mainTexture = texture;
                    material.SetFloat("_TileMeters", spec.tileMeters);
                    material.SetFloat("_Metallic", spec.metallic);
                    material.SetFloat("_Glossiness", spec.smoothness);
                    Texture2D detail = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.detailTexture);
                    if (detail == null) throw new InvalidOperationException("Missing surface detail: " + spec.detailTexture);
                    material.SetTexture("_DetailTex", detail);
                    material.SetFloat("_BumpMeters", spec.bumpMeters);
                }
                if (spec.color[3] < 1)
                {
                    material.SetFloat("_Mode", 2);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.renderQueue = 3000;
                }
                if (spec.name == "Glow")
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(.96f, .98f, 1f) * .65f);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                EditorUtility.SetDirty(material);
                materials.Add(spec.name, material);
            }
            return materials;
        }

        private static void SaveReusablePrefab(string name, Dictionary<int, GameObject> objects, string folder)
        {
            foreach (GameObject candidate in objects.Values)
            {
                if (candidate.name != name) continue;
                GameObject copy = Object.Instantiate(candidate);
                copy.name = name;
                copy.transform.SetParent(null);
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (copy.GetComponentInChildren<TextMesh>(true) != null)
                {
                    copy.SetActive(false);
                    copy.AddComponent<PrototypeSceneBootstrap>().InitializeNormalizedText();
                    copy.SetActive(true);
                }
                PrefabUtility.SaveAsPrefabAsset(copy, folder + "/" + name + ".prefab");
                Object.DestroyImmediate(copy);
                return;
            }
        }

        private static List<string> ValidateRoutes(RouteSpec[] routes)
        {
            List<string> errors = new List<string>();
            PrototypeDoor[] doors = Object.FindObjectsOfType<PrototypeDoor>();
            Quaternion[] original = new Quaternion[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                original[i] = doors[i].transform.localRotation;
                doors[i].transform.localRotation = Quaternion.Euler(0, doors[i].OpenAngle, 0);
            }
            Physics.SyncTransforms();
            try
            {
                foreach (RouteSpec route in routes)
                {
                    for (int i = 1; i < route.points.Length; i++)
                    {
                        Vector3 from = Vector(route.points[i - 1].position), to = Vector(route.points[i].position);
                        int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .2f));
                        for (int j = 0; j <= count; j++)
                        {
                            Vector3 point = Vector3.Lerp(from, to, j / (float)count);
                            if (!Physics.Raycast(point + Vector3.up * .4f, Vector3.down, out RaycastHit support, .85f)
                                || Mathf.Abs(support.point.y - point.y) > .32f)
                                errors.Add(route.name + ": missing ground at " + point.ToString("F2"));
                            foreach (Collider obstacle in Physics.OverlapCapsule(point + Vector3.up * .3f, point + Vector3.up * 1.5f, .27f))
                                if (obstacle.bounds.max.y > point.y + .32f)
                                    errors.Add(route.name + ": blocked by " + obstacle.name + " at " + point.ToString("F2"));
                            if (errors.Count >= 12) return errors;
                        }
                    }
                }
            }
            finally
            {
                for (int i = 0; i < doors.Length; i++) doors[i].transform.localRotation = original[i];
                Physics.SyncTransforms();
            }
            return errors;
        }

        private static void CapturePreviews(Dictionary<int, GameObject> objects, NodeSpec[] nodes, string folder, string prefix)
        {
            GameObject holder = new GameObject("TemporaryCaptureCamera");
            Camera camera = holder.AddComponent<Camera>();
            camera.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.7f, .82f, .87f);
            camera.nearClipPlane = .06f;
            camera.allowHDR = false;
            GameObject roof = null;
            foreach (NodeSpec spec in nodes)
                if (spec.name == "Roof_CutawayToggle") roof = objects[spec.id];
            Capture(camera, folder, prefix + "-Exterior.png", new Vector3(43, 28, -45), new Vector3(0, 5.5f, -3), 55);
            roof.SetActive(false);
            Capture(camera, folder, prefix + "-FourthFloor.png", new Vector3(24, 43, -25), new Vector3(0, 10.8f, 0), 52);
            roof.SetActive(true);
            Capture(camera, folder, prefix + "-LiftLobby.png", new Vector3(-1.5f, 12.45f, -.8f), new Vector3(.25f, 12.3f, 4.5f), 75);
            Capture(camera, folder, prefix + "-Stairwell.png", new Vector3(-15.5f, 12.42f, 3.7f), new Vector3(-14.42f, 11.6f, -.8f), 80);
            if (prefix != "Unity")
            {
                Capture(camera, folder, prefix + "-Classroom.png", new Vector3(-3.8f, 12.45f, 7.3f), new Vector3(-10.9f, 12.5f, 5.9f), 75);
                Capture(camera, folder, prefix + "-Corridor.png", new Vector3(-10.9f, 12.45f, -.35f), new Vector3(8, 12.3f, 0), 70);
            }
            if (prefix == "Campus")
            {
                Capture(camera, folder, prefix + "-WindowJunction.png", new Vector3(-10.1f, 12.6f, 12.4f), new Vector3(-10.3f, 12.4f, 6), 65);
                Capture(camera, folder, prefix + "-ClassroomWindows.png", new Vector3(-8.6f, 12.45f, 3.5f), new Vector3(-7.5f, 12.6f, 8.9f), 75);
            }
            Object.DestroyImmediate(holder);
        }

        private static void Capture(Camera camera, string folder, string filename, Vector3 position, Vector3 target, float fov)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            camera.fieldOfView = fov;
            RenderTexture render = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder, filename), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                render.Release();
                Object.DestroyImmediate(render);
                Object.DestroyImmediate(image);
            }
        }
    }
}
