# Educational debrief

The review now has a six-node overview and individual illustrated detail pages. All 18 illustrations and all explanatory copy are localized in English, Simplified Chinese and Traditional Chinese. Existing right-ray A hover/confirmation feedback is reused; the entire row, including its arrow, opens the detail. Back returns to the overview. Restart reloads the exercise and Exit quits the build (stops Play Mode in Editor).

Runtime content: `CampusDebriefContent.cs`. Layout: `LanguageSelectionUI.Debrief.cs`. Startup/call/debrief dispatch remains in `LanguageSelectionUI.cs`. Images are in `Assets/Resources/CampusDebrief`. No scene geometry or gameplay checkpoint rules are changed. The previous completion status labels are omitted from the UI; internal checkpoint records remain intact.

Educational boundaries: a wet towel does not make dense smoke safe; extinguisher use depends on training, fire size and a clear retreat; three seconds is a game rule; refuge placement requires subsequent assistance; the emergency number is 999 for Hong Kong.

Copy references:
- https://www.hkfsd.gov.hk/sc/education/cep_edu/cep/building_ep/
- https://www.119.gov.cn/kp/zjts/2022/180.shtml
- https://www.hkfsd.gov.hk/eng/source/safety/what_to_do.pdf
- https://www.usfa.fema.gov/prevention/home-fires/prepare-for-fire/fire-extinguishers/
- https://www.hantsfire.gov.uk/safety/the-workplace/supporting-people-with-disabilities/
- https://www.hkfsd.gov.hk/sc/fire_ambulance_services/fire_information.html

Validation menu: Campus Emergency / XR / Validate educational debrief. This exercises localized rows, details, back transitions, layout bounds, gameplay blocking and restart using Editor Play Mode. It generates actual scene-camera captures without saving scene changes. Headset readability and physical controller interaction require device verification.
