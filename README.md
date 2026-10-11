# COMP5424 Group 3 — Campus Emergency Preparedness

A VR fire evacuation preparedness practice prototype for international students at fictional Harbourview University. The experience supports practice rather than formal assessment and does not replace official emergency training.

## Campus_XR (XR branch)

Open `Assets/Scenes/TeachingBuildingCampusXR.unity` in Unity **2022.3.62f3**. This version runs on Windows with Quest 3 through Quest Link or Air Link and OpenXR. See [PC VR setup](Docs/Quest3PCVRSetup.md).

The player starts at the fourth-floor classroom 401 spawn. Aim the right-hand ray at a valid green destination and press the **right trigger** to teleport. **A** interacts with doors, towels and taps; the **right stick** turns by 30 degrees; the **right grip** returns the towel; holding the **left grip** suspends teleport aiming. See [all controls](Docs/XRControls.md) and [locomotion implementation](Docs/XRLocomotion.md).

Editor and simulated-controller validation reports are in `Docs`. The current teleport behavior has also been confirmed working by the project owner on the headset.

### Quick start

1. Check out the `XR` branch and open the project in Unity **2022.3.62f3**. Allow Unity to restore the packages recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.
2. Connect Quest 3 through Quest Link or Air Link and enter the Link environment. Use Meta Quest Link as the active OpenXR runtime.
3. Open `Assets/Scenes/TeachingBuildingCampusXR.unity` and enter Play mode. This is also the enabled build scene for the Windows XR version.
4. The player starts at classroom 401 on the fourth floor; no keyboard shortcut is required.

This project targets Windows PC VR, not a standalone Android build. The PC VR setup document records the initial dependency-installation stage; its earlier scene-implementation limitations have since been superseded by the current XR scene.

### Quest 3 controls

| Input | Action |
| --- | --- |
| Aim the right controller | Point the teleport ray at a destination |
| Right trigger | Press to teleport to a green destination; holding does not repeat and releasing does not teleport |
| Right A button | Interact with doors, towels and taps; holding displays the short-range interaction ray |
| Right stick left/right | Turn by 30 degrees; return to center before turning again |
| Right grip | Return the held towel to its original counter |
| Left grip | Hold to suspend teleport aiming; release to resume |
| Head movement | Look around using headset tracking |

Green indicates a valid landing point; red indicates an invalid target. Teleportation checks obstructions, support and headroom, with a maximum ray distance of 12 metres and a maximum height difference of 4 metres. After tracking or application focus is restored, release the trigger before pressing it again. Holding the trigger pauses turning and towel return. Left stick, left trigger and B/X/Y have no campus gameplay bindings; system buttons retain their normal functions.

## Current campus scene (desktop controls)

Open `Assets/Scenes/TeachingBuildingCampus.unity` in Unity **2022.3.62f3**. This is the current campus environment, with scanned PBR surfaces, HDRI daylight, classroom furniture, physical signage, noticeboards, an articulated student and an optional localized smoke/alarm preview. Press **F4/F2** to walk, **Shift** to run, **Space** to jump and **V** to switch first/third person. Movement accelerates and brakes smoothly, with distinct walk/run, airborne and landing poses. First person shows animated sleeves and detailed hands; held towels follow the right hand in either view. Both evacuation routes support tested desktop stair ascent/descent. The keyboard controls below apply to the desktop scene; use the XR scene and controller mapping above for Quest 3. Headset performance has not been benchmarked.

The fourth-floor WC is now opposite classroom 401, with room 403 in the former WC space. All four floors have a basin, mirror, working tap and pickup towel, plus a signed refuge room with a distinct door finish. Press **E** to pick up a towel and use the tap to wet it; **R** restores the towel and turns off all taps. The standalone viewer includes washroom and refuge-room views. See `CampusModelingValidation.txt` and `CampusModelingGeneratorValidation.txt` in `Docs/Previews/Campus` for the corresponding checks.

Open [the standalone campus preview](Docs/Previews/Campus/TeachingBuildingCampusPreview.html) without Unity, or read [campus scene instructions](Docs/TeachingBuildingCampus.md). Current Unity captures and validation reports are in `Docs/Previews/Campus`; earlier material comparisons are in `Docs/Previews/Campus/History`.

## Earlier scene versions

- **Prototype:** `Assets/Scenes/TeachingBuilding.unity`; [instructions](Docs/TeachingBuildingPrototype.md), [browser preview](Docs/Previews/TeachingBuildingPreview.html).
- **Refined:** `Assets/Scenes/TeachingBuildingRefined.unity`; [instructions](Docs/TeachingBuildingRefined.md), [rendering fixes](Docs/TeachingBuildingRenderingAudit.md), [browser preview](Docs/Previews/Refined/TeachingBuildingRefinedPreview.html).

These versions remain available for iteration history. Campus still shares textures and generation helpers with Refined. Keep the scene lighting folders and the shared assets when distributing the Unity project.

## Project maintenance

See [file organization and cleanup](Docs/ProjectFileAudit.md) and [the cleanup record](Docs/ProjectCleanupCandidates.json). Unity caches, logs and generated IDE project files are excluded by `.gitignore`.

## Team Members

|     Name     |  StudentID  | GitHub Account |
| --- | --- | --- |
| Lin Zhiwei   |  26105448g  | RudeusLin      |  
| Fu  Jiahao   |  26062063g  | FJH-123123     |  
| Li  Yisheng  |  26124143g  | LiYS-08        |  
| Liu Tianran  |  26039772g  | LTR132322      |  
| Mo  Jiangwei |  26104251g  | MoooJianG      |
