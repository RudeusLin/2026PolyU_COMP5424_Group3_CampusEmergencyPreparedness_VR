using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusEmergency.Player.XR;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    /// <summary>Scoped, repeatable migration of the seven October modeling requests.</summary>
    public static class CampusSceneModifications
    {
        internal const string AssetFolder = "Assets/Environment/CampusSceneModifications";
        internal const string PreviewFolder = "Docs/Previews/SceneModifications";
        private static readonly List<string> Changes = new List<string>();

        [MenuItem("Campus Emergency/XR/Apply modeling requests to 401 sample")]
        public static void ApplyAndCapture() => Run(false);

        [MenuItem("Campus Emergency/XR/Complete modeling requests after sample review")]
        public static void CompleteAndValidate() => Run(true, true);

        [MenuItem("Campus Emergency/XR/Refresh modeling requests without rebaking")]
        public static void RefreshAndValidate() => Run(true);

        private static void Run(bool allClassrooms, bool rebake = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(".utmp");
            const string backup = ".utmp/TeachingBuildingCampusXR.BeforeSceneModifications.unity";
            if (!File.Exists(backup)) File.Copy(CampusXRSceneBuilder.XRScene, backup);
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            string changePath = PreviewFolder + "/ChangedObjects.txt";
            string[] previousChanges = File.Exists(changePath) ? File.ReadAllLines(changePath) : new string[0];
            ApplyToOpenScene(allClassrooms);
            File.WriteAllLines(changePath, previousChanges.Concat(Changes).Distinct());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            // Validate the serialized result, including newly imported material/mesh references.
            EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
            CampusSceneModificationsValidation.Validate(allClassrooms);
            if (allClassrooms) CampusXRTeleportValidation.ValidateScene();
            if (rebake)
            {
                RebakeLighting();
                EditorSceneManager.OpenScene(CampusXRSceneBuilder.XRScene);
                CampusSceneModificationsValidation.Validate(true);
            }
            CapturePreviews();
            Debug.Log("CAMPUS_SCENE_MODIFICATIONS_PASS: " + (allClassrooms ? "all classrooms" : "401 sample"));
        }

        internal static void ApplyToOpenScene(bool allClassrooms = true)
        {
            if (EditorSceneManager.GetActiveScene().path != CampusXRSceneBuilder.XRScene)
                throw new InvalidOperationException("This migration only applies to the Campus XR scene.");
            Changes.Clear();
            Directory.CreateDirectory(AssetFolder);
            Directory.CreateDirectory(PreviewFolder);
            AssetDatabase.Refresh();
            for (int floor = 1; floor <= 4; floor++)
            {
                Transform level = GameObject.Find("Floor_" + floor.ToString("00")).transform;
                AddCorridorLetters(level);
                if (floor == 4) MoveAlarm(level);
                foreach (Transform room in level.Cast<Transform>().Where(t => t.name.StartsWith("Classroom_")).ToArray())
                    if (allClassrooms || (floor == 4 && room.localPosition.x < 0)) ArrangeClassroom(room);
                if (floor >= 3)
                {
                    foreach (Transform equipment in level.Cast<Transform>().Where(t => t.name == "FireEquipmentPoint" && t.localPosition.x < 0).ToArray())
                        Remove(equipment);
                    var marker = GameObject.Find("FireEquipment_F" + floor.ToString("00") + "_A");
                    if (marker != null) Remove(marker.transform);
                }
                RefineRestroom(level.Find(floor == 4 ? "Restrooms_F04" : "Restrooms"));
            }
            BreakStairFlight();
            AddStairGuards();
            Physics.SyncTransforms();
        }

        private static void AddCorridorLetters(Transform level)
        {
            foreach (int side in new[] { -1, 1 })
            {
                string letter = side < 0 ? "A" : "B";
                Transform item = Child(level, "CorridorEnd_" + letter);
                item.localPosition = new Vector3(side * 12.82f, 1.88f, 0f);
                item.localRotation = Quaternion.Euler(0, side < 0 ? -90 : 90, 0);
                item.localScale = Vector3.one;
                var text = item.GetComponent<TextMesh>();
                if (text != null)
                {
                    Object.DestroyImmediate(text);
                    Object.DestroyImmediate(item.GetComponent<MeshRenderer>());
                }
                string texturePath = AssetFolder + "/Corridor-" + letter + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                if (importer == null) throw new InvalidOperationException("Generate corridor letter textures first: " + texturePath);
                if (!importer.alphaIsTransparency || importer.mipmapEnabled == false)
                {
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                string materialPath = AssetFolder + "/CorridorLetter_" + letter + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Sprites/Default"));
                    material.mainTexture = texture;
                    material.color = new Color(.12f, .49f, .57f);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                Transform ink = item.Find("PrintedLettering");
                if (ink == null)
                {
                    ink = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
                    ink.name = "PrintedLettering";
                    ink.SetParent(item, false);
                    Object.DestroyImmediate(ink.GetComponent<Collider>());
                }
                ink.localScale = new Vector3(1.3f * texture.width / texture.height, 1.3f, 1);
                ink.GetComponent<Renderer>().sharedMaterial = material;
                Record(item, "large teal corridor end letter");
            }
        }

        private static void MoveAlarm(Transform level)
        {
            Transform group = level.Find("Alarm_401");
            if (group == null)
            {
                var alarm = level.GetComponentsInChildren<FacilityNotice>(true)
                    .Single(n => n.Kind == FacilityNotice.FacilityKind.Alarm && n.transform.localPosition.x < 0);
                Vector3 oldPosition = alarm.transform.position;
                group = Child(level, "Alarm_401");
                group.position = oldPosition;
                // The visible press plate and sign are siblings of the interactive body.
                var plate = level.Cast<Transform>().Single(t => t.name == "AlarmPressPanel"
                    && Mathf.Abs(t.position.x - oldPosition.x) < .1f);
                var sign = level.Cast<Transform>().Single(t => t.name == "Sign_FIRE ALARM"
                    && Mathf.Abs(t.position.x - oldPosition.x) < .1f);
                alarm.transform.SetParent(group, true);
                plate.SetParent(group, true);
                sign.SetParent(group, true);
            }
            // North classroom wall; viewed from inside toward the exit, east is left.
            group.localPosition = new Vector3(-6.25f, 1.35f, 1.66f);
            group.localRotation = Quaternion.Euler(0, 180, 0);
            var anchor = GameObject.Find("Alarm_F04_A");
            if (anchor != null) anchor.transform.position = group.position;
            foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>())
            {
                renderer.lightmapIndex = -1;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
            }
            Record(group, "moved complete existing alarm into 401, including plate, sign, collider and interaction");
        }

        private static void ArrangeClassroom(Transform room)
        {
            var desks = room.Cast<Transform>().Where(t => t.name == "DeskAndChair")
                .OrderBy(t => t.localPosition.x).ThenBy(t => t.localPosition.z).ToList();
            if (desks.Count == 0) throw new InvalidOperationException("No desk template in " + room.name);
            while (desks.Count < 6)
            {
                var copy = Object.Instantiate(desks[0].gameObject, room).transform;
                copy.name = "DeskAndChair";
                desks.Add(copy);
            }
            foreach (Transform extra in desks.Skip(6)) Remove(extra);
            // Desks face the west teaching wall. Rows run east-west, with a central aisle.
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 2; column++)
                {
                    var desk = desks[row * 2 + column];
                    desk.localPosition = new Vector3(-1.65f + row * 1.85f, 0, column == 0 ? -.9f : 1.65f);
                    desk.localRotation = Quaternion.Euler(0, -90, 0);
                    foreach (Renderer renderer in desk.GetComponentsInChildren<Renderer>())
                    {
                        renderer.lightmapIndex = -1;
                        GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                    }
                }
            Record(room, "2 columns x 3 rows; six complete desks with their chairs and tabletop props");
        }

        private static void BreakStairFlight()
        {
            Transform level = GameObject.Find("Stair_B_F01").transform;
            for (int step = 4; step <= 9; step++)
            {
                Transform tread = level.Find("Step_Second_" + step);
                if (tread != null) Remove(tread);
            }
            foreach (Transform edge in level.Cast<Transform>().Where(t => t.name == "StepEdge"
                && t.localPosition.x > 16 && t.localPosition.z > -.95f && t.localPosition.z < .85f).ToArray()) Remove(edge);
            // Split the two stringers and rails, leaving a visible structural gap.
            foreach (Transform beam in level.Cast<Transform>().Where(t => (t.name == "StairStringer" || t.name == "StairHandrail")
                && t.localPosition.x > 15.5f).ToArray())
            {
                Vector3 midpoint = beam.localPosition;
                Vector3 half = beam.localRotation * Vector3.up * beam.localScale.y * .5f;
                Vector3 a = midpoint - half, b = midpoint + half;
                if (a.z > b.z) { Vector3 swap = a; a = b; b = swap; }
                Vector3 lowerEnd = Vector3.Lerp(a, b, .25f), upperStart = Vector3.Lerp(a, b, .75f);
                var lower = Object.Instantiate(beam.gameObject, level).transform;
                lower.name = "BrokenLower_" + beam.name;
                SetBeam(lower, a, lowerEnd);
                SetBeam(beam, upperStart, b);
                beam.name = "BrokenUpper_" + beam.name;
            }
            foreach (Transform post in level.Cast<Transform>().Where(t => t.name == "HandrailPost"
                && t.localPosition.x > 15.5f && Mathf.Abs(t.localPosition.z) < 1f).ToArray()) Remove(post);
            Material concrete = level.Find("Step_Second_10").GetComponent<Renderer>().sharedMaterial;
            Transform damage = Child(level, "BrokenFlight_Details");
            Box(damage, "UpperFracture", new Vector3(16.58f, 3.09f, .92f), new Vector3(1.62f, .18f, .22f), concrete, false, 11);
            Box(damage, "LowerFracture", new Vector3(16.58f, 2.19f, -.92f), new Vector3(1.62f, .18f, .22f), concrete, false, -14);
            for (int i = 0; i < 5; i++)
                Box(damage, "ConcreteFragment_" + i, new Vector3(16.05f + i * .23f, .12f, -.35f + i * .18f),
                    new Vector3(.19f + i * .025f, .13f, .23f), concrete, false, 18 + i * 23);
            // Opaque to the existing environment ray mask, invisible to the camera.
            Guard(level, "BrokenFlight_TeleportBarrier", new Vector3(16.58f, 2.7f, .78f), new Vector3(1.96f, 5.4f, .1f));
            Record(level, "B stair F02 to F01: six treads removed, split rails/stringers, nonwalkable fragments and solid teleport barrier");
        }

        private static void SetBeam(Transform beam, Vector3 start, Vector3 end)
        {
            beam.localPosition = (start + end) * .5f;
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            beam.localScale = new Vector3(beam.localScale.x, Vector3.Distance(start, end), beam.localScale.z);
            foreach (Renderer renderer in beam.GetComponentsInChildren<Renderer>()) renderer.lightmapIndex = -1;
        }

        private static void RebakeLighting()
        {
            string settingsPath = AssetFolder + "/XRLightingSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
            if (settings == null)
            {
                settings = Object.Instantiate(Lightmapping.lightingSettings);
                settings.name = "XR scene modifications lighting";
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            Lightmapping.lightingSettings = settings;
            // Detach the shared desktop lighting data before baking this scene's own data.
            Lightmapping.lightingDataAsset = null;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("CAMPUS_MODIFICATIONS_LIGHTING_START");
            if (!Lightmapping.Bake() || LightmapSettings.lightmaps.Length == 0)
                throw new InvalidOperationException("XR lighting bake failed.");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("CAMPUS_MODIFICATIONS_LIGHTING_PASS: " + LightmapSettings.lightmaps.Length + " lightmaps");
        }

        private static void AddStairGuards()
        {
            foreach (string side in new[] { "A", "B" })
            {
                Transform stairs = GameObject.Find("Stairwell_" + side).transform;
                Transform guards = Child(stairs, "Stairwell_InvisibleGuards");
                float center = side == "A" ? -15.5f : 15.5f;
                // Stop lateral shortcuts between flights, but leave both end landings open.
                Guard(guards, "BetweenFlights", new Vector3(center, 7.1f, 0), new Vector3(.46f, 14.2f, 3.6f));
                foreach (float offset in new[] { -2.23f, 2.23f })
                    Guard(guards, "OuterFlight_" + (offset < 0 ? "West" : "East"),
                        new Vector3(center + offset, 7.1f, 0), new Vector3(.54f, 14.2f, 3.6f));
                Record(guards, "invisible solid Default-layer colliders; main and half landing turns remain open");
            }
        }

        private static void RefineRestroom(Transform room)
        {
            if (room == null) throw new InvalidOperationException("Restroom missing.");
            Transform station = room.Find("WashStation");
            Material ceramic = MaterialAsset("Porcelain", new Color(.84f, .87f, .85f), .05f, .62f);
            Material chrome = MaterialAsset("BrushedChrome", new Color(.62f, .68f, .70f), .72f, .78f);
            Material cabinet = MaterialAsset("CabinetLaminate", new Color(.43f, .57f, .55f), .05f, .25f);
            Material mirror = MaterialAsset("MirrorGlass", new Color(.63f, .74f, .77f), .65f, .96f);
            Material dark = MaterialAsset("GroutAndRubber", new Color(.15f, .21f, .23f), 0, .15f);
            foreach (Renderer r in station.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("Counter") || r.name.StartsWith("BasinSide") || r.name == "BasinBottom") r.sharedMaterial = ceramic;
                if (r.name == "CounterCabinet") r.sharedMaterial = cabinet;
                if (r.name.StartsWith("Tap") && r.name != "TapWaterFlow" || r.name == "BasinDrain" || r.name == "MirrorFrame") r.sharedMaterial = chrome;
                if (r.name == "Mirror") r.sharedMaterial = mirror;
            }
            foreach (Renderer r in room.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "WashCounter" || r.name == "ToiletTank") r.sharedMaterial = ceramic;
                if (r.name == "CounterSupport") r.sharedMaterial = cabinet;
            }
            Transform details = Child(station, "WashStation_FinishDetails");
            Box(details, "CabinetToeKick", new Vector3(0, .055f, -.24f), new Vector3(1.78f, .11f, .04f), dark);
            Box(details, "CabinetDoorJoint", new Vector3(0, .45f, -.236f), new Vector3(.012f, .62f, .008f), dark);
            foreach (float x in new[] { -.25f, .25f })
                Box(details, "CabinetPull_" + x, new Vector3(x, .69f, -.254f), new Vector3(.18f, .018f, .025f), chrome);
            Box(details, "CounterBacksplash", new Vector3(0, 1.01f, .32f), new Vector3(1.95f, .14f, .035f), ceramic);
            Box(details, "MirrorBacking", new Vector3(0, 1.65f, .33f), new Vector3(1.9f, 1.01f, .035f), dark);
            Box(details, "SoapDispenser", new Vector3(.82f, 1.04f, .02f), new Vector3(.12f, .18f, .12f), ceramic);
            Box(details, "SoapPump", new Vector3(.82f, 1.15f, -.01f), new Vector3(.09f, .035f, .1f), chrome);
            Transform faucet = station.Find("CampusFaucet");
            Box(details, "TapLeverPivot", faucet.localPosition + new Vector3(.02f, .3f, .005f), new Vector3(.08f, .06f, .07f), chrome);
            foreach (Transform divider in room.Cast<Transform>().Where(t => t.name == "StallDivider").ToArray())
            {
                var p = divider.localPosition;
                divider.localPosition = new Vector3(p.x, 1.08f, p.z);
                divider.localScale = new Vector3(divider.localScale.x, 1.82f, divider.localScale.z);
                divider.GetComponent<Renderer>().sharedMaterial = cabinet;
                Transform finishes = Child(room, "CubicleFinish_" + p.x);
                Box(finishes, "DoorLatch", new Vector3(p.x + .52f, 1.05f, p.z + .05f), new Vector3(.055f, .14f, .035f), chrome);
                foreach (float x in new[] { p.x - .68f, p.x + .68f })
                    Box(finishes, "PartitionFoot_" + x, new Vector3(x, .09f, p.z), new Vector3(.045f, .18f, .045f), chrome);
            }
            foreach (Transform toilet in room.Cast<Transform>().Where(t => t.name == "ToiletBase").ToArray())
            {
                toilet.GetComponent<Renderer>().sharedMaterial = ceramic;
                Transform finish = Child(room, "ToiletFinish_" + toilet.localPosition.x);
                Box(finish, "SeatRim", new Vector3(toilet.localPosition.x, .475f, toilet.localPosition.z), new Vector3(.52f, .045f, .58f), ceramic);
                Box(finish, "BowlInset", new Vector3(toilet.localPosition.x, .501f, toilet.localPosition.z - .02f), new Vector3(.31f, .006f, .36f), dark);
                Box(finish, "FlushPlate", new Vector3(toilet.localPosition.x, .935f, toilet.localPosition.z - .43f), new Vector3(.13f, .015f, .07f), chrome);
            }
            Record(room, "wash station, mirror, chrome, cabinet, soap and cubicle/toilet details; towel/tap transforms preserved");
        }

        private static Material MaterialAsset(string name, Color color, float metallic, float smoothness)
        {
            string path = AssetFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision = false, float yaw = 0)
        {
            Transform item = parent.Find(name);
            if (item == null)
            {
                item = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                item.name = name;
                item.SetParent(parent, false);
            }
            item.localPosition = position;
            item.localScale = size;
            item.localRotation = Quaternion.Euler(0, yaw, 0);
            item.GetComponent<Renderer>().sharedMaterial = material;
            item.GetComponent<Collider>().enabled = collision;
        }

        private static void Guard(Transform parent, string name, Vector3 position, Vector3 size)
        {
            Transform item = Child(parent, name);
            item.localPosition = position;
            item.gameObject.layer = 0;
            var collider = item.GetComponent<BoxCollider>();
            if (collider == null) collider = item.gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = false;
        }

        private static void Remove(Transform item)
        {
            Record(item, "removed complete object hierarchy");
            Object.DestroyImmediate(item.gameObject);
        }

        private static void Record(Transform item, string action)
        {
            string path = item.name;
            for (Transform p = item.parent; p != null; p = p.parent) path = p.name + "/" + path;
            Changes.Add(path + ": " + action);
        }

        internal static void CapturePreviews()
        {
            var holder = new GameObject("SceneModificationsCapture");
            try
            {
                var camera = holder.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.nearClipPlane = .06f;
                for (int floor = 1; floor <= 4; floor++)
                {
                    float y = (floor - 1) * 3.6f;
                    foreach (string side in new[] { "A", "B" })
                        TeachingBuildingBuilder.Capture(camera, PreviewFolder, "F" + floor.ToString("00") + "-" + side + ".png",
                            new Vector3(0, y + 1.65f, 0), new Vector3(side == "A" ? -12.8f : 12.8f, y + 1.85f, 0), 65);
                }
                TeachingBuildingBuilder.Capture(camera, PreviewFolder, "401-Alarm.png", new Vector3(-6f, 12.45f, 4.2f), new Vector3(-6.5f, 12.1f, 1.65f), 72);
                TeachingBuildingBuilder.Capture(camera, PreviewFolder, "401-SixDesks.png", new Vector3(-3.15f, 13.4f, 8.2f), new Vector3(-6.9f, 11.4f, 4.9f), 80);
                TeachingBuildingBuilder.Capture(camera, PreviewFolder, "B-Stair-Break.png", new Vector3(16.58f, 5.22f, 2.7f), new Vector3(16.58f, 2.4f, -.25f), 72);
                TeachingBuildingBuilder.Capture(camera, PreviewFolder, "F04-Restroom.png", new Vector3(-7.8f, 12.45f, -3.7f), new Vector3(-5.65f, 12.1f, -1.9f), 72);
                TeachingBuildingBuilder.Capture(camera, PreviewFolder, "F04-Cubicles.png", new Vector3(-5.3f, 12.45f, -4.1f), new Vector3(-8.2f, 11.8f, -6.9f), 76);
            }
            finally { Object.DestroyImmediate(holder); }
        }
    }
}
