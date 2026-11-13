using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusEmergency.Player.XR;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    /// <summary>Repeatable visual refinement of the saved desktop and XR campus scenes.</summary>
    public static class CampusXRVisualUpgrade
    {
        internal const string AssetFolder = "Assets/Environment/CampusXRVisuals";
        internal const string PreviewFolder = "Docs/Previews/XRVisualUpgrade";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static CampusVisualMeshes meshes;

        [MenuItem("Campus Emergency/XR/Refine models and bake lighting")]
        public static void ApplyAndCapture() => Run(true);

        [MenuItem("Campus Emergency/XR/Refine models and update local reflections")]
        public static void ApplyModelsAndCapture() => Run(false);

        [MenuItem("Campus Emergency/Refine desktop models and materials")]
        public static void ApplyDesktopAndCapture() => Run(false, CampusXRSceneBuilder.SourceScene);

        private static void Run(bool rebake, string scenePath = CampusXRSceneBuilder.XRScene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(".utmp/XRVisualUpgrade");
            bool desktop = scenePath == CampusXRSceneBuilder.SourceScene;
            string previewFolder = desktop ? "Docs/Previews/DesktopVisualUpgrade" : PreviewFolder;
            Directory.CreateDirectory(previewFolder);
            string backup = ".utmp/XRVisualUpgrade/" + Path.GetFileNameWithoutExtension(scenePath) + ".BeforeVisualUpgrade.unity";
            if (!File.Exists(backup)) File.Copy(scenePath, backup);
            EditorSceneManager.OpenScene(scenePath);
            if (!File.Exists(previewFolder + "/Before-Classroom.png")) Capture("Before", previewFolder);
            var colliders = Object.FindObjectsOfType<Collider>(true).ToDictionary(c => c, c =>
                c.transform.localToWorldMatrix.ToString("F6") + "|" + EditorJsonUtility.ToJson(c));
            var scripts = Object.FindObjectsOfType<MonoBehaviour>(true).Where(c => c != null)
                .ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            int realtimeLights = Object.FindObjectsOfType<Light>().Count(l => l.lightmapBakeType != LightmapBakeType.Baked);
            ApplyToOpenScene(rebake);
            Physics.SyncTransforms();
            if (Object.FindObjectsOfType<Collider>(true).Length != colliders.Count
                || colliders.Any(p => p.Key == null || p.Value != p.Key.transform.localToWorldMatrix.ToString("F6") + "|" + EditorJsonUtility.ToJson(p.Key)))
                throw new InvalidOperationException("A visual edit changed an existing collider.");
            if (Object.FindObjectsOfType<Light>().Count(l => l.lightmapBakeType != LightmapBakeType.Baked) > realtimeLights)
                throw new InvalidOperationException("Visual refinement added a realtime light.");
            if (scripts.Any(p => p.Key == null || EditorJsonUtility.ToJson(p.Key) != p.Value))
                throw new InvalidOperationException("Visual refinement changed an existing behaviour.");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            if (rebake) Bake();
            BakeLocalReflections();
            EditorSceneManager.OpenScene(scenePath);
            if (!desktop) CampusSceneModificationsValidation.Validate(true);
            Capture("After", previewFolder);
            WriteReport(colliders.Count, rebake, scenePath, previewFolder);
            Debug.Log("CAMPUS_XR_VISUAL_UPGRADE_PASS");
        }

        internal static void ApplyToOpenScene(bool refineLighting = false)
        {
            string scenePath = EditorSceneManager.GetActiveScene().path;
            if (scenePath != CampusXRSceneBuilder.XRScene && scenePath != CampusXRSceneBuilder.SourceScene)
                throw new InvalidOperationException("Visual refinement requires a desktop or XR campus scene.");
            foreach (string folder in new[] { AssetFolder, AssetFolder + "/Meshes", AssetFolder + "/Materials", AssetFolder + "/Textures" })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            meshes = new CampusVisualMeshes();
            PrepareMaterials();
            RefineSurfaces();
            for (int floor = 1; floor <= 4; floor++)
            {
                Transform level = GameObject.Find("Floor_" + floor.ToString("00")).transform;
                int roomIndex = 0;
                foreach (Transform room in level.Cast<Transform>().Where(t => t.name.StartsWith("Classroom_")).OrderBy(t => t.localPosition.x))
                    RefineClassroom(room, floor * 11 + roomIndex++);
                RefineRestroom(level.Find(floor == 4 ? "Restrooms_F04" : "Restrooms"));
                RefineLuminaires(level);
            }
            RefineBrokenStair();
            if (refineLighting) ConfigureLighting();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void PrepareMaterials()
        {
            Materials.Clear();
            Clone("Wall", "WarmPlaster", new Color(.96f, .95f, .91f), .08f);
            Clone("WallLower", "SageWallCoating", new Color(.68f, .76f, .73f), .18f);
            Clone("Ceiling", "AcousticCeiling", new Color(.98f, .98f, .96f), .04f);
            var stone = Clone("Floor", "NeutralTerrazzo", Color.white, .48f);
            stone.mainTexture = Texture("NeutralTerrazzo.png");
            Clone("Wood", "SatinOak", new Color(.97f, .96f, .92f), .38f);
            Clone("Metal", "PowderCoatedSteel", new Color(.25f, .29f, .30f), .34f).SetFloat("_Metallic", .28f);
            Clone("SeatFabric", "WovenSeat", new Color(.11f, .31f, .34f), .07f);
            Solid("Porcelain", new Color(.91f, .93f, .91f), 0, .77f);
            Solid("Chrome", new Color(.69f, .73f, .75f), .92f, .76f);
            Solid("Rubber", new Color(.12f, .14f, .14f), 0, .13f);
            Solid("Partition", new Color(.45f, .60f, .55f), 0, .27f);
            Solid("Paper", new Color(.91f, .89f, .81f), 0, .12f);
            Solid("BookNavy", new Color(.10f, .21f, .29f), 0, .19f);
            Solid("BookOchre", new Color(.55f, .33f, .15f), 0, .18f);
            Solid("BookSage", new Color(.29f, .43f, .35f), 0, .18f);
            Solid("Rebar", new Color(.24f, .20f, .16f), .54f, .27f);
            Solid("Aggregate", new Color(.49f, .48f, .43f), 0, .13f);
            Solid("Mirror", new Color(.93f, .95f, .96f), 1, .98f);
            var tile = Solid("RestroomTile", Color.white, 0, .53f);
            tile.shader = Shader.Find("CampusEmergency/ArchitecturalSurface");
            tile.mainTexture = Texture("RestroomTile.png");
            tile.SetTexture("_DetailTex", Texture("RestroomTileDetail.png", true));
            tile.SetFloat("_TileMeters", 2.4f);
            tile.SetFloat("_BumpMeters", .001f);
            tile.SetFloat("_Glossiness", .53f);
            foreach (var material in Materials.Values) EditorUtility.SetDirty(material);
        }

        private static Material Clone(string source, string name, Color color, float smoothness)
        {
            string path = AssetFolder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/TeachingBuildingCampus/Materials/" + source + ".mat");
                if (original == null) throw new InvalidOperationException("Missing finish source: " + source);
                material = new Material(original) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            Materials[name] = material;
            return material;
        }

        private static Material Solid(string name, Color color, float metal, float smoothness)
        {
            string path = AssetFolder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Glossiness", smoothness);
            Materials[name] = material;
            return material;
        }

        private static Texture2D Texture(string name, bool linear = false)
        {
            string path = AssetFolder + "/Textures/" + name;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Run Tools/generate_xr_visual_textures.py first: " + path);
            bool dirty = importer.sRGBTexture == linear || !importer.mipmapEnabled || importer.anisoLevel != 4;
            if (dirty)
            {
                importer.sRGBTexture = !linear;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void RefineSurfaces()
        {
            var replacements = new Dictionary<string, string>
            {
                { "Wall", "WarmPlaster" }, { "WallLower", "SageWallCoating" }, { "Ceiling", "AcousticCeiling" },
                { "Floor", "NeutralTerrazzo" }, { "Corridor", "NeutralTerrazzo" },
                { "Wood", "SatinOak" }, { "SeatFabric", "WovenSeat" }
            };
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>(true))
            {
                if (renderer.GetComponentInParent<CampusStudentAvatar>() != null || renderer.sharedMaterial == null) continue;
                if (replacements.TryGetValue(renderer.sharedMaterial.name, out string replacement)) renderer.sharedMaterial = Materials[replacement];
                if (renderer.name == "DeskLeg" || renderer.name == "ChairLeg")
                {
                    renderer.sharedMaterial = Materials["PowderCoatedSteel"];
                    renderer.GetComponent<MeshFilter>().sharedMesh = meshes.Lathe("RoundFurnitureTube", new[] {
                        new Vector2(.5f, -.5f), new Vector2(.5f, .5f) });
                }
                if (renderer.name == "Desktop") renderer.GetComponent<MeshFilter>().sharedMesh = meshes.Box(renderer.transform.localScale, .025f);
                if (renderer.name == "ChairBack" || renderer.name == "ChairSeat")
                    renderer.GetComponent<MeshFilter>().sharedMesh = meshes.Chair(renderer.name == "ChairBack");
                if (renderer.name == "Desktop" || renderer.name == "ChairBack" || renderer.name == "ChairSeat" || renderer.name == "DeskLeg" || renderer.name == "ChairLeg")
                    ProbeLit(renderer);
            }
        }

        private static void RefineClassroom(Transform room, int seed)
        {
            var details = new CampusFinishBatch(room, "XRClassroomFinishes");
            var box = meshes.Box(new Vector3(.04f, .02f, .04f));
            int index = 0;
            foreach (Transform desk in room.Cast<Transform>().Where(t => t.name == "DeskAndChair").OrderBy(t => t.localPosition.x).ThenBy(t => t.localPosition.z))
            {
                // Decorative pieces follow the authored table transform; colliders and aisles stay fixed.
                Action<Mesh, Material, Vector3, Vector3, Quaternion> add = (mesh, mat, p, s, q) =>
                    details.Add(mesh, mat, desk.localPosition + desk.localRotation * p, s, desk.localRotation * q);
                foreach (Transform leg in desk.Cast<Transform>().Where(t => t.name == "DeskLeg" || t.name == "ChairLeg"))
                    add(box, Materials["Rubber"], new Vector3(leg.localPosition.x, .012f, leg.localPosition.z),
                        new Vector3(leg.localScale.x + .006f, .024f, leg.localScale.z + .006f), Quaternion.identity);
                foreach (float x in new[] { -.48f, .48f })
                    add(meshes.Box(new Vector3(.032f, .035f, .50f)), Materials["PowderCoatedSteel"], new Vector3(x, .69f, 0),
                        new Vector3(.032f, .035f, .50f), Quaternion.identity);
                foreach (float x in new[] { -.18f, .18f })
                    add(meshes.Box(new Vector3(.025f, .28f, .025f)), Materials["PowderCoatedSteel"], new Vector3(x, .55f, -.84f),
                        new Vector3(.025f, .28f, .025f), Quaternion.identity);
                var notebook = desk.Find("Notebook");
                if (notebook != null)
                {
                    float angle = ((seed + index * 13) % 17 - 8) * 1.3f;
                    Vector3 p = new Vector3(.18f + .035f * (index % 3), .819f, .035f - .025f * (index % 2));
                    notebook.localPosition = p;
                    notebook.localRotation = Quaternion.Euler(0, angle, 0);
                    notebook.GetComponent<Renderer>().sharedMaterial = Materials["Paper"];
                    var spine = desk.Find("NotebookSpine");
                    if (spine != null)
                    {
                        spine.localPosition = p + notebook.localRotation * new Vector3(-.12f, .007f, 0);
                        spine.localRotation = notebook.localRotation;
                    }
                    string cover = new[] { "BookNavy", "BookOchre", "BookSage" }[(seed + index) % 3];
                    add(meshes.Box(new Vector3(.225f, .003f, .295f), .001f), Materials[cover], p + Vector3.up * .011f,
                        new Vector3(.225f, .003f, .295f), notebook.localRotation);
                    if (index % 2 == 0)
                        add(meshes.Box(new Vector3(.012f, .012f, .14f), .004f), Materials["BookNavy"], new Vector3(-.27f, .815f, .07f),
                            new Vector3(.012f, .012f, .14f), Quaternion.Euler(0, 23 + index * 7, 0));
                }
                index++;
            }
            details.Save();
        }

        private static void RefineRestroom(Transform room)
        {
            var station = room.Find("WashStation");
            foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = renderer.name;
                if (name.StartsWith("Counter") || name.StartsWith("Basin") || name == "ToiletBase" || name == "ToiletTank" || name == "SeatRim")
                    renderer.sharedMaterial = Materials["Porcelain"];
                if (name == "CounterCabinet" || name == "StallDivider") renderer.sharedMaterial = Materials["Partition"];
                if (name.StartsWith("Tap") && name != "TapWaterFlow" || name.Contains("Pull") || name == "BasinDrain" || name == "MirrorFrame")
                    renderer.sharedMaterial = Materials["Chrome"];
                if (name == "Mirror") renderer.sharedMaterial = Materials["Mirror"];
                if (name == "BasinBottom" || name == "BasinSide" || name == "TapStem" || name == "TapSpout" || name == "BowlInset")
                    renderer.enabled = false;
                if (name == "ToiletTank" || name == "CounterCabinet" || name == "StallDivider")
                    renderer.GetComponent<MeshFilter>().sharedMesh = meshes.Box(renderer.transform.localScale, .035f);
                if (name == "ToiletBase")
                    renderer.GetComponent<MeshFilter>().sharedMesh = meshes.Lathe("SculptedToiletBowl", new[] {
                        new Vector2(.28f,-1), new Vector2(.31f,-.90f), new Vector2(.26f,-.45f), new Vector2(.34f,.05f),
                        new Vector2(.48f,.62f), new Vector2(.50f,.88f), new Vector2(.47f,1), new Vector2(.37f,.93f),
                        new Vector2(.28f,.40f), new Vector2(.14f,.10f), new Vector2(.001f,.08f) });
                if (name == "SeatRim")
                    renderer.GetComponent<MeshFilter>().sharedMesh = meshes.Lathe("OvalToiletSeat", new[] {
                        new Vector2(.42f,-.5f), new Vector2(.49f,-.2f), new Vector2(.5f,.2f), new Vector2(.46f,.5f),
                        new Vector2(.36f,.5f), new Vector2(.34f,.1f), new Vector2(.35f,-.4f), new Vector2(.42f,-.5f) });
            }
            var finish = new CampusFinishBatch(station, "XRWashroomFinishes");
            var basin = meshes.Lathe("RoundedInsetBasin", new[] {
                new Vector2(.25f,0), new Vector2(.46f,.75f), new Vector2(.5f,.98f), new Vector2(.49f,1),
                new Vector2(.43f,.96f), new Vector2(.35f,.25f), new Vector2(.19f,.12f), new Vector2(.001f,.10f) }, true);
            finish.Add(basin, Materials["Porcelain"], new Vector3(.25f,.79f,-.035f), new Vector3(.75f,.155f,.50f));
            var faucetPath = new List<Vector3> { new Vector3(0,.025f,0), new Vector3(0,.23f,0) };
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f, u = 1 - t;
                faucetPath.Add(u*u*u * new Vector3(0,.23f,0) + 3*u*u*t * new Vector3(0,.36f,0)
                    + 3*u*t*t * new Vector3(0,.34f,-.245f) + t*t*t * new Vector3(0,.265f,-.245f));
            }
            // Avoid coincident points at the start of the elbow.
            faucetPath.RemoveAt(1);
            finish.Add(meshes.Tube("CurvedMixerSpout", faucetPath.ToArray(), .023f, 16), Materials["Chrome"],
                station.Find("CampusFaucet").localPosition, Vector3.one);
            finish.Save();
            var details = new CampusFinishBatch(room, "XRRestroomFinishes");
            foreach (Transform toilet in room.Cast<Transform>().Where(t => t.name == "ToiletBase"))
            {
                details.Add(meshes.Box(new Vector3(.54f,.035f,.23f), .015f), Materials["Porcelain"],
                    toilet.localPosition + new Vector3(0,.715f,-.43f), new Vector3(.54f,.035f,.23f));
                details.Add(meshes.Lathe("BowlWater", new[] { new Vector2(.5f,0), new Vector2(0,0) }),
                    Materials["Partition"], toilet.localPosition + new Vector3(0,.045f,-.015f), new Vector3(.18f,1,.21f));
            }
            bool fourth = room.name == "Restrooms_F04";
            Vector3 tileSize = new Vector3(fourth ? 8.8f : 4.6f,.012f,7.3f);
            Vector3 tileCenter = new Vector3(fourth ? -6.75f : 10.55f,.008f,-5.2f);
            details.Add(meshes.Box(tileSize,.003f), Materials["RestroomTile"], tileCenter, tileSize);
            details.Save();
            Transform probeRoot = Child(room, "XRRestroomReflection");
            probeRoot.localPosition = new Vector3(tileCenter.x,1.65f,-4.9f);
            var probe = probeRoot.GetComponent<ReflectionProbe>();
            if (probe == null) probe = probeRoot.gameObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.resolution = 128;
            probe.boxProjection = true;
            probe.size = new Vector3(tileSize.x,3.3f,6.9f);
            probe.center = Vector3.zero;
            probe.importance = 20;
            probe.blendDistance = .25f;
            probe.intensity = .90f;
            probe.nearClipPlane = .08f;
        }

        private static void RefineLuminaires(Transform level)
        {
            var details = new CampusFinishBatch(level, "XRCeilingFinishes");
            foreach (var fixture in level.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "CeilingLight"))
            {
                Vector3 p = level.InverseTransformPoint(fixture.transform.position);
                Vector3 size = fixture.transform.lossyScale;
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 edge = new Vector3(size.x + .035f,.045f,.018f);
                    details.Add(meshes.Box(edge,.005f), Materials["Chrome"], p + new Vector3(0,.015f,side * (size.z + .018f) * .5f), edge);
                    edge = new Vector3(.018f,.045f,size.z);
                    details.Add(meshes.Box(edge,.005f), Materials["Chrome"], p + new Vector3(side * (size.x + .018f) * .5f,.015f,0), edge);
                }
            }
            foreach (float x in new[] { -4f,4f })
                for (int slat = 0; slat < 7; slat++)
                {
                    Vector3 size = new Vector3(.32f,.012f,.012f);
                    details.Add(meshes.Box(size,.004f), Materials["PowderCoatedSteel"], new Vector3(x,3.35f,-.09f + slat * .03f), size);
                }
            details.Save();
        }

        private static void RefineBrokenStair()
        {
            var stair = GameObject.Find("Stair_B_F01").transform;
            var damage = stair.Find("BrokenFlight_Details");
            // The desktop walkthrough keeps its original usable B stair.
            if (damage == null) return;
            int index = 0;
            foreach (var renderer in damage.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.GetComponent<MeshFilter>().sharedMesh = meshes.Fracture("ConcreteFracture_" + index, 31 + index++ * 7);
                renderer.sharedMaterial = Materials["Aggregate"];
                ProbeLit(renderer);
            }
            var details = new CampusFinishBatch(stair, "XRFractureFinishes");
            foreach (int side in new[] { -1,1 })
                for (int rod = 0; rod < 5; rod++)
                {
                    float height = side > 0 ? 3.14f : 2.22f;
                    var points = new[] { new Vector3(0,0,0), new Vector3(0,-.01f,-side*.15f),
                        new Vector3(.015f * (rod-2),-.055f,-side*.29f) };
                    details.Add(meshes.Tube("ExposedRebar_" + side + "_" + rod,points,.005f,8), Materials["Rebar"],
                        new Vector3(15.98f + rod*.29f,height,side*.94f), Vector3.one);
                }
            details.Save();
        }

        private static void ConfigureLighting()
        {
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (light.name != "InteriorLight") continue;
                light.type = LightType.Area;
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.transform.rotation = Quaternion.Euler(90,0,0);
                light.areaSize = new Vector2(1.8f,.38f);
                light.color = new Color(1f,.95f,.87f);
                light.intensity = light.transform.parent.name.StartsWith("Restrooms") ? 2.2f
                    : light.transform.parent.name.StartsWith("Stair_") ? 1.5f : 1.8f;
                light.bounceIntensity = 1.15f;
                light.range = 8;
            }
            RenderSettings.ambientIntensity = .65f;
            RenderSettings.reflectionIntensity = .85f;
        }

        private static void Bake()
        {
            string path = AssetFolder + "/XRVisualLighting.asset";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                settings = Object.Instantiate(Lightmapping.lightingSettings);
                settings.name = "XR visual lighting";
                AssetDatabase.CreateAsset(settings,path);
            }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.filteringMode = LightingSettings.FilterMode.Advanced;
            settings.denoiserTypeDirect = LightingSettings.DenoiserType.OpenImage;
            settings.denoiserTypeIndirect = LightingSettings.DenoiserType.OpenImage;
            settings.denoiserTypeAO = LightingSettings.DenoiserType.OpenImage;
            settings.lightmapResolution = 12;
            settings.lightmapMaxSize = 1024;
            settings.directSampleCount = 64;
            settings.indirectSampleCount = 128;
            settings.environmentSampleCount = 128;
            settings.maxBounces = 4;
            settings.aoMaxDistance = .23f;
            settings.aoExponentDirect = .25f;
            Lightmapping.lightingSettings = settings;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorUtility.UnloadUnusedAssetsImmediate();
            Debug.Log("CAMPUS_XR_VISUAL_BAKE_START");
            if (!Lightmapping.Bake() || LightmapSettings.lightmaps.Length == 0)
                throw new InvalidOperationException("XR visual lighting bake failed.");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("CAMPUS_XR_VISUAL_BAKE_PASS");
        }

        internal static void BakeLocalReflections()
        {
            // The original building does not mark every surface ReflectionProbeStatic.
            // Capture the room geometry explicitly, while excluding the movable student and towel.
            var movable = Object.FindObjectsOfType<CampusStudentAvatar>(true).SelectMany(a => a.GetComponentsInChildren<Renderer>(true))
                .Concat(Object.FindObjectsOfType<CampusTowel>(true).SelectMany(t => t.GetComponentsInChildren<Renderer>(true))).Distinct()
                .ToDictionary(r => r, r => r.enabled);
            try
            {
                foreach (var entry in movable) entry.Key.enabled = false;
                foreach (var probe in Object.FindObjectsOfType<ReflectionProbe>().Where(p => p.name == "XRRestroomReflection"))
                {
                    string prefix = probe.gameObject.scene.path == CampusXRSceneBuilder.SourceScene ? "Desktop_" : "";
                    string key = prefix + probe.transform.parent.parent.name;
                    string path = AssetFolder + "/RestroomReflection_" + key + ".exr";
                    probe.mode = ReflectionProbeMode.Custom;
                    probe.renderDynamicObjects = true;
                    if (!Lightmapping.BakeReflectionProbe(probe,path))
                        throw new InvalidOperationException("Local reflection bake failed: " + path);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                    if (cubemap == null) throw new InvalidOperationException("Local reflection did not import as a cubemap: " + path);
                    probe.customBakedTexture = cubemap;
                    string materialPath = AssetFolder + "/Materials/Mirror_" + key + ".mat";
                    var mirror = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (mirror == null)
                    {
                        mirror = new Material(Shader.Find("CampusEmergency/BakedMirror"));
                        AssetDatabase.CreateAsset(mirror,materialPath);
                    }
                    mirror.SetTexture("_Cube",cubemap);
                    mirror.SetVector("_Cube_HDR",probe.textureHDRDecodeValues);
                    mirror.SetVector("_ProbePosition",probe.transform.position);
                    mirror.SetVector("_BoxMin",probe.bounds.min);
                    mirror.SetVector("_BoxMax",probe.bounds.max);
                    probe.transform.parent.Find("WashStation/Mirror").GetComponent<Renderer>().sharedMaterial = mirror;
                    EditorUtility.SetDirty(mirror);
                }
            }
            finally { foreach (var entry in movable) entry.Key.enabled = entry.Value; }
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("CAMPUS_XR_VISUAL_LOCAL_REFLECTIONS_PASS");
        }

        internal static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent,false);
            return child;
        }

        internal static void RenderPart(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 size)
        {
            var item = Child(parent,name);
            item.localPosition = position;
            item.localRotation = Quaternion.identity;
            item.localScale = size;
            var filter = item.GetComponent<MeshFilter>();
            if (filter == null) filter = item.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = item.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = item.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ProbeLit(renderer);
        }

        private static void ProbeLit(MeshRenderer renderer)
        {
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            renderer.receiveGI = ReceiveGI.LightProbes;
            renderer.scaleInLightmap = 0;
            renderer.lightmapIndex = -1;
        }

        private static void Capture(string prefix, string folder)
        {
            Directory.CreateDirectory(folder);
            var holder = new GameObject("XRVisualCapture");
            try
            {
                var camera = holder.AddComponent<Camera>();
                camera.nearClipPlane = .06f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.allowHDR = false;
                TeachingBuildingBuilder.Capture(camera,folder,prefix + "-Classroom.png",new Vector3(-3.15f,13.4f,8.2f),new Vector3(-6.9f,11.4f,4.9f),80);
                TeachingBuildingBuilder.Capture(camera,folder,prefix + "-Corridor.png",new Vector3(0,12.45f,0),new Vector3(-12.8f,12.65f,0),65);
                TeachingBuildingBuilder.Capture(camera,folder,prefix + "-Restroom.png",new Vector3(-7.8f,12.45f,-3.7f),new Vector3(-5.65f,12.1f,-1.9f),72);
                TeachingBuildingBuilder.Capture(camera,folder,prefix + "-Cubicles.png",new Vector3(-5.3f,12.45f,-4.1f),new Vector3(-8.2f,11.8f,-6.9f),76);
                TeachingBuildingBuilder.Capture(camera,folder,prefix + "-BrokenStair.png",new Vector3(16.58f,5.22f,2.7f),new Vector3(16.58f,2.4f,-.25f),72);
                TeachingBuildingBuilder.Capture(camera,folder,prefix + "-Furniture.png",new Vector3(-5.1f,11.85f,6.8f),new Vector3(-6.6f,11.35f,5.1f),65);
            }
            finally { Object.DestroyImmediate(holder); }
        }

        private static void WriteReport(int colliderCount, bool rebaked, string scenePath, string folder)
        {
            var renderers = Object.FindObjectsOfType<MeshRenderer>(true);
            if (renderers.Any(r => r.sharedMaterials.Any(m => m == null))) throw new InvalidOperationException("Missing visual material.");
            if (Object.FindObjectsOfType<ReflectionProbe>().Any(p => p.texture == null)) throw new InvalidOperationException("Missing reflection texture.");
            File.WriteAllText(folder + "/Validation.txt","Unity " + Application.unityVersion
                + "\nScene: " + scenePath
                + "\nPASS: All " + colliderCount + " original colliders and world transforms unchanged"
                + "\nPASS: No added realtime lights or interactive colliders"
                + "\nPASS: Saved lightmaps and baked reflections resolve"
                + (rebaked ? "\nPASS: Full scene lighting rebaked" : "\nLighting: original scene lightmaps retained; four local reflections updated. Full GI rebake not run.")
                + (scenePath == CampusXRSceneBuilder.XRScene ? "\nPASS: Saved seven-request modeling/teleport validation"
                    : "\nPASS: Existing desktop behaviour configurations retained")
                + "\nPASS: All renderer material references resolve"
                + "\nMeshRenderers (including inactive): " + renderers.Length
                + "\nPhysical headset performance and stereo readability: NOT RUN\n");
        }
    }
}
