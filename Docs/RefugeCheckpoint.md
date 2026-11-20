# Refuge checkpoint interaction

The 2F refuge is cleared of furniture. Wheelchair parking is at (3.75, 3.65, -5.5), facing the doorway (+Z). The confirmation marker is mounted immediately in front of the rear wall/window area at (3.75, 4.6, -5.95).

Right-hand A confirms after entering the refuge while escorting, with a clear supported parking route and sufficient separation from the player. The wheelchair moves automatically to the marker's parking position and faces the door. No new button is required.

Marker feedback: grey when requirements are unmet, green when parking is available, gold with a pulsing halo when a tracked right ray aims at a valid checkpoint within interaction reach (2.6m). Hover also works while the normal teleport ray is active. Obstacles, lost tracking and lost focus remove the hover. Confirmation displays SAFE and a cyan expanding floor ring around the parked wheelchair; the completed marker ceases accepting interaction.

## Accessible double doorway

The 2F refuge opening is widened from 1.5m to 2.4m, including adjacent wall colliders and finishes. Right A on either leaf toggles both leaves. Doors remain physical blockers while closed or moving. Escort landing orientation follows teleport travel, with a validated prior-heading fallback near corridor walls; sideways gaze no longer displaces the chair into the jamb. Diagonal refuge crossings can use a supported, collision-checked path through the real doorway centre at X=4.1, Z=-1.5. No wall/door collision checks are disabled.

Both leaves open inward to avoid obstructing corridor wheelchair approach. Verified: 12 approach/gaze cases, closed-door blocking, A-linked opening, and full six-node route regression.
