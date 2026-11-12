> Historical documentation: the Prototype and Refined .unity files were removed from the active checkout. Restore them from commit 3c058a6 or explicitly regenerate them before following scene-opening or validation steps. Shared assets remain available. See [cleanup record](LegacySceneCleanup.md).

# 四层教学楼原型使用说明

已提供加入原创纹理、倒角模型和室内细节的 `TeachingBuildingRefined.unity`，详见[细化版说明](TeachingBuildingRefined.md)。本文说明初版场景的布局与操作。

## 先看离线三维预览

用 Edge 或 Chrome 打开 `Docs/Previews/TeachingBuildingPreview.html`。这个 HTML 文件内含全部布局数据，无需 Unity、网络或额外素材。

- 鼠标拖动旋转，滚轮缩放，双击恢复当前视角。
- 查看建筑总览、1F 至 4F 的独立布局、四楼电梯厅和楼梯间。
- 隐藏屋顶，或显示从四楼通向集合点的两条疏散路线。

预览和 Unity 场景来自同一份布局数据；浏览器采用简化材质与光照，预览图片不是 Unity 渲染截图。

当前已在 Unity 2022.3.62f3 的独立验证工程中完成真实渲染、文字遮挡与两条疏散路线的物理检查；尚未接入或测试 VR 头显。

## 打开与查看

1. 用 Unity **2022.3.62f3** 打开工程。
2. 打开 `Assets/Scenes/TeachingBuilding.unity`。
3. 点击 Play，默认显示建筑总览。
4. 点击 `Enter fourth floor`，或按 F4，从四楼 401 开始漫游。

总览视角可以用鼠标右键拖动旋转、滚轮缩放。按 C 隐藏屋顶，查看四楼内部布局。

如果正在查看修复前的版本，先停止 Play，在 Project 面板刷新资源并等待编译完成，再点击 Play。场景文字由 `PrototypeSceneBootstrap` 统一初始化：共用支持墙壁遮挡的字体材质，将布局中的世界字号换算为 Unity TextMesh 尺寸，并记录转换状态，避免重新加载时重复缩小。原场景会自动应用修复，无需重建建筑。

## 漫游操作

| 操作 | 按键 |
| --- | --- |
| 移动 | W / A / S / D |
| 看向不同方向 | 鼠标移动 |
| 加快移动 | 左 Shift |
| 开关门、查看设施提示 | 看向门或按钮，按 E |
| 切换到总览 | Tab |
| 从二楼 201 开始 | F2 |
| 从四楼 401 开始 | F4 |
| 返回当前起点 | R |
| 释放鼠标 | Esc；点击 Game 视图重新锁定 |
| 切换烟雾示意 | H |

楼梯入口的防火门和一楼侧出口默认关闭，需要靠近并按 E 打开。教学楼起点教室的门默认打开，方便先检查路线。到达室外绿色集合区域后，显示漫游完成提示。

## 原型内容

- 四层教学楼，层高 3.6 米，内部布局、教室门牌和楼层标识可辨认。
- 两座从四楼连续通向一楼的双跑楼梯，包含平台、扶手和台阶边缘标记。
- 一楼主入口、两个楼梯侧出口、室外通道、集合点、树木、长椅和花池。
- 全楼一套电梯井和轿厢，以及四套电梯厅门、显示面板、呼梯按钮和停用标识。
- 每层卫生间，包含入口标识、基础隔间、便器、洗手区和镜面示意。
- 每层两组消防器材点和手动报警按钮，配有应急灯、烟感和疏散导向。
- 201、401 为重点教室；其他教室提供基础布置。

## 当前交互范围

这是场景原型，提供电脑端布局检查与漫游。VR 头显、传送移动、正式练习判定、计分与真实火灾效果尚未接入。

普通电梯在火灾练习中停用；查看按钮会解释停用原因并提示使用楼梯。当前不提供正常呼梯、开门或乘梯运行。

报警按钮和消防器材提供识别提示，便于确认交互位置；它们还未触发报警系统或灭火逻辑。H 键烟雾仅为视觉示意，未限制路线、造成伤害或触发练习判定。

## 场景组织与复用

- `Building/Floor_01` 至 `Floor_04`：楼板、房间、候梯区、卫生间和每层设施。
- `Building/Stairwell_A`、`Stairwell_B`：独立的连续楼梯结构。
- `Building/Elevator`：一套井道与轿厢，不随楼层重复创建。
- `Building/Exterior/Roof_CutawayToggle`：用于总览剖开屋顶。
- `GameplayAnchors`：玩家起点、危险区域、出口、电梯、报警和器材的位置标记。
- `Preview`：电脑端总览相机和玩家。接入正式 VR Rig 时禁用整个 `Preview` 节点，并检查正式 Rig 的碰撞和设施交互。

布局生成器集中维护尺寸与重复构件，课桌椅和消防器材另存为可复用 Prefab，材质按用途复用。当前场景中的构件为展开对象，方便直接编辑。门交互、设施提示和漫游控制分别承担各自职责。

## 重新生成

Unity 菜单：`Campus Emergency > Build teaching building prototype`。

菜单从 `Assets/Environment/TeachingBuildingLayout.json` 重建场景与 Prefab，并在有效许可证的编辑器中执行几何检查、输出 `Unity-*.png` 截图。它会替换对生成场景和 Prefab 所做的手动调整；需要保留时先另存副本。

布局生成器源码位于 `Tools/generate_teaching_building.py`，Unity 导入器位于 `Assets/Editor/TeachingBuildingBuilder.cs`。仅使用 Unity 自带模型和现有渲染管线，不需要下载美术资源或添加 Unity 依赖包。

在工程根目录运行 `python "Tools/generate_teaching_building.py"`，可重新导出 JSON、Unity 场景、材质、Prefab 和离线 HTML。此命令会覆盖生成资源，保留手工修改前请另存副本。

## 预览与验证

生成完成后，`Docs/Previews` 中包含：

- `TeachingBuilding-Exterior.png`：建筑外观与集合场地。
- `TeachingBuilding-FourthFloor.png`：隐藏屋顶后的四楼布局。
- `TeachingBuilding-LiftLobby.png`：四楼电梯厅与消防设施。
- `TeachingBuilding-Stairwell.png`：四楼楼梯平台与下行通道。
- `TeachingBuildingPreview.html`：可独立打开的三维预览。
- `PrototypeValidation.txt`：离线场景引用检查、C# 编译和路线几何检查结果。
- `Unity-Exterior.png`、`Unity-FourthFloor.png`、`Unity-LiftLobby.png`、`Unity-Stairwell.png`：Unity 实际渲染截图。
- `UnityValidation.txt`：Unity 内两条路线的物理采样结果。
- `WorldTextValidation.txt`：文字尺寸、遮挡、场景重新加载和字体图集更新的实际验证结果。

离线检查覆盖 401 起点经 A/B 两座楼梯分别到集合点的地面支撑和玩家胶囊通行空间采样，沿路线每约 0.2 米检查一次，按门已打开的状态检查。A/B 路线分别通过 718/797 个采样点。脚本已使用本机 Unity 2022.3 的程序集完成 C# 编译检查。

验证脚本：`Tools/verify_teaching_building.py`，依赖 Python 和 NumPy。它检查场景对象、内部引用、资源 GUID、设施数量及几何路线，结果仅代表离线检查。

2026-10-02 的 Unity 批处理验证覆盖原始导出场景与 Unity 导入器重建场景：96 个文字对象使用世界字体材质，所有标牌文字均处于面板范围内；重复初始化及保存后重新加载保持字号一致；字体图集重建后材质引用同步。遮挡实验中，可见文字正常渲染，被不透明墙壁挡住后相对仅有墙壁的画面产生 0 个差异像素。A/B 两条路线也通过了 Unity Raycast 与 OverlapCapsule 的采样检查。以上不代表完整玩家操作或实际头显测试；VR 性能与移动仍待验证。
