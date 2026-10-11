# Fire blockers and outdoor teleport — 2026-10-12

Baseline: e95be57, tagged baseline/extinguisher-verified-20261012 (user-confirmed extinguisher version).

Nine fires now have solid BoxColliders instead of sphere triggers. Footprint: 4.2 x 4.2 metres, height: 3.45 metres. Bottom is 5cm below the supporting floor and top is 3.4m above it. Main corridor blockers are centered on the 3m-wide passage rather than the offset flames, closing edge gaps. Existing flame visual positions are preserved.

Teleport rays stop at the box. Destination validation also rejects a swept virtual-body path crossing fire, preventing edge/overhead targeting from bypassing it. CampusXRFireBarrierGuard constrains tracked virtual movement at the barrier; physical headset movement itself cannot be stopped by software.

Spray visuals stop at the first solid face. Aiming at a fire base may extinguish the fire enclosed by that face; overlapping volumes are handled without making their own fire roots inaccessible. Separate obstacles and fire volumes still obstruct the stream. Existing continuous-three-second rules are unchanged. Extinguishing disables the collider together with the fire effect, reopening the passage.

Outdoor walkable whitelist now includes Forecourt and MainWalkway, alongside Ground and SideExitPath. Non-trigger ground colliders are present. Existing range, slope, support and clearance checks remain in force.

Files:
- Interaction/FireSafety/CampusFireBarrier.cs: swept body path against live box volumes.
- Player/XR/Locomotion/CampusXRFireBarrierGuard.cs: tracked virtual movement guard.
- Editor/XR/CampusFireBarrierSetup.cs: repeatable scene/prefab setup and outdoor landing verification.
- CampusTeleportValidator.cs: fire rejection and crossing check.
- CampusXRExtinguisherController.cs: distinguish the target's own enclosing volume from separate obstructions.

Validation reports: FireBarrierValidation.txt and ExtinguisherValidation.txt. Hardware comfort and appearance require Quest 3 verification.

Rollback: save pending changes and close Unity, then create a branch from the confirmed tag:

git switch -c review/extinguisher-baseline baseline/extinguisher-verified-20261012

This preserves the new implementation on feature/extinguisher. No reset --hard is needed.