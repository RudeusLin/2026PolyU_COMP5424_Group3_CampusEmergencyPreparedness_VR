# COMP5424 Group 3 Weekly Activity Record (Draft)

Project: Campus Emergency Preparedness / Campus_XR

Reporting period: 2026-09-21 to 2026-10-18

Compiled on: 2026-10-18

Repository: https://github.com/RudeusLin/2026PolyU_COMP5424_Group3_CampusEmergencyPreparedness_VR

## Week 1: 2026-09-21 to 09-27

**Objective:** Establish the team repository and project description.

**Activities and outcomes:**

- On 09-23, created the project repository and revised the README several times to add the project title and team information.
- Established version control for subsequent Unity prototype development.

**Evidence:** `c5cef99` (Initial commit), `fa1f589` (Revise README with new title and team details), and other README updates on the same day.

**Issues and record limitations:** Available evidence does not establish whether requirements interviews, formal design discussions, or user research were completed this week; these activities are therefore not recorded as completed.

**Next steps:** Import the Unity project and develop prototypes of the campus and evacuation routes.

## Week 2: 2026-09-28 to 10-04

**Objective:** Turn the project description into a viewable campus environment prototype.

**Activities and outcomes:**

- On 09-29, committed the Campus1 base project, fixed the Unity version at 2022.3.62f3, and synchronised repository history.
- On 10-02, added a four-storey teaching building and a refined evacuation scene, establishing a visual foundation for the campus layout and evacuation routes.
- Retained the prototype and refined scenes, together with previews and instructions, to support comparison and iteration.

**Evidence:** `d4e417e` (Unity base project), `300bc0d` (history merge), and `74ba2b5` (four-storey teaching building and refined scene).

**Issues and record limitations:** These commits demonstrate the creation of an environment prototype, but do not establish that Quest 3 interactions, the final executable, or user evaluation were complete.

**Next steps (inferred from the following week's records):** Improve stair traversal, character animation, and facility interactions, and migrate the desktop experience to XR.

## Week 3: 2026-10-05 to 10-11

**Objective:** Improve campus interactions and establish the basic Quest 3 PC VR experience.

**Activities and outcomes:**

- On 10-05, organised project files and obsolete assets, fixed stair traversal, and added an animated campus character.
- On 10-07, uploaded session infrastructure, including PracticeSession, PracticeSessionController, SessionActionRelay, and SessionActionZone. This represents framework development, not completion of an end-to-end evaluation workflow.
- On 10-10, improved campus models, facility layouts, character movement, and first-person hands; synchronised Campus1 updates into Campus_XR while retaining the XR configuration.
- Following decisions recorded in the chat, retained Unity 2022.3.62f3 and adopted Windows + Quest Link/Air Link + OpenXR for PC VR.
- Implemented ray-based teleportation, turning, and shared facility interactions; aligned the initial spawn with the existing F4 spawn in fourth-floor classroom 401.
- Fixed invisible controllers and rays, tracking initialisation, and input focus issues, and established the right trigger as the teleport confirmation button.
- Added XR quick-start and controller instructions; the main branch introduced indoor fire effects as targets for the next stage of interaction development.

**Validation and feedback:**

- Project documents record Editor and simulated-controller checks; the project owner subsequently confirmed in the chat that teleportation worked.
- This feedback supports basic teleport functionality, but does not establish that all interactions, headset performance, or target classroom PCs were validated.

**Evidence:** `1841d37`, `2b64132`, `9ab63ee`, `0c68802`, `bc2bacb`, `e2f6101`, and `4bace1e`; `Docs/Campus1Sync_20261010.md`, `Docs/QuestControllerFix_20261011.md`, and `Docs/ControllerValidation.txt`. Some fixes were committed together on 10-11; individual implementation dates are not inferred.

**Issues and resolution:** Migration from desktop movement to XR exposed spawn, tracking, and input-visibility issues. These were addressed through spawn alignment, OpenXR session-focus checks, and input-binding adjustments.

**Next steps:** Implement the extinguisher workflow while retaining teleportation, and improve towel interaction, status prompts, and route guidance.

## Week 4: 2026-10-12 to 10-18

**Objective:** Refine key interactions, integrate visual guidance and effects, and prepare a deliverable prototype.

**Activities and outcomes:**

- On 10-12, synchronised fire effects from main into Campus_XR, preserved existing locomotion, and established a rollback baseline for successive extinguisher interaction iterations.
- Implemented the sequence: pick up the extinguisher → remove the safety pin → aim at the base of the fire → spray continuously. A fire is extinguished immediately after three seconds of continuous hits on the same target; interruption or switching targets resets the timer.
- Used the right-hand B button to switch between Action and Spray modes, allowing teleportation while holding the extinguisher. The right trigger teleports or sprays according to the selected mode.
- Iterated on fire blocking and hit detection: initially tested solid boxes and a target band approximately one metre high at the bottom, then addressed interference between floors in response to user feedback. The final approach removed physical fire collisions, used fire regions that restrict only teleport rays, and restored targeting at the fire base. These were successive alternatives, not simultaneous final features.
- Enabled teleport targeting on the outdoor ground; improved HUD spacing and position and added a visibility toggle.
- Moved the towel to the left hand and added a pose that raises it near the face while avoiding the central view; the user confirmed in the chat that this version worked.
- On 10-13 to 10-14, integrated UI updates, a directional arrow, and a target beacon to help users locate the current objective.
- On 10-17, integrated smoke effects on the fourth floor and at other locations, and removed building structures that caused ground flickering.
- Revised the Phase 2 report as recorded in the chat, adding guidance features, the purpose of visual effects, and evaluation priorities; verified that the exported main text occupied four pages. Report edits have no corresponding Git commit, so a separate completion date is not inferred.
- Built the Windows x64 player locally and fixed compilation errors caused by two Editor-only API calls; the build succeeded and a ZIP was generated. These fixes were committed on 10-18.
- Removed two legacy scenes and their .meta files while retaining XR, Campus, and shared assets; rechecked dependencies, rebuilt and packaged the project, and pushed the current version to main on 10-18.

**Validation and feedback:**

- Extinguisher, towel, and teleportation documents contain simulated-device or Editor validation; the chat records repeated user feedback and requests for further corrections.
- Before cleanup, checked 779 recursive dependencies across the two retained scenes; hashes of 769 retained asset files remained unchanged after cleanup.
- The post-cleanup Windows build succeeded. The latest ZIP was approximately 52.45 MiB, with 155 files passing integrity checks.
- This evidence does not establish completion of PQ604 hardware performance testing, the full headset workflow, or formal user evaluation.

**Key evidence:**

- `e95be57`: extinguisher workflow, three-second suppression, and B-button mode switching.
- `74daa09`, `db4790b`, `a28efa1`, and `c03ed02`: iterations on fire-region blocking, aiming, and cross-floor interference.
- `7a7dd8d`, `113dd8e`, and `5972989`: HUD, left-hand towel interaction, and control documentation.
- `34b1cbe` and `49072eb`: UI and directional guidance.
- `67f2e7f`, `49476bf`, and `3c058a6`: smoke integration and flickering fixes.
- `3b623b7` and `d3bbb7f`: Windows build fixes, legacy scene cleanup, and the main update.
- `Docs/ExtinguisherWorkflow.md`, `Docs/XRTowelValidation.txt`, and `Docs/LegacySceneCleanup.md`; local build logs and packaging verification results recorded in the chat.

**Recommended next steps (not yet completed):** On the target PCs in PQ604A/B/C with Quest 3, verify startup, language selection, teleportation, extinguisher and towel interactions, the evacuation endpoint, and exit/restart. Then observe users, focusing on route decisions, mode switching, prompt readability, reliance on guidance, and comfort, and record issues and improvement priorities.

## Commit author references (for team verification)

| Name | Git author identifier | Main areas covered by visible commits |
| --- | --- | --- |
| Lin Zhiwei | RudeusLin | Developed XR movement and item interactions, integrated team updates, and prepared documentation and builds. |
| Mo Jiangwei | MoooJianG | Built and refined campus models, improved stair traversal, and developed character animation and first-person hands. |
| Liu Tianran | LTR132322 | Added session workflow scripts, including controllers, action relays and zones, and organised related project files. |
| Li Yisheng | LiYS-08 | Developed interface screens and objective guidance, including the directional arrow and beacon for navigating the campus. |
| Fu Jiahao | fjh | Added fire and smoke effects, integrated smoke into the XR scene, and corrected ground-flickering issues. |

## Accessing the evidence

Commit URL format: `https://github.com/RudeusLin/2026PolyU_COMP5424_Group3_CampusEmergencyPreparedness_VR/commit/<commit-hash>`.

Version covered by this summary: [main commit d3bbb7f](https://github.com/RudeusLin/2026PolyU_COMP5424_Group3_CampusEmergencyPreparedness_VR/commit/d3bbb7f). Earlier control schemes and historical validation conclusions in project documents apply only to their respective versions and should not be treated as current behaviour or the latest test results.
