using UnityEngine;
using CampusEmergency.Prototype;

namespace CampusEmergency.Interaction.FireSafety
{
    /// <summary>A fire is extinguished only by an uninterrupted three-second hit at its base.</summary>
    public sealed class CampusFireTarget : MonoBehaviour, IPrototypeInteractable
    {
        public const float RequiredSeconds = 3f;
        public const float BaseRadius = .5f; // Legacy scene marker; no longer used for spray targeting.
        public const float SprayBandHeight = 1f;
        public bool Extinguished { get; private set; }
        public float ContinuousSeconds { get; private set; }
        public Vector3 AimPoint => transform.position + Vector3.up * .12f;
        public string Prompt => Extinguished ? "Fire extinguished" : "FIRE: spray the lower 1m of the box for 3 continuous seconds";
        public string Interact(ICampusInteractor actor = null) => Prompt;
        public bool IsInSprayBand(Vector3 point)
        {
            var box=GetComponent<BoxCollider>();
            if(box==null || !box.enabled || Extinguished) return false;
            var b=box.bounds;
            return point.y>=b.min.y-.001f && point.y<=Mathf.Min(b.max.y,b.min.y+SprayBandHeight)+.001f
                && point.x>=b.min.x-.001f && point.x<=b.max.x+.001f
                && point.z>=b.min.z-.001f && point.z<=b.max.z+.001f;
        }
        public void Interrupt() => ContinuousSeconds = 0f;
        public bool ApplySpray(float deltaTime)
        {
            if (Extinguished) return false;
            ContinuousSeconds += Mathf.Max(0f, deltaTime);
            if (ContinuousSeconds + .00001f < RequiredSeconds) return false;
            Extinguished = true;
            ContinuousSeconds = RequiredSeconds;
            foreach (var effect in GetComponentsInChildren<ParticleSystem>())
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var light in GetComponentsInChildren<Light>()) light.enabled = false;
            var fire = GetComponent<CampusForecourtFire>();
            if (fire != null) fire.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            return true;
        }
    }
}
