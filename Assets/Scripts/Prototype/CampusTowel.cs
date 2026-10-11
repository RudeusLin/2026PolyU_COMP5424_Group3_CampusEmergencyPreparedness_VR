using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed class CampusTowel : MonoBehaviour, IPrototypeInteractable
    {
        private Transform counter;
        private Vector3 counterPosition;
        private Quaternion counterRotation;
        private Vector3 counterScale;
        private Collider[] colliders;
        private Renderer[] surfaces;
        private ICampusInteractor holder;
        private MaterialPropertyBlock wetTint;

        public bool IsWet { get; private set; }
        public string Prompt => "[E] Pick up towel";

        private void Awake()
        {
            counter = transform.parent;
            counterPosition = transform.localPosition;
            counterRotation = transform.localRotation;
            counterScale = transform.localScale;
            colliders = GetComponentsInChildren<Collider>();
            surfaces = GetComponentsInChildren<Renderer>();
        }

        public string Interact(ICampusInteractor explorer)
        {
            if (explorer == null || !explorer.TryHoldTowel(this))
                return "You are already holding a towel.";
            holder = explorer;
            foreach (Collider collider in colliders) collider.enabled = false;
            RefreshHoldPose();
            return "TOWEL PICKED UP\nUse the tap to wet the towel. R returns it to the counter.";
        }

        public void RefreshHoldPose()
        {
            if (holder == null) return;
            transform.SetParent(holder.TowelHoldPoint, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = counterScale * .65f;
        }

        public void Wet()
        {
            IsWet = true;
            if (wetTint == null) wetTint = new MaterialPropertyBlock();
            foreach (Renderer surface in surfaces)
            {
                wetTint.SetColor("_Color", surface.sharedMaterial.color * new Color(.62f, .72f, .78f, 1));
                surface.SetPropertyBlock(wetTint);
            }
        }

        public void ResetToCounter()
        {
            if (holder != null) holder.ReleaseTowel(this);
            holder = null;
            transform.SetParent(counter, false);
            transform.localPosition = counterPosition;
            transform.localRotation = counterRotation;
            transform.localScale = counterScale;
            IsWet = false;
            foreach (Collider collider in colliders) collider.enabled = true;
            foreach (Renderer surface in surfaces) surface.SetPropertyBlock(null);
        }
    }
}
