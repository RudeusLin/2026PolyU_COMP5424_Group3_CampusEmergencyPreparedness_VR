# 项目文件整理与清理记录

完成日期：2026-10-05。Unity 2022.3.62f3。以提交 `801a20febab63c5e808304944ca31aacefd79fc9` 为基线，完成资产清理、历史预览归档、生成流程修正和验证。

已删除 **240 项无引用生成资产及对应 `.meta`（480 个文件，8.42 MiB）**，另删除默认 `SampleScene.unity` 及 `.meta`，总计 **482 个文件**。七张历史对比图已归档，未删除。逐文件执行记录见 [ProjectCleanupCandidates.json](ProjectCleanupCandidates.json)；其中 `assets` 是已删除资产列表，不是当前存在的资产。

## 当前项目入口

| 文件区域 | 用途与维护约定 |
| --- | --- |
| `Assets/Scenes/TeachingBuildingCampus.unity` | 当前校园主场景；README 已优先介绍此入口 |
| `Assets/Environment/TeachingBuildingCampus/` | Campus 材质、网格、贴字、Prefab、音频、扫描材质与原始输入 |
| `Assets/Scenes/TeachingBuildingCampus/` | 当前场景保存的 LightingData、光照图与反射图；共享工程时一并保留 |
| `Assets/Environment/TeachingBuildingRefined/Textures/` | Campus 仍直接复用其中 16 个贴图，不能整目录删除 |
| `Assets/Environment/*Layout.json` | Prototype、Refined、Campus 三阶段布局与生成输入 |
| `Assets/Scripts/Prototype/` | Campus 仍使用的移动、门、设施提示和演练氛围逻辑 |
| `Assets/Editor/`、`Assets/Resources/` | 构建、烘焙、验证与运行 Shader |
| `Tools/` | 布局生成、扫描素材导入和离线预览工具 |
| `Docs/Previews/Campus/` | 当前网页预览、Unity 截图和验证报告 |
| `Docs/Previews/Campus/History/` | 七张历史 PBR／日光对比图，见 [归档说明](Previews/Campus/History/README.md) |
| 原型与 Refined 场景及预览 | 保留迭代历史、旧版本菜单与验证入口 |
| `Packages/`、`ProjectSettings/` | Unity 工程配置，保持现有依赖与设置 |

`EditorBuildSettings.asset` 的场景列表仍为空。本次没有把 Campus 配置为唯一构建场景。

## 已删除的生成产物

| 类别 | 资产数（每项另含 `.meta`） |
| --- | ---: |
| 未被保存场景使用的手动 Campus 反射导出 | 6 |
| 旧贴字材质 | 82 |
| 未引用的 PosterCream 材质 | 1 |
| 旧 Campus 网格 | 55 |
| 旧贴字 PNG | 81 |
| 已被扫描材质替代的 Oak／Terrazzo 程序纹理与细节图 | 4 |
| Refined 未引用产物：5 个手动反射导出、AccentGold 材质、1 个网格 | 7 |
| 旧 Campus 光照图：编号 1、2 的 dir／light 文件 | 4 |
| **合计** | **240** |

删除依据是 Unity `AssetDatabase.GetDependencies` 与生成器输入检查，而非文件名或修改时间。旧材质到旧贴字 PNG 的引用按清单成组清理；当前贴字材质和 PNG 继续保留。删除默认 SampleScene 后，剩余三场景、19 个 Prefab、三个 Resources Shader 的全部 Assets 依赖均存在，且没有指向已删除资产的依赖。

实际场景依赖保存在 `Assets/Scenes/TeachingBuildingCampus/` 下：`LightingData.asset`、`Lightmap-0_comp_*` 与 `ReflectionProbe-*.exr`。本次删除的是无引用的旧光照图和 Environment 下重复手动反射导出。

## 生成流程已同步调整

- `Tools/upgrade_campus_scene.py` 只生成仍使用的织物、软木和烟雾纹理，并保留模拟警报音频；木纹与石材直接使用随项目保存的扫描素材，不再生成旧 Oak／Terrazzo 程序贴图。
- Campus 布局不再注册未使用的 PosterCream；公告纸使用既有 Paper 材质。布局节点保持 7196 个，共享材料定义由 36 个减为 35 个。
- `TeachingBuildingBuilder.cs` 只创建布局几何实际使用的材料，避免重建时保存无用材料。
- `TeachingBuildingLighting.cs` 使用场景统一光照烘焙保存反射图，并逐个检查探针结果；不再额外手动烘焙重复 EXR。
- `generate_teaching_building.py` 原本从默认 SampleScene 读取场景配置；已将该配置提取到 `Tools/teaching_building_scene_settings.yaml`。删除 SampleScene 后，旧版生成器仍可运行，55 个生成文件与调整前逐字节一致。
- 隔离工程已验证 Campus 完整重建。交付工程保留原有场景、Prefab 的对象编号和烘焙文件，避免不必要的序列化差异；这套实际交付资产已再次通过清理后的运行验证。运行截图与报告同步更新。当前 Campus 重建没有恢复清单中的旧资产。

旧版生成工具仍用于重建其对应历史版本；完整重建历史版本可能再次创建当时的产物，这不属于当前 Campus 流程。

## 保留共享资源的原因

Campus 直接复用 Refined 的 Bark、Brick、BrushedSteel、Concrete、Foliage、Grass、Paving、Plaster 各一张基础贴图和一张 Detail 贴图，共 **16 张**。整套 Refined 删除会破坏当前 Campus 输入。

`SourceTextures/` 的 AO、Diffuse、Displacement、Roughness 原图、HDRI、`manifest.json` 和 `LICENSE.md` 是 `import_campus_pbr.py` 的校验与导入输入，即使部分不在保存场景的直接依赖中也必须保留。三个 Layout JSON、历史场景和对应生成工具也继续保留。

Unity `Library/`、`Logs/`、Python `__pycache__/`、自动生成的 IDE 工程文件与个人设置已经被 `.gitignore` 排除；本次没有清理这些本地工作目录。`.git/` 保留版本历史。

## 本次验证结果

| 检查 | 结果 |
| --- | --- |
| Python Campus 重新生成 | 7196 个节点、35 个共享材料定义；A 路线 718 个采样、B 路线 797 个采样通过 |
| 几何与标牌布局 | 49 处墙窗接缝、146 个标牌载体检查通过 |
| Unity 完整重建 | 1 个光照图集、6 个反射探针；烘焙后无 renderer UV 重叠 |
| Unity 保存场景 Play Mode | 光照、材质、扫描 PBR、HDRI、教室反射、Shader 与标牌检查通过 |
| 实际渲染差异 | 日光 887521 像素、标牌 8912 像素、烟雾 8218 像素；达到现有验证阈值 |
| 烟雾与空间警报 | 三次启停通过；关闭后清空粒子并停止音频；粒子上限 48 |
| Unity 依赖复查 | 3 场景、19 Prefab、3 Resources Shader；无缺失 Assets 依赖、无已删依赖 |
| 删除 GUID 引用检查 | 241 个已删除 GUID 未被保留的序列化 Assets 引用 |
| 旧版生成模板回归 | 不依赖 SampleScene；55 个生成文件与调整前逐字节一致 |
| Campus 重新生成后的旧资产检查 | 240 项旧资产及对应 `.meta` 未重新出现 |
| 浏览器回归 | 八个视角、1F–4F、屋顶剖切、路线、太阳阴影和烟雾开关通过；检查期间无 JavaScript／WebGL 错误日志 |
| 视觉检查 | 已查看最新 Unity 教室和公告栏截图 |

详细结果见 [构建报告](Previews/Campus/CampusValidation.txt)、[运行报告](Previews/Campus/CampusRuntimeValidation.txt) 和 [几何报告](Previews/Campus/CampusGeometryValidation.txt)。网页光照属于近似预览，实际 PBR 效果以 Unity 截图为准。头显帧率、立体渲染和 VR 移动尚未验证。
