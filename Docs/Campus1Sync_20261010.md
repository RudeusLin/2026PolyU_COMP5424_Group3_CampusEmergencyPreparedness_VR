# Campus1 同步记录（2026-10-10）

本次以 Campus_XR 复制时的共同 Git 基线 `2b64132`，合并 Campus1 的 `0c68802`（Improve campus modeling, movement and first-person hands）。源项目未修改；目标项目原有未提交改动已保留。

## 已同步的内容

- 校园场景、布局 JSON、材质、网格、预制体、标识纹理及烘焙灯光资源。
- 四层洗手台、水龙头开关、毛巾拾取/浸湿/归还，以及避难房间、403 房间与四楼卫生间调整。
- 人物手指与持物姿态、桌面第一人称手臂、平滑加减速、跑步和跳跃逻辑。
- PracticeSession 及其控制器、区域和事件转发代码，保持源项目的接线范围；本次不新增完整演练流程。
- 配套编辑器工具、Python 生成工具、文档与源项目预览图。源项目预览图与历史验证报告属于 Campus1，不代表 XR 头显截图或本次头显测试。

## XR 内容的保留方式

校园场景以 Unity 序列化对象 ID 三方合并。保留现有 XR Origin 实例与引用，更新校园与灯光节点，没有重建或替换 XR Rig。

包依赖、OpenXR/Quest Link 配置、已有输入资产、传送算法、落点验证、转向、头显/控制器追踪以及 XR 展示脚本保持原样。Rig 仅增添持物组件与右手毛巾挂点。

桌面角色仍处于 XR 场景的禁用状态。新增桌面跑跳和第一人称手臂保留在桌面代码中，XR 继续使用射线传送。

## 新交互兼容层

源项目把交互接口改成了依赖 PrototypeExplorer 的方法。这里改为共享 `ICampusInteractor`，由桌面角色和 `CampusXRInventory` 分别实现，避免 XR 依赖禁用的桌面角色。

- 右手射线 + 扳机：开关门、拾取毛巾、开关水龙头、浸湿持有的毛巾。
- 右手握持键：把毛巾归还原柜台，恢复干燥状态和碰撞体。
- 一次只能持有一条毛巾；持有时关闭其碰撞体。
- 左摇杆传送、左握持键取消、右摇杆转向与原来一致。
- 传送时继续保留持物状态；XR 持物组件禁用时归还毛巾。

## 文件组织

- `Assets/Scripts/Player/Desktop/PrototypeExplorer.*.cs`：源项目角色更新按现有分文件结构合并。
- `Assets/Scripts/Player/Desktop/CampusFirstPersonArms.cs`：桌面第一人称手臂。
- `Assets/Scripts/Player/Avatar/CampusStudentAvatar.cs`：共享人物动画和手部细节。
- `Assets/Scripts/Interaction/Common/ICampusInteractor.cs`：共享持物能力。
- `Assets/Scripts/Interaction/Common/IPrototypeInteractable.cs`：共享交互入口。
- `Assets/Scripts/Prototype/CampusFaucet.cs`、`CampusTowel.cs`：共享设施逻辑，沿用导入资产的 GUID。
- `Assets/Scripts/Player/XR/Interaction/CampusXRInventory.cs`：XR 持物与归还。
- `Assets/Scripts/Interaction/XR/CampusXRInteractionRay.cs`：原有射线检测，新增传入交互者及 XR 按键文案转换。
- `Assets/Editor/XR/CampusXRSyncValidation.cs`：增量接线与同步验证。

所有移动相关文件继续分类，移动文件时保留原 .meta GUID。未创建 Git 提交。

## 验证与使用

打开 `Assets/Scenes/TeachingBuildingCampusXR.unity`。同步验证入口：
`CampusEmergency.Editor.CampusXRSyncValidation.Validate`。

本次结果见 `XRSyncValidation.txt` 和 `XRTeleportValidation.txt`，Unity 日志在 `Logs/XRSyncIntegrate.log`。没有执行 Quest 3 真机按键、视觉效果或性能测试。

## 恢复资料

同步前的创作文件备份位于：
`E:/0_PolyU/COMP5424_XR/Project_XR/Campus_XR_SyncBackups/20261010_161516/AuthoringBeforeSync.zip`。

同目录保存 SHA-256 清单、同步前 Git 状态与差异。压缩包包含 Assets、Packages、ProjectSettings、Tools、Docs 及项目根文件；不包含可重新生成的 Library、Temp、Logs 和 Git 对象库。恢复时需依据清单处理本次新增文件，单纯覆盖解压不能移除新增文件。

本次完成结果：Unity 2022.3.62f3 编译、增量接线、保存后独立进程重载、四层交互测试、原传送回归全部通过。重载日志：Logs/XRSyncReloadVerify.log（退出码 0）。另已核验：146 项源版本文件变化均已对应同步或兼容合并；资产 GUID 与场景对象 ID 无重复，场景本地引用完整；原 XR Rig 各既有组件参数保持一致，仅增加持物组件和挂点；Campus1 Git 工作区仍干净。
