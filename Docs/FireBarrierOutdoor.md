# 火焰射线限制与室外传送

当前版本移除了九处火焰的全部 Collider（包括 Trigger），并删除了追踪位移守卫 CampusXRFireBarrierGuard。火焰不占用人物净空，不会推回 XR Origin，也不会挡住喷射射线。

CampusFireBarrier 保留为纯数学传送射线区域：射线先寻找最近的真实墙壁或地面，再检查从右手到该命中点的线段。线段接触或穿过未扑灭火区时显示无效，右扳机不会提交传送；射线仍画到真实命中点。射线起点已在火区内也会拒绝。墙后、落点地板以下的火区不会误判。扑灭立即解除该火区限制。

检测区域沿用 4.2m × 4.2m 走廊覆盖范围，顶部仍裁切至上方楼板底下 5cm；它是稳定的火区近似，不逐粒子检测火焰或烟雾。选中火焰时可在 Scene 视图看到线框。人物的实际走动不受此区域限制。

灭火恢复初版：喷嘴射线瞄准根部半径 0.5m 区域，射程 3m，同一火焰连续喷射 3 秒后立即消失。使用根部圆环，不再使用盒体下部一米矩形区域。墙壁和楼板继续阻挡喷射。

室外 Ground、Forecourt、MainWalkway、SideExitPath 保留实体地面和传送表面标记；距离、高差、坡度、支撑及普通障碍净空检查继续生效。B 模式切换、拔销、HUD X/H 开关不变。

代码：CampusFireBarrier.cs 管理数学区域；CampusTeleportController.cs 拒绝穿火传送；CampusXRExtinguisherController.cs 检测根部；CampusFireBarrierSetup.cs 生成区域并移除火焰碰撞体。CampusXRInteractionRay.cs 通过数学区域保留火焰说明交互。

验证：FireBarrierValidation.txt、FloorSeparationValidation.txt、ExtinguisherValidation.txt、ControllerValidation.txt。自动验证不代表 Quest 3 真机体验已确认。

修改前版本为 a28efa1。保存修改、关闭 Unity 后，可从该提交新建回滚试验分支：`git switch -c review/before-fire-ray a28efa1`。当前功能版本保留在 feature/extinguisher；不使用 reset --hard。