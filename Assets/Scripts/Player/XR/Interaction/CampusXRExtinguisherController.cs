using System;
using UnityEngine;
using UnityEngine.InputSystem;
using CampusEmergency.Interaction.FireSafety;

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
        private InputAction modeButton, leftGrip, rightGrip;
        private bool triggerReleased;
        private CampusFireTarget currentTarget;
        private CampusFireTarget[] fires;
        private ParticleSystem spray;
        private LineRenderer aim, ring;
        private Material visualMaterial;
        public CampusExtinguisher Held { get; private set; }
        public bool SprayMode { get; private set; }
        public bool BlocksTeleport => Held != null && (SprayMode || !triggerReleased);
        public string Status { get; private set; }
        public string Controls => Held == null ? null : (SprayMode ? "MODE: SPRAY" : "MODE: ACTION")
            + " / B: SWITCH / RIGHT GRIP: RETURN\n" + (SprayMode ? "TRIGGER: SPRAY AT FIRE BASE" : "TRIGGER: TELEPORT")
            + (Held.PinRemoved ? " / PIN REMOVED" : " / LEFT GRIP NEAR PIN: PULL");

        public void Configure(CampusXRInput controls, CampusXRInventory items, CampusXRFeedback messages,
            Transform right, Transform left, Transform camera)
        { input = controls; inventory = items; feedback = messages; rightHand = right; leftHand = left; head = camera; }
        private void OnEnable()
        {
            modeButton = new InputAction("Extinguisher mode", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            leftGrip = new InputAction("Pull pin", InputActionType.Button, "<XRController>{LeftHand}/gripPressed");
            rightGrip = new InputAction("Return extinguisher", InputActionType.Button, "<XRController>{RightHand}/gripPressed");
            modeButton.Enable(); leftGrip.Enable(); rightGrip.Enable();
        }
        private void Start() { fires = FindObjectsOfType<CampusFireTarget>(); BuildVisuals(); }
        public bool TryPickup(CampusExtinguisher item)
        {
            if (Held != null || inventory.HeldTowel != null || item == null || item.IsHeld) return false;
            Held = item; item.Hold(rightHand); SprayMode = false; triggerReleased = false;
            StopSpray(); return true;
        }
        public void SwitchMode()
        {
            if (Held == null) return;
            SprayMode = !SprayMode; triggerReleased = false; StopSpray();
            feedback.Notify(SprayMode ? "SPRAY mode: release trigger, pull pin, aim at fire base." : "ACTION mode: release trigger, then press to teleport.");
        }
        private void Update() => ProcessInput(input != null && input.HasInteractionFocus);
        private void ProcessInput(bool focus)
        {
            if (Held == null) return;
            bool tracked = input.RightTracked;
            foreach (var renderer in Held.GetComponentsInChildren<Renderer>()) renderer.enabled = tracked;
            if (!focus || !tracked) { triggerReleased = false; StopSpray(); return; }
            if (modeButton.WasPressedThisFrame()) SwitchMode();
            if (!input.TeleportHeld) triggerReleased = true;
            if (rightGrip.WasPressedThisFrame() && !input.TeleportHeld) { ReturnHeld(); return; }
            if (input.LeftTracked && leftGrip.WasPressedThisFrame())
                feedback.Notify(Held.TryPullPin(leftHand.position) ? "PIN REMOVED. B: spray mode, aim at the fire base."
                    : Held.PinRemoved ? "Pin already removed." : "Bring LEFT hand within 18cm of the yellow safety pin.");
            if (!SprayMode || input.InteractionHeld) { StopSpray(); return; }
            var target = FindTarget(Held.Nozzle.position, Held.Nozzle.forward, out var end);
            ShowAim(Held.Nozzle.position, end, target);
            bool spraying = triggerReleased && input.TeleportHeld && Held.PinRemoved;
            if (spraying)
            {
                spray.transform.SetPositionAndRotation(Held.Nozzle.position, Held.Nozzle.rotation);
                var main = spray.main; main.startLifetime = Mathf.Clamp(Vector3.Distance(Held.Nozzle.position, end) / 8f, .01f, .375f);
                if (!spray.isPlaying) spray.Play();
            }
            else spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ApplyTarget(spraying ? target : null, Time.deltaTime);
            Status = !Held.PinRemoved ? "STEP 2: pull the yellow safety pin with LEFT grip."
                : target == null ? "STEP 3: aim at the fire BASE within 3m."
                : "STEP 4: hold trigger on this base: " + target.ContinuousSeconds.ToString("0.0") + " / 3.0s";
        }
        public void ApplyTarget(CampusFireTarget target, float seconds)
        {
            if (currentTarget != target) { if (currentTarget != null) currentTarget.Interrupt(); currentTarget = target; }
            if (target != null && target.ApplySpray(seconds))
            { feedback.Notify("FIRE EXTINGUISHED. B: action mode to move to the next fire."); currentTarget = null; }
        }
        public CampusFireTarget FindTarget(Vector3 start, Vector3 direction, out Vector3 end)
        {
            direction.Normalize();
            float distance = 3f;
            // Physical geometry blocks the stream; fire target triggers never block locomotion.
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
            StopSpray();
            if (Held != null) { foreach (var r in Held.GetComponentsInChildren<Renderer>(true)) r.enabled = true; Held.ReturnHome(); }
            Held = null; SprayMode = false; triggerReleased = false;
            feedback.Notify("Extinguisher returned; safety pin reset.");
        }
        private void StopSpray()
        {
            ApplyTarget(null, 0f); Status = string.Empty;
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
            visualMaterial = new Material(Shader.Find("Sprites/Default"));
            aim = Line("Extinguisher aim", .006f); ring = Line("Fire base target", .015f);
            var go = new GameObject("Extinguishing agent"); go.transform.SetParent(transform, false);
            spray = go.AddComponent<ParticleSystem>(); spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main; main.playOnAwake = false; main.loop = true; main.startSpeed = 8f;
            main.startLifetime = .375f; main.startSize = .055f; main.startColor = new Color(.82f,.94f,1f,.65f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 120;
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
            if (Held != null) ReturnHeld(); else StopSpray();
            modeButton?.Dispose(); leftGrip?.Dispose(); rightGrip?.Dispose();
        }
        private void OnDestroy() { if (visualMaterial != null) Destroy(visualMaterial); }
    }
}
