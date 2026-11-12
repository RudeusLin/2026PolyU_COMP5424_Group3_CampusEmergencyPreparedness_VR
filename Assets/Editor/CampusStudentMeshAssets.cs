using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CampusEmergency.Editor
{
    internal static class CampusStudentMeshAssets
    {
        [Serializable] private sealed class Source
        {
            public float[] vertices, normals, uv;
            public int[] triangles;
        }

        internal static Mesh Load(string sourcePath, string meshFolder, Dictionary<string, Mesh> cache)
        {
            string name = "StudentSurface_" + Path.GetFileNameWithoutExtension(sourcePath);
            if (cache.TryGetValue(name, out Mesh cached)) return cached;
            Source source = JsonUtility.FromJson<Source>(File.ReadAllText(sourcePath));
            if (source == null || source.vertices == null || source.normals == null || source.triangles == null
                || source.vertices.Length == 0 || source.vertices.Length % 3 != 0
                || source.normals.Length != source.vertices.Length || source.triangles.Length % 3 != 0)
                throw new InvalidOperationException("Invalid student mesh: " + sourcePath);
            int count = source.vertices.Length / 3;
            foreach (int index in source.triangles)
                if (index < 0 || index >= count) throw new InvalidOperationException("Invalid student mesh index: " + sourcePath);
            Vector3[] vertices = new Vector3[count], normals = new Vector3[count];
            Vector2[] uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(source.vertices[i*3], source.vertices[i*3+1], source.vertices[i*3+2]);
                normals[i] = new Vector3(source.normals[i*3], source.normals[i*3+1], source.normals[i*3+2]);
                uv[i] = source.uv != null && source.uv.Length == count * 2
                    ? new Vector2(source.uv[i*2], source.uv[i*2+1])
                    : new Vector2(Mathf.Atan2(vertices[i].x, vertices[i].z) / (2*Mathf.PI) + .5f, vertices[i].y);
            }
            string path = meshFolder + "/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = name };
            else mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = source.triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            cache.Add(name, mesh);
            return mesh;
        }
    }
}
