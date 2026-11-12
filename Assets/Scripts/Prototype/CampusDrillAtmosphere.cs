using UnityEngine;
using CampusEmergency.Player;

namespace CampusEmergency.Prototype
{
    // Manual fire-alarm audio. Corridor smoke is handled separately.
    public sealed class CampusDrillAtmosphere : MonoBehaviour
    {
        [SerializeField] private AudioClip alarmClip;
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
            if (alarm == null) OnEnable();
            if (alarm != null && !alarm.isPlaying) alarm.Play();
        }

        public void Configure(Material _, AudioClip clip)
        {
            alarmClip = clip;
        }

        private void OnEnable()
        {
            if (alarm != null || alarmClip == null) return;
            GameObject emitter = new GameObject("DrillAlarm");
            emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = new Vector3(7.8f, 12.95f, 1.05f);
            alarm = emitter.AddComponent<AudioSource>();
            alarm.clip = alarmClip;
            alarm.playOnAwake = false;
            alarm.loop = true;
            alarm.volume = .12f;
            alarm.spatialBlend = 1f;
            alarm.minDistance = 2f;
            alarm.maxDistance = 18f;
            alarm.rolloffMode = AudioRolloffMode.Linear;
            alarm.Stop();
        }

        private void OnDisable()
        {
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
                    "  FIRE DRILL / M ",
                    "  消防演练 / M ",
                    "  消防演練 / M ") +
                (alarm != null && alarm.mute
                    ? LanguageSelectionUI.T("UNMUTE", "取消静音", "取消靜音")
                    : LanguageSelectionUI.T("MUTE", "静音", "靜音")), captionStyle);
        }
    }
}
