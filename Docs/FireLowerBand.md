> 历史方案，已停用。当前恢复根部瞄准，火焰没有碰撞体；参见 [当前逻辑](FireBarrierOutdoor.md)。以下记录仅供版本对照，不是当前操作说明。

# Fire lower-band aiming

Spray now targets the first solid fire box surface within 3 metres of the nozzle. The valid band spans its full width and the bottom 1 metre of its world-space height. The small central root sphere is no longer used. Aim high and the stream still stops at the box, but suppression does not accumulate.

A green rectangle marks the valid exposed face. A yellow rectangle directs the player downward when aiming at the upper part. The beam stops at the first obstruction, and cannot target a separate fire behind it or a wall. Coplanar overlapping fire boxes retain the currently selected eligible target; a different front face is not bypassed to preserve a lock.

Pin removal, mode switching, continuous 3-second timing, interruption reset and extinguishing behavior are unchanged. Leaving the lower band or changing target resets progress. Full fire colliders and player/teleport blocking are retained. A subsequent floor-separation fix is documented in FloorSeparation.md.

Validation: ExtinguisherValidation.txt and ControllerValidation.txt cover broad off-center targeting, 1m height boundary, side faces, 3m surface range, wall obstruction, stable coplanar targeting, timer resets, fire-blocker release and movement/input regression. Headset visuals require hardware confirmation.

Previous version: 7a7dd8d. Current development remains on feature/extinguisher; commits can be reverted independently.