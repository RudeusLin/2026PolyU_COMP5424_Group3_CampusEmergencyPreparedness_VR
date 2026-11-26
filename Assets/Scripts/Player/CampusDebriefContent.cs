namespace CampusEmergency.Player
{
    /// <summary>Localized educational copy, independent of checkpoint completion records.</summary>
    public static class CampusDebriefContent
    {
        public const int Count = 6;
        public sealed class Entry
        {
            public readonly string Title, Reason, Caution;
            public Entry(string title, string reason, string caution)
            { Title = title; Reason = reason; Caution = caution; }
        }
        static string T(string en, string sc, string tc) => LanguageSelectionUI.T(en, sc, tc);
        public static Entry Get(int node)
        {
            switch (node)
            {
                case 0: return new Entry(
                    T("01 Activate the Alarm", "01 启动火警", "01 啟動火警"),
                    T("Raising the alarm early alerts people who may not have noticed the fire, giving everyone more time to evacuate.",
                      "尽早发出警报，可以提醒尚未发现火情的人采取行动，为大家争取撤离时间。",
                      "盡早發出警報，可以提醒尚未發現火情的人採取行動，為大家爭取撤離時間。"),
                    T("Activate the alarm only when it is safe to do so. Do not approach the fire or delay your escape.\n\nEmergency services must still be notified according to the emergency procedure.",
                      "在确保自身安全的前提下启动警报，不要靠近火源或延误撤离。\n\n启动火警后，仍需按应急程序通知消防部门。",
                      "在確保自身安全的前提下啟動警報，不要靠近火源或延誤撤離。\n\n啟動火警後，仍需按應急程序通知消防部門。"));
                case 1: return new Entry(
                    T("02 Prepare a Wet Towel", "02 准备湿毛巾", "02 準備濕毛巾"),
                    T("A wet towel over the nose and mouth may filter some smoke particles. This step highlights the importance of reducing smoke exposure during evacuation.",
                      "湿毛巾捂住口鼻，可对部分烟尘起到一定过滤作用，帮助理解疏散时减少烟气接触的重要性。",
                      "濕毛巾捂住口鼻，可對部分煙塵起到一定過濾作用，幫助理解疏散時減少接觸煙氣的重要性。"),
                    T("A wet towel does not provide complete respiratory protection or make dense smoke safe to enter. Do not delay your escape to find or wet a towel.",
                      "湿毛巾不能提供全面的呼吸防护，也不能让浓烟区域变得安全。不要为了寻找或打湿毛巾而耽误逃生。",
                      "濕毛巾不能提供全面的呼吸防護，也不能讓濃煙區域變得安全。不要為了尋找或弄濕毛巾而延誤逃生。"));
                case 2: return new Entry(
                    T("03 Find Another Route", "03 避火改道", "03 避火改道"),
                    T("The shortest route is not always safe. If fire or smoke blocks a staircase, reassess the passage and look for an available alternative.",
                      "最短的路线不一定安全。发现楼梯被火焰或烟雾阻挡时，应重新判断通道情况，寻找可用的替代路线。",
                      "最短的路線不一定安全。發現樓梯被火焰或煙霧阻擋時，應重新判斷通道情況，尋找可用的替代路線。"),
                    T("Do not attempt to pass through flames or dense smoke. Check exit signs, follow staff instructions and adjust your evacuation route.",
                      "不要强行穿越火焰或浓烟。留意出口标志，遵循现场人员指引，及时调整撤离方向。",
                      "不要強行穿越火焰或濃煙。留意出口標誌，遵循現場人員指引，及時調整撤離方向。"));
                case 3: return new Entry(
                    T("04 Extinguish and Descend", "04 灭火并下楼", "04 滅火並下樓"),
                    T("Aiming at the base of the flames directs the extinguishing agent towards the burning material. This step practises removing the pin, aiming and spraying.",
                      "对准火焰根部喷射，才能让灭火剂作用于燃烧位置。本环节用于练习拔销、瞄准和喷射的基本操作。",
                      "對準火焰根部噴射，才能讓滅火劑作用於燃燒位置。本環節用於練習拔銷、瞄準和噴射的基本操作。"),
                    T("In a real emergency, consider using a suitable extinguisher only if you are trained, the fire is small and your escape route is clear. Leave immediately if the fire cannot be controlled.\n\nThe three-second spray requirement is a game rule, not a real-world extinguishing time.",
                      "真实情况下，只有受过训练、火势较小且退路畅通时，才考虑使用合适的灭火器；无法控制时立即撤离。\n\n连续喷射 3 秒是本游戏的判定规则，不代表真实灭火所需时间。",
                      "真實情況下，只有受過訓練、火勢較小且退路暢通時，才考慮使用合適的滅火器；無法控制時立即撤離。\n\n連續噴射 3 秒是本遊戲的判定規則，不代表真實滅火所需時間。"));
                case 4: return new Entry(
                    T("05 Assist the Wheelchair User", "05 护送轮椅人物", "05 護送輪椅人物"),
                    T("Evacuation must also consider people with reduced mobility. Helping someone reach a designated refuge supports their subsequent, organised evacuation.",
                      "疏散也需要考虑行动不便者的需要。协助其到达指定避难区域，可以为后续有组织的撤离创造条件。",
                      "疏散也需要考慮行動不便者的需要。協助其到達指定避難區域，可以為後續有組織的撤離創造條件。"),
                    T("Communicate before offering assistance. A refuge is a temporary waiting place. Report the person's location to responsible staff and follow the emergency plan for further assistance. Reaching the refuge does not mean the rescue is complete.",
                      "先沟通，再提供帮助。安全房是临时避难地点，安置后仍应向负责人员报告位置，并按应急计划安排后续协助，不能视为救援已经结束。",
                      "先溝通，再提供幫助。安全房是臨時避難地點，安置後仍應向負責人員報告位置，並按應急計劃安排後續協助，不能視為救援已經結束。"));
                case 5: return new Entry(
                    T("06 Evacuate and Call 999", "06 撤离与报警", "06 撤離與報警"),
                    T("Gathering at the assembly point supports headcounts and helps identify missing people. Reporting the location and conditions clearly helps emergency responders take action.",
                      "到达集合点便于清点人员、发现失联者；及时报警并提供准确位置和现场情况，有助于救援人员开展行动。",
                      "到達集合點便於清點人員、發現失聯者；及時報警並提供準確位置和現場情況，有助於救援人員採取行動。"),
                    T("From a safe location, call 999 in Hong Kong. Give the address, describe the fire and report anyone trapped. Follow the operator's instructions. Do not re-enter the building without authorisation.",
                      "在安全地点拨打香港紧急服务电话 999，说明地址、火情及是否有人被困，按接线员指示提供资料。不要擅自返回建筑。",
                      "在安全地點撥打香港緊急服務電話 999，說明地址、火情及是否有人被困，按接線員指示提供資料。不要擅自返回建築。"));
                default: throw new System.ArgumentOutOfRangeException(nameof(node));
            }
        }
        public static string ImageResource(int node)
        {
            string language = T("EN", "SC", "TC");
            return "CampusDebrief/Node" + (node + 1).ToString("00") + "_" + language;
        }
    }
}
