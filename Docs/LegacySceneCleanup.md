# Legacy scene cleanup

Removed only these scene files and their .meta files:
- Assets/Scenes/TeachingBuilding.unity
- Assets/Scenes/TeachingBuildingRefined.unity

Retained TeachingBuildingCampusXR.unity and TeachingBuildingCampus.unity. Unity AssetDatabase.GetDependencies checked 779 recursive dependencies across both retained scenes before deletion, with no dependency on either removed scene. All existing shared models, materials, textures, prefabs, lighting assets and scene lighting directories are retained. XR scene rebuilding and desktop validation still require the Campus scene.

Legacy rebuild menus are grouped under Campus Emergency/Legacy. Legacy render/offline checks now explain how to restore a missing scene. Generators and shared helpers are retained because Campus uses them. Historical instructions and earlier cleanup reports describe their original snapshots, not the current scene inventory.

Restore the exact removed scenes and GUIDs:
```powershell
git restore --source=3c058a6 -- Assets/Scenes/TeachingBuilding.unity Assets/Scenes/TeachingBuilding.unity.meta Assets/Scenes/TeachingBuildingRefined.unity Assets/Scenes/TeachingBuildingRefined.unity.meta
```

A local backup ZIP is also stored outside the Unity project in Project/Builds/LegacyScenes_BeforeCleanup.zip. The two Windows build compatibility fixes made before cleanup remain unchanged.

Post-cleanup validation: Unity recursive dependency audit passed again; Windows x64 XR build succeeded with exit code 0. All 769 retained Assets dependency files were byte-identical before and after cleanup. Temporary audit scripts were removed after validation. Headset runtime testing was not repeated.
