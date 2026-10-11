# Quest 3 手柄与射线修复（2026-10-11）

## 本次修复
- 原 Rig 只有手柄追踪节点，没有可见模型。现增加蓝色左手、橙色右手的简化控制器模型；只在实际追踪有效时显示，不添加碰撞体。
- 射线、转向、归还毛巾原来使用 Application.isFocused，可能因电脑窗口失焦阻断头显输入。改为 OpenXR 会话焦点；系统菜单占用焦点时仍暂停操作。
- HeadTracked / LeftTracked / RightTracked 开启初始状态检查，进入游戏时已被追踪的设备也能立即被识别。
- 模拟实际 Oculus Touch 布局发现 OpenXR 1.14.3 的旧 Vector2Control 摇杆与 FinishSetup 所需 StickControl 类型不兼容。已启用包提供的 USE_STICK_CONTROL_THUMBSTICKS Standalone 定义。依赖版本未更换，PackageCache 源码未修改。
- HUD 增加头显未追踪、左/右手柄未追踪、系统菜单焦点提示。
- 保留四楼出生点、校园资源、毛巾交互、传送落点白名单、防穿墙及室内空间检测。

## 验证
真实 Unity Play Mode 下添加模拟 XRHMD 和 Oculus Touch 输入设备，经过 Input System 读取实际绑定，验证：初始追踪、手柄显示、右手直线、左摇杆弧线、松杆调用真实 TeleportationProvider 移动相机、追踪丢失后隐藏与取消。
无窗口测试使用临时克隆 InputSettings 接收合成事件，测试结束恢复原设置，不改变项目真实输入策略。焦点判断另有断言覆盖头显焦点与电脑窗口焦点不一致的情况。

报告：Docs/ControllerValidation.txt
日志：Logs/ControllerPlayValidation.log
重跑：Tools/XRSetup/RunUnityCheck.ps1 -Method CampusEmergency.Editor.CampusControllerValidation.Begin -LogName ControllerPlayValidation.log -PlayMode

这是模拟设备集成验证，尚未用真实 Quest 3 测试视觉位置、手柄按键与性能。

## 使用
连接 Link 后打开 Assets/Scenes/TeachingBuildingCampusXR.unity 并进入 Play。
正常追踪时能看到蓝色左手模型、橙色右手模型及右手交互直线。
左摇杆先归中，再前推保持显示传送弧线；绿色有效，红色不可落地；松开确认，左握持键取消。
如果没有射线，请读取头显 HUD 中的追踪提示。

## 备份
Tools/XRSetup/BeforeControllerFix 保存本次修改前的相关脚本、Rig prefab、ProjectSettings 和 HUD 源文件。
