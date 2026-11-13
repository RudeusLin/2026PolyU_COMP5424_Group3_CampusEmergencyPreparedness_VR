# XR 场景模型与视觉细化

本次更新针对 `Assets/Scenes/TeachingBuildingCampusXR.unity`，改善家具、卫生间和断梯的模型轮廓，以及室内材质与反射。场景修改通过编辑器生成并保存，运行时无需执行生成代码。

家具、洁具、材质与局部反射现已同步到键鼠版 `Assets/Scenes/TeachingBuildingCampus.unity`。两个场景分别保存，Hierarchy 显示 `TeachingBuildingCampus` 时正在查看桌面版。Unity 已在 Play 模式运行时，先停止，再重新打开需要查看的场景，最后进入 Play 模式；语言面板关闭后可进入教室和卫生间观察细节。

## 内容

- 八间教室使用带曲率的座面与椅背、圆管金属桌椅腿、桌下支撑、椅背支架和橡胶脚垫。桌面书本采用少量固定角度变化，并加入书皮与笔。
- 四层卫生间使用带内腔的圆角洗手盆、连续弯管水龙头、带碗体和座圈的洁具，补充柔和的瓷器、铬金属和隔断材质。卫生间铺设 600 mm 规格的低反光地砖。
- 走廊和教室使用浅色水磨石、暖白灰泥、柔和的绿色墙裙与哑光橡木。灯具增加金属边框，走廊顶棚补充通风格栅。
- B 侧断梯使用不规则混凝土断面和短钢筋，保持原有缺口。
- 保留现有全局照明，为四层卫生间独立更新局部反射。反射捕获包含房间几何，排除可移动人物与毛巾；运行时读取保存的立方体贴图。新增装饰几何按房间和材质合并，减少额外绘制批次。

## 查看与重建

打开 XR 场景即可看到已保存的结果。[同机位前后对比](Previews/XRVisualUpgrade/index.html)使用 Unity 实际渲染，可切换教室、家具、走廊、洗手台、厕位与断梯。

键鼠版有独立的[桌面场景前后对比](Previews/DesktopVisualUpgrade/index.html)。Unity 菜单 **Campus Emergency → Refine desktop models and materials** 可重复应用桌面版细化；完整重建桌面场景也会调用相同的视觉生成器。桌面版保留原来的 36 套桌椅、完整 B 侧楼梯与键鼠操作，XR 版保留 48 套桌椅与断梯。两个场景的合并装饰网格和房间反射分别保存，避免相互覆盖。

Unity 菜单 **Campus Emergency → XR → Refine models and update local reflections** 可重复应用模型与材质，并更新四层局部反射。生成器复用已有对象和资源，不重复增加装饰或反射探针。完整重建 XR 场景时同样应用这些细化并更新反射。

另提供 **Refine models and bake lighting**，将室内光源调整为烘焙面光源并重算整栋照明。本次整栋烘焙受本机可用内存限制未完成；当前交付保留原有有效光照数据，未使用失败的烘焙结果。局部反射已独立更新。内存充足时可通过该菜单继续烘焙。

贴图已随工程保存。需要重新生成时，运行：

```shell
python Tools/generate_xr_visual_textures.py
```

浅色水磨石派生于项目已有的 Poly Haven CC0 扫描贴图，其来源记录仍见 `Assets/Environment/TeachingBuildingCampus/SourceTextures/`。地砖底色、拼缝和细节贴图由脚本生成。新增资源集中在 `Assets/Environment/CampusXRVisuals/`，独立于桌面场景使用的材质。

## 验证范围

保存场景重新打开后，模型、材质和局部反射检查通过。XR 版回归结果：

- [建模与楼梯传送](Previews/XRVisualUpgrade/ModelingValidation.txt)：237 项通过。
- [模拟控制器交互](Previews/XRVisualUpgrade/InteractionValidation.txt)：15 项通过，覆盖四层毛巾、水龙头及 401 报警按钮。
- [固定 Route A](Previews/XRVisualUpgrade/RouteAValidation.txt)：159 项通过，覆盖既有路线、火灾限制和完成记录。
- [场景完整性](Previews/XRVisualUpgrade/SceneIntegrity.json)：2,649 个原有碰撞体及 3,080 个碰撞体或祖先变换完全一致，原有 7,853 个对象全部保留。

桌面版的[角色移动与双侧楼梯路线](Previews/DesktopVisualUpgrade/WalkthroughValidation.txt)和[四层卫生间交互](Previews/DesktopVisualUpgrade/ModelingValidation.txt)均通过 Unity Play Mode 检查。独立[保存数据核对](Previews/DesktopVisualUpgrade/SceneIntegrity.json)确认 2,434 个碰撞体、2,825 个碰撞体或祖先变换、78 个脚本组件配置一致，原有 7,608 个对象全部保留。桌面版资源同步已核对文件哈希及 97 个新增相关 GUID，引用完整，现有 XR 场景与共用视觉资源未被覆盖。

新增装饰复用既有倒角几何生成方法，并按房间和材质合并网格；四层反射使用保存的立方体贴图，无需运行时更新。未引入新的运行时组件或包依赖。更多检查见 [视觉验证报告](Previews/XRVisualUpgrade/Validation.txt)。

头显实际帧率、立体材质观感及舒适度需使用设备验收；编辑器渲染不替代头显测试。
