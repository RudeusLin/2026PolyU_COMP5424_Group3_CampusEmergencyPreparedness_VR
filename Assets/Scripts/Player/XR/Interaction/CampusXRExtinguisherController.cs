using System;
using UnityEngine;
using UnityEngine.InputSystem;
using CampusEmergency.Interaction.FireSafety;
using CampusEmergency.Player;

namespace CampusEmergency.Player.XR
{
    // Runs before teleport so a B press cannot also cause a teleport in the same frame.
    [DefaultExecutionOrder(-50)]
    public sealed class CampusXRExtinguisherController : MonoBehaviour
    {
        [SerializeField] private CampusXRInput input;
        [SerializeField] private CampusXRInventory inventory;
        [SerializeField] private CampusXRFeedback feedback;
        [SerializeField] private Transform rightHand, leftHand, head;
        private InputAction sprayTrigger;
        private bool triggerReleased;
        private CampusFireTarget currentTarget;
        private CampusFireTarget[] fires;
        private ParticleSystem spray;
        private LineRenderer aim, ring;
        private Material visualMaterial;
        private CampusFireProgress progress;
        public CampusExtinguisher Held { get; private set; }
        public CampusExtinguisher Owned { get; private set; }
        public int LastReturnFrame { get; private set; } = -1;
        public bool SprayMode { get; private set; }
        public bool BlocksTeleport => false;
        public string Status { get; private set; }
        public string Controls => Held==null ? null : LanguageSelectionUI.T("RIGHT A AT PIN: REMOVE / LEFT TRIGGER: SPRAY / X: SWITCH", "右手A对准保险销：拔销 / 左扳机：喷射 / X：切换", "右手A對準保險銷：拔銷 / 左扳機：噴射 / X：切換");
        public void Configure(CampusXRInput controls, CampusXRInventory items, CampusXRFeedback messages,
            Transform right, Transform left, Transform camera)
        { input = controls; inventory = items; feedback = messages; rightHand = right; leftHand = left; head = camera; }
        private void OnEnable()
        {
            sprayTrigger=new InputAction("Spray left trigger",InputActionType.Button,"<XRController>{LeftHand}/triggerPressed");sprayTrigger.Enable();
        }
        private void Start() { fires = FindObjectsOfType<CampusFireTarget>(); BuildVisuals(); }
        public void RefreshTargets() => fires = FindObjectsOfType<CampusFireTarget>();
        public bool TryPickup(CampusExtinguisher item)
        {
            if(Owned!=null || inventory==null || !inventory.IsEmpty || item==null || item.IsHeld)return false;
            Owned=item;item.Hold(leftHand);inventory.Equip(CampusXRInventory.EquipmentSlot.Extinguisher);return true;
        }
        public void SetEquipped(bool equipped)
        {
            bool newlyEquipped = equipped && Owned != null && Held != Owned;
            StopSpray();Held=equipped?Owned:null;SprayMode=equipped;triggerReleased=false;
            if (newlyEquipped) Owned.ResetSafetyPin();
            if(Owned!=null)foreach(var renderer in Owned.GetComponentsInChildren<Renderer>(true))renderer.enabled=equipped;
        }
        // Kept for old editor tools; mode is now selected by the inventory, never B.
        public void SwitchMode() { StopSpray(); }
        public bool TryPointAtPin(Ray ray,float reach,out Vector3 point)
        {
            point=Vector3.zero;
            if(Held==null || Held.PinRemoved || Held.Pin==null)return false;
            point=Held.Pin.position;var offset=point-ray.origin;float along=Vector3.Dot(offset,ray.direction);
            return along>0 && along<=reach && (offset-ray.direction*along).magnitude<.065f
                && !Physics.Linecast(ray.origin,point,~0,QueryTriggerInteraction.Ignore);
        }
        public string PullPin()
        {
            if(Held==null)return "";
            Held.TryPullPin(Held.Pin.position);
            triggerReleased=sprayTrigger!=null && !sprayTrigger.IsPressed();
            return LanguageSelectionUI.T("PIN REMOVED. Hold LEFT TRIGGER to spray.","保险销已拔出，按住左扳机喷射。","保險銷已拔出，按住左扳機噴射。");
        }
        private void Update() => ProcessInput(input != null && input.HasInteractionFocus);
        private void ProcessInput(bool focus)
        {
            if (Held == null) return;
            bool tracked = input.LeftTracked;
            foreach (var renderer in Held.GetComponentsInChildren<Renderer>()) renderer.enabled = tracked;
            if (!focus || !tracked) { triggerReleased = false; StopSpray(); return; }
            if(!sprayTrigger.IsPressed())triggerReleased=true;
            var target = FindTarget(Held.Nozzle.position, Held.Nozzle.forward, out var end);
            ShowAim(Held.Nozzle.position, end, target);
            bool spraying = triggerReleased && sprayTrigger.IsPressed() && Held.PinRemoved;
            UpdateSprayVisual(spraying,end);
            ApplyTarget(spraying ? target : null, Time.deltaTime);
            if(!spraying && Held.PinRemoved && progress!=null)progress.Show(target);
            Status = !Held.PinRemoved ? LanguageSelectionUI.T("STEP 2: point the right ray at the pin and press A.", "步骤2：右手射线对准保险销按A。", "步驟2：右手射線對準保險銷按A。")
                : target == null ? LanguageSelectionUI.T("STEP 3: aim at the fire BASE within 3m.", "步骤3：在3米内瞄准火源根部。", "步驟3：在3米內瞄準火源根部。")
                : LanguageSelectionUI.T("STEP 4: hold trigger on this base: ", "步骤4：对准此火源根部持续按住扳机：", "步驟4：對準此火源根部持續按住扳機：") + target.ContinuousSeconds.ToString("0.0") + " / 3.0s";
        }
        public void UpdateSprayVisual(bool spraying,Vector3 end)
        {
            if(spray==null || Held==null)return;
            if(!spraying){spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return;}
            spray.transform.SetPositionAndRotation(Held.Nozzle.position,Held.Nozzle.rotation);
            var main=spray.main;main.startLifetime=Mathf.Clamp(Vector3.Distance(Held.Nozzle.position,end)/8f,.08f,.375f);
            var renderer=spray.GetComponent<ParticleSystemRenderer>();renderer.enabled=true;
            if(!spray.isPlaying){spray.Clear(true);spray.Play(true);spray.Emit(12);}
        }
        public void ApplyTarget(CampusFireTarget target, float seconds)
        {
            if (currentTarget != target) { if (currentTarget != null) currentTarget.Interrupt(); currentTarget = target; }
            if (target != null && target.ApplySpray(seconds))
            { feedback.Notify(LanguageSelectionUI.T("FIRE EXTINGUISHED. Right trigger teleports; X switches equipment.", "火源已扑灭，右扳机可传送，X切换物品。", "火源已撲滅，右扳機可傳送，X切換物品。")); currentTarget = null; }
            if(progress!=null)progress.Show(target);
        }
        public CampusFireTarget FindTarget(Vector3 start, Vector3 direction, out Vector3 end)
        {
            direction.Normalize();
            float distance = 3f;
            // Walls and floors block spray; analytic fire regions do not obstruct the stream.
            if (Physics.Raycast(start, direction, out var hit, distance, ~0, QueryTriggerInteraction.Ignore)) distance = hit.distance;
            end = start + direction * distance;
            if (head != null && (Physics.CheckSphere(start, .02f, ~0, QueryTriggerInteraction.Ignore)
                || Physics.Linecast(head.position, start, ~0, QueryTriggerInteraction.Ignore))) return null;
            CampusFireTarget best = null; float nearest = float.MaxValue;
            if (fires == null) fires = FindObjectsOfType<CampusFireTarget>();
            foreach (var fire in fires)
            {
                if (fire == null || !fire.isActiveAndEnabled || fire.Extinguished) continue;
                Vector3 offset = fire.AimPoint - start;
                float along = Vector3.Dot(offset, direction);
                float radial2 = offset.sqrMagnitude - along * along;
                float r2 = CampusFireTarget.BaseRadius * CampusFireTarget.BaseRadius;
                if (along < 0f || radial2 > r2) continue;
                float entry = Mathf.Max(0f, along - Mathf.Sqrt(Mathf.Max(0f, r2 - radial2)));
                // Verify unobstructed line to the base, not just the near side of its tolerance sphere.
                if (entry > distance || entry >= nearest || Physics.Linecast(start, fire.AimPoint, ~0, QueryTriggerInteraction.Ignore)) continue;
                best = fire; nearest = entry;
            }
            return best;
        }
        public void ReturnHeld()
        {
            inventory.Equip(CampusXRInventory.EquipmentSlot.Empty);
        }
        private void StopSpray()
        {
            ApplyTarget(null, 0f); Status = string.Empty;
            if(progress!=null)progress.Hide();
            if (spray != null) spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (aim != null) aim.enabled = false;
            if (ring != null) ring.enabled = false;
        }
        private LineRenderer Line(string name, float width)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = visualMaterial;
            line.startWidth = line.endWidth = width; line.useWorldSpace = true; line.enabled = false;
            return line;
        }
        private void BuildVisuals()
        {
            progress=gameObject.AddComponent<CampusFireProgress>();progress.Configure(head);
            visualMaterial = new Material(Shader.Find("Sprites/Default"));
            aim = Line("Extinguisher aim", .006f); ring = Line("Fire base target", .015f);
            var go = new GameObject("Extinguishing agent"); go.transform.SetParent(transform, false);
            spray = go.AddComponent<ParticleSystem>(); spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main; main.playOnAwake = false; main.loop = true; main.startSpeed = 8f;
            main.startLifetime = .375f; main.startSize = .055f; main.startColor = new Color(.82f,.94f,1f,.65f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 120;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission = spray.emission; emission.rateOverTime = 180f;
            var shape = spray.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 5f; shape.radius = .018f;
            spray.GetComponent<ParticleSystemRenderer>().sharedMaterial = visualMaterial;
        }
        private void ShowAim(Vector3 start, Vector3 end, CampusFireTarget target)
        {
            aim.enabled = true; aim.positionCount = 2; aim.SetPosition(0,start); aim.SetPosition(1,end);
            aim.startColor = aim.endColor = target != null ? Color.green : Color.red;
            ring.enabled = target != null;
            if (target == null) return;
            ring.positionCount = 33; ring.startColor = ring.endColor = Color.green;
            for(int i=0;i<33;i++) { float a=i*Mathf.PI*2f/32f; ring.SetPosition(i,target.transform.position+new Vector3(Mathf.Cos(a)*.5f,.04f,Mathf.Sin(a)*.5f)); }
        }
        private void OnDisable()
        {
            StopSpray();sprayTrigger?.Dispose();sprayTrigger=null;
        }
        private void OnDestroy() { if (visualMaterial != null) Destroy(visualMaterial); }
    }
}
