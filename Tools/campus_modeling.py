"""Apply the agreed room, washroom and refuge-room changes to Campus only."""
import copy
import hashlib
import json
import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

import generate_teaching_building as base
import refine_teaching_building as refined

ROOT = Path(__file__).resolve().parents[1]
LAYOUT = ROOT / "Assets/Environment/TeachingBuildingCampusLayout.json"
BEFORE = ROOT / ".utmp/TeachingBuildingCampusBeforeModeling.json"
LETTERING = "Assets/Environment/TeachingBuildingCampus/Textures/Lettering"
REVISION = 1


def printed_label(item, width, height):
    """Rasterize only changed lettering; retain all existing campus glyph assets."""
    candidates = [Path(os.environ.get("CAMPUS_FONT_DIR", "")) / "Arial.ttf",
                  Path("C:/Windows/Fonts/arial.ttf"),
                  Path("/System/Library/Fonts/Supplemental/Arial.ttf"),
                  Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")]
    font_path = next((p for p in candidates if p.is_file()), None)
    if font_path is None:
        raise RuntimeError("Set CAMPUS_FONT_DIR to a folder containing Arial.ttf")
    font = ImageFont.truetype(str(font_path), 96)
    lines = item["text"].split("\n")
    lengths = [font.getlength(line) for line in lines]
    pixels = Image.new("RGBA", (int(max(lengths)) + 20, len(lines) * 112 + 20))
    draw = ImageDraw.Draw(pixels)
    for i, line in enumerate(lines):
        draw.text(((pixels.width - lengths[i]) / 2, 10 + i * 112), line,
                  font=font, fill="white", anchor="lt")
    aspect = pixels.width / pixels.height
    h = min(height, width / aspect)
    w = h * aspect
    pixels.thumbnail((1024, 1024), Image.Resampling.LANCZOS)
    digest = hashlib.sha256(("modeling-v1:" + item["text"]).encode()).hexdigest()[:16]
    relative = f"{LETTERING}/Modeling_{digest}.png"
    path = ROOT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    pixels.save(path)
    refined.texture_metadata(path)
    meta = Path(str(path) + ".meta")
    meta.write_text(meta.read_text().replace("maxTextureSize: 512", "maxTextureSize: 1024")
                    .replace("alphaIsTransparency: 0", "alphaIsTransparency: 1"))
    item.update(printTexture=relative, printWidth=w, printHeight=h)


def apply_modeling_changes(data):
    if data.get("modelingRevision") == REVISION:
        base.NODES = data["nodes"]
        return finish_modeling_layout(data)
    base.NODES = nodes = data["nodes"]
    original = list(nodes)
    by_id = {n["id"]: n for n in original}

    def children(parent):
        return [n for n in nodes if n["parent"] == parent]

    def descendants(parent):
        result = []
        for child in children(parent):
            result.append(child)
            result.extend(descendants(child["id"]))
        return result

    def replace_sign(sign, value, material="Teal", color=(1, 1, 1, 1)):
        panel = next(n for n in children(sign["id"]) if n["name"] == "SignPanel")
        label = next(n for n in children(sign["id"]) if n["kind"] == "text")
        sign["name"] = "Sign_" + value
        label["name"] = "Label_" + value
        panel.update(scale=[1.28, .36, .018], material=material)
        sign["position"][1] = 1.7
        label.update(text=value, textColor=list(color), position=[0, 0, -.011])
        printed_label(label, 1.08, .27)

    material_names = {m["name"] for m in data["materials"]}
    for name, color, metallic, smoothness in [
            ("RefugeDoor", [.22, .31, .37, 1], .45, .3),
            ("MirrorSurface", [.94, .96, .98, 1], 1, .98),
            ("TowelFabric", [.88, .85, .75, 1], 0, .04),
            ("TapWater", [.32, .72, .92, .65], 0, .65)]:
        if name not in material_names:
            data["materials"].append(dict(name=name, color=color, metallic=metallic, smoothness=smoothness))

    for level in [n for n in original if n["name"].startswith("Floor_")]:
        floor, parent = level["floor"], level["id"]
        restroom = next(n for n in children(parent) if n["name"] == "Restrooms")
        mirror = next(n for n in children(restroom["id"]) if n["name"] == "Mirror")
        washroom_parent = restroom["id"]
        if floor == 4:
            # Keep both door openings and structural room walls; exchange room use.
            new_restroom = base.node("Restrooms_F04", parent, floor=4)
            washroom_parent = new_restroom
            restroom["name"] = "Room403"
            wc_sign = next(n for n in children(restroom["id"]) if n["name"] == "Sign_WC")
            room_sign = next(n for n in children(parent) if n["name"] == "Sign_403")
            room_frame = next(n for n in children(parent) if n["name"] == "DoorFrame"
                              and abs(n["position"][0] + 7.8) < .01 and n["position"][2] < 0)
            room_frame["parent"] = room_sign["parent"] = new_restroom
            replace_sign(room_sign, "WC")
            replace_sign(wc_sign, "403", "Paper", (.10, .17, .19, 1))
            for child in list(children(restroom["id"])):
                x, _, z = child["position"]
                sanitary = (child["name"] in ("ToiletBase", "ToiletTank", "StallDivider",
                                               "WashCounter", "CounterSupport", "Mirror")
                            or (child["name"] in ("Wall_Partition", "WallDado") and z < -5))
                if not sanitary:
                    continue
                child["parent"] = new_restroom
                child["position"][0] -= 18
                if child["name"] == "Wall_Partition":
                    # Cubicle panels stop before the facade glazing; they are not masonry.
                    child.update(name="StallSidePanel", position=[x - 18, 1.1, -6.85],
                                 scale=[.09, 2.2, 3.3])
                elif child["name"] == "WallDado":
                    child.update(active=False, collision=False)
            base.box("CollaborationTable", restroom["id"], (10.15, .76, -4.2),
                     (1.65, .08, 1), "Wood", bevel=.02)
            for x in [9.52, 10.78]:
                for z in [-4.55, -3.85]:
                    base.box("CollaborationTableLeg", restroom["id"], (x, .36, z),
                             (.055, .72, .055), "Steel")
            for z in [-3.2, -5.2]:
                base.box("CollaborationBench", restroom["id"], (10.15, .43, z),
                         (1.65, .1, .4), "Wood", bevel=.015)
            # WC is reached directly across from 401; its basin is beside the entrance.
            station_position, yaw = (-5.65, 0, -1.9), 0
            mirror.update(position=[-5.65, 1.65, -1.615], scale=[1.8, .95, .025])
            for window in [n for n in original if n["name"] == "WindowBay" and n["floor"] == 4
                           and n["position"][2] < -8.8]:
                x = window["position"][0]
                if not (-12.8 < x < 0 or 7.5 < x < 12.8):
                    continue
                glass_material = "FrostedGlass" if x < 0 else "Glass"
                for pane in children(window["id"]):
                    if pane["name"] == "WindowGlass":
                        pane["material"] = glass_material
        else:
            station_position, yaw = (12.3, 0, -3.5), 90

        for old_counter in children(washroom_parent):
            if old_counter["name"] in ("WashCounter", "CounterSupport"):
                old_counter.update(active=False, collision=False)
        mirror["material"] = "MirrorSurface"
        station = base.node("WashStation", washroom_parent, station_position, yaw=yaw, floor=floor)
        mirror.update(parent=station, position=[0, 1.65, .285], scale=[1.8, .95, .025], rotation=base.quat_y(0))
        for x, width in [(-.65, .65), (.82, .3)]:
            base.box("CounterSide", station, (x, .91, 0), (width, .08, .7), "White", bevel=.012)
        for z, depth in [(-.29, .12), (.27, .16)]:
            base.box("CounterEdge", station, (0, .91, z), (1.95, .08, depth), "White", bevel=.012)
        base.box("CounterCabinet", station, (0, .39, .05), (1.85, .78, .56), "WallLower", bevel=.02)
        base.box("BasinBottom", station, (.25, .79, -.035), (.65, .035, .42), "White", bevel=.02)
        for x in [-.10, .60]:
            base.box("BasinSide", station, (x, .86, -.035), (.06, .16, .48), "White", bevel=.012)
        for z in [-.255, .185]:
            base.box("BasinSide", station, (.25, .86, z), (.7, .16, .055), "White", bevel=.012)
        base.node("BasinDrain", station, (.25, .812, -.035), "cylinder", (.055, .004, .055), "Steel")
        faucet = base.node("CampusFaucet", station, (.25, .93, .24), component="faucet")
        base.node("TapBase", faucet, (0, .018, 0), "cylinder", (.075, .018, .075), "Steel", collision=True)
        base.node("TapStem", faucet, (0, .15, 0), "cylinder", (.045, .15, .045), "Steel", collision=True)
        base.box("TapSpout", faucet, (0, .295, -.13), (.045, .045, .27), "Steel", bevel=.008)
        base.node("TapNozzle", faucet, (0, .265, -.245), "cylinder", (.05, .04, .05), "Steel")
        base.box("TapLever", faucet, (.065, .32, .005), (.18, .025, .045), "Steel", bevel=.008)
        flow = base.node("TapWaterFlow", faucet, (0, .045, -.245), "cylinder",
                         (.018, .18, .018), "TapWater", active=False)
        nodes[faucet - 1]["waterNode"] = flow
        towel = base.node("CampusTowel", station, (-.65, .974, -.015), component="towel")
        base.box("FoldedTowel", towel, (0, 0, 0), (.5, .035, .36), "TowelFabric", bevel=.008)
        base.box("TowelFold", towel, (0, .024, .065), (.49, .018, .21), "TowelFabric", False, bevel=.004)
        for x in [-.19, -.16]:
            base.box("TowelStripe", towel, (x, .035, .065), (.015, .003, .2), "Teal", False)
        for x in [-.94, .94]:
            base.box("MirrorFrame", station, (x, 1.65, .31), (.04, 1.02, .025), "Steel", False)
        for y in [1.16, 2.14]:
            base.box("MirrorFrame", station, (0, y, .31), (1.92, .04, .025), "Steel", False)

        central_sign = next(n for n in children(parent) if n["name"] ==
                            ("Sign_SECURITY" if floor == 1 else f"Sign_{floor}04"))
        replace_sign(central_sign, "REFUGE ROOM")
        entrance_x = 5.5 if floor == 1 else 4.1
        frame = next(n for n in children(parent) if n["name"] == "DoorFrame"
                     and abs(n["position"][0] - entrance_x) < .01 and n["position"][2] < 0)
        frame["name"] = "RefugeDoor"
        hinge = next(n for n in children(frame["id"]) if n.get("component") == "door")
        hinge.update(startsOpen=False, rotation=base.quat_y(0))
        panel = next(n for n in children(hinge["id"]) if n["name"] == "DoorPanel")
        panel["material"] = "RefugeDoor"
        base.box("RefugePushBar", hinge["id"], (.71, 1.04, -.09), (1.06, .045, .045), "Steel", False, bevel=.008)
        base.box("RefugeDoorCloser", hinge["id"], (.55, 2.12, -.065), (.23, .075, .065), "Steel", False, bevel=.012)
        base.node(f"RefugeRoom_F{floor:02}", parent, (3.75 if floor > 1 else 5.75, 0, -5.2))

    for board in [n for n in original if n["floor"] == 4 and n["name"] == "CampusNoticeBoard"
                  and n["position"][0] > 0]:
        board["position"][0] = 6.15
    for label in [n for n in original if n["kind"] == "text" and "404 / FACULTY OFFICE" in n["text"]]:
        label["text"] = "403 / COLLABORATION\n\nREFUGE ROOM / CENTRAL"
        printed_label(label, .46, .42)
    for name in ["WashStation", "RefugeDoor"]:
        if name not in data["prefabNames"]:
            data["prefabNames"].append(name)
    finish_modeling_layout(data)
    # Moved legacy nodes may now reference an appended parent; keep import order valid.
    by_id = {n["id"]: n for n in nodes}
    ordered, visited = [], set()

    def visit(item):
        if item["id"] in visited:
            return
        if item["parent"]:
            visit(by_id[item["parent"]])
        visited.add(item["id"])
        ordered.append(item)

    for item in nodes:
        visit(item)
    data["nodes"] = base.NODES = ordered
    data["modelingRevision"] = REVISION
    return True


def finish_modeling_layout(data):
    changed = False
    nodes = data["nodes"]
    for sign in [n for n in nodes if n["name"] == "Sign_REFUGE ROOM"]:
        position = [5.5 if sign["floor"] == 1 else 4.1, 2.68, -1.38]
        if sign["position"] != position:
            sign["position"] = position
            changed = True
    refuge_frames = {n["id"] for n in nodes if n["name"] == "RefugeDoor"}
    hinges = {n["id"] for n in nodes if n["parent"] in refuge_frames and n.get("component") == "door"}
    for part in nodes:
        if part["parent"] in hinges and part["kind"] in ("box", "cylinder") and part["material"] == "Wood":
            part["material"] = "RefugeDoor"
            changed = True
    if not any(n["name"] == "RestroomCeilingFixture" for n in nodes):
        parent = next(n["id"] for n in nodes if n["name"] == "Restrooms_F04")
        base.box("RestroomCeilingFixture", parent, (-5.65, 3.27, -3.3),
                 (1.5, .055, .38), "Glow", False, floor=4, roof=True)
        base.node("RestroomTaskLight", parent, (-5.65, 3.05, -3.3), "light",
                  lightIntensity=1.1, lightRange=7, floor=4)
        changed = True
    return changed


def main():
    import verify_teaching_building as verify
    from upgrade_campus_scene import preview_html, validate_window_junctions
    data = json.loads(LAYOUT.read_text(encoding="utf-8"))
    before = copy.deepcopy(data)
    if not apply_modeling_changes(data):
        print("Campus modeling changes already applied.")
        return
    counts, errors = verify.validate_routes(data)
    if errors:
        raise RuntimeError("Modeling changes obstruct an evacuation route")
    data["facadeJunctionCount"] = validate_window_junctions(data)
    BEFORE.parent.mkdir(exist_ok=True)
    BEFORE.write_text(json.dumps(before, ensure_ascii=False), encoding="utf-8")
    LAYOUT.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    preview_html(data)
    base.ensure_metadata()
    print(f"Campus modeling updated: {len(data['nodes'])} nodes; {data['facadeJunctionCount']} facade junctions PASS")


def upgrade_modeling_preview(template):
    template = template.replace("const views={", "const views={washroom:{eye:[-7.1,12.45,-3.7],target:[-5.65,12.1,-1.7],fov:1.15},refuge:{eye:[3.3,12.45,.7],target:[4.8,12.4,-1.5],fov:1.25},")
    template = template.replace("const descriptions={", "const descriptions={washroom:'4F · 洗手台、镜面与毛巾；交互在 Unity 中体验。',refuge:'4F · 防火房间入口与独立门牌。',")
    template = template.replace("student:'学生角色'", "student:'学生角色',washroom:'洗手台',refuge:'防火房间'")
    template = template.replace("student:'学生角色 · Unity 中按 V 切换第三人称'", "student:'学生角色 · Unity 中按 V 切换第三人称',washroom:'4F · 洗手台与毛巾',refuge:'4F · 防火房间'")
    anchor = "document.querySelectorAll('[data-view]').forEach(b=>b.addEventListener"
    buttons = "document.querySelector('.modes').insertAdjacentHTML('beforeend','<button data-view=\"washroom\">洗手台</button><button data-view=\"refuge\">防火房间</button>');"
    return template.replace(anchor, buttons + anchor, 1)


if __name__ == "__main__":
    main()
