# 跨楼层火区隔离修复

1. 火区盒体保留 4.2m 横向范围，上限不再统一固定：扫描与占地范围相交的上方 FloorSlab / Landing / HalfLanding，将顶部裁切至最近板底下方 5cm，避免下层火区侵入上层楼板。原火焰视觉位置不变。
2. 逐个火区检查：仅当起点或终点脚底确实高于该火区裁切后的顶部，才免除该火区的传送路径扫掠。仅有台阶高度差、但仍处于同一火区高度范围内，不会免除检测。跨高度传送仍要求真实射线首先命中可行走面，检查距离、高差、坡度、支撑和目标人体净空，不再以穿过下层的直线人体扫掠代替楼梯路径。
3. TeleportationProvider 完成合法传送时，通过 endLocomotion 更新追踪守卫的安全位置，避免守卫将瞬间传送误当实际走动并回拉。正常头显走动仍进行连续火区碰撞检测。

下部一米宽区域灭火、三秒连续判定、扑灭后解除阻挡、HUD X/H 开关均保留。

验证：FireBarrierValidation.txt 检查实际九处火区与楼板边界；FloorSeparationValidation.txt 复现旧跨层斜线误判，验证上下楼落点、上层走动、同层禁穿、射线阻挡、火区内禁落点及真实传送后不回拉。ControllerValidation.txt / ExtinguisherValidation.txt 为整体回归。真机舒适度与具体楼梯路线仍需体验确认。

修改前版本 db4790b；本次在 feature/extinguisher 上单独提交，方便使用 git revert 撤销这一版。