using System;
using CampusEmergency.Prototype;
using UnityEditor;
using UnityEngine;

namespace CampusEmergency.Editor
{
    public static class CampusAtmosphereAssets
    {
        public static void Configure(GameObject item, string folder)
        {
            string path = folder + "/Materials/SmokeParticle.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>("CampusSmoke");
                if (shader == null) throw new InvalidOperationException("Missing CampusSmoke shader.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Textures/SmokeSprite.png");
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "/Audio/DrillAlarm.wav");
            if (material.mainTexture == null || clip == null)
                throw new InvalidOperationException("Missing campus atmosphere texture or audio.");
            EditorUtility.SetDirty(material);
            item.AddComponent<CampusDrillAtmosphere>().Configure(material, clip);
        }
    }
}
