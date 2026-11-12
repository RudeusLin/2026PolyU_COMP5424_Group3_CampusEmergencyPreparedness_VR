"""Improve Campus lighting and floor recognition without changing evacuation geometry."""
import copy
import json
from pathlib import Path

import generate_teaching_building as base
import verify_teaching_building as verify
from campus_modeling import printed_label

ROOT = Path(__file__).resolve().parents[1]
LAYOUT = ROOT / "Assets/Environment/TeachingBuildingCampusLayout.json"
BEFORE = ROOT / ".utmp/TeachingBuildingCampusBeforePolish.json"
REVISION = 1
FLOOR_COLORS = {
    1: [.18, .32, .48, 1],
    2: [.46, .32, .15, 1],
    3: [.36, .25, .43, 1],
    4: [.17, .29, .46, 1],
}


def apply_scene_polish(data):
    base.NODES = nodes = data["nodes"]
    if data.get("scenePolishRevision") == REVISION:
        return
    before_nodes = copy.deepcopy(nodes)
    original = list(nodes)
    by_id = {n["id"]: n for n in original}
    children = {}
    for item in original:
        children.setdefault(item["parent"], []).append(item)
    for floor, color in FLOOR_COLORS.items():
        data["materials"].append(dict(name=f"FloorAccent{floor:02}", color=color, smoothness=.12))

    # Absolute values keep incremental updates and complete regeneration identical.
    for item in original:
        parent = by_id.get(item["parent"], {})
        if item["kind"] == "light" and item["name"] == "InteriorLight":
            if parent.get("name", "").startswith("Stair_"):
                item.update(lightIntensity=1.45, lightRange=7.5)
            elif parent.get("name", "").startswith("Floor_") and abs(item["position"][2]) < .1:
                item.update(lightIntensity=1.35, lightRange=7.5)
            elif parent.get("name", "").startswith("Classroom_"):
                item.update(lightIntensity=1.15 if abs(item["position"][0]) < .1 else .85)

        # Floor colours accompany large text; emergency EXIT signs keep their identity.
        if item["kind"] != "group" or not item.get("floor"):
            continue
        floor = item["floor"]
        accent = f"FloorAccent{floor:02}"
        if item["name"].startswith(("Sign_A / FLOOR", "Sign_B / FLOOR")):
            panel = next(n for n in children[item["id"]] if n["name"] == "SignPanel")
            label = next(n for n in children[item["id"]] if n["kind"] == "text")
            item["position"][1] = 2.15
            panel.update(scale=[1.3, .68, .025], material=accent)
            label.update(text=f"{item['name'][5]}  /  LEVEL {floor:02}", textSize=.23,
                         textColor=[1, 1, 1, 1])
            printed_label(label, 1.1, .22)
            label["position"][1] = .13
            detail = base.label(item["id"], "EXIT AT LEVEL 01", (0, -.17, -.0145), .07)
            printed_label(nodes[detail - 1], 1.08, .105)
            for x in [-.45, .45]:
                base.box("FloorSignSuspension", item["id"], (x, .795, .0075),
                         (.016, .91, .016), "Steel", False)
        elif item["name"].startswith("Sign_FLOOR"):
            panel = next((n for n in children[item["id"]] if n["name"] == "SignPanel"), None)
            if panel:
                panel["material"] = accent
        elif item["name"] == "FloorIdentity":
            next(n for n in children[item["id"]] if n["name"] == "IdentityPanel")["material"] = accent

    for level in [n for n in original if n["name"].startswith("Floor_")]:
        # Baked sources sit beneath matching fixtures beside both stair approaches.
        for x in [-11.5, 11.5]:
            fixture = base.node("StairApproachLuminaire", level["id"], (x, 3.18, 0))
            base.box("ApproachLightHousing", fixture, (0, .025, 0), (1.1, .065, .32),
                     "White", False, bevel=.012, roof=True)
            base.box("ApproachLightDiffuser", fixture, (0, -.012, 0), (1.02, .018, .25),
                     "Glow", False, bevel=.005, roof=True)
            base.node("StairApproachLight", level["id"], (x, 3.08, 0), "light",
                      lightIntensity=.95, lightRange=4.5, roof=True)

    # Keep desks and collision bounds fixed; vary only the small objects on top.
    books = [n for n in original if n["name"] == "StudentBook" and n["floor"] == 4]
    for index, book in enumerate(books):
        book["material"] = ["PosterBlue", "PosterCoral", "AccentGold"][index % 3]
        book["rotation"] = base.quat_y([-7, 5, -3][index % 3])
        for pages in children[book["parent"]]:
            if pages["name"] == "BookPages":
                pages["rotation"] = list(book["rotation"])
    for item in original:
        if item["floor"] == 4 and item["name"] in ("DeskUnderframe", "BookShelf", "CabinetPull", "BoardTray"):
            item["bevel"] = .004
    data["scenePolishRevision"] = REVISION
    data["scenePolishOriginalNodeCount"] = len(before_nodes)
    data["scenePolishBeforeNodes"] = [old for old, new in zip(before_nodes, nodes) if old != new]


def upgrade_polish_preview(template):
    template = template.replace('<button data-view="stair">楼梯间</button>',
                                '<button data-view="stair">楼梯间</button><button data-view="landing">楼层标识</button>')
    template = template.replace("const views={", "const views={landing:{eye:[-15.5,12.42,1.5],target:[-15.5,12.95,4.16],fov:1.05},", 1)
    template = template.replace("stair:'楼梯通往一楼侧出口。'", "stair:'楼梯通往一楼侧出口。',landing:'楼层配色结合大号文字；从一楼出口前往集合点。'")
    template = template.replace("stair:'四楼楼梯间'", "stair:'四楼楼梯间',landing:'4F · 楼层标识'")
    assert 'data-view="landing"' in template
    return template


def main():
    data = json.loads(LAYOUT.read_text(encoding="utf-8"))
    if data.get("scenePolishRevision") == REVISION:
        # The ignored local baseline can be recovered after a fresh checkout.
        before = copy.deepcopy(data)
        old_nodes = {n["id"]: n for n in data["scenePolishBeforeNodes"]}
        before["nodes"] = [old_nodes.get(n["id"], n) for n in data["nodes"][:data["scenePolishOriginalNodeCount"]]]
        before["materials"] = [m for m in before["materials"] if m["name"] not in
                               {f"FloorAccent{floor:02}" for floor in FLOOR_COLORS}]
        for key in list(before):
            if key.startswith("scenePolish"):
                del before[key]
        BEFORE.parent.mkdir(parents=True, exist_ok=True)
        BEFORE.write_text(json.dumps(before, ensure_ascii=False), encoding="utf-8")
        print("Campus scene polish is already applied; Unity update baseline restored.")
        return
    before = copy.deepcopy(data)
    BEFORE.parent.mkdir(parents=True, exist_ok=True)
    BEFORE.write_text(json.dumps(before, ensure_ascii=False), encoding="utf-8")
    apply_scene_polish(data)
    assert data["routes"] == before["routes"], "Scene polish changed route definitions"
    counts, errors = verify.validate_routes(data)
    if errors:
        raise RuntimeError("Scene polish obstructs an evacuation route: " + str(errors))
    # Every existing collider keeps exactly the same transform and dimensions.
    for old, new in zip(before["nodes"], data["nodes"]):
        if old["collision"]:
            assert all(old[key] == new[key] for key in ("position", "rotation", "scale", "collision"))
    assert len(data["materials"]) <= 64
    LAYOUT.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    from upgrade_campus_scene import preview_html
    preview_html(data)
    print(f"Campus polish: {len(data['nodes']) - len(before['nodes'])} new nodes; routes {counts} PASS")


if __name__ == "__main__":
    main()
