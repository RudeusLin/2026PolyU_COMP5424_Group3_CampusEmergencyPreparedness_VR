# Current Quest 3 controls

| Controller | Button | Action |
|---|---|---|
| Right | Trigger | Teleport to valid green destination, also while carrying equipment |
| Right | A | Interact with doors, alarm, taps, items, UI and wheelchair; aim at held extinguisher pin to remove it |
| Right | Stick left/right | Snap turn 30 degrees |
| Left | X | Cycle Empty → Towel → Extinguisher → Empty, skipping items not yet acquired |
| Left | Trigger | Towel: toggle raised/lowered (wet only); extinguisher: hold to spray |
| Left | Y | Toggle help/status HUD |
| Keyboard | H | Toggle HUD |
| Both | Grip | No gameplay action |
| Right B / left stick | — | No gameplay action |

Pick up an extinguisher only with empty hands. Items equip in the left hand; X stows items without returning them. Towel wetness persists. Each time the extinguisher is re-equipped, its safety pin is restored; aim the right ray at it and press A again before spraying. Re-equipped towels start lowered. Switching items immediately cancels spray and its continuous-fire timer. Release the left trigger after switching or losing tracking/focus before spraying again. Aim the left nozzle at the same fire base continuously for 3 seconds to extinguish. Right A uses a 6.5cm aiming tolerance around the pin and cannot operate through geometry.

There is no B mode switching, proximity-grip pin pulling or item-return requirement. Fire completion unlocks descent without returning the extinguisher. Wheelchair assistance remains right-ray A start/pause, then escort to the refuge entrance and open its door.

## Wheelchair escort override

Right trigger remains ray teleport; right stick snap-turns. The active wheelchair is transparent to movement/interaction rays. Left stick does not move. A starts/pauses escort (aimed doors and other interactions take priority). Safe same-floor player/chair destinations are checked together. Equipment switching is suspended during escort; the prior item is restored after pause/delivery.

### Refuge parking confirmation

The escorted chair travels about 1.4m ahead and 0.25m to your left; narrow passages reduce the offset, and unsafe paired destinations are rejected. Looking around does not move the chair. After escorting into the refuge, aim the right interaction ray at the green floor marker or the downward arrow near the far-left corner (viewed from the doorway) and press A. The parked chair keeps approximately 0.65m and 0.77m clearance to the adjacent walls; the central approach stays open. The wheelchair parks automatically facing the refuge doorway; normal controls and the previous item are restored. Stand clear of the marker. Entering the room alone no longer completes rescue.

## Ground-floor exit fires

Three fires block each of the main entrance and side exits A/B. After the 2F rescue, the default guidance still leads to the main entrance. On approach, the route prompts X to equip the extinguisher. Remove the pin with right A, then spray each base continuously for 3 seconds. Clear all three fires at your chosen exit before teleporting through it. Clearing a side exit alone does not change the route: actually crossing outward switches the arrows and beacon to its outdoor assembly route. Reaching the assembly point then opens the simulated 999 call, even if the main-entrance fires remain. You may also travel directly to the assembly point after crossing the side exit.

## After the emergency call

The debrief includes RESTART and EXIT PRACTICE. Aim the right ray at a button and press A. RESTART reloads the current scene, clears the run, returns to the 401 spawn and asks for the language again. EXIT PRACTICE quits a built application; in Unity Editor it stops Play Mode. Both buttons retain hover and confirmation feedback and support all three languages.


Exit instructions use the same numbered teal headings, separate explanatory lines and gold keycaps as the other route cards. Equip, remove-pin and spray prompts share a fixed 1F exit-side anchor; held equipment no longer sends the pin prompt back to its 3F pickup position. The initial briefing and localized route map now include all three exit fires under step 06.

## World-space guidance

Task and doorway instructions use nearby upright walls, teal frames and an angle-dependent information cue above the text. Exterior routes use noncolliding signboards. The refuge confirmation label is on the side wall; continue to aim at the floor marker or downward arrow and press A. Startup, phone and debrief screens retain their existing controls and positions with matching frames. Y/H still toggles the status HUD.
