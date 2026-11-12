# Campus_XR 射线传送与代码结构

## 打开与操作

在 Unity 2022.3.62f3 打开 Assets/Scenes/TeachingBuildingCampusXR.unity，通过 Meta Horizon Link / Air Link 连接 Quest 3 后进入 Play。此场景已加入 Build Settings，目标为 Windows 64 位。

- 右手瞄准：持续显示直线传送射线，绿色为有效落点，红色不可传送。
- 右手扳机按下：立即传送；按住不连发，松开不触发。
- 右手 A：在 2.6 m 内切换为物体交互，操作门、毛巾和水龙头。
- 左手握把：未持有灭火器时按住暂停传送、松开恢复；持有灭火器时用于近距离拔销。
- 右手摇杆左右推：每次转向 30°，回中后可再次转向。
- 右手握把：归还当前持有的毛巾或灭火器。
- 左手扳机：持有湿毛巾时按一次抬起、再按一次放下，无需持续按住。
- 头显/右手失去追踪或 OpenXR 会话失去焦点时暂停交互；恢复后需先松开扳机再按下。
- 左摇杆不再控制移动。完整按键见 [XRControls.md](XRControls.md)。

头显视角随追踪直接更新。传送保留面向和眼高，并由 XRI TeleportationProvider 补偿房间尺度内头部相对 XR Origin 的水平偏移。无头显的桌面检查请打开原 TeachingBuildingCampus.unity。

## 代码分层

| 目录 | 职责 |
|---|---|
| Assets/Scripts/Player/Desktop | 原桌面控制器兼容入口；按 Input、Locomotion、Camera、Interaction、Modes、HUD 拆成 partial 文件 |
| Assets/Scripts/Player/Avatar | 原 CampusStudentAvatar，保留原 .meta GUID、命名空间、程序动画和模型引用 |
| Assets/Scripts/Player/XR/Input | CampusXRInput 统一启停并读取具名输入动作 |
| Assets/Scripts/Player/XR/Locomotion | 扳机锁存状态、直线检测、落点安全校验、分段转向 |
| Assets/Scripts/Player/XR/Presentation | 射线与落点环、空间文字提示、原人物模型的本地显示策略 |
| Assets/Scripts/Player/XR/CampusXRSession.cs | 第一次有效头显追踪时对齐出生点，检测集合点到达 |
| Assets/Scripts/Interaction/Common | 桌面与 XR 共用的 IPrototypeInteractable 接口 |
| Assets/Scripts/Interaction/XR | 右手射线适配既有门与设施逻辑 |
| Assets/Editor/XR | 依赖配置、XR 场景生成、编辑器物理验证 |
| Assets/XR/Campus | 独立输入动作、可复用 XR Origin Prefab、射线材质 |
| Tools/XRSetup | 安装前备份、Unity 批处理启动入口 |

运行链：CampusXRInput → CampusTeleportController → CampusTeleportValidator → XRI TeleportationProvider → XROrigin。
显示由 CampusTeleportVisual / CampusXRFeedback 负责；右手设施交互独立于传送目标选择。

保留桌面控制器类名与序列化字段，是为了兼容原场景、生成器和已有验证程序。partial 文件共享旧状态，但在 XR 场景中整个桌面玩家处于禁用状态，不参与 XR 移动。

## 传送规则

使用自定义安全检测加官方 XRI TeleportationProvider。CampusTeleportSurface 是明确的可传送标记，不依赖“看起来水平”的物体。

生成时只给 FloorSlab、Ground、SideExitPath、Landing、HalfLanding、Step_First_*、Step_Second_* 的碰撞体加标记。桌面、屋顶、电梯轿厢地面、墙和栏杆不在列表中。

- 直线检测，首次实体碰撞即停止，不跳过墙壁寻找后方地面。火焰没有 Collider：单独检查射线是否接触或穿过数学火区，若是则拒绝传送，不执行人体扫掠或追踪回拉。详见 [火焰射线限制](FireBarrierOutdoor.md)。
- 头显或手柄在碰撞体内、头显到手柄之间有障碍时拒绝传送，防止手柄伸墙后绕过射线阻挡。
- 最大距离 12 m、最大高度差 4 m、最大表面坡度 35°。
- 以半径 0.22 m 的胶囊检查身体净空；高度按头显高度加余量计算，并限制在 1.25–2.2 m。
- 中心和四周支撑探测防止落在地板边缘。
- 按下右扳机的当帧重新检测目标；无有效目标、失去追踪、取消或移动系统忙时不传送。
- 传送落点抬高 0.035 m，避免脚点与地板表面重合。

传送校验不是现实空间的防撞系统，也不会限制用户真实走动。正式演练是否允许跨越路线节点、烟区或楼层，应由后续任务规则进一步限制。

## 模型与已有交互

原 CampusStudent.prefab 和模型几何保持不变，在 XR Rig 内复用。为避免第一人称看到头部或身体穿入视野，当前本地模型 Renderer 隐藏；保留模型用于后续身体呈现扩展。传送位移不会驱动走路摆腿。

右手通过共用接口调用原 PrototypeDoor / FacilityNotice。开门逻辑保持原样；报警按钮和消防设备目前仍为信息预览，未在此任务扩展为报警或灭火玩法。

## 场景生成与维护

菜单：Campus Emergency → XR → Build campus XR scene。

该入口读取原 Campus 场景、生成 XR 派生场景和 Rig，然后执行验证。它不保存对原场景的修改。XR 场景属于生成输出；手工编辑后请另存或同步构建器，再执行完整重建。

更改楼体：先用原生成器更新 TeachingBuildingCampus，再执行 XR 场景生成。
更改控制逻辑：修改对应运行模块，无需重建楼体。
更改已有输入动作：在 CampusXR.inputactions 中编辑；构建器复用现有资产以保留 action GUID。

Tools/generate_teaching_building.py 从实际 .meta 读取脚本 GUID，ensure_metadata 不覆盖既有 C# / JSON 元数据。Tools/verify_teaching_building.py 的离线编译只覆盖桌面原型，XR 的编译与行为检查由 Unity 执行。

## 验证

菜单：Campus Emergency → XR → Validate campus teleport scene。
报告：Docs/XRTeleportValidation.txt。
验证包括序列化输入引用、单一摄像机/监听器、桌面玩家隔离、可传送表面、真实场景落点、净空/家具/边缘/墙体拒绝以及真实 XRI provider 更新方法的头部偏移补偿。

这是编辑器物理与组件行为验证，不等同于 Quest 3 实机验证。需要在头显上确认控制器指向、按键手感、双目显示、转向舒适度、楼梯通行和帧率。

批处理入口：
Tools/XRSetup/RunUnityCheck.ps1
Tools/XRSetup/RunUnityCheck.ps1 -Method CampusEmergency.Editor.CampusXRTeleportValidation.ValidateScene -LogName XRTeleportVerify.log

脚本异步启动 Unity，最终结果以 Logs 下日志与 Docs 报告为准。运行前关闭同工程的 Unity 窗口。
