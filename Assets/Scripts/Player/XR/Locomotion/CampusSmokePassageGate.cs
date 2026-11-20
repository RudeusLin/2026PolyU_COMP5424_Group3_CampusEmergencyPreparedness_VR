using System;
using UnityEngine;

namespace CampusEmergency.Player.XR
{
    /// <summary>Analytic smoke checkpoint; independent of ceiling particles and physics colliders.</summary>
    [Serializable]
    public sealed class CampusSmokePassageGate
    {
        // West of the 401 washroom: leaves the wash station reachable before protection.
        [SerializeField] private Bounds passage = new Bounds(new Vector3(-10.875f, 12.05f, 0f), new Vector3(2.75f, 2.5f, 3.3f));
        private Vector3 safePosition;
        private bool hasSafePosition;
        public Bounds Passage => passage;

        public void ObserveFeet(Vector3 feet)
        {
            if (passage.Contains(feet)) return;
            safePosition = feet;
            hasSafePosition = true;
        }

        public bool Allows(Vector3 from, Vector3 to, bool protectedByTowel)
        {
            // Recovery if a saved/debug spawn starts inside: choose the closest boundary.
            Vector3 safe = safePosition;
            if (!hasSafePosition)
            {
                safe = from;
                safe.x = from.x >= passage.center.x ? passage.max.x + .3f : passage.min.x - .3f;
            }
            return CanTraverse(passage, from, to, protectedByTowel, safe);
        }

        public static bool CanTraverse(Bounds bounds, Vector3 from, Vector3 to, bool protectedByTowel, Vector3 entrySafePosition)
        {
            if (protectedByTowel) return true;
            if (bounds.Contains(from))
            {
                if (bounds.Contains(to)) return false;
                // Retreat only through the entry side. Reaching the opposite side is not an escape.
                Vector3 boundary = bounds.ClosestPoint(entrySafePosition);
                Vector3 outward = entrySafePosition - boundary;
                outward.y = 0f;
                if (outward.sqrMagnitude < .0001f) return false;
                outward.Normalize();
                return Vector3.Dot(to - boundary, outward) > .01f
                    && Vector3.Dot(to - from, outward) > .01f;
            }
            if (bounds.Contains(to)) return false;
            Vector3 delta = to - from;
            return delta.sqrMagnitude < .000001f
                || !bounds.IntersectRay(new Ray(from, delta.normalized), out float distance)
                || distance > delta.magnitude;
        }
    }
}
