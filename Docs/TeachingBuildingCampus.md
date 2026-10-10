# 校园场景升级版

在 Unity **2022.3.62f3** 中打开 `Assets/Scenes/TeachingBuildingCampus.unity`。这是保留四层建筑和 A/B 两条疏散路线的独立美术版本，使用现有 Built-in 渲染管线。原型和细化版场景继续保留。

无需 Unity 的查看入口为 `Docs/Previews/Campus/TeachingBuildingCampusPreview.html`，可直接用 Chrome 或 Edge 打开。预览默认进入 401 教室，侧栏可切换建筑、楼层、电梯厅、楼梯间、走廊、公告栏、外窗与隔墙，以及学生角色。浏览器光照经过简化，实际 Unity 截图保存在同目录。

## 房间与洗手台修改

四楼原 403 的空间现在用作厕所，入口位于 401 对面；原厕所空间改为 403 协作房间。其他楼层的厕所位置保持原布局。厕所窗户使用磨砂玻璃，原厕所位置的 403 恢复普通玻璃。厕所隔间采用低于顶棚、与外窗分开的隔板。

四层各有一套 `WashStation`：带凹槽和排水口的洗手盆、金属水龙头、镜面与边框，以及可拿起的折叠毛巾。按 **E** 拿起毛巾，再对准水龙头按 **E** 开启水流并冲湿；已拿起的毛巾会改变颜色，界面显示 `TOWEL DRY/WET`。未拿毛巾时也能开关水龙头。手持毛巾时再次操作水龙头可关闭水流。每次按 **R** 或重新选择起点，毛巾返回原台面并恢复干燥，四层水龙头全部关闭。拾取后毛巾碰撞体停用，使用动态光照，不影响玩家通行。

各层中部南侧房间增加 `REFUGE ROOM` 门牌，门扇采用独立的金属材质并加入推杆和闭门器。四楼原本遮住入口的公告栏移到右侧实墙。这里只提供房间、门和标识，不增加避难判定或额外任务。镜面使用烘焙反射探针提供反射效果。

本次修改由 `Tools/campus_modeling.py` 维护，并已接入 Campus 完整生成流程。完整重新生成仍使用 `python Tools/upgrade_campus_scene.py`，随后在 Unity 选择 **Campus Emergency → Build campus teaching building**。`WashStation.prefab` 与 `RefugeDoor.prefab` 可复用。离线预览增加“洗手台”和“防火房间”视角；交互在 Unity Play Mode 中执行。

实际场景回归入口为 `CampusEmergency.Editor.CampusModelingValidation.Begin`，报告保存至 `Docs/Previews/Campus/CampusModelingValidation.txt`。它检查四层水龙头启停、毛巾拾取和冲湿、重复拾取限制、重置后的姿态与碰撞恢复，以及 401 到厕所、洗手台和中部房间的实际移动。

## 本次场景内容

- 1K 扫描水磨石与白橡木 PBR 材质，配合原创座椅织物、软木和已有墙面材质。
- 401 教室增加软垫座椅、桌下书架、笔记本电脑、书本、教学触控台、收纳柜、插座和绿植。
- 四楼走廊增加校园活动与安全提示公告栏、楼层识别牌、导向牌吊杆；防火门加入推杆和闭门器细节。
- 室外加入教学楼导览牌及步道灯柱，保持集合点和疏散通道。
- 可选的局部烟雾与原创模拟警报，用于观察火警氛围和标识可见性。
- 原创校园学生模型，含衣服、背包、面部和可活动的手臂、腿部；Unity 支持第一／第三人称切换。

场景细节与模拟警报由代码生成；地面、木饰面和天空使用 Poly Haven 的 CC0 扫描素材，来源、作者和校验记录位于 `Assets/Environment/TeachingBuildingCampus/SourceTextures/`。不增加 Unity Package 依赖。课程提交时应按要求声明 AI 辅助制作与个人贡献。

## 建筑结构与真实感修正

外窗不再按固定 5 m 网格跨过房间隔墙。Campus 生成器从各层隔墙的实际端点推导窗洞，把教室、卫生间和楼梯间隔墙接到连续的实心窗间墙；电梯后部服务空间使用实墙。外墙总厚度 400 mm，玻璃逐块停在铝合金框内，窗洞设侧壁收口和石材窗台，室内设独立墙裙、踢脚线、升起的卷帘和开启把手。卫生间使用磨砂玻璃。

401 教室增加墙上时钟及空调风口，白板改为课程演练内容，插座和线槽贴合墙面。原有门口、课桌间通道及 A/B 路线保持可通行。浏览器新增 **08 外窗与隔墙** 视角，Unity 新增 `Campus-WindowJunction.png` 和 `Campus-ClassroomWindows.png`，方便从室外和室内检查窗墙接口。

生成器会逐处检查隔墙端部的实体墙接触和玻璃避让。相同检查可复现 Refined 布局的旧问题，并在当前 Campus 布局检查 47 个连接点；四楼两块厕所隔间墙已改为不接外窗的矮隔板，因此不再计入结构墙接口。Unity Play Mode 验证还会针对保存后的实际碰撞体重新检查这 47 处连接，避免只检查 JSON 而遗漏场景导入问题。

## 扫描材质与自然光

地面使用 [Terrazzo Tiles](https://polyhaven.com/a/terrazzo_tiles)，桌面、木门及橱柜饰面使用 [White Oak Veneer](https://polyhaven.com/a/white_oak_veneer)，保留扫描色彩与物理纹理比例。Unity 使用 1024 px 底色与线性 AO / Height / Smoothness 贴图；石材和木材均为非金属，地面保留适度抛光反射，木饰面较哑光。现有 Standard 光照模型通过 ArchitecturalSurface Shader 采样凹凸细节。

天空使用 [Kloofendal 48d Partly Cloudy](https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky) HDRI。阳光照进教室后窗，室内灯减弱到原强度的一半；Campus 专属照明配置增加间接反弹和采样，并为 401 增加独立的盒投射反射探针。其他场景继续使用原配置。

浏览器同步使用扫描底色和缓存的 2048 px 太阳阴影，**太阳阴影**复选框可比较开关效果。阴影仅在建筑剖切状态改变时重建，旋转和缩放复用缓存。浏览器光照和反射为近似效果；Unity 实际渲染截图包含完整 PBR、HDRI、烘焙照明和室内反射，是视觉检查的依据。

## 标牌与预览界面

Campus 标牌使用独立 UV 的透明印字贴图和哑光 Standard 材质，接受场景光照；不再依赖无光照的动态字体图集。普通房号门牌为 480 × 220 mm，安装高度 1.6 m；本次调整的 WC、403 和防火房间门牌采用 1280 × 360 mm 牌面，其中防火房间标识移到门洞上方。消防设备标签、纸质消防行动告示和电梯警告按各自用途排版。文字与牌面保留约 2 mm 间距，并检查完整文字区域不越出牌面。重要出口与消防标识保留。白板用手写风格列出三步演练提纲，房号只放在入口。公告栏使用独立钉住、略微倾斜的纸张，正文左对齐。活动内容为场景示例。

浏览器界面为不透明、可收起的紧凑工具面板，提供十一个视角、四层选择、屋顶剖切、路线、阴影和烟雾开关。窄于 600 px 时默认收起，选择视角或楼层后自动收起；Esc 可收起面板。场景主体不再透过侧栏与文字叠加。

## 楼梯通行与学生角色

旧楼梯把每个踏步做成从楼梯底部向上填充的实心块，最大厚度 1.8 m。下楼进入三楼平台后的下一段楼梯时，上层踏步压缩净空，CharacterController 的跨步移动会被阻挡。现在踏板统一为 150 mm 厚，保留原有踏面标高、150 mm 步高、300 mm 踏面深度、扶手、支撑梁和平台；A/B 两条疏散路线不变。

`CampusStudent` 是约 1.74 m 高的校园学生模型，衣服、背包、头发、面部和鞋子由共享布局生成，保存为 `Assets/Environment/TeachingBuildingCampus/Prefabs/CampusStudent.prefab`。`CampusStudentAvatar` 按实际移动速度驱动四肢，停止时回到站立姿态。模型没有额外碰撞体，通行仍由父物体上 1.8 m 高、半径 0.28 m 的 CharacterController 控制。

Play 后按 **F4** 或 **F2** 进入步行，按 **V** 在第一、第三人称之间切换；**WASD** 移动、左右 **Shift** 加速、**空格** 跳跃、**E** 交互。步行速度为 2.6 m/s，跑步速度为 4.2 m/s，起步与停止逐渐加速／减速，斜向移动不会额外加速。跳跃目标高度为 0.75 m，支持短暂的离地容错和落地前输入缓冲；空中不能连续跳跃，撞到顶棚会停止上升并下落。

人物动作区分站立、步行、跑步、腾空和落地，速度驱动摆臂、屈膝、身体倾斜与动作过渡。手部在运行时补充四根手指的关节、指尖和拇指细节。第一人称隐藏头部和躯干，同时显示独立的袖口与双手；双手复用人物原有网格、材质和手部细节，随步行、跑步、跳跃及落地轻微摆动，镜头本身不增加晃动。靠近墙面时双手回缩，不增加碰撞体。

拿起毛巾后，第一人称把毛巾放在右手，第三人称把毛巾挂到人物右手，切换视角时同步转移；手持时折叠毛巾适当缩小，重置后恢复台面的原始位置、旋转和大小。第三人称跟随镜头用球形检测避开墙壁，距离过近时暂时隐藏模型。总览模式中也能看到人物。人物与双手使用动态光照探针，不把可动模型烘焙成固定建筑阴影。

移动与手部验证入口为 Unity 菜单 **Campus Emergency → Validate campus movement and hands**，报告为 `Docs/Previews/Campus/CampusMovementValidation.txt`。第一人称双手、手持毛巾和第三人称跑步／跳跃截图保存在同目录。浏览器预览继续用于查看建筑；键盘移动、跳跃和手部动作在 Unity Play Mode 中体验。

真实移动回归入口为 `CampusEmergency.Editor.CampusWalkthroughValidation.Begin`，报告保存到 `Docs/Previews/Campus/CampusWalkthroughValidation.txt`。它使用与键盘输入相同的移动方法，逐点走完 A/B 路线，上下楼分别检查步行、加速及 30 Hz 下降；门在测试中临时打开。浏览器是场景查看器，通行验证在 Unity 执行。头显输入和 VR 实机性能仍需单独验证。

`campus_signage.py` 负责牌面与文字贴图，`campus_preview_ui.py` 负责 Campus 独立样式。生成文字优先读取 `CAMPUS_FONT_DIR` 或 macOS Supplemental 中的 Arial.ttf 和 Chalkboard.ttc；Windows 自动使用系统 Arial 和 Segoe Print，Linux 可使用 DejaVu Sans。不同字体的印字尺寸会重新适配牌面。已生成的 PNG 随项目保存，Unity 与浏览器查看不依赖这些本机字体。

## 操作

Play 后按 **F4** 进入四楼，WASD 移动，Shift 加速，空格跳跃，鼠标观察，E 开门或查看设施；V 切换视角，Tab 返回总览，C 在总览中显示／隐藏屋顶，R 返回起点并重置交互和动作。

**H** 开关局部火警氛围；开启后 **M** 静音／恢复模拟警报，同时显示文字提示。默认关闭，不自动播放警报。Unity 运行时使用最多 48 个柔边烟雾粒子；浏览器用静态半透明形体示意烟雾，不播放声音。开关关闭时粒子与音频停止。

该模块是环境预览。烟雾不阻断路线，警报不启动正式演练流程，也没有火灾传播、灭火操作或完成判定。正式 XR Rig、舒适移动、头显帧率及立体渲染仍需实机验证。

## 修改与重新生成

`Tools/campus_student.py` 维护薄踏板与人物的共享几何。在 Python 重新生成布局后，可以从 Unity 菜单选择 **Campus Emergency → Update campus stairs and student**，直接更新现有 Campus 场景的楼梯和人物并重新烘焙，保留其他场景对象编号。完整重建入口仍可使用。

旧材质对比图集中在 `Docs/Previews/Campus/History/`。文件清理记录见 `Docs/ProjectFileAudit.md`。生成器只创建仍在使用的织物、软木和烟雾纹理；石材与木纹直接使用扫描资源。Unity 构建只保存被几何使用的材质，反射图由场景光照烘焙统一保存到场景光照目录。

`Tools/upgrade_campus_scene.py` 读取已保存的 `TeachingBuildingRefinedLayout.json`，输出独立的 Campus 布局、原创纹理、已随项目保存的扫描材质、模拟警报和离线预览。`import_campus_pbr.py` 校验原始文件，并将 AO、凹凸高度和逆粗糙度打包为线性细节贴图；重新生成不需要联网。脚本需要 Python、NumPy 和 Pillow。

```shell
python Tools/upgrade_campus_scene.py
```

随后在 Unity 中选择 **Campus Emergency → Build campus teaching building**，导入布局、重建 Campus 场景、保存复用 Prefab、检查路线并烘焙照明。重建会替换这个版本的生成资源；手工修改请另存场景或 Prefab。该操作不会重建原型或 Refined 场景。

生成资源集中在 `Assets/Environment/TeachingBuildingCampus/`。分享场景时应保留完整项目，包括 Campus 与 Refined 资源、`Assets/Scenes/TeachingBuildingCampus/` 中的烘焙数据，以及现有脚本和 Resources Shader；Campus 的墙面等材质仍复用部分 Refined 纹理。

## 验证记录

离线布局检查保留了原有路线定义，A/B 路线分别通过 718／797 个采样点。新版本的运行和渲染验证以 `Docs/Previews/Campus/` 下的报告为准，不能将桌面验证作为头显测试结果。

2026-10-10 建模修改完成后，当前布局为 7410 个节点、48 个共享材质定义。完整生成流程在隔离目录通过字体回退、父节点导入顺序和重复执行检查。Unity 2022.3.62f3 保存场景通过四层水龙头开关、毛巾拾取与冲湿、重复拾取限制、重置恢复，以及 401 到厕所、洗手台和防火房间的实际通行检查；水流开关截图有 543 个明显变化像素。重新烘焙得到一张光照图、六个反射探针，UV 重叠为零。A/B 路线十组实际上下楼测试共 20540 次移动全部通过，47 处窗墙接口、材质照明与烟雾启停回归通过。浏览器十一个视角、四层和显示开关检查通过，没有 JavaScript 或 WebGL 错误。973 个 YAML 场景、材质、Prefab 与光照资源的 501729 个引用检查通过，无重复 GUID、缺失文件或断开的本地／外部 fileID。最新报告为 `CampusModelingValidation.txt`、`CampusModelingGeneratorValidation.txt`、`CampusModelingAssetValidation.txt`、`CampusRuntimeValidation.txt` 和 `CampusWalkthroughValidation.txt`。

同日的人物移动与双手更新已在独立 Unity 工程副本中验证，当前场景和相关脚本与测试副本的文件校验一致。实际键盘移动入口通过起步加速、2.6／4.2 m/s 步行与跑步、斜向速度归一化和停止减速检查；30／60／120 Hz 的跳跃实测最高点分别为 0.661／0.705／0.727 m，均正常落地且不能空中二次起跳。离地容错、落地前按键缓冲和低顶棚阻挡检查通过。双手与隐藏双手截图有 143220 个明显变化像素，左右半屏均可见；手部没有碰撞体并使用动态照明。视角切换、毛巾的手部挂点与原始大小恢复、跑步／腾空／落地姿态检查通过。A/B 十组上下楼与四层洗手台交互重新通过。最新报告为 `CampusMovementValidation.txt`，效果图为 `Campus-Hands.png`、`Campus-HandsTowel.png`、`Campus-StudentRun.png` 和 `Campus-StudentJump.png`。打开主场景等待脚本编译后重新进入 Play 即可体验，无需手动添加手部组件。

2026-10-05 已在独立 Unity 2022.3.62f3 工程中通过 C# 编译、两条路线的物理采样和实际光照烘焙（一张光照图、六个反射探针，烘焙 UV 重叠为零）。桌面 Play Mode 验证了保存后的材质与照明、三种 Shader、三次火警氛围启停，以及烟雾实际可见像素；粒子和音频关闭后停止，重复开启保持一个发射器和一个音源。交付场景、材质及 Prefab 的资源引用检查通过，无重复 GUID。

浏览器九种视角及火警氛围开关检查通过，未记录 JavaScript 或 WebGL 错误。头显帧率、立体效果、XR 移动及 M 键静音的人工体验仍未验证。

窗墙修正后，Unity 保存场景的 49 个实际碰撞体连接点检查通过；两条疏散路线、光照烘焙、零 UV 重叠、场景重载及氛围启停检查均重新通过。新增室内与外窗实际渲染已检查，未再出现隔墙跨玻璃的问题。

PBR 与 HDRI 升级后重新通过 Unity 编译、路线、窗墙接口和运行验证。三组 1K 材质的底色／细节贴图色彩空间检查通过；保存后的天空与教室反射探针存在，阳光开关使教室 886856 个像素产生可见变化。浏览器太阳阴影开关在场景区域产生 110832 个变化像素，未记录 JavaScript/WebGL 错误。

标牌与界面调整后再次通过 Unity 编译、保存后重载及实际渲染。146 处牌面布局检查通过；实际运行验证确认贴字材质受光照、无自发光且不越出牌面，并通过公告栏贴字开关的像素差检查。采用柔和透明边缘以保留细小笔画。浏览器八个视角、四个楼层、显示开关、自动收起和 Esc 操作检查通过；默认 479 × 758 窗口无水平溢出。最终资源引用检查见 `CampusSignageAssetValidation.txt`。


楼梯与人物更新后，保存场景通过 A/B 两条路线的步行与加速上下楼，以及 30 Hz 加速下降，共十组实际 CharacterController 移动检查（20540 次移动）。第一／第三人称切换、贴墙镜头、人物脚底接触地面、动态探针照明和无额外人物碰撞体检查通过。最新报告为 `CampusWalkthroughValidation.txt`，正面与背面模型截图为 `Campus-Student.png` 和 `Campus-StudentBack.png`。保存的三场景、20 个 Prefab 和三个 Resources Shader 共 1085 个 Assets 依赖路径完整，旧清理清单中的资产没有重新出现。
