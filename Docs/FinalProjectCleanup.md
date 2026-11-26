# Final project cleanup — 26 November 2026

## Removed

- `Assets/Scenes/TeachingBuildingRefined` and its folder metadata: 11 orphan lighting/reflection files plus metadata, 6,390,679 bytes. The corresponding legacy scene was previously removed. No references from other Assets or ProjectSettings files were found.
- `.utmp`: local temporary screenshots, approximately 12.7 MiB.
- Regenerable Unity Library/Temp/Obj/Logs caches and generated root IDE project/solution files, with the editor closed. These are excluded from Git. Reopening Unity will rebuild caches and may restore packages.

## Preserved

Both retained scenes (XR and desktop Campus), all Resources and StreamingAssets, shared generators, source meshes/textures, XR settings, third-party licensing and import sources, samples with possible authoring dependencies, documentation, questionnaires and historical deliverables. Identical texture/probe contents were not merged because distinct GUIDs and generation paths may be intentional.

## Verification

Unity AssetDatabase audit passed before and after asset cleanup: 154 roots, 1178 recursive dependencies including the temporary audit helper. Dependency sets were identical. SHA-256 checks confirmed 2354 protected source/metadata files were byte-identical. The temporary helper was then removed. A candidate list alone was not used as deletion authority: settings, generators and dynamic loading can use assets outside direct scene dependencies.

This cleanup does not claim a new headset test or executable build. Removing unused legacy files mostly reduces checkout size; Unity normally excludes non-Resources assets that are not build dependencies. Clearing Library reduces local disk usage, not runtime memory or executable size.

## Recovery

Removed asset/temporary-image backup: `E:/0_PolyU/COMP5424_XR/Project/output/FinalCleanup/RemovedFiles_Backup.zip` (outside this Unity project). Extract into the project root to restore original asset paths and GUIDs. Generated caches need no backup. The full audit, candidate inventory and duplicate-content list are alongside this ZIP. The cleanup was subsequently accepted by the project owner and included in the final source version `FINAL-20261126-R2`.
