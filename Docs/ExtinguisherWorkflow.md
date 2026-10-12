# XR 灭火器流程

## 操作

1. 对准场景中的灭火器，按右手 **A** 拿起。瓶身、握把、软管、压力表和标签一起移动。与毛巾互斥，先归还手中物品。
2. 左手靠近灭火器握把旁的**黄色保险销**（18 厘米内），按**左握持键**拔销。持有灭火器时左握持键不再暂停传送。
3. 右手 **B** 在“行动 / 灭火”模式间切换。拿起后默认行动模式。HUD 始终显示模式。
4. 灭火模式下，用右手转动灭火器，使喷嘴射线指向火焰根部。恢复初版半径 0.5 米的根部球形容差、3 米射程；绿色射线和根部圆环表示有效命中。火焰区域不阻挡喷射，墙壁、楼板等实体仍阻挡。
5. 按住**右扳机**连续命中同一火焰根部 3 秒，火焰、烟雾、火星和灯光立即关闭。松开、偏离、遮挡、换目标、切换模式、失去追踪或焦点均清零，不累计。

行动模式：右扳机仍为传送，持有灭火器也能移动。灭火模式：右扳机仅喷射。每次 B 切换、恢复追踪或应用焦点后，必须先松开扳机再按下。A 交互期间暂停喷射。

右握持键归还灭火器（先松开扳机），保险销复位；已扑灭的火不会复燃。重新进入 Play 恢复场景。右摇杆保留 30 度转向，按住扳机时暂停转向。暂不限制灭火剂容量。

火焰可以被 A 射线识别和显示说明，A 不会直接扑灭。火焰没有实体碰撞或 Trigger。数学火区仅检查传送射线，接触或穿过时不可确认传送，扑灭后解除。不会限制实际头显走动；不实现火焰伤害。参见 [火焰阻挡与室外移动](FireBarrierOutdoor.md)。

## 文件组织

- `Assets/Scripts/Interaction/FireSafety/CampusExtinguisher.cs`：完整物体拿取、拔销、归位。
- `Assets/Scripts/Interaction/FireSafety/CampusFireTarget.cs`：单个火焰连续计时、立即熄灭。
- `Assets/Scripts/Player/XR/Interaction/CampusXRExtinguisherController.cs`：B 模式、喷射瞄准与输入、粒子和 HUD 状态。
- `Assets/Editor/XR/CampusExtinguisherSetup.cs`：可重复运行的场景/预制体配置。只配置 XR 场景；8 个灭火器和 9 处火焰。
- `Assets/Editor/XR/CampusExtinguisherValidation.cs`：接入现有控制器 Play Mode 测试。

保留原灭火器模型，附加简化黄色销和黑色喷嘴。现有 `CampusForecourtFire` 效果代码保持不变。

## 回滚与迭代

用户确认火焰正常的基线：`67b80ec`。
永久本地标签：`baseline/fire-verified-20261012`。
开发分支：`feature/extinguisher`。原 `XR` 分支停留在已确认基线。

退出 Play，保存需要保留的修改并关闭 Unity 后，在 Campus_XR 目录执行：

```powershell
git status --short
# 如果有未提交修改，先提交到当前功能分支，或 git stash push -u 保存。
git switch XR
```

这会回到原已确认版本，功能分支的提交仍保留。也可从标签创建回滚试验分支：

```powershell
git switch -c review/fire-baseline baseline/fire-verified-20261012
```

返回开发版本：`git switch feature/extinguisher`。后续每版独立提交；需撤销指定版本时使用 `git revert <提交号>`，不要使用 `reset --hard` 丢弃工作。若将来 XR 分支已推进，以基线标签为准。

## 验证

`ExtinguisherSceneValidation.txt`：完整模型和火焰交互组件覆盖。
`ExtinguisherValidation.txt`：模拟控制器下的拿取、B 模式、保险销、障碍遮挡、3 秒与中断、物品互斥和归还。
`ControllerValidation.txt`：原传送和交互回归。

自动测试不代表真机视觉或握持舒适度已验证。请在 Quest 3 上确认销的位置、喷射方向、模式提示和 3 秒流程。
