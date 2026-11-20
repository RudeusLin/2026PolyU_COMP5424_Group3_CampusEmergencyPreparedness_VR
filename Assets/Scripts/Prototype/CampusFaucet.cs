using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed class CampusFaucet : MonoBehaviour, IPrototypeInteractable
    {
        [SerializeField] private GameObject waterFlow;
        private static AudioClip runningWaterClip;
        private AudioSource runningWaterAudio;
        public bool IsRunning => waterFlow != null && waterFlow.activeSelf;
        public string Prompt => "[E] Use tap / wet held towel";

        public void Configure(GameObject flow)
        {
            waterFlow = flow;
            SetRunning(false);
        }

        public void SetRunning(bool running)
        {
            if (waterFlow != null) waterFlow.SetActive(running);
            if (!running || !IsRunning || !isActiveAndEnabled)
            {
                if (runningWaterAudio != null) runningWaterAudio.Stop();
                return;
            }
            if (!Application.isPlaying) return;
            if (runningWaterAudio == null)
            {
                if (runningWaterClip == null) runningWaterClip = Resources.Load<AudioClip>("Audio/TapWater");
                if (runningWaterClip == null)
                {
                    Debug.LogError("Missing Audio/TapWater audio clip.", this);
                    return;
                }
                GameObject emitter = new GameObject("TapWaterAudio");
                emitter.transform.SetParent(transform, false);
                runningWaterAudio = emitter.AddComponent<AudioSource>();
                runningWaterAudio.clip = runningWaterClip;
                runningWaterAudio.playOnAwake = false;
                runningWaterAudio.loop = true;
                runningWaterAudio.volume = .6f;
                runningWaterAudio.spatialBlend = 1f;
                runningWaterAudio.minDistance = .5f;
                runningWaterAudio.maxDistance = 8f;
                runningWaterAudio.rolloffMode = AudioRolloffMode.Linear;
                runningWaterAudio.dopplerLevel = 0f;
            }
            if (!runningWaterAudio.isPlaying) runningWaterAudio.Play();
        }

        public string Interact(ICampusInteractor explorer)
        {
            if (explorer != null && explorer.HeldTowel != null && !explorer.HeldTowel.IsWet)
            {
                SetRunning(true);
                explorer.HeldTowel.Wet();
                return "TOWEL WET\nPress E on the tap again to turn off the water.";
            }
            SetRunning(!IsRunning);
            return IsRunning ? "TAP ON\nPick up the towel, then use the tap to wet it." : "TAP OFF";
        }

        private void OnEnable() => SetRunning(IsRunning);
        private void OnDisable() => SetRunning(false);
    }
}
