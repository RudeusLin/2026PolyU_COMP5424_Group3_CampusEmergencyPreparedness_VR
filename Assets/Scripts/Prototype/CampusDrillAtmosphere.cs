using UnityEngine;
using CampusEmergency.Player;

namespace CampusEmergency.Prototype
{
    // Persistent drill smoke. The manual alarm starts the audio separately.
    public sealed class CampusDrillAtmosphere : MonoBehaviour
    {
        [SerializeField] private Material smokeMaterial;
        [SerializeField] private AudioClip alarmClip;
        private ParticleSystem smoke;
        private AudioSource alarm;
        private GUIStyle captionStyle;
        public bool IsAlarmActive => gameObject.activeInHierarchy && alarm != null && alarm.isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePersistentSmoke()
        {
            foreach (CampusDrillAtmosphere atmosphere in FindObjectsOfType<CampusDrillAtmosphere>(true))
                if (!atmosphere.gameObject.activeSelf) atmosphere.gameObject.SetActive(true);
        }

        public void ActivateAlarm()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            // Re-activate an already visible drill object whose effects were stopped.
            if (smoke == null || alarm == null) OnEnable();
            else
            {
                if (!smoke.isPlaying) smoke.Play();
                if (!alarm.isPlaying) alarm.Play();
            }
            if (alarm != null && !alarm.isPlaying) alarm.Play();
        }

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
            // Smoke is always present; the alarm button starts the audio.
            alarm.Stop();
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
            captionStyle.font = LanguageSelectionUI.CurrentFont;
            GUI.Box(new Rect(22, Screen.height - 76, 555, 30),
                LanguageSelectionUI.T(
                    "  FIRE DRILL / PERSISTENT SMOKE / M ",
                    "  消防演练 / 持续烟雾 / M ",
                    "  消防演練 / 持續煙霧 / M ") +
                (alarm != null && alarm.mute
                    ? LanguageSelectionUI.T("UNMUTE", "取消静音", "取消靜音")
                    : LanguageSelectionUI.T("MUTE", "静音", "靜音")), captionStyle);
        }
    }
}
