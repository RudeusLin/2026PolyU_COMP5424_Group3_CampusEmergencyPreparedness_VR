# Campus_XR 当前按键：右手扳机传送

本说明自 2026-10-11 的右手扳机改动起生效，替代此前的左摇杆瞄准、松杆传送操作。

| 按键或动作 | 功能 |
| --- | --- |
| 转动右手控制器 | 持续显示直线射线，指向目标地面 |
| 右手扳机（食指） | 绿色落点时，按下立即传送；按住不连发，松开不传送 |
| 右手 A 键 | 对准门、毛巾、水龙头等，按下进行交互；按住期间切换为短距离交互射线 |
| 右摇杆左/右 | 每次拨动转向 30 度，回中后可再次拨动 |
| 右手握持键（中指） | 将毛巾归还原柜台 |
| 左手握持键（中指） | 按住暂停传送瞄准，松开恢复；不会撤销已经完成的传送 |
| 左摇杆、左扳机、B/X/Y | 当前没有绑定校园玩法功能 |
| 头部转动 | 查看四周，由头显追踪 |
| Meta / 系统菜单键 | 保留系统功能；系统菜单占用焦点时暂停游戏交互 |

## 传送规则
射线来自右手控制器，最大检测距离 12 米。绿色射线与落点圈表示可传送，红色表示目标不可落地。墙、门、家具、缺少净空或支撑的落点均不可传送，不穿过第一个遮挡物。保留单次最大楼层高度差 4 米。

右手追踪有效即可传送，不要求左手同时被追踪。恢复追踪或应用焦点后，必须先松开扳机再按下；A 键交互和传送不会在同一次操作中同时执行。扳机按住期间暂停转向与毛巾归还，避免同帧目标改变。

## 代码
- Player/XR/Input/CampusXRInput.cs：Teleport 读取右扳机，Interact 读取右手 A。
- Player/XR/Locomotion/CampusTeleportController.cs：右手直线瞄准、按下锁存、防连发与传送请求。
- Interaction/XR/CampusXRInteractionRay.cs：A 键触发原物体交互。
- CampusXROrigin.prefab 与 XR 场景：rayOrigin 指向 Right Controller Aim。
- CampusXR.inputactions：保持已有 Action GUID，仅调整 Teleport 类型/绑定和 Interact 绑定。
- 原左摇杆传送逻辑已移除，不保留第二套移动触发方式。

## 验证入口
Tools/XRSetup/RunUnityCheck.ps1 -Method CampusEmergency.Editor.CampusControllerValidation.Begin -LogName RightTriggerPlayValidation.log -PlayMode

通过模拟 Oculus Touch 按键驱动实际 Input System 与 TeleportationProvider。具体结果以 Docs/ControllerValidation.txt 为准。真机视觉和实际按键仍需 Quest 3 测试。

修改前备份：Tools/XRSetup/BeforeRightTriggerTeleport。
