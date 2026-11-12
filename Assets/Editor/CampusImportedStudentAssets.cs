using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CampusEmergency.Editor
{
    internal static class CampusImportedStudentAssets
    {
        internal const string Folder = "Assets/ThirdParty/Rocketbox/MaleAdult08";
        private const string MeshFolder = "Assets/Environment/TeachingBuildingCampus/Meshes";
        private const string MaterialFolder = "Assets/Environment/TeachingBuildingCampus/Materials";

        internal static GameObject Create(string modelPath, Transform parent, string name)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) throw new InvalidOperationException("Missing imported student FBX: " + modelPath);
            Directory.CreateDirectory(MeshFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            try
            {
                GameObject model = Object.Instantiate(source, holder.transform, false);
                model.name = "RocketboxStudent";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length != 1 || renderers[0].sharedMesh.subMeshCount != 3)
                    throw new InvalidOperationException("Expected the pinned Rocketbox character with three material sections.");
                Material[] materials = { Material("Body", "body"), Material("Head", "head"), Material("Hair", "opacity") };
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    renderer.sharedMaterials = materials;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.lightmapIndex = -1;
                    renderer.updateWhenOffscreen = true;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                }
                Bounds bounds = PosedBounds(renderers);
                model.transform.localScale *= 1.74f / bounds.size.y;
                bounds = PosedBounds(renderers);
                model.transform.localPosition -= Vector3.up * holder.transform.InverseTransformPoint(bounds.min).y;
                RelaxShoulder(holder.transform, model.transform, "L", -1);
                RelaxShoulder(holder.transform, model.transform, "R", 1);
                Transform rightHand = Bone(model.transform, "Bip01 R Hand");
                Transform hold = new GameObject("HandHoldPoint").transform;
                hold.SetParent(rightHand, false);
                hold.position = rightHand.position + holder.transform.forward * .035f - holder.transform.up * .03f;
                hold.rotation = holder.transform.rotation * Quaternion.Euler(90, 0, 0);
                Transform sources = new GameObject("FirstPersonSources").transform;
                sources.SetParent(holder.transform, false);
                Transform leftView = ForearmSource(holder.transform, model.transform, renderers[0], sources, "L");
                Transform rightView = ForearmSource(holder.transform, model.transform, renderers[0], sources, "R");
                sources.gameObject.SetActive(false);
                CampusStudentAvatar avatar = holder.AddComponent<CampusStudentAvatar>();
                avatar.ConfigureImportedRig(model.transform, leftView, rightView, hold);
                if (holder.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("Imported student visuals must not add movement colliders.");
                return holder;
            }
            catch
            {
                Object.DestroyImmediate(holder);
                throw;
            }
        }

        private static Transform Bone(Transform model, string name) =>
            model.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);

        private static void RelaxShoulder(Transform root, Transform model, string side, int sign)
        {
            Transform shoulder = Bone(model, "Bip01 " + side + " UpperArm");
            Transform elbow = Bone(model, "Bip01 " + side + " Forearm");
            Vector3 target = root.TransformDirection(new Vector3(sign * .09f, -1, .07f));
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position, target) * shoulder.rotation;
        }

        private static Bounds PosedBounds(SkinnedMeshRenderer[] renderers)
        {
            bool first = true;
            Bounds bounds = default;
            foreach (var renderer in renderers)
            {
                SkinWorldGeometry(renderer, out Vector3[] vertices, out _);
                foreach (Vector3 point in vertices)
                {
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            if (first || bounds.size.y < .01f) throw new InvalidOperationException("Empty imported student mesh.");
            return bounds;
        }

        internal static void SkinWorldGeometry(SkinnedMeshRenderer renderer, out Vector3[] vertices, out Vector3[] normals)
        {
            Mesh mesh = renderer.sharedMesh;
            vertices = mesh.vertices;
            normals = mesh.normals;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bindposes = mesh.bindposes;
            Transform[] bones = renderer.bones;
            var matrices = new Matrix4x4[bones.Length];
            var normalMatrices = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                matrices[i] = bones[i].localToWorldMatrix * bindposes[i];
                normalMatrices[i] = matrices[i].inverse.transpose;
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 source = vertices[i], normal = normals[i], point = Vector3.zero, direction = Vector3.zero;
                void Add(int bone, float weight)
                {
                    if (weight <= 0) return;
                    point += matrices[bone].MultiplyPoint3x4(source) * weight;
                    direction += normalMatrices[bone].MultiplyVector(normal) * weight;
                }
                BoneWeight w = weights[i];
                Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1);
                Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                vertices[i] = point;
                normals[i] = direction.normalized;
            }
        }

        private static Material Material(string name, string texture)
        {
            string path = MaterialFolder + "/StudentImported" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(Shader.Find("Standard"));
            material.shader = Shader.Find("Standard");
            material.name = "StudentImported" + name;
            material.color = Color.white;
            material.mainTexture = Texture(texture + "_color");
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Glossiness", name == "Hair" ? .07f : .23f);
            if (name != "Hair")
            {
                material.SetTexture("_BumpMap", Texture(texture + "_normal"));
                material.SetFloat("_BumpScale", .65f);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetFloat("_Mode", 1);
                material.SetFloat("_Cutoff", .35f);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Texture(string name)
        {
            string path = Folder + "/m014_" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureShape != TextureImporterShape.Texture2D)
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.SaveAndReimport();
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Missing Rocketbox texture: " + path);
            return texture;
        }

        private static Transform ForearmSource(Transform root, Transform model, SkinnedMeshRenderer renderer,
            Transform parent, string side)
        {
            Transform elbow = Bone(model, "Bip01 " + side + " Forearm");
            Transform hand = Bone(model, "Bip01 " + side + " Hand");
            var armBones = new HashSet<Transform>(elbow.GetComponentsInChildren<Transform>(true));
            BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            Transform[] bones = renderer.bones;
            float Weight(int index)
            {
                BoneWeight w = weights[index];
                return (armBones.Contains(bones[w.boneIndex0]) ? w.weight0 : 0)
                    + (armBones.Contains(bones[w.boneIndex1]) ? w.weight1 : 0)
                    + (armBones.Contains(bones[w.boneIndex2]) ? w.weight2 : 0)
                    + (armBones.Contains(bones[w.boneIndex3]) ? w.weight3 : 0);
            }
            Mesh mesh = renderer.sharedMesh;
            SkinWorldGeometry(renderer, out Vector3[] vertices, out Vector3[] normals);
            Vector3 up = (elbow.position - hand.position).normalized;
            Quaternion frame = Quaternion.LookRotation(Vector3.ProjectOnPlane(root.forward, up).normalized, up);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Quaternion.Inverse(frame) * (vertices[i] - elbow.position);
                normals[i] = (Quaternion.Inverse(frame) * normals[i]).normalized;
            }
            Vector2[] uv = mesh.uv;
            var remap = new Dictionary<int, int>();
            var armVertices = new List<Vector3>();
            var armNormals = new List<Vector3>();
            var armUV = new List<Vector2>();
            // Forearms belong to the pinned model's body material section.
            int[] triangles = mesh.GetTriangles(0);
            var selected = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
                if (Weight(triangles[i]) + Weight(triangles[i+1]) + Weight(triangles[i+2]) >= 1.5f)
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int original = triangles[i + corner];
                        if (!remap.TryGetValue(original, out int index))
                        {
                            index = armVertices.Count;
                            remap.Add(original, index);
                            armVertices.Add(vertices[original]);
                            armNormals.Add(normals[original]);
                            armUV.Add(uv[original]);
                        }
                        selected.Add(index);
                    }
            if (selected.Count == 0) throw new InvalidOperationException("No imported forearm triangles: " + side);
            var part = new Mesh { name = "RocketboxFirstPerson" + side, vertices = armVertices.ToArray(),
                normals = armNormals.ToArray(), uv = armUV.ToArray(), triangles = selected.ToArray() };
            part.RecalculateBounds();
            part.RecalculateTangents();
            string path = MeshFolder + "/" + part.name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(part, path);
            else
            {
                // Reassign geometry so Unity refreshes the mesh's native render buffers as well as serialized data.
                existing.Clear();
                existing.vertices = part.vertices;
                existing.normals = part.normals;
                existing.uv = part.uv;
                existing.triangles = part.triangles;
                existing.tangents = part.tangents;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(part);
                part = existing;
            }
            var item = new GameObject(side == "L" ? "LeftForearm" : "RightForearm");
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = part;
            MeshRenderer surface = item.AddComponent<MeshRenderer>();
            surface.sharedMaterial = renderer.sharedMaterials[0];
            surface.receiveGI = ReceiveGI.LightProbes;
            surface.shadowCastingMode = ShadowCastingMode.Off;
            Transform wrist = new GameObject("Wrist").transform;
            wrist.SetParent(item.transform, false);
            wrist.localPosition = new Vector3(0, -.225f, .018f);
            Transform hold = new GameObject("HandHoldPoint").transform;
            hold.SetParent(wrist, false);
            hold.localPosition = new Vector3(0, -.065f, .037f);
            hold.localRotation = Quaternion.Euler(90, 0, 0);
            return item.transform;
        }
    }

    internal sealed class CampusRocketboxImportSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CampusImportedStudentAssets.Folder + "/", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.isReadable = true;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CampusImportedStudentAssets.Folder + "/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal.png", StringComparison.Ordinal);
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !assetPath.EndsWith("_specular.png", StringComparison.Ordinal);
            importer.alphaIsTransparency = assetPath.EndsWith("_opacity_color.png", StringComparison.Ordinal);
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
        }
    }
}
