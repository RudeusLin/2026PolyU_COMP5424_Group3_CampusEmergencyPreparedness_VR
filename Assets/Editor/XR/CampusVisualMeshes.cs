using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace CampusEmergency.Editor
{
    /// <summary>Small shared meshes for the XR architectural finishes; all dimensions are in metres.</summary>
    internal sealed class CampusVisualMeshes
    {
        internal const string Folder = CampusXRVisualUpgrade.AssetFolder + "/Meshes";
        private readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        internal Mesh Box(Vector3 size, float radius = .008f) =>
            TeachingBuildingBuilder.BeveledBox(size, radius, Folder, cache);

        internal Mesh Get(string name, Func<Mesh> build)
        {
            if (cache.TryGetValue(name, out var mesh)) return mesh;
            string path = Folder + "/" + name + ".asset";
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var generated = build();
            generated.name = name;
            generated.RecalculateBounds();
            generated.RecalculateTangents();
            if (mesh == null)
            {
                mesh = generated;
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.CopySerialized(generated, mesh);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(mesh);
            }
            cache[name] = mesh;
            return mesh;
        }

        internal Mesh Chair(bool back) => Get(back ? "MouldedChairBack" : "ContouredSeat", () =>
        {
            const int columns = 12, rows = 10;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            int faceSize = (columns + 1) * (rows + 1);
            foreach (int side in new[] { 1, -1 })
            {
                int start = vertices.Count;
                for (int row = 0; row <= rows; row++)
                    for (int column = 0; column <= columns; column++)
                    {
                        float u = column / (float)columns * 2 - 1, v = row / (float)rows;
                        Vector3 p;
                        if (back)
                        {
                            float width = .48f - .07f * Mathf.Pow(v, 4);
                            float y = v - .5f - .07f * Mathf.Pow(Mathf.Abs(u), 6) * Mathf.Pow(v, 8);
                            float curve = .5f * (u*u - .5f) + .07f * Mathf.Sin(v * Mathf.PI);
                            p = new Vector3(u * width, y, curve + side * .19f);
                        }
                        else
                        {
                            float width = .48f - .035f * (1-v);
                            float y = .16f * (u*u - .5f) + .06f * Mathf.Cos(v * Mathf.PI) + side * .28f;
                            p = new Vector3(u * width, y, v - .5f);
                        }
                        vertices.Add(p);
                        uvs.Add(new Vector2((u+1)*.5f,v));
                    }
                for (int row = 0; row < rows; row++)
                    for (int column = 0; column < columns; column++)
                    {
                        int a = start + row * (columns+1) + column, b = a + columns+1;
                        bool reverse = back ? side < 0 : side > 0;
                        if (reverse) triangles.AddRange(new[] { a,b,a+1,a+1,b,b+1 });
                        else triangles.AddRange(new[] { a,a+1,b,a+1,b+1,b });
                    }
            }
            Action<int,int> edge = (a,b) =>
            {
                if (!back) { int swap=a; a=b; b=swap; }
                triangles.AddRange(new[] { a,a+faceSize,b,b,a+faceSize,b+faceSize });
            };
            for (int column=0; column<columns; column++)
            {
                edge(column,column+1);
                edge(rows*(columns+1)+column+1,rows*(columns+1)+column);
            }
            for (int row=0; row<rows; row++)
            {
                edge((row+1)*(columns+1),row*(columns+1));
                edge(row*(columns+1)+columns,(row+1)*(columns+1)+columns);
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0,uvs);
            mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();
            return mesh;
        });

        internal Mesh Lathe(string name, Vector2[] profile, bool roundedRectangle = false) => Get(name, () =>
        {
            const int segments = 40;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row < profile.Length; row++)
                for (int column = 0; column <= segments; column++)
                {
                    float angle = column * Mathf.PI * 2 / segments;
                    float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                    if (roundedRectangle)
                    {
                        x = Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), .48f);
                        z = Mathf.Sign(z) * Mathf.Pow(Mathf.Abs(z), .48f);
                    }
                    vertices.Add(new Vector3(x * profile[row].x, profile[row].y, z * profile[row].x));
                    uvs.Add(new Vector2(column / (float)segments, row / (float)(profile.Length - 1)));
                }
            for (int row = 0; row < profile.Length - 1; row++)
                for (int column = 0; column < segments; column++)
                {
                    int a = row * (segments + 1) + column, b = a + segments + 1;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        });

        internal Mesh Tube(string name, Vector3[] path, float radius, int sides = 12) => Get(name, () =>
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            for (int row = 0; row < path.Length; row++)
            {
                Vector3 direction = (path[Mathf.Min(row + 1, path.Length - 1)] - path[Mathf.Max(0, row - 1)]).normalized;
                Vector3 right = Vector3.Cross(direction, Vector3.right).normalized;
                if (right.sqrMagnitude < .1f) right = Vector3.Cross(direction, Vector3.forward).normalized;
                Vector3 up = Vector3.Cross(direction, right).normalized;
                for (int column = 0; column <= sides; column++)
                {
                    float angle = column * Mathf.PI * 2 / sides;
                    vertices.Add(path[row] + radius * (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)));
                    uvs.Add(new Vector2(column / (float)sides, row / (float)(path.Length - 1)));
                }
            }
            for (int row = 0; row < path.Length - 1; row++)
                for (int column = 0; column < sides; column++)
                {
                    int a = row * (sides + 1) + column, b = a + sides + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        });

        internal Mesh Fracture(string name, int seed) => Get(name, () =>
        {
            var mesh = UnityEngine.Object.Instantiate(Box(Vector3.one, .05f));
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                float noise = Mathf.Sin(p.x * 23.7f + p.z * 17.1f + seed) * .10f
                    + Mathf.Sin(p.z * 41.3f + p.y * 13.1f + seed) * .06f;
                p.y *= .72f + noise;
                p.z += noise * .6f;
                vertices[i] = p;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            return mesh;
        });
    }

    /// <summary>Combines added noninteractive details by material, keeping draw calls bounded.</summary>
    internal sealed class CampusFinishBatch
    {
        private readonly Transform root;
        private readonly string key;
        private readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();

        internal CampusFinishBatch(Transform parent, string name)
        {
            root = CampusXRVisualUpgrade.Child(parent, name);
            var ancestors = new List<string>();
            for (Transform item = parent; item != null; item = item.parent)
            {
                ancestors.Insert(0, item.name);
                if (item.name.StartsWith("Floor_") || item.name.StartsWith("Stairwell_")) break;
            }
            string prefix = parent.gameObject.scene.path == CampusXRSceneBuilder.SourceScene ? "Desktop_" : "";
            key = prefix + string.Join("_", ancestors) + string.Format(CultureInfo.InvariantCulture,
                "_{0:F2}_{1:F2}_{2}", parent.localPosition.x, parent.localPosition.z, name).Replace('.', '_');
        }

        internal void Add(Mesh mesh, Material material, Vector3 position, Vector3 size, Quaternion rotation = default)
        {
            if (!parts.TryGetValue(material, out var list)) parts[material] = list = new List<CombineInstance>();
            if (rotation.Equals(default(Quaternion))) rotation = Quaternion.identity;
            list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, size) });
        }

        internal void Save()
        {
            foreach (var entry in parts)
            {
                string name = key + "_" + entry.Key.name;
                string path = CampusVisualMeshes.Folder + "/" + name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    mesh = new Mesh { name = name };
                    AssetDatabase.CreateAsset(mesh, path);
                }
                mesh.Clear();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(entry.Value.ToArray(), true, true);
                EditorUtility.SetDirty(mesh);
                CampusXRVisualUpgrade.RenderPart(root, entry.Key.name, mesh, entry.Key, Vector3.zero, Vector3.one);
            }
        }
    }
}
