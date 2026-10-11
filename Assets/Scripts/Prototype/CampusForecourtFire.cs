using UnityEngine;
using UnityEngine.Rendering;

namespace CampusEmergency.Prototype
{
    // Play-mode preview on the south forecourt. It does not change routes or drill rules.
    public sealed class CampusForecourtFire : MonoBehaviour
    {
        // Beside the central walkway, on the paving in front of the south facade.
        public static readonly Vector3 PlazaPosition = new Vector3(3.6f, 0.06f, -16.6f);

        private static Texture2D flameSprite;
        private static Texture2D puffSprite;
        private static Material fireMaterial;
        private static Material smokeMaterial;
        private Light glow;
        private float flickerSeed;

        public static void Ensure(Transform buildingRoot)
        {
            if (!Application.isPlaying || buildingRoot == null) return;
            Transform outdoor = buildingRoot.Find("Outdoor");
            if (outdoor == null || outdoor.Find("ForecourtFire") != null) return;
            GameObject item = new GameObject("ForecourtFire");
            item.transform.SetParent(outdoor, false);
            item.transform.localPosition = PlazaPosition;
            item.AddComponent<CampusForecourtFire>();
        }

        private void Awake()
        {
            if (transform.Find("Flames") != null) return;
            flickerSeed = Random.value * 10f;
            Shader fireShader = Resources.Load<Shader>("CampusFire");
            Shader smokeShader = Resources.Load<Shader>("CampusSmoke");
            if (fireShader == null || smokeShader == null)
            {
                Debug.LogError("Missing CampusFire or CampusSmoke shader.", this);
                return;
            }
            if (flameSprite == null) flameSprite = MakeFlameSprite();
            if (puffSprite == null) puffSprite = MakePuffSprite();
            if (fireMaterial == null) fireMaterial = MakeMaterial(fireShader, flameSprite);
            if (smokeMaterial == null) smokeMaterial = MakeMaterial(smokeShader, puffSprite);

            CreateFlames("Flames", 0.34f, 0.85f, 1.6f, 2.8f, 0.7f, 1.15f, 0.55f, 0.9f, 22f, OuterFlame());
            CreateFlames("FlameCore", 0.16f, 0.35f, 2.2f, 3.6f, 0.32f, 0.55f, 0.28f, 0.5f, 16f, InnerFlame());
            CreateEmbers();
            CreateBaseGlow();
            CreateSmoke();
            CreateLight();
        }

        private void Update()
        {
            if (glow == null) return;
            float wave = Mathf.PerlinNoise(flickerSeed, Time.time * 3.4f);
            glow.intensity = Mathf.Lerp(1.7f, 3.3f, wave);
            glow.range = Mathf.Lerp(5.4f, 7.1f, wave);
        }

        private void CreateFlames(string name, float radius, float spread, float minRise, float maxRise,
            float minSize, float maxSize, float minLife, float maxLife, float rate, Gradient gradient)
        {
            ParticleSystem particles = CreateSystem(name, Vector3.zero);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            main.startColor = Color.white;
            main.maxParticles = 64;
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            Lift(particles, spread, minRise, maxRise);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.85f;
            noise.frequency = 0.45f;
            noise.scrollSpeed = 1.1f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.08f)));
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Finish(particles, renderer, fireMaterial, 1f);
        }

        private void CreateEmbers()
        {
            ParticleSystem particles = CreateSystem("Embers", new Vector3(0f, 0.15f, 0f));
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.35f);
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = new Color(1f, 0.62f, 0.18f, 1f);
            main.gravityModifier = 0.85f;
            main.maxParticles = 36;
            var emission = particles.emission;
            emission.rateOverTime = 12f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            Lift(particles, 0.7f, 2.4f, 4.2f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.8f, 0.35f), 0f), new GradientColorKey(new Color(0.55f, 0.08f, 0.01f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Finish(particles, renderer, fireMaterial, 2f);
        }

        private void CreateBaseGlow()
        {
            ParticleSystem particles = CreateSystem("BaseGlow", new Vector3(0f, 0.02f, 0f));
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.35f, 2.05f);
            main.startColor = new Color(1f, 0.38f, 0.06f, 0.42f);
            main.maxParticles = 6;
            var emission = particles.emission;
            emission.rateOverTime = 8f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            Finish(particles, renderer, fireMaterial, 0f);
        }

        private void CreateSmoke()
        {
            ParticleSystem particles = CreateSystem("Smoke", new Vector3(0f, 1.65f, 0f));
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.2f);
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.05f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.maxParticles = 24;
            var emission = particles.emission;
            emission.rateOverTime = 5f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;
            Lift(particles, 0.45f, 0.85f, 1.45f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.55f;
            noise.frequency = 0.22f;
            noise.scrollSpeed = 0.35f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(1f, 2.3f)));
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient soot = new Gradient();
            soot.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.07f, 0.07f, 0.08f), 0f),
                    new GradientColorKey(new Color(0.16f, 0.16f, 0.17f), 0.35f),
                    new GradientColorKey(new Color(0.34f, 0.34f, 0.36f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.5f, 0.12f),
                    new GradientAlphaKey(0.22f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = soot;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Finish(particles, renderer, smokeMaterial, -1f);
        }

        private void CreateLight()
        {
            GameObject holder = new GameObject("FireLight");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            glow = holder.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.45f, 0.1f);
            glow.intensity = 2.4f;
            glow.range = 6.2f;
            glow.shadows = LightShadows.None;
        }

        private ParticleSystem CreateSystem(string name, Vector3 localPosition)
        {
            GameObject emitter = new GameObject(name);
            emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = localPosition;
            ParticleSystem particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = 48;
            return particles;
        }

        private static void Finish(ParticleSystem particles, ParticleSystemRenderer renderer, Material material, float sortingFudge)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 1.4f;
            renderer.sortingFudge = sortingFudge;
            particles.Play();
        }

        private static void Lift(ParticleSystem particles, float spread, float minRise, float maxRise)
        {
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-spread, spread);
            velocity.y = new ParticleSystem.MinMaxCurve(minRise, maxRise);
            velocity.z = new ParticleSystem.MinMaxCurve(-spread, spread);
        }

        private static Material MakeMaterial(Shader shader, Texture2D texture)
        {
            Material material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = texture };
            return material;
        }

        private static Gradient OuterFlame()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.92f, 0.55f), 0f),
                    new GradientColorKey(new Color(1f, 0.42f, 0.05f), 0.38f),
                    new GradientColorKey(new Color(0.55f, 0.06f, 0.01f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.08f),
                    new GradientAlphaKey(0.75f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Gradient InnerFlame()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.97f, 0.82f), 0f),
                    new GradientColorKey(new Color(1f, 0.72f, 0.18f), 0.45f),
                    new GradientColorKey(new Color(1f, 0.28f, 0.02f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.06f),
                    new GradientAlphaKey(0.5f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Texture2D MakeFlameSprite()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ForecourtFlame",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float height = y / (size - 1f);
                float halfWidth = Mathf.Lerp(0.42f, 0.07f, Mathf.Pow(height, 0.72f));
                for (int x = 0; x < size; x++)
                {
                    float across = (x / (size - 1f) - 0.5f) / halfWidth;
                    float body = Mathf.Clamp01(1f - across * across);
                    body *= Mathf.SmoothStep(1f, 0.78f, height);
                    float alpha = body * body;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D MakePuffSprite()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ForecourtSmokePuff",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y / (size - 1f) - 0.5f) * 2f;
                for (int x = 0; x < size; x++)
                {
                    float u = (x / (size - 1f) - 0.5f) * 2f;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    float alpha = falloff * falloff;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
