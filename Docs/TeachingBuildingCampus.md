# 校园场景升级版

在 Unity **2022.3.62f3** 中打开 `Assets/Scenes/TeachingBuildingCampus.unity`。这是保留四层建筑和 A/B 两条疏散路线的独立美术版本，使用现有 Built-in 渲染管线。原型和细化版场景继续保留。

无需 Unity 的查看入口为 `Docs/Previews/Campus/TeachingBuildingCampusPreview.html`，可直接用 Chrome 或 Edge 打开。预览默认进入 401 教室，侧栏可切换建筑、楼层、电梯厅、楼梯间、走廊、公告栏及外窗与隔墙。浏览器光照经过简化，实际 Unity 截图保存在同目录。

## 本次场景内容

- 1K 扫描水磨石与白橡木 PBR 材质，配合原创座椅织物、软木和已有墙面材质。
- 401 教室增加软垫座椅、桌下书架、笔记本电脑、书本、教学触控台、收纳柜、插座和绿植。
- 四楼走廊增加校园活动与安全提示公告栏、楼层识别牌、导向牌吊杆；防火门加入推杆和闭门器细节。
- 室外加入教学楼导览牌及步道灯柱，保持集合点和疏散通道。
- 可选的局部烟雾与原创模拟警报，用于观察火警氛围和标识可见性。

场景细节与模拟警报由代码生成；地面、木饰面和天空使用 Poly Haven 的 CC0 扫描素材，来源、作者和校验记录位于 `Assets/Environment/TeachingBuildingCampus/SourceTextures/`。不增加 Unity Package 依赖。课程提交时应按要求声明 AI 辅助制作与个人贡献。

## 建筑结构与真实感修正

外窗不再按固定 5 m 网格跨过房间隔墙。Campus 生成器从各层隔墙的实际端点推导窗洞，把教室、卫生间和楼梯间隔墙接到连续的实心窗间墙；电梯后部服务空间使用实墙。外墙总厚度 400 mm，玻璃逐块停在铝合金框内，窗洞设侧壁收口和石材窗台，室内设独立墙裙、踢脚线、升起的卷帘和开启把手。卫生间使用磨砂玻璃。

401 教室增加墙上时钟及空调风口，白板改为课程演练内容，插座和线槽贴合墙面。原有门口、课桌间通道及 A/B 路线保持可通行。浏览器新增 **08 外窗与隔墙** 视角，Unity 新增 `Campus-WindowJunction.png` 和 `Campus-ClassroomWindows.png`，方便从室外和室内检查窗墙接口。

生成器会逐处检查隔墙端部的实体墙接触和玻璃避让。相同检查可复现 Refined 布局的旧问题，并在 Campus 布局检查 49 个连接点。Unity Play Mode 验证还会针对保存后的实际碰撞体重新检查这 49 处连接，避免只检查 JSON 而遗漏场景导入问题。

## 扫描材质与自然光

地面使用 [Terrazzo Tiles](https://polyhaven.com/a/terrazzo_tiles)，桌面、木门及橱柜饰面使用 [White Oak Veneer](https://polyhaven.com/a/white_oak_veneer)，保留扫描色彩与物理纹理比例。Unity 使用 1024 px 底色与线性 AO / Height / Smoothness 贴图；石材和木材均为非金属，地面保留适度抛光反射，木饰面较哑光。现有 Standard 光照模型通过 ArchitecturalSurface Shader 采样凹凸细节。

天空使用 [Kloofendal 48d Partly Cloudy](https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky) HDRI。阳光照进教室后窗，室内灯减弱到原强度的一半；Campus 专属照明配置增加间接反弹和采样，并为 401 增加独立的盒投射反射探针。其他场景继续使用原配置。

浏览器同步使用扫描底色和缓存的 2048 px 太阳阴影，**太阳阴影**复选框可比较开关效果。阴影仅在建筑剖切状态改变时重建，旋转和缩放复用缓存。浏览器光照和反射为近似效果；Unity 实际渲染截图包含完整 PBR、HDRI、烘焙照明和室内反射，是视觉检查的依据。

## 标牌与预览界面

Campus 标牌使用独立 UV 的透明印字贴图和哑光 Standard 材质，接受场景光照；不再依赖无光照的动态字体图集。门牌缩为 480 × 220 mm，安装高度 1.6 m；消防设备标签、纸质消防行动告示和电梯警告按各自用途排版。文字与牌面保留约 2 mm 间距，并检查完整文字区域不越出牌面。重要出口与消防标识保留。白板用手写风格列出三步演练提纲，房号只放在入口。公告栏使用独立钉住、略微倾斜的纸张，正文左对齐。活动内容为场景示例。

浏览器界面改为不透明、可收起的紧凑工具面板，保留八个视角、四层选择、屋顶剖切、路线、阴影和烟雾开关。窄于 600 px 时默认收起，选择视角或楼层后自动收起；Esc 可收起面板。场景主体不再透过侧栏与文字叠加。

`campus_signage.py` 负责牌面与文字贴图，`campus_preview_ui.py` 负责 Campus 独立样式。生成文字需要本机 Arial.ttf 和 Chalkboard.ttc；默认查找 macOS Supplemental 字体目录，可用 `CAMPUS_FONT_DIR` 指定装有这些字体的目录。已生成的 PNG 随项目保存，Unity 与浏览器查看不依赖这些本机字体。

## 操作

Play 后按 **F4** 进入四楼，WASD 移动，鼠标观察，E 开门或查看设施；Tab 返回总览，C 在总览中显示／隐藏屋顶，R 返回起点。

**H** 开关局部火警氛围；开启后 **M** 静音／恢复模拟警报，同时显示文字提示。默认关闭，不自动播放警报。Unity 运行时使用最多 48 个柔边烟雾粒子；浏览器用静态半透明形体示意烟雾，不播放声音。开关关闭时粒子与音频停止。

该模块是环境预览。烟雾不阻断路线，警报不启动正式演练流程，也没有火灾传播、灭火操作或完成判定。正式 XR Rig、舒适移动、头显帧率及立体渲染仍需实机验证。

## 修改与重新生成

旧材质对比图集中在 `Docs/Previews/Campus/History/`。文件清理记录见 `Docs/ProjectFileAudit.md`。生成器只创建仍在使用的织物、软木和烟雾纹理；石材与木纹直接使用扫描资源。Unity 构建只保存被几何使用的材质，反射图由场景光照烘焙统一保存到场景光照目录。

`Tools/upgrade_campus_scene.py` 读取已保存的 `TeachingBuildingRefinedLayout.json`，输出独立的 Campus 布局、原创纹理、已随项目保存的扫描材质、模拟警报和离线预览。`import_campus_pbr.py` 校验原始文件，并将 AO、凹凸高度和逆粗糙度打包为线性细节贴图；重新生成不需要联网。脚本需要 Python、NumPy 和 Pillow。

```shell
python Tools/upgrade_campus_scene.py
```

随后在 Unity 中选择 **Campus Emergency → Build campus teaching building**，导入布局、重建 Campus 场景、保存复用 Prefab、检查路线并烘焙照明。重建会替换这个版本的生成资源；手工修改请另存场景或 Prefab。该操作不会重建原型或 Refined 场景。

生成资源集中在 `Assets/Environment/TeachingBuildingCampus/`。分享场景时应保留完整项目，包括 Campus 与 Refined 资源、`Assets/Scenes/TeachingBuildingCampus/` 中的烘焙数据，以及现有脚本和 Resources Shader；Campus 的墙面等材质仍复用部分 Refined 纹理。

## 验证记录

离线布局检查保留了原有路线定义，A/B 路线分别通过 718／797 个采样点。新版本的运行和渲染验证以 `Docs/Previews/Campus/` 下的报告为准，不能将桌面验证作为头显测试结果。

2026-10-05 已在独立 Unity 2022.3.62f3 工程中通过 C# 编译、两条路线的物理采样和实际光照烘焙（一张光照图、六个反射探针，烘焙 UV 重叠为零）。桌面 Play Mode 验证了保存后的材质与照明、三种 Shader、三次火警氛围启停，以及烟雾实际可见像素；粒子和音频关闭后停止，重复开启保持一个发射器和一个音源。交付场景、材质及 Prefab 的资源引用检查通过，无重复 GUID。

浏览器八种视角及火警氛围开关检查通过，未记录 JavaScript 或 WebGL 错误。头显帧率、立体效果、XR 移动及 M 键静音的人工体验仍未验证。

窗墙修正后，Unity 保存场景的 49 个实际碰撞体连接点检查通过；两条疏散路线、光照烘焙、零 UV 重叠、场景重载及氛围启停检查均重新通过。新增室内与外窗实际渲染已检查，未再出现隔墙跨玻璃的问题。

PBR 与 HDRI 升级后重新通过 Unity 编译、路线、窗墙接口和运行验证。三组 1K 材质的底色／细节贴图色彩空间检查通过；保存后的天空与教室反射探针存在，阳光开关使教室 886856 个像素产生可见变化。浏览器太阳阴影开关在场景区域产生 110832 个变化像素，未记录 JavaScript/WebGL 错误。

标牌与界面调整后再次通过 Unity 编译、保存后重载及实际渲染。146 处牌面布局检查通过；实际运行验证确认贴字材质受光照、无自发光且不越出牌面，并通过公告栏贴字开关的像素差检查。采用柔和透明边缘以保留细小笔画。浏览器八个视角、四个楼层、显示开关、自动收起和 Esc 操作检查通过；默认 479 × 758 窗口无水平溢出。最终资源引用检查见 `CampusSignageAssetValidation.txt`。
