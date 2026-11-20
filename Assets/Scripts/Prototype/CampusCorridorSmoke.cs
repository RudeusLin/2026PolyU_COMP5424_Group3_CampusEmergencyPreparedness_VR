using UnityEngine;
using UnityEngine.Rendering;

namespace CampusEmergency.Prototype
{
    // Doorway smoke and shallow corridor ceiling layers shared by desktop and XR.
    public sealed class CampusCorridorSmoke : MonoBehaviour
    {
        const int Tiles = 4;
        const int Cell = 64;

        private static Texture2D sheet;
        private static Material material;
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;
        [SerializeField, Range(0f, 1f)] private float emissionMultiplier = 1f;
        [SerializeField] private bool ceilingLayer;

        public Bounds SmokeBounds { get; private set; }
        public bool IsStairProfile { get; private set; }
        private Vector3 emitterBox;
        private void ConstrainProfile()
        {
            Vector3 original=transform.position;
            float floor=Mathf.Floor((original.y+.15f)/3.6f)*3.6f;
            IsStairProfile=Mathf.Abs(original.x)>12.8f;
            bool broad=ceilingLayer;
            transform.position=new Vector3(original.x,floor+2.85f,original.z);
            ceilingLayer=true;
            emitterBox=IsStairProfile?new Vector3(1.35f,.08f,1.35f):new Vector3(broad?24f:3.2f,.08f,broad?1.8f:.65f);
            Vector3 centre=transform.position;
            if(IsStairProfile){
                centre.x=original.x<0?(Mathf.Abs(original.x+14.42f)<Mathf.Abs(original.x+16.58f)?-14.42f:-16.58f):(Mathf.Abs(original.x-14.42f)<Mathf.Abs(original.x-16.58f)?14.42f:16.58f);
                transform.position=centre;
            }
            SmokeBounds=new Bounds(new Vector3(centre.x,floor+2.85f,centre.z),new Vector3(IsStairProfile?1.75f:broad?24.5f:4f,.8f,IsStairProfile?2.1f:broad?2.7f:1.4f));
            opacity*=IsStairProfile?.45f:broad?.8f:.5f;
        }
        private void Awake()
        {
            ConstrainProfile();
            if (transform.Find(ceilingLayer ? "Ceiling Haze" : "Haze") != null) return;
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
            if (ceilingLayer)
            {
                CreateLayer("Ceiling Haze", emitterBox, 0f, 0f, IsStairProfile?.8f:1.7f, IsStairProfile?1.3f:2.4f, 4f, 6f, IsStairProfile?7f:12f, IsStairProfile?44:80,
                    billboardHeight: new Vector2(.26f,.42f));
            }
            else
            {
                CreateLayer("Haze", new Vector3(3.6f, 0.35f, 0.9f), 0.15f, 0.32f, 1.9f, 2.6f, 6.5f, 9f, 10f, 30);
                CreateLayer("Wisps", new Vector3(3.2f, 0.55f, 0.7f), 0.4f, 0.75f, 0.85f, 1.4f, 3.2f, 5f, 13f, 32);
            }
        }

        private void CreateLayer(string name, Vector3 box, float minRise, float maxRise,
            float minSize, float maxSize, float minLife, float maxLife, float rate, int maxParticles,
            Vector2 billboardHeight = default, float heightOffset = 0f, float opacityScale = 1f)
        {
            GameObject emitter = new GameObject(name);
            emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = Vector3.up * heightOffset;
            ParticleSystem particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = ceilingLayer ? 0f : 0.04f;
            var width = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            if (ceilingLayer)
            {
                // Give the layer a visible face from the corridor, while keeping it shallow.
                main.startSize3D = true;
                main.startSizeX = width;
                main.startSizeY = new ParticleSystem.MinMaxCurve(billboardHeight.x, billboardHeight.y);
            }
            else
            {
                main.startSize = width;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            }
            main.startColor = new Color(1f, 1f, 1f, opacityScale);
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.rateOverTime = rate * emissionMultiplier;
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            velocity.y = new ParticleSystem.MinMaxCurve(minRise, maxRise);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.015f, 0.015f);
            var noise = particles.noise;
            // No vertical turbulence at the ceiling: keep the entire layer above eye level.
            noise.enabled = !ceilingLayer;
            noise.strength = 0.28f;
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.22f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var size = particles.sizeOverLifetime;
            size.enabled = !ceilingLayer;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.55f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.45f)));
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = !ceilingLayer;
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
            var properties=new MaterialPropertyBlock();properties.SetFloat("_ClipBoundsEnabled",1);
            properties.SetVector("_ClipMin",SmokeBounds.min);properties.SetVector("_ClipMax",SmokeBounds.max);properties.SetFloat("_ClipFade",.13f);renderer.SetPropertyBlock(properties);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 2f;
            renderer.sortingFudge = 1f;
            particles.Play();
        }

        private Gradient SmokeGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(ceilingLayer ? new Color(0.53f, 0.55f, 0.57f) : new Color(0.16f, 0.17f, 0.18f), 0f),
                    new GradientColorKey(ceilingLayer ? new Color(0.65f, 0.67f, 0.69f) : new Color(0.34f, 0.35f, 0.36f), 0.45f),
                    new GradientColorKey(ceilingLayer ? new Color(0.73f, 0.74f, 0.75f) : new Color(0.55f, 0.56f, 0.57f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.75f * opacity, 0.16f),
                    new GradientAlphaKey(0.42f * opacity, 0.7f),
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
