using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CampusEmergency.Interaction.FireSafety;
using CampusEmergency.Prototype;

namespace CampusEmergency.Player.XR
{
    /// <summary>Three-fire exit checkpoint; the whole doorway opens after all three are extinguished.</summary>
    public sealed class CampusExitFireEncounter : MonoBehaviour
    {
        [SerializeField] private CampusFireTarget[] targets;
        private static readonly HashSet<CampusExitFireEncounter> active = new HashSet<CampusExitFireEncounter>();
        public CampusFireTarget[] Targets => targets;
        public bool IsCleared => targets != null && targets.Length == 3 && targets.All(t => t != null && t.Extinguished);
        public CampusFireTarget Nearest(Vector3 position) => targets == null ? null : targets.Where(t => t != null && !t.Extinguished)
            .OrderBy(t => Vector3.Distance(position, t.transform.position)).FirstOrDefault();
        public Bounds Passage => new Bounds(new Vector3(0f, 1.65f, -7.2f), new Vector3(8f, 3.3f, 1.8f));
        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);
        public static CampusExitFireEncounter Ensure()
        {
            var existing = FindObjectOfType<CampusExitFireEncounter>();
            if (existing != null) return existing;
            var root = new GameObject("Main Entrance Fire Checkpoint");
            var floor = GameObject.Find("TeachingBuilding/Building/Floor_01");
            if (floor != null) root.transform.SetParent(floor.transform, true);
            var encounter = root.AddComponent<CampusExitFireEncounter>();
            encounter.targets = new CampusFireTarget[3];
            for (int i = 0; i < 3; i++)
            {
                var fire = new GameObject("Exit Fire " + (i + 1));
                fire.transform.SetParent(root.transform, false);
                fire.transform.position = new Vector3((i - 1) * 1.1f, .06f, -7.2f);
                fire.AddComponent<CampusForecourtFire>();
                encounter.targets[i] = fire.AddComponent<CampusFireTarget>();
                fire.AddComponent<CampusFireBarrier>().Configure(new Vector3(0, .85f, 0), new Vector3(1.5f, 1.8f, 1.7f));
            }
            return encounter;
        }
        public static bool BlocksPath(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            foreach (var region in active)
            {
                if (region == null || !region.isActiveAndEnabled || region.IsCleared) continue;
                var bounds = region.Passage;
                if (bounds.Contains(from) || bounds.Contains(to)) return true;
                if (delta.sqrMagnitude > .000001f && bounds.IntersectRay(new Ray(from, delta.normalized), out float distance)
                    && distance <= delta.magnitude) return true;
            }
            return false;
        }
    }
}
