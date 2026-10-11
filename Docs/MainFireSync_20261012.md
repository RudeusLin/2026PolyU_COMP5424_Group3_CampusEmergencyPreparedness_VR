# Main fire effects sync — 2026-10-12

Source: origin/main at 4bace1e (Add some fire effects inside the building).
Previous imported campus revision: 0c68802. XR baseline: e2f6101.

Imported CampusForecourtFire.cs and CampusFire.shader with their original .meta GUIDs. Added all nine source fire objects, with identical names, local transforms and component references, to both TeachingBuildingCampus.unity and TeachingBuildingCampusXR.unity. The existing parent transform is retained; only its child list is extended. XR rig, spawn, input and locomotion code are unchanged.

The effect creates outer/core flames, embers, base glow, smoke and a flickering point light at runtime. It uses the existing CampusSmoke shader and has no input handlers or colliders. It adds visual effects, not fire damage, blocked teleport zones or drill rules.

No conflicting changes were found in this effect delta. SceneTemplateSettings.json is already identical. .vsconfig and UpgradeLog.htm are machine/tool artifacts and were not imported. The source desktop movement serialization is checked against existing code defaults rather than replacing XR movement.

Headset appearance and frame rate with nine simultaneous effects require hardware verification. Existing headset confirmation predates this addition.

Validation: Unity 2022.3.62f3 batch compile and CampusXRSyncValidation.Validate passed (Logs/MainFireSyncValidation.log). Play Mode CampusControllerValidation.Begin passed with simulated Oculus Touch input and the real teleport provider (Logs/MainFireControllerValidation.log). Headset unavailable during automation; hardware rendering/performance not tested. Serialized-object comparison confirmed all existing scene blocks unchanged except the shared parent child list. Source movement values match existing defaults: 2.6, 4.2, 14, 18, 0.75, 20.
