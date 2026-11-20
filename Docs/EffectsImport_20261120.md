# Effects integration — 20 November 2026

Source: origin/Effects at 4bad4860a73045cc87137ed29d0a929fa8641f21 (a19a335 smoke/fire layout; 4bad486 audio), author fjh.

Imported 16 changed files: ceiling smoke on all floors, reduced existing smoke density, staircase fire adjustment, B-side scene fires, one-shot spatial alarm at 12 locations, clustered fire audio and tap-water audio. XR scene merged by object IDs; all pre-existing local scene changes and newly added objects preserved. No branch checkout or overwrite of the local XR scene was performed.

Compatibility: CampusDrillAtmosphere.HasAlarmBeenActivated stores the alarm event separately from one-shot audio playback; CampusRouteAFlow uses the event so playback ending cannot erase alarm progress. Existing named-fire checks prevent duplicate runtime B-side fires.

Preserved: 401 rear-centre spawn, virtual hands, equipment switching and left-hand extinguisher, right-A pin interaction, ray-teleport wheelchair escort, blackboard map and route/debrief changes.

Backup: E:/0_PolyU/COMP5424_XR/Project/5_Phase3/Backups/BeforeEffects_20261120/local_before_effects.zip, manifest.json, local.patch and merge_record.json. To undo only this import, restore the backed-up versions of imported existing files plus the route/validation compatibility edits; remove only new files listed by the merge record that were absent from manifest.json. Preserve unrelated work made afterwards. No Git reset is required.

Editor regression results: see EffectsImportValidation.txt. Hardware audio balance, smoke readability and performance require Quest 3 testing.
