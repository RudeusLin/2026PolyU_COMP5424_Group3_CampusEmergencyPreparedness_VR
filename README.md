# COMP5424 Group 3 — Campus Emergency Preparedness

A VR fire evacuation preparedness practice prototype for international students at fictional Harbourview University. The experience supports practice rather than formal assessment and does not replace official emergency training.

## Campus_XR (XR branch)

Open `Assets/Scenes/TeachingBuildingCampusXR.unity` in Unity **2022.3.62f3**. This version runs on Windows with Quest 3 through Quest Link or Air Link and OpenXR. See [PC VR setup](Docs/Quest3PCVRSetup.md).

The player starts at the fourth-floor classroom 401 spawn. Aim the right-hand ray at a valid green destination and press the **right trigger** to teleport. **A** interacts with doors, towels and taps; the **right stick** turns by 30 degrees; the **right grip** returns the towel; holding the **left grip** suspends teleport aiming. See [all controls](Docs/XRControls.md) and [locomotion implementation](Docs/XRLocomotion.md).

Editor and simulated-controller validation reports are in `Docs`. The current teleport behavior has also been confirmed working by the project owner on the headset.

## Current campus scene

Open `Assets/Scenes/TeachingBuildingCampus.unity` in Unity **2022.3.62f3**. This is the current campus environment, with scanned PBR surfaces, HDRI daylight, classroom furniture, physical signage, noticeboards, an articulated student and an optional localized smoke/alarm preview. Press **F4/F2** to walk, **Shift** to run, **Space** to jump and **V** to switch first/third person. Movement accelerates and brakes smoothly, with distinct walk/run, airborne and landing poses. First person shows animated sleeves and detailed hands; held towels follow the right hand in either view. Both evacuation routes support tested desktop stair ascent/descent; headset performance remains unverified.

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
