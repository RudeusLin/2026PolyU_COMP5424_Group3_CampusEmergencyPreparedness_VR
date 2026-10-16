using UnityEngine;
using UnityEngine.Rendering;

namespace CampusEmergency.Prototype
{
    // Soft animated smoke in the fourth-floor corridor outside classroom 401.
    public sealed class CampusCorridorSmoke : MonoBehaviour
    {
        const int Tiles = 4;
        const int Cell = 64;

        private static Texture2D sheet;
        private static Material material;

        private void Awake()
        {
            if (transform.Find("Haze") != null) return;
            Shader shader = Resources.Load<Shader>("CampusSmoke");
            if (shader == null)
            {
                Debug.LogError("Missing CampusSmoke shader.", this);
                return;
            }
            if (sheet == null) sheet = MakeSheet();
            if (material == null)
            {
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = sheet };
            }
            CreateLayer("Haze", new Vector3(3.6f, 0.35f, 0.9f), 0.15f, 0.32f, 1.9f, 2.6f, 6.5f, 9f, 10f, 30);
            CreateLayer("Wisps", new Vector3(3.2f, 0.55f, 0.7f), 0.4f, 0.75f, 0.85f, 1.4f, 3.2f, 5f, 13f, 32);
        }

        private void CreateLayer(string name, Vector3 box, float minRise, float maxRise,
            float minSize, float maxSize, float minLife, float maxLife, float rate, int maxParticles)
        {
            GameObject emitter = new GameObject(name);
            emitter.transform.SetParent(transform, false);
            ParticleSystem particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = 0.04f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(minRise, maxRise);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.22f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.55f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.45f)));
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = SmokeGradient();
            var sheetAnimation = particles.textureSheetAnimation;
            sheetAnimation.enabled = true;
            sheetAnimation.mode = ParticleSystemAnimationMode.Grid;
            sheetAnimation.numTilesX = Tiles;
            sheetAnimation.numTilesY = Tiles;
            sheetAnimation.animation = ParticleSystemAnimationType.WholeSheet;
            sheetAnimation.timeMode = ParticleSystemAnimationTimeMode.FPS;
            sheetAnimation.fps = 8f;
            sheetAnimation.cycleCount = 8;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 2f;
            renderer.sortingFudge = 1f;
            particles.Play();
        }

        private static Gradient SmokeGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.16f, 0.17f, 0.18f), 0f),
                    new GradientColorKey(new Color(0.34f, 0.35f, 0.36f), 0.45f),
                    new GradientColorKey(new Color(0.55f, 0.56f, 0.57f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.75f, 0.16f),
                    new GradientAlphaKey(0.42f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Texture2D MakeSheet()
        {
            int size = Tiles * Cell;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CorridorSmokeSheet",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int frame = 0; frame < Tiles * Tiles; frame++)
            {
                int column = frame % Tiles;
                int rowFromTop = frame / Tiles;
                int originX = column * Cell;
                int originY = (Tiles - 1 - rowFromTop) * Cell;
                float shift = frame * 0.17f;
                for (int y = 0; y < Cell; y++)
                {
                    float v = y / (Cell - 1f);
                    for (int x = 0; x < Cell; x++)
                    {
                        float u = x / (Cell - 1f);
                        float dx = u - 0.5f;
                        float dy = v - 0.48f;
                        float radius = Mathf.Sqrt(dx * dx * 1.15f + dy * dy);
                        float edge = Mathf.Clamp01(1f - Mathf.InverseLerp(0.18f, 0.48f, radius));
                        float n = Mathf.PerlinNoise(u * 3.2f + shift, v * 3.2f + 2.1f);
                        float n2 = Mathf.PerlinNoise(u * 6.4f - shift, v * 6.4f + 4.7f);
                        float body = Mathf.Clamp01(n * 0.78f + n2 * 0.4f);
                        byte alpha = (byte)(Mathf.Clamp01(edge * body) * 220f);
                        byte gray = (byte)Mathf.Lerp(145f, 195f, n2);
                        pixels[(originY + y) * size + originX + x] = new Color32(gray, gray, gray, alpha);
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
