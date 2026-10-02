# 四层教学楼细化版

场景：`Assets/Scenes/TeachingBuildingRefined.unity`。使用 Unity 2022.3.62f3、Built-in 渲染管线。细化版包含四层布局、两座双跑楼梯、普通电梯、消防设施和室外集合点，并加入原创表面纹理、烘焙照明和金属反射。

在 Unity 中先停止 Play，刷新 Project 资源，等待导入及编译完成，然后双击细化版场景并点击 Play。F4 进入四楼 401 教室，F2 进入二楼；WASD 移动，鼠标观察，E 开门或查看设施，Tab 返回总览，C 在总览中切换屋顶与吊顶显示。

Game 窗口显示模糊时，先将顶部 Scale 调为 1×。Free Aspect 会跟随窗口尺寸，建议在分辨率下拉菜单中添加 Fixed Resolution 的 1920×1080；窗口放大后按 1× 检查，窗口较小时可以缩小显示。Scale 2× 只会放大已有像素，不能提高渲染分辨率。总览里的细窗框和小文字仍受屏幕像素数量限制，进入教室近距离查看更适合判断细节。

离线入口：`Docs/Previews/Refined/TeachingBuildingRefinedPreview.html`。用 Edge 或 Chrome 打开，可以查看建筑总览、各层布局、电梯厅、楼梯间、401 教室和四楼走廊。模型数据与纹理内嵌在 HTML 中，无需联网。浏览器光照经过简化；`Refined-*.png` 为 Unity 实际渲染截图。

| 部分 | 已完成的细化 |
| --- | --- |
| 教室 | 白板放在实墙上，课桌朝向白板；加入投影设备、白板托槽、笔记本和安全告示，重点细化 201/401 |
| 门窗 | 门扇设真正的观察窗与独立玻璃、金属框和踢脚板；窗户增加窗台及横框 |
| 走廊 | 墙裙、收口条、公告栏、楼层牌、吊顶格栅、灯具外壳及按实际尺度平铺的地砖 |
| 楼梯 | 补充侧梁、平台地面及出口指引，保持连续双跑楼梯与踏步边缘标记 |
| 电梯厅 | 补充侧门框、中央门缝、门槛、按钮边框、楼层显示与火警提示 |
| 消防设施 | 灭火器肩部、阀门、压力表、软管、底座及标签；消防柜增加门框、铰链与锁扣 |
| 外观 | 砖墙裙、转角立柱、楼层编号、入口雨棚格栅、排水管、屋顶设备；树冠分簇、地面延伸、路缘与长椅细化 |
| 模型与材质 | 81 个共享倒角网格、29 种材质、10 组原创颜色与表面细节纹理；增加细微凹凸、粗糙度变化和接缝遮蔽 |
| 照明 | 室内灯采用烘焙软阴影与间接光；太阳使用混合照明，门扇使用光照探针；五个烘焙反射探针用于室内外金属反射 |

资源集中在 `Assets/Environment/TeachingBuildingRefined/`：`Textures`、`Materials`、`Meshes` 和 `Prefabs`。可复用 Prefab 包括 `DeskAndChair`、`FireEquipmentPoint`、`WindowBay`、`ElevatorLanding`、`Classroom_Detailed`、`Stairwell_A` 和 `Stairwell_B`。

几何布局使用一份共享 JSON，Unity 和离线预览读取相同数据。重复尺寸的网格及材质共用资源，门扇和交互设施保持独立对象。纹理采用世界坐标映射，避免长墙、窄梁和地面上的纹理拉伸。

分享场景时同时保留 `Assets/Scenes/TeachingBuildingRefined/` 下的光照图和 LightingData，以及 `Assets/Environment/TeachingBuildingRefined/Lighting/` 下的天空、照明设置与反射纹理。仅复制 `.unity` 文件会缺少相关资源。修改墙体、楼板或灯光位置后，需要重新烘焙；普通门扇可以正常开关，不会被作为静态物体固定在烘焙中。

制作源文件：

- `Tools/refine_teaching_building.py`：在原布局基础上生成细化布局、原创纹理和独立离线 HTML。
- `Assets/Environment/TeachingBuildingRefinedLayout.json`：完整细化布局和资源输出路径。
- `Assets/Editor/TeachingBuildingBuilder.cs`：统一导入器，生成场景、材质、倒角网格与 Prefab，并执行路线检查、截图。
- `Assets/Editor/TeachingBuildingLighting.cs`：设置照明、光照 UV 使用方式、光照探针与反射探针，并烘焙保存。
- `Assets/Editor/TeachingBuildingRenderCheck.cs`：检查保存后的资源、文字像素颜色和墙壁遮挡。
- `Assets/Resources/ArchitecturalSurface.shader`：Built-in 管线使用的世界坐标平铺材质。
- `Assets/Scripts/Prototype/PrototypeSceneBootstrap.cs`：统一初始化文字材质及尺寸，Prefab 单独加载时也能正确显示。

修改生成器后，在工程根目录运行：

```powershell
python "Tools/refine_teaching_building.py"
```

然后执行 Unity 菜单 `Campus Emergency > Build refined teaching building`。这一步会重建细化版场景及其生成资源，并进行光照烘焙；如果已手工修改场景或 Prefab，应先另存副本。优先使用 Progressive GPU，Unity 在不支持或显存不足时可回退 CPU。1024 像素图集用于控制单张图的显存需求。Python 生成器依赖本机已有的 Pillow 与 NumPy，Unity 场景使用 Unity 内建 API。

2026-10-02 已在独立 Unity 验证工程中完成以下检查：

- C# 编译通过；细化场景及材质实际渲染通过。
- A/B 两条四楼到集合点的路线通过 Unity Raycast/OverlapCapsule 物理采样；对应离线检查通过 718/797 个采样点。
- 144 个文字对象使用支持墙壁遮挡的字体材质；所有标牌文字位于面板范围内，保存后重新加载保持尺寸。
- 倒角网格的顶点、法线、三角形朝向和边界检查通过；倒角保持在已有碰撞体范围内，并补齐切线与第二套 UV。
- 七个 Prefab 可加载，包含文字的 Prefab 不会重复缩小字号；场景重新加载后可见网格均有材质。
- 烘焙完成后直接检查 Unity 的 UV 重叠状态，最终重叠数量为零；两张光照图集和五个反射探针保存、重新加载通过。
- 文字颜色、组件禁用前后画面一致性、不透明墙后的文字零像素遮挡检查通过。
- 隐藏屋顶时同时隐藏吊顶格栅、灯具和悬挂设备；调整层级前后世界矩阵保持不变，既有烘焙照明重新加载通过。
- 离线预览的六种视角通过浏览器检查，无 JavaScript 或 WebGL 错误；透明对象按视深排序，表面纹理生成 mipmap。

验证报告为 `Docs/Previews/Refined/RefinedValidation.txt` 和 `ModelValidation.txt`；截图为同目录下的 `Refined-Exterior.png`、`Refined-FourthFloor.png`、`Refined-Classroom.png`、`Refined-Corridor.png`、`Refined-LiftLobby.png` 和 `Refined-Stairwell.png`。代码问题与修复说明见 `Docs/TeachingBuildingRenderingAudit.md`。

当前仍是场景和电脑漫游原型。普通电梯在火灾练习中停用，未实现正常乘梯运行；VR 头显移动、性能、正式计分和完整练习流程尚未验证。
