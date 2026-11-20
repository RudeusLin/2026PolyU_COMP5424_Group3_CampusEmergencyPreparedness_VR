using System.Collections.Generic;
using UnityEngine;
using CampusEmergency.Player;

namespace CampusEmergency.Prototype
{
    // Manual fire-alarm audio. Corridor smoke is handled separately.
    public sealed class CampusDrillAtmosphere : MonoBehaviour
    {
        [SerializeField] private AudioClip alarmClip;
        private readonly List<AudioSource> alarms = new List<AudioSource>();
        private bool muted;
        private GUIStyle captionStyle;
        public bool HasAlarmBeenActivated { get; private set; }
        public bool IsAlarmActive => gameObject.activeInHierarchy && alarms.Exists(source => source != null && source.isPlaying);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePersistentSmoke()
        {
            foreach (CampusDrillAtmosphere atmosphere in FindObjectsOfType<CampusDrillAtmosphere>(true))
                if (!atmosphere.gameObject.activeSelf) atmosphere.gameObject.SetActive(true);
        }

        public void ActivateAlarm()
        {
            HasAlarmBeenActivated = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            if (alarms.Count == 0) OnEnable();
            foreach (AudioSource alarm in alarms)
                if (!alarm.isPlaying) alarm.Play();
        }

        public void Configure(Material _, AudioClip clip)
        {
            alarmClip = clip;
        }

        private void OnEnable()
        {
            if (alarms.Count != 0 || alarmClip == null) return;
            for (int floor = 1; floor <= 4; floor++)
            {
                GameObject level = GameObject.Find("Floor_" + floor.ToString("00"));
                if (level == null) continue;
                CreateAlarm(level.transform, floor, "West", -11.5f);
                CreateAlarm(level.transform, floor, "Middle", 0f);
                CreateAlarm(level.transform, floor, "East", 11.5f);
            }
        }

        private void CreateAlarm(Transform level, int floor, string location, float x)
        {
            GameObject emitter = new GameObject("DrillAlarm_F" + floor.ToString("00") + "_" + location);
            emitter.transform.SetParent(transform, false);
            emitter.transform.position = level.TransformPoint(new Vector3(x, 2.4f, 1.05f));
            AudioSource alarm = emitter.AddComponent<AudioSource>();
            alarm.clip = alarmClip;
            alarm.playOnAwake = false;
            alarm.loop = false;
            alarm.volume = .12f;
            alarm.spatialBlend = 1f;
            alarm.minDistance = 2f;
            alarm.maxDistance = 18f;
            alarm.rolloffMode = AudioRolloffMode.Linear;
            alarm.dopplerLevel = 0f;
            alarm.mute = muted;
            alarm.Stop();
            alarms.Add(alarm);
        }

        private void OnDisable()
        {
            foreach (AudioSource alarm in alarms)
                if (alarm != null) alarm.Stop();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                muted = !muted;
                foreach (AudioSource alarm in alarms)
                    if (alarm != null) alarm.mute = muted;
            }
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
                (muted
                    ? LanguageSelectionUI.T("UNMUTE", "取消静音", "取消靜音")
                    : LanguageSelectionUI.T("MUTE", "静音", "靜音")), captionStyle);
        }
    }
}
