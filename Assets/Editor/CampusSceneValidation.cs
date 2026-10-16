using System;
using System.IO;
using System.Linq;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    [InitializeOnLoad]
    public static class CampusSceneValidation
    {
        private const string PendingKey = "CampusSceneValidation.Pending";
        private const string Folder = "Docs/Previews/Campus";

        static CampusSceneValidation()
        {
            if (SessionState.GetBool(PendingKey, false))
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Begin()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/TeachingBuildingCampus.unity");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Validate;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Validate()
        {
            SessionState.SetBool(PendingKey, false);
            try
            {
                CampusDrillAtmosphere atmosphere = Object.FindObjectsOfType<CampusDrillAtmosphere>(true).Single();
                Require(atmosphere.gameObject.activeSelf, "Drill alarm must start enabled");
                Require(LightmapSettings.lightmaps.Length > 0, "Saved scene lost its lighting");
                int junctions = CheckWindowJunctions();
                int daylightPixels = CheckScannedMaterialsAndDaylight();
                int letteringPixels = CheckPrintedSignage();
                foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>(true))
                    Require(renderer.sharedMaterial != null, "Missing material on " + renderer.name);
                foreach (string name in new[] { "CampusSmoke", "ArchitecturalSurface", "WorldSpaceText" })
                {
                    Shader shader = Resources.Load<Shader>(name);
                    Require(shader != null && shader.isSupported, "Unsupported shader " + name);
                    Require(!ShaderUtil.GetShaderMessages(shader).Any(message => message.severity.ToString() == "Error"),
                        "Shader compilation error in " + name);
                }
                Vector3 drillEye = new Vector3(3.6f, 12.45f, -.5f), drillTarget = new Vector3(8, 13f, 2f);
                AudioSource audio = atmosphere.GetComponentsInChildren<AudioSource>(true).Single();
                Require(!audio.isPlaying, "Alarm must not start by itself");
                Require(audio.clip != null && audio.volume <= .12f && audio.spatialBlend == 1f,
                    "Alarm must have a quiet spatial audio clip");
                Capture("Campus-DrillPreview.png", drillEye, drillTarget, 74);
                atmosphere.ActivateAlarm();
                Require(audio.isPlaying, "Alarm activation must start audio");
                Capture("Campus-Noticeboard.png", new Vector3(-4.35f, 12.45f, .85f), new Vector3(-4.35f, 12.65f, -1.34f), 80);
                Capture("Campus-ClassroomDetails.png", new Vector3(-9.8f, 12.45f, 4.4f), new Vector3(-2.7f, 12.2f, 6.6f), 74);
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Folder + "/CampusRuntimeValidation.txt",
                    "Unity " + Application.unityVersion + " desktop Play Mode\n"
                    + "Saved scene lighting/materials: PASS\n"
                    + "Saved wall/masonry contact and no crossing window glass: " + junctions + " PASS\n"
                    + "1K scanned PBR textures, linear detail maps, HDRI and classroom reflection probe: PASS\n"
                    + "Visible direct-daylight pixels in classroom: " + daylightPixels + " PASS\n"
                    + "Printed sign carriers, lit textures and visible noticeboard lettering pixels: " + letteringPixels + " PASS\n"
                    + "CampusSmoke/ArchitecturalSurface/WorldSpaceText shaders: PASS\n"
                    + "Drill alarm starts enabled without playing: PASS\n"
                    + "Alarm activation starts quiet spatial audio: PASS\n"
                    + "Generated desktop render captures: PASS\n"
                    + "Headset frame rate, stereo rendering, locomotion and manual M-key audio perception unverified.\n");
                Debug.Log("CAMPUS_RUNTIME_VALIDATION_SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static int CheckPrintedSignage()
        {
            MeshRenderer[] lettering = Object.FindObjectsOfType<MeshRenderer>(true)
                .Where(r => r.name == "PrintedLettering").ToArray();
            Require(lettering.Length > 140, "Printed Campus signs missing after scene reload");
            foreach (MeshRenderer ink in lettering)
            {
                Material material = ink.sharedMaterial;
                Require(material != null && material.mainTexture != null && material.shader.name == "Standard"
                    && material.IsKeywordEnabled("_ALPHABLEND_ON") && !material.IsKeywordEnabled("_EMISSION"),
                    "Printed signs must have lit, non-emissive lettering: " + ink.transform.parent.name);
                Transform carrier = ink.transform.parent.parent.Find("SignPanel");
                if (carrier == null) continue;
                foreach (Vector3 corner in new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, .5f, 0) })
                {
                    Vector3 local = carrier.InverseTransformPoint(ink.transform.TransformPoint(corner));
                    Require(Mathf.Abs(local.x) <= .5f && Mathf.Abs(local.y) <= .5f,
                        "Lettering extends outside its physical carrier: " + ink.transform.parent.name);
                    float gap = (-local.z - .5f) * carrier.localScale.z;
                    Require(gap > .0005f && gap < .004f, "Lettering floats away from its carrier");
                }
            }
            Vector3 eye = new Vector3(-4.35f, 12.45f, .85f), target = new Vector3(-4.35f, 12.65f, -1.34f);
            Color32[] printed = Capture("Campus-Noticeboard.png", eye, target, 80);
            Color32[] blank;
            try
            {
                foreach (MeshRenderer ink in lettering) ink.enabled = false;
                blank = Capture("Campus-NoticeboardWithoutLettering.png", eye, target, 80);
            }
            finally { foreach (MeshRenderer ink in lettering) ink.enabled = true; }
            int changed = printed.Where((p, i) => Mathf.Abs(p.r - blank[i].r) > 5
                || Mathf.Abs(p.g - blank[i].g) > 5 || Mathf.Abs(p.b - blank[i].b) > 5).Count();
            Require(changed > 500, "Printed lettering is not visibly rendered on the noticeboard");
            return changed;
        }

        private static int CheckScannedMaterialsAndDaylight()
        {
            const string materials = "Assets/Environment/TeachingBuildingCampus/Materials/";
            foreach (string name in new[] { "Floor", "Corridor", "Wood" })
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materials + name + ".mat");
                Require(material.mainTexture != null && material.mainTexture.width == 1024,
                    "Scanned albedo missing or downscaled: " + name);
                Texture2D detail = material.GetTexture("_DetailTex") as Texture2D;
                Require(detail != null && detail.width == 1024, "Scanned PBR detail missing: " + name);
                TextureImporter albedoImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.mainTexture)) as TextureImporter;
                TextureImporter detailImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(detail)) as TextureImporter;
                Require(albedoImporter.sRGBTexture && !detailImporter.sRGBTexture, "PBR texture color space: " + name);
                Require(material.GetFloat("_Metallic") == 0, "Wood and stone must be dielectric");
            }
            Require(RenderSettings.skybox != null && RenderSettings.skybox.shader.name == "Skybox/Panoramic"
                && RenderSettings.skybox.mainTexture != null, "Saved HDRI sky missing");
            Require(Object.FindObjectsOfType<ReflectionProbe>().Any(p => p.name == "Reflection_Classroom401"
                && p.resolution == 256 && p.bakedTexture != null), "Room reflection probe missing");
            Light sun = RenderSettings.sun;
            Require(sun != null && sun.shadows == LightShadows.Soft && sun.transform.forward.z < 0,
                "Daylight must enter the rear classroom windows");
            Vector3 eye = new Vector3(-3.8f, 12.45f, 7.3f), target = new Vector3(-10.9f, 12.5f, 5.9f);
            Color32[] daylight = Capture("Campus-Daylight.png", eye, target, 75);
            Color32[] withoutSun;
            try
            {
                sun.enabled = false;
                withoutSun = Capture("Campus-WithoutDirectSun.png", eye, target, 75);
            }
            finally { sun.enabled = true; }
            int changed = daylight.Where((p, i) => Mathf.Abs(p.r - withoutSun[i].r) > 5
                || Mathf.Abs(p.g - withoutSun[i].g) > 5 || Mathf.Abs(p.b - withoutSun[i].b) > 5).Count();
            Require(changed > 1000, "Direct daylight does not visibly illuminate the classroom");
            return changed;
        }

        private static int CheckWindowJunctions()
        {
            BoxCollider[] colliders = Object.FindObjectsOfType<BoxCollider>(true);
            BoxCollider[] piers = colliders.Where(c => c.name == "WindowPier").ToArray();
            BoxCollider[] glass = colliders.Where(c => c.name == "WindowGlass").ToArray();
            int count = 0;
            foreach (BoxCollider wall in colliders.Where(c => c.name == "Wall_Partition" && c.bounds.size.x < .4f))
                foreach (float facadeZ in new[] { -9f, 9f })
                {
                    Bounds bounds = wall.bounds;
                    float end = facadeZ < 0 ? bounds.min.z : bounds.max.z;
                    if (Mathf.Abs(end - facadeZ) > .25f) continue;
                    Vector3 contact = new Vector3(bounds.center.x, bounds.center.y, end);
                    Require(piers.Any(p =>
                    {
                        Bounds masonry = p.bounds;
                        masonry.Expand(.002f);
                        return masonry.Contains(contact) && masonry.min.x <= bounds.min.x
                            && masonry.max.x >= bounds.max.x;
                    }), "Partition does not join masonry: " + contact);
                    Require(!glass.Any(p => Mathf.Abs(p.bounds.center.z - facadeZ) < .05f
                        && p.bounds.min.y <= contact.y && p.bounds.max.y >= contact.y
                        && p.bounds.max.x > bounds.min.x && p.bounds.min.x < bounds.max.x),
                        "Window glass crosses a partition: " + contact);
                    count++;
                }
            var layout = JsonUtility.FromJson<TeachingBuildingBuilder.Layout>(File.ReadAllText("Assets/Environment/TeachingBuildingCampusLayout.json"));
            Require(count == (layout.facadeJunctionCount > 0 ? layout.facadeJunctionCount : 49),
                "Unexpected number of wall/facade junctions: " + count);
            return count;
        }

        private static Color32[] Capture(string filename, Vector3 eye, Vector3 target, float fov)
        {
            GameObject holder = new GameObject("CampusValidationCamera");
            Camera camera = holder.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            camera.fieldOfView = fov;
            camera.nearClipPlane = .06f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = false;
            RenderTexture render = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
            Texture2D image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                image.Apply();
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(Path.Combine(Folder, filename), image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                render.Release();
                Object.DestroyImmediate(render);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(holder);
            }
        }
    }
}
