# Campus_XR：Quest 3 PC VR 依赖

目标：Unity 2022.3.62f3，Windows x86_64，通过 Meta Horizon Link / Air Link 使用 Quest 3。

## 固定版本

| 包 | 版本 |
|---|---|
| OpenXR Plugin | 1.14.3 |
| XR Interaction Toolkit | 2.6.4 |
| XR Plug-in Management | 4.7.0 |
| Input System | 1.14.0 |
| XR Core Utilities | 2.5.2 |
| XR Legacy Input Helpers（间接功能依赖） | 2.1.12 |
| Mathematics | 1.2.6 |

版本由 Packages/manifest.json 与 Packages/packages-lock.json 记录。Starter Assets 位于 Assets/Samples/XR Interaction Toolkit/2.6.4/Starter Assets，保留官方样例和元数据。

## 配置入口

- Campus Emergency > XR > Configure PC VR dependencies：显式配置 Windows OpenXR loader、Oculus Touch profile、Direct3D11、Multi Pass，以及样例使用的 XR Interaction Layer 31（Teleport）。
- Campus Emergency > XR > Validate PC VR dependencies：检查版本、loader、输入后端、样例和 OpenXR 必需验证规则。报告输出至 Docs/XRDependencyValidation.txt。
- 批处理入口：CampusEmergency.Editor.CampusXRProjectSetup.Configure / Validate。

两个菜单均不会重建或修改教学楼场景，也不会在编辑器启动时自动执行。

Active Input Handling 使用 Both，兼容现有 UnityEngine.Input 桌面预览脚本及新 Input System。切换输入后端后需要重新启动编辑器。

采用 Multi Pass 是为了先保留现有 Built-in 自定义 Shader 的兼容性。完成场景 XR 改造后，应在头显上评估画面与性能，再决定是否适配 Single Pass Instanced。

## 当前边界

本步骤只安装依赖和基础项目配置。没有在校园场景放入 XR Origin，也没有实现射线传送或修改人物模型。安装成功不等于场景已能进行完整 VR 交互。

不为此 PC VR 目标安装 Android SDK/NDK/JDK、Meta XR All-in-One、SteamVR 或手部追踪扩展。

Meta Horizon Link 已在本机安装；检查时系统 OpenXR Runtime 指向 Meta Horizon。实际使用时仍需在头显中连接 Link / Air Link，然后启动 Unity 或 Windows 应用。本步骤不包含头显运行、控制器按键或帧率验证。

## 备份与验证

修改前的依赖及项目设置保存在 Tools/XRSetup/BeforeInstall。该备份仅针对本次安装前状态，不应在后续开发后直接覆盖使用。

以 Docs/XRDependencyValidation.txt 的实际结果为准；批处理导入/编译日志为 Logs/XRDependencySetup.log，重新打开后的独立验证日志为 Logs/XRDependencyVerify.log。

## 官方依据

- Meta Link 开发说明：https://developers.meta.com/vr/documentation/unity/unity-link/
- Unity OpenXR 1.14（支持 Unity 2021.3 LTS+，Windows Oculus PC + Link）：https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/index.html
- XR Interaction Toolkit 2.6：https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.6/manual/index.html