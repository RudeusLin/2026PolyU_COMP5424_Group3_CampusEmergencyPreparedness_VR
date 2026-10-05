using UnityEngine;

namespace CampusEmergency.Prototype
{
    // Optional H-key atmosphere preview. It does not change evacuation rules or routes.
    public sealed class CampusDrillAtmosphere : MonoBehaviour
    {
        [SerializeField] private Material smokeMaterial;
        [SerializeField] private AudioClip alarmClip;
        private ParticleSystem smoke;
        private AudioSource alarm;
        private GUIStyle captionStyle;

        public void Configure(Material material, AudioClip clip)
        {
            smokeMaterial = material;
            alarmClip = clip;
        }

        private void OnEnable()
        {
            if (smokeMaterial == null) return;
            if (smoke == null)
            {
                // The browser keeps the static smoke proxy; Unity uses soft particles at runtime.
                foreach (Transform child in transform)
                    if (child.name == "SmokeVolume") child.gameObject.SetActive(false);
                GameObject emitter = new GameObject("LocalizedDrillSmoke");
                emitter.transform.SetParent(transform, false);
                emitter.transform.localPosition = new Vector3(7.8f, 12.95f, 1.05f);
                smoke = emitter.AddComponent<ParticleSystem>();
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = smoke.main;
                main.playOnAwake = false;
                main.loop = true;
                main.maxParticles = 48;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4f);
                main.startSpeed = .1f;
                main.startSize = new ParticleSystem.MinMaxCurve(.5f, .8f);
                main.startColor = new Color(.36f, .39f, .41f, .34f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var emission = smoke.emission;
                emission.rateOverTime = 8f;
                var shape = smoke.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = .2f;
                var velocity = smoke.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.y = .12f;
                var size = smoke.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .65f, 1f, 1.4f));
                var color = smoke.colorOverLifetime;
                color.enabled = true;
                Gradient fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(0f, 1f) });
                color.color = fade;
                emitter.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
                alarm = emitter.AddComponent<AudioSource>();
                alarm.clip = alarmClip;
                alarm.playOnAwake = false;
                alarm.loop = true;
                alarm.volume = .12f;
                alarm.spatialBlend = 1f;
                alarm.minDistance = 2f;
                alarm.maxDistance = 18f;
                alarm.rolloffMode = AudioRolloffMode.Linear;
            }
            smoke.Play();
            alarm.Play();
        }

        private void OnDisable()
        {
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (alarm != null) alarm.Stop();
        }

        private void Update()
        {
            if (alarm != null && Input.GetKeyDown(KeyCode.M)) alarm.mute = !alarm.mute;
        }

        private void OnGUI()
        {
            if (captionStyle == null)
            {
                captionStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
                captionStyle.normal.textColor = Color.white;
            }
            GUI.Box(new Rect(22, Screen.height - 76, 555, 30),
                "  DRILL PREVIEW / SIMULATED ALARM / H OFF / M " +
                (alarm != null && alarm.mute ? "UNMUTE" : "MUTE"), captionStyle);
        }
    }
}
