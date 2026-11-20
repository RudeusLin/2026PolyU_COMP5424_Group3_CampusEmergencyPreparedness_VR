# Wheelchair assistance — ray teleport

Right-ray A starts escort. Right trigger teleports; right stick retains 30-degree snap turning. Left stick has no movement binding. Aim at doors and press A to open; A with no other interactable target pauses escort.

During escort, chair/NPC colliders are temporarily disabled and the interaction ray also explicitly skips the active chair. Their original collider states are restored when escort ends. World walls, doors and furniture still block movement.

Each teleport checks same-floor support, player clearance, wheelchair clearance, swept paths and fire restrictions. A valid teleport places the chair 1.05 m ahead in the teleport travel direction, with a validated existing-heading fallback near walls. Choose short clear destinations through doorways; invalid paired destinations cannot teleport. The chair moves once with the player rather than following continuously. Equipment is stowed during escort and restored when paused or delivered. Entering the refuge does not complete rescue automatically. Aim the right ray at the green PARK HERE marker and press A. When player and wheelchair are near the refuge and the parking path is clear, the chair snaps to the marked parking point, faces the refuge doorway (+Z), and stops. Keep at least 0.85 m away from the parking point to avoid overlap. Confirmation restores equipment and unlocks node 6.

The 401 blackboard still displays the localized briefing map after language selection.

Hardware comfort and visual clearance require headset verification.

First interaction positions the player behind the wheelchair; both initially face east, towards B. The parking marker is a visible green sphere with a localized A-confirm label.

The 2F refuge now has a 2.4m linked inward-opening double doorway. Either leaf + A toggles both. Validated paths through the doorway centre avoid diagonal jamb clipping; closed doors and walls still block escort. See [current checkpoint details](RefugeCheckpoint.md).
