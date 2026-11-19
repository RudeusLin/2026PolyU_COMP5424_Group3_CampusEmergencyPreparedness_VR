using System.Collections.Generic;
using CampusEmergency.Interaction.FireSafety;
using UnityEngine;

namespace CampusEmergency.Prototype
{
    // One positional loop per connected group of nearby, burning fire effects.
    public sealed class CampusFireAudio : MonoBehaviour
    {
        public const float MergeDistance = 3f;
        private const float MaxHeightDifference = 1.5f;
        private static CampusFireAudio instance;
        private readonly List<CampusForecourtFire> fires = new List<CampusForecourtFire>();
        private readonly List<CampusForecourtFire> remaining = new List<CampusForecourtFire>();
        private readonly List<CampusForecourtFire> cluster = new List<CampusForecourtFire>();
        private readonly List<CampusForecourtFire> representatives = new List<CampusForecourtFire>();
        private readonly List<CampusForecourtFire> stale = new List<CampusForecourtFire>();
        private readonly Dictionary<CampusForecourtFire, AudioSource> sources = new Dictionary<CampusForecourtFire, AudioSource>();
        private AudioClip clip;
        private float nextRefresh;

        public static void Register(CampusForecourtFire fire)
        {
            if (!Application.isPlaying) return;
            if (instance == null)
            {
                instance = new GameObject("FireAudio").AddComponent<CampusFireAudio>();
                instance.clip = Resources.Load<AudioClip>("Audio/FireBurning");
                if (instance.clip == null) Debug.LogError("Missing Audio/FireBurning audio clip.", instance);
            }
            if (!instance.fires.Contains(fire)) instance.fires.Add(fire);
            instance.Refresh();
        }

        public static void Unregister(CampusForecourtFire fire)
        {
            if (instance == null) return;
            instance.fires.Remove(fire);
            instance.Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .25f;
            Refresh();
        }

        private void Refresh()
        {
            remaining.Clear();
            foreach (CampusForecourtFire fire in fires)
            {
                if (fire == null || !fire.isActiveAndEnabled) continue;
                CampusFireTarget target = fire.GetComponent<CampusFireTarget>();
                if (target == null || !target.Extinguished) remaining.Add(fire);
            }
            remaining.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            representatives.Clear();
            while (remaining.Count > 0)
            {
                CampusForecourtFire representative = remaining[0];
                representatives.Add(representative);
                cluster.Clear();
                cluster.Add(representative);
                remaining.RemoveAt(0);
                // Include chains of adjacent flames so a continuous fire has one sound.
                for (int member = 0; member < cluster.Count; member++)
                {
                    Vector3 position = cluster[member].transform.position;
                    for (int i = remaining.Count - 1; i >= 0; i--)
                    {
                        Vector3 delta = remaining[i].transform.position - position;
                        if (Mathf.Abs(delta.y) > MaxHeightDifference || delta.sqrMagnitude > MergeDistance * MergeDistance) continue;
                        cluster.Add(remaining[i]);
                        remaining.RemoveAt(i);
                    }
                }
            }

            stale.Clear();
            foreach (var pair in sources)
                if (!representatives.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (CampusForecourtFire fire in stale)
            {
                AudioSource source = sources[fire];
                if (source != null)
                {
                    source.Stop();
                    Destroy(source.gameObject);
                }
                sources.Remove(fire);
            }
            if (clip == null) return;
            foreach (CampusForecourtFire fire in representatives)
            {
                if (!sources.TryGetValue(fire, out AudioSource source))
                {
                    GameObject emitter = new GameObject("Burning_" + fire.name);
                    emitter.transform.SetParent(transform, false);
                    emitter.transform.position = fire.transform.position + Vector3.up * .5f;
                    source = emitter.AddComponent<AudioSource>();
                    source.clip = clip;
                    source.playOnAwake = false;
                    source.loop = true;
                    source.volume = .45f;
                    source.spatialBlend = 1f;
                    source.minDistance = 2f;
                    source.maxDistance = 16f;
                    source.rolloffMode = AudioRolloffMode.Linear;
                    source.dopplerLevel = 0f;
                    sources.Add(fire, source);
                    source.Play();
                }
                source.transform.position = fire.transform.position + Vector3.up * .5f;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
