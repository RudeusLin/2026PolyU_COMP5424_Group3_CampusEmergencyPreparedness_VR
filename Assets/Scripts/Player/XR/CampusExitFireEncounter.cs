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
        [SerializeField] private int side;
        public int Side => side;
        public bool IsSideExit => side != 0;
        public Vector3 HintPosition => side == 0 ? new Vector3(-1.4f,1.85f,-6.1f) : new Vector3(side*17f,2.05f,4f);
        public string ExitName => side == 0 ? LanguageSelectionUI.T("Main entrance", "正门", "正門")
            : side < 0 ? LanguageSelectionUI.T("Side exit A", "A侧门", "A側門") : LanguageSelectionUI.T("Side exit B", "B侧门", "B側門");
        public bool CrossedOutward(Vector3 from, Vector3 to)
        {
            if (!IsSideExit || !IsCleared || Mathf.Abs(from.y) > .6f || Mathf.Abs(to.y) > .6f) return false;
            float start = from.x * side, end = to.x * side;
            if (start >= 18.05f || end < 18.05f || end <= start) return false;
            float t = (18.05f-start)/(end-start);
            float z = Mathf.Lerp(from.z,to.z,t);
            return z >= 1.8f && z <= 4.2f;
        }
        private static readonly HashSet<CampusExitFireEncounter> active = new HashSet<CampusExitFireEncounter>();
        public CampusFireTarget[] Targets => targets;
        public bool IsCleared => targets != null && targets.Length == 3 && targets.All(t => t != null && t.Extinguished);
        public CampusFireTarget Nearest(Vector3 position) => targets == null ? null : targets.Where(t => t != null && !t.Extinguished)
            .OrderBy(t => Vector3.Distance(position, t.transform.position)).FirstOrDefault();
        public Bounds Passage => side == 0 ? new Bounds(new Vector3(0f, 1.65f, -7.2f), new Vector3(8f, 3.3f, 1.8f)) : new Bounds(new Vector3(side*17.65f,1.65f,3f),new Vector3(1.4f,3.3f,3.8f));
        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);
        public static CampusExitFireEncounter Ensure() => EnsureExit(0);
        public static CampusExitFireEncounter[] EnsureSideExits() => new[] { EnsureExit(-1), EnsureExit(1) };
        private static CampusExitFireEncounter EnsureExit(int exitSide)
        {
            var existing = FindObjectsOfType<CampusExitFireEncounter>().FirstOrDefault(e => e.side == exitSide);
            if (existing != null) return existing;
            var root = new GameObject(exitSide == 0 ? "Main Entrance Fire Checkpoint" : "Side Exit " + (exitSide < 0 ? "A" : "B") + " Fire Checkpoint");
            var floor = GameObject.Find("TeachingBuilding/Building/Floor_01");
            if (floor != null) root.transform.SetParent(floor.transform, true);
            var encounter = root.AddComponent<CampusExitFireEncounter>();
            encounter.side = exitSide;
            encounter.targets = new CampusFireTarget[3];
            var authoredFires = exitSide == 1 ? FindObjectsOfType<CampusFireTarget>().Where(t => t.transform.position.x > 15f && t.transform.position.y < .8f && Mathf.Abs(t.transform.position.z-3f) < 3f && t.GetComponentInParent<CampusExitFireEncounter>() == null).OrderBy(t => t.transform.position.z).Take(3).ToArray() : new CampusFireTarget[0];
            for (int i = 0; i < 3; i++)
            {
                // Reuse authored ground-floor B flames rather than leaving overlapping blockers.
                var authored = i < authoredFires.Length ? authoredFires[i] : null;
                var fire = authored != null ? authored.gameObject : new GameObject((exitSide == 0 ? "Exit" : exitSide < 0 ? "Side A" : "Side B") + " Fire " + (i + 1));
                fire.transform.SetParent(root.transform, false);
                fire.transform.position = exitSide == 0 ? new Vector3((i - 1) * 1.1f, .06f, -7.2f) : new Vector3(exitSide*17.65f,.06f,3f+(i-1)*.8f);
                if (authored == null) fire.AddComponent<CampusForecourtFire>();
                encounter.targets[i] = fire.GetComponent<CampusFireTarget>() ?? fire.AddComponent<CampusFireTarget>();
                (fire.GetComponent<CampusFireBarrier>() ?? fire.AddComponent<CampusFireBarrier>()).Configure(new Vector3(0, .85f, 0), exitSide == 0 ? new Vector3(1.5f, 1.8f, 1.7f) : new Vector3(1.2f,1.8f,1.4f));
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
