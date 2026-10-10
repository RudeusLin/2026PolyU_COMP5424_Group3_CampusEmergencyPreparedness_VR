"""Create a separate campus art pass from the saved refined layout.

All new textures, furniture details, poster layouts and preview audio are original.
The existing scenes, refined assets and route definitions are retained.
"""
import base64
import copy
import json
import math
import wave
import io
from pathlib import Path

import numpy as np
from PIL import Image

import generate_teaching_building as base
import refine_teaching_building as refined
import verify_teaching_building as verify
import import_campus_pbr as pbr
from campus_preview_lighting import upgrade_preview
from campus_signage import prepare_signage
from campus_preview_ui import restyle_preview
from campus_student import prepare_walkthrough
from campus_modeling import apply_modeling_changes, upgrade_modeling_preview

ROOT = Path(__file__).resolve().parents[1]
ASSETS = "Assets/Environment/TeachingBuildingCampus"
PREVIEWS = "Docs/Previews/Campus"


def world_geometry(nodes):
    world = {}
    for item in nodes:
        world[item['id']] = (world[item['parent']] if item['parent'] else np.eye(4)) @ verify.matrix(item)
    return world


def rebuild_windows(data):
    """Derive facade openings from room partitions, with real masonry junctions."""
    nodes = data['nodes']
    old_windows = {n['id'] for n in nodes if n['name'] == 'WindowBay'}
    side_windows = [copy.deepcopy(n) for n in nodes if n['id'] in old_windows and abs(n['position'][0]) == 18]
    widths = {n['parent']: n['scale'][0] for n in nodes if n['name'] == 'Wall_WindowSill'}
    retained = [n for n in nodes if n['id'] not in old_windows and n['parent'] not in old_windows]
    ids = {n['id']: i + 1 for i, n in enumerate(retained)}
    references = ['parent', 'spawnSecond', 'spawnFourth', 'assembly', 'roofNode', 'smokeNode', 'walkCamera', 'overviewCamera']
    for item in retained:
        item['id'] = ids[item['id']]
        for key in references:
            if item.get(key):
                item[key] = ids[item[key]]
    data['nodes'] = base.NODES = retained
    transforms = world_geometry(retained)
    b, n = base.box, base.node

    def opening(parent, position, width, yaw, frosted=False):
        group = n('WindowBay', parent, position, yaw=yaw)
        b('Wall_WindowSill', group, (0, .525, 0), (width, 1.05, .4), 'Brick')
        # Tiny eased edges use the existing detail/probe lighting path, avoiding
        # subpixel lightmap charts on this 14 mm interior finish layer.
        b('WindowInteriorDado', group, (0, .52, .207), (width, 1.04, .014), 'WallLower', False, bevel=.002)
        b('WindowInteriorSkirting', group, (0, .07, .22), (width, .14, .025), 'Teal', False)
        b('Wall_WindowHeader', group, (0, 3.065, 0), (width, .67, .4), 'Wall')
        b('WindowSillShelf', group, (0, 1.065, 0), (width + .04, .04, .49), 'Stone', False, bevel=.008)
        # Frames sit inside the reveal. Individual panes stop at each mullion.
        count = max(1, math.ceil(width / 1.35))
        pane_width = width / count
        for x in [-width / 2, width / 2]:
            b('WindowReveal', group, (x, 1.895, 0), (.025, 1.67, .4), 'Stone', False)
        for y in [1.11, 2.34, 2.68]:
            b('WindowFrame', group, (0, y, 0), (width, .055, .11), 'WindowAluminium', False, bevel=.007)
        for i in range(count + 1):
            b('WindowMullion', group, (-width / 2 + i * pane_width, 1.895, 0),
              (.055, 1.625, .11), 'WindowAluminium', False, bevel=.007)
        for i in range(count):
            x = -width / 2 + (i + .5) * pane_width
            for low, high in [(1.1375, 2.3125), (2.3675, 2.6525)]:
                b('WindowGlass', group, (x, (low + high) / 2, 0),
                  (pane_width - .055, high - low, .012), 'FrostedGlass' if frosted else 'Glass')
            b('WindowLatch', group, (x + pane_width / 2 - .1, 1.78, .075),
              (.022, .12, .035), 'Steel', False, bevel=.004)
        if not frosted:
            b('RollerBlindCassette', group, (0, 2.745, .265), (width - .07, .08, .09), 'White', False, bevel=.008)
            b('RaisedRollerBlind', group, (0, 2.61, .265), (width - .1, .19, .014), 'Paper', False)
            b('BlindBottomRail', group, (0, 2.515, .265), (width - .1, .018, .022), 'Steel', False)

    for facade in [item for item in retained if item['name'].startswith('Facade_F')]:
        floor, parent = facade['floor'], facade['id']
        for z in [-9, 9]:
            boundaries = {-18., 18.}
            for item in retained:
                if item['name'] != 'Wall_Partition' or item['floor'] != floor:
                    continue
                transform = transforms[item['id']]
                center = transform[:3, 3]
                extent = np.abs(transform[:3, :3]) @ np.ones(3) / 2
                endpoint = center[2] + math.copysign(extent[2], z)
                if extent[0] < .2 and abs(endpoint - z) < .25:
                    boundaries.add(round(float(center[0]), 4))
            entrance = floor == 1 and z < 0
            if entrance:
                boundaries.update([-3., 3.])
            ordered = sorted(boundaries)
            for left, right in zip(ordered, ordered[1:]):
                if (z > 0 and left == -2.2 and right == 2.2) or (entrance and -3 <= left < right <= 3):
                    continue
                count = math.ceil((right - left) / 4.8)
                boundaries.update(left + (right - left) * i / count for i in range(1, count))
            solids = []
            for x in sorted(boundaries):
                half = .3 if round(x, 4) in ordered else .14
                left, right = max(-18, x - half), min(18, x + half)
                if solids and left - solids[-1][1] < .55:
                    solids[-1][1] = right
                else:
                    solids.append([left, right])
            if z > 0:
                # The enclosed service core behind the lift has a solid rear wall.
                solids = [s for s in solids if not (-2.5 <= s[0] and s[1] <= 2.5)] + [[-2.5, 2.5]]
                solids.sort()
            for left, right in solids:
                if entrance and -3 < (left + right) / 2 < 3:
                    continue
                pier = n('FacadeMasonryPier', parent, ((left + right) / 2, 0, z), yaw=180 if z > 0 else 0)
                b('WindowPier', pier, (0, 1.7, 0), (right - left, 3.4, .4), 'Wall')
                b('PierInteriorDado', pier, (0, .52, .207), (right - left, 1.04, .014), 'WallLower', False, bevel=.002)
            for first, second in zip(solids, solids[1:]):
                left, right = first[1], second[0]
                if right - left < .5 or (entrance and -3 < (left + right) / 2 < 3):
                    continue
                opening(parent, ((left + right) / 2, 0, z), right - left, 180 if z > 0 else 0,
                        frosted=z < 0 and left >= 7.2)
    for old in side_windows:
        opening(ids[old['parent']], old['position'], widths[old['id']] - .6,
                -90 if old['position'][0] > 0 else 90)
        for offset in [-widths[old['id']] / 2 + .15, widths[old['id']] / 2 - .15]:
            b('SideWindowPier', ids[old['parent']], (old['position'][0], 1.7, old['position'][2] + offset),
              (.4, 3.4, .3), 'Wall')


def validate_window_junctions(data):
    transforms = world_geometry(data['nodes'])
    geometry = []
    for item in data['nodes']:
        t = transforms[item['id']]
        if item['name'] == 'WindowBay':
            inward = np.array([-t[0, 3], 0, -t[2, 3]])
            assert np.dot(t[:3, 2], inward) > 0, 'Window interior fittings face outdoors'
        center, extent = t[:3, 3], np.abs(t[:3, :3]) @ np.ones(3) / 2
        geometry.append((item, center - extent, center + extent))
    count = 0
    for wall, low, high in geometry:
        if wall['name'] != 'Wall_Partition' or high[0] - low[0] > .4:
            continue
        for z in [-9, 9]:
            endpoint = low[2] if z < 0 else high[2]
            if abs(endpoint - z) > .25:
                continue
            x, y = (low[0] + high[0]) / 2, (low[1] + high[1]) / 2
            assert any(n['name'] == 'WindowPier' and a[0] <= low[0] and b[0] >= high[0]
                       and a[1] <= y <= b[1] and a[2] - 1e-5 <= endpoint <= b[2] + 1e-5
                       for n, a, b in geometry), f"Unsupported partition at {x}, {z}, floor {wall['floor']}"
            for glass, a, b in geometry:
                if glass['name'] == 'WindowGlass' and a[1] <= y <= b[1] and abs((a[2] + b[2]) / 2 - z) < .05:
                    assert b[0] <= low[0] or a[0] >= high[0], f'Glass crosses wall at {x}, {z}'
            count += 1
    assert count >= 40, 'Missing facade junction coverage'
    return count


def create_textures():
    rng = np.random.default_rng(54242026)
    size = 512
    y, x = np.mgrid[:size, :size]
    folder = ROOT / ASSETS / "Textures"
    folder.mkdir(parents=True, exist_ok=True)
    grain = rng.normal(0, 1, (size, size))
    weave = 236 + np.sin(x * math.tau / 4) * 7 + np.sin(y * math.tau / 4) * 7 + grain * 2
    cork = 238 + grain * 7 + np.sin((x * 11 + y * 13) * math.tau / size) * 3
    # Stone and wood come from the bundled scans; only these surfaces are procedural.
    for name, array in [("Fabric", weave), ("Cork", cork)]:
        if array.ndim == 2:
            array = np.repeat(array[..., None], 3, axis=2)
        path = folder / (name + ".png")
        Image.fromarray(np.clip(array, 0, 255).astype(np.uint8)).save(path)
        refined.texture_metadata(path)
        ao = np.ones_like(grain) * .99
        height = np.clip(.6 + grain * .012, 0, 1)
        smoothness = np.ones_like(grain) * .4
        detail = folder / (name + "Detail.png")
        Image.fromarray((np.stack([ao, height, smoothness], axis=2) * 255).astype(np.uint8)).save(detail)
        refined.texture_metadata(detail, linear=True)
    radius = np.sqrt(((x - 255.5) / 255.5) ** 2 + ((y - 255.5) / 255.5) ** 2)
    alpha = np.clip(1 - radius, 0, 1) ** 2
    sprite = np.ones((size, size, 4), dtype=np.uint8) * 255
    sprite[..., 3] = (alpha * 255).astype(np.uint8)
    path = folder / "SmokeSprite.png"
    Image.fromarray(sprite).save(path)
    refined.texture_metadata(path)
    # Generated preview tone, not a recording of an actual campus alarm.
    sample_rate = 22050
    t = np.arange(sample_rate * 4) / sample_rate
    phase = t % 2
    frequency = 520 + 80 * np.sin(t * math.tau * .8)
    carrier = np.sin(np.cumsum(frequency) * math.tau / sample_rate)
    envelope = np.clip(phase / .08, 0, 1) * np.clip((1.1 - phase) / .12, 0, 1)
    signal = (.18 * carrier * envelope * 32767).astype("<i2")
    audio_path = ROOT / ASSETS / "Audio/DrillAlarm.wav"
    audio_path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(audio_path), "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(sample_rate)
        audio.writeframes(signal.tobytes())


def art_pass(data):
    base.NODES = data["nodes"]
    base.MATERIALS = {m["name"]: m["color"] for m in data["materials"]}
    data["materials"].extend([
        dict(name="WindowAluminium", color=[.16, .19, .20, 1], metallic=.65, smoothness=.36),
        dict(name="FrostedGlass", color=[.80, .85, .84, .68], metallic=0, smoothness=.18),
    ])
    rebuild_windows(data)
    original = list(base.NODES)
    colors = {"Wall": [.89, .88, .84, 1], "WallLower": [.72, .79, .77, 1],
              "Floor": [.91, .90, .86, 1], "Corridor": [.89, .90, .87, 1],
              "Wood": [.76, .58, .38, 1], "Teal": [.035, .29, .31, 1]}
    for material in data["materials"]:
        if material["name"] in colors:
            material["color"] = colors[material["name"]]
    for name, color, surface in [("SeatFabric", [.06, .26, .31, 1], "Fabric"),
                                  ("Cork", [.67, .48, .30, 1], "Cork")]:
        data["materials"].append(dict(name=name, color=color, texture=f"{ASSETS}/Textures/{surface}.png",
                                     detailTexture=f"{ASSETS}/Textures/{surface}Detail.png",
                                     tileMeters=.3 if surface == "Fabric" else 1, bumpMeters=.0005,
                                     metallic=0, smoothness=.06))
    for name, color in [("PosterCoral", [.76, .26, .18, 1]),
                        ("PosterBlue", [.12, .30, .47, 1])]:
        data["materials"].append(dict(name=name, color=color, metallic=0, smoothness=.12))
    for material in data["materials"]:
        name = material["name"]
        if name in ["Floor", "Corridor", "Wood"]:
            scan = "ScannedOak" if name == "Wood" else "ScannedTerrazzo"
            material.update(texture=f"{ASSETS}/Textures/PBR/{scan}.png",
                            detailTexture=f"{ASSETS}/Textures/PBR/{scan}Detail.png",
                            color=[1, 1, 1, 1], tileMeters=.5 if name == "Wood" else 2,
                            bumpMeters=.0004 if name == "Wood" else .0012,
                            metallic=0, smoothness=.85 if name == "Wood" else .65)
        if name in ["Wall", "WallLower", "Ceiling"]:
            material["smoothness"] = .08
        if name == "Glass":
            material.update(color=[.88, .94, .96, .09], smoothness=.92)
    for item in original:
        if item["kind"] == "sun":
            item["rotation"] = base.look_rotation([0, 0, 0], [-.4, -.85, -.5])
        if item["kind"] == "light":
            item["lightIntensity"] *= .5
    base.MATERIALS = {m["name"]: m["color"] for m in data["materials"]}
    n, b, s = base.node, base.box, base.sign

    def text(parent, value, position, size, color="Navy"):
        return base.label(parent, value, position, size, color=base.MATERIALS[color])

    def plant(parent, position, scale=1):
        group = n("CampusPlanter", parent, position, scale=(scale, scale, scale))
        n("CeramicPot", group, (0, .26, 0), "cylinder", (.36, .25, .36), "White", collision=True)
        n("PotSoil", group, (0, .515, 0), "cylinder", (.31, .008, .31), "Bark")
        for i in range(7):
            angle = math.tau * i / 7
            p = (.22 * math.cos(angle), .85 + i % 3 * .13, .22 * math.sin(angle))
            base.beam(group, "PlantStem", (0, .52, 0), p, "Leaf", .018)
            n("PlantLeaf", group, p, "sphere", (.15, .48, .09), "LeafLight" if i % 2 else "Leaf",
              rotation=base.quat_y(i * 51))
        return group

    def poster(parent, position, headline, body, accent, width=.82, height=.94):
        group = n("Poster_" + headline, parent, position)
        b("PosterPaper", group, (0, 0, 0), (width, height, .012), "Paper", False)
        b("PosterHeader", group, (0, height / 2 - .13, -.009), (width, .26, .009), accent, False)
        text(group, headline, (0, height / 2 - .13, -.02), .075, "White")
        text(group, body, (0, -.035, -.021), .058)
        b("PosterRule", group, (0, -.32, -.012), (width - .14, .015, .008), accent, False)
        text(group, "HARBOURVIEW / STUDENT LIFE", (0, -.39, -.021), .028)
        for px in [-width / 2 + .04, width / 2 - .04]:
            n("NoticePin", group, (px, height / 2 - .025, -.022), "sphere", (.024, .024, .02), "AccentGold")

    def notice_board(parent, position, yaw=180):
        group = n("CampusNoticeBoard", parent, position, yaw=yaw)
        b("NoticeFrame", group, (0, 0, 0), (3.2, 1.5, .075), "Wood", False, bevel=.025)
        b("CorkBacking", group, (0, 0, -.045), (3.1, 1.4, .015), "Cork", False)
        for px, title, body, accent in [(-1, "CAMPUS LIFE", "STUDENT SOCIETIES\n\nMeet / Learn / Connect", "PosterBlue"),
                                        (0, "DESIGN WEEK", "IDEAS IN ACTION\n\nExhibition / Studio 403", "Teal"),
                                        (1, "SAFETY FIRST", "Keep exits clear\n\nKnow your assembly point", "PosterCoral")]:
            poster(group, (px, -.05, -.06), title, body, accent)
        text(group, "WHAT'S ON / TEACHING BLOCK A", (0, .61, -.058), .065, "White")
        return group

    for item in original:
        if item["name"] in ["ChairSeat", "ChairBack"]:
            item["material"] = "SeatFabric"
        if item["kind"] == "text" and item["text"].startswith("CLASSROOM 401"):
            item["text"] = "FIRE EVACUATION DRILL\n\nRaise the alarm / Follow EXIT signs\nUse stairs / Go to the assembly point"
            item["textSize"] = .10
        if item["kind"] == "text" and "CAMPUS NOTICE" in item["text"] and item["floor"] == 4:
            base.NODES[item["parent"] - 1]["active"] = False
        if item["name"] == "Smoke_VisualOnly_ToggleH":
            item["component"] = "campusAtmosphere"
        if item["name"] == "SmokeVolume":
            index = item["id"] % 5
            item["position"] = [7.5 + index * .18, 13.35 + (index % 2) * .1, 1.9 + index * .15]
            item["scale"] = [.75, .5, .75]
        if item["name"] == "Hazard_F04_Corridor":
            item["position"] = [7.8, 10.8, 2.1]

    room = next(item for item in original if item["name"] == "Classroom_Detailed" and item["floor"] == 4)
    rid = room["id"]
    desks = [item for item in original if item["name"] == "DeskAndChair" and item["parent"] == rid]
    for i, desk in enumerate(desks):
        group = desk["id"]
        b("DeskUnderframe", group, (0, .65, 0), (1.06, .045, .045), "Metal", False)
        b("BookShelf", group, (0, .58, .04), (.8, .025, .35), "Metal", False)
        b("SeatCushion", group, (0, .49, -.65), (.44, .035, .42), "SeatFabric", False, bevel=.012)
        if i in [1, 3, 5]:
            laptop = n("StudentLaptop", group, (-.15, .814, .08), yaw=-8)
            b("LaptopBase", laptop, (0, .012, 0), (.31, .024, .22), "Steel", False, bevel=.009)
            screen = n("LaptopScreenHinge", laptop, (0, .022, .105), rotation=[math.sin(-.12), 0, 0, math.cos(-.12)])
            b("LaptopLid", screen, (0, .1, 0), (.31, .2, .012), "Navy", False, bevel=.006)
            b("LaptopDisplay", screen, (0, .1, -.008), (.285, .175, .005), "PosterBlue", False)
            text(screen, "HARBOURVIEW", (0, .105, -.012), .02, "White")
        if i in [2, 4, 7]:
            b("StudentBook", group, (.2, .827, .06), (.21, .035, .27), "PosterCoral", False, bevel=.005)
            b("BookPages", group, (.2, .829, .055), (.2, .024, .26), "Paper", False)
            b("Pen", group, (.35, .815, -.1), (.015, .015, .18), "Navy", False)
    plant(rid, (3.85, 0, 2.85), .85)
    storage = n("TeachingStorage", rid, (4.08, 0, 1))
    b("StorageCabinet", storage, (0, .49, 0), (.48, .98, 1.9), "White", True, bevel=.025)
    b("CabinetOakTop", storage, (0, 1.01, 0), (.54, .055, 1.96), "Wood", False, bevel=.018)
    for z in [-.48, .48]:
        b("CabinetDoorInset", storage, (-.251, .5, z), (.014, .85, .91), "WallLower", False)
        b("CabinetPull", storage, (-.274, .68, z), (.03, .15, .025), "Steel", False)
    for z in [-1.4, .8, 2.4]:
        b("ElectricalSocket", rid, (4.425, .42, z), (.035, .09, .17), "White", False, bevel=.006)
    b("CableTrunking", rid, (4.44, .42, .7), (.035, .06, 4.1), "White", False)
    lectern = next(item for item in original if item["parent"] == rid and item["name"] == "Lectern")
    lx, ly, lz = lectern["position"]
    b("TeachingConsole", rid, (lx, 1.14, lz), (.55, .04, .46), "Navy", False, bevel=.01)
    b("ConsoleTouchscreen", rid, (lx, 1.165, lz), (.3, .008, .23), "PosterBlue", False)
    text(rid, "BLOCK A / 401", (-4.34, 2.95, 1), .12, "Teal")
    base.NODES[-1]["rotation"] = base.quat_y(-90)
    # Wall-mounted fixtures give the teaching space everyday, human-scale detail.
    clock = n("ClassroomClock", rid, (-4.422, 2.78, -2.45), yaw=-90)
    for name, radius, depth, z, material in [("ClockRim", .32, .026, 0, "WindowAluminium"),
                                            ("ClockFace", .287, .006, -.029, "White")]:
        n(name, clock, (0, 0, z), "cylinder", (radius, depth, radius), material,
          rotation=[math.sqrt(.5), 0, 0, math.sqrt(.5)])
    for i in range(12):
        a = i * math.tau / 12
        b("ClockTick", clock, (.124 * math.sin(a), .124 * math.cos(a), -.037),
          (.008, .024 if i % 3 == 0 else .012, .005), "Navy", False,
          rotation=[0, 0, -math.sin(a / 2), math.cos(a / 2)])
    base.beam(clock, "ClockMinuteHand", (0, 0, -.043), (.096, .055, -.043), "Navy", .009)
    base.beam(clock, "ClockHourHand", (0, 0, -.049), (-.064, .036, -.049), "Navy", .012)
    ac = n("ClassroomAirConditioner", rid, (4.345, 2.77, 1.5), yaw=90)
    b("ACBody", ac, (0, 0, 0), (1.12, .3, .22), "White", False, bevel=.035)
    b("ACOutlet", ac, (0, -.105, -.116), (.98, .065, .015), "Navy", False, bevel=.008)
    for y in [-.128, -.103, -.078]:
        b("ACLouver", ac, (0, y, -.133), (.98, .009, .027), "Wall", False)

    level = next(item for item in original if item["name"] == "Floor_04")
    fid = level["id"]
    notice_board(fid, (-4.35, 1.85, -1.34))
    study = n("CampusNoticeBoard", fid, (4.45, 1.82, -1.34), yaw=180)
    b("NoticeFrame", study, (0, 0, 0), (2.05, 1.38, .07), "Wood", False, bevel=.025)
    b("CorkBacking", study, (0, 0, -.045), (1.94, 1.27, .016), "Cork", False)
    poster(study, (-.49, 0, -.06), "STUDY SPACES", "403 / COLLABORATION\n\n404 / FACULTY OFFICE", "PosterBlue")
    poster(study, (.49, 0, -.06), "BE PREPARED", "Find EXIT signs\n\nFollow campus guidance", "Teal")
    for x in [-11.85, 11.85]:
        marker = n("FloorIdentity", fid, (x, 2.02, -1.34), yaw=180)
        b("IdentityPanel", marker, (0, 0, 0), (.92, 1.55, .055), "Teal", False, bevel=.015)
        text(marker, "04", (0, .25, -.04), .42, "White")
        text(marker, "TEACHING\nBLOCK A", (0, -.37, -.04), .085, "White")
    for sign in [item for item in original if item["kind"] == "group" and item["name"].startswith("Sign_< STAIR") and item["floor"] == 4]:
        for x in [-1.7, 1.7]:
            b("WayfindingSuspension", fid, (x, 3.075, 1.42), (.018, .55, .018), "Steel", False, roof=True)
    # Door detail follows the hinged leaf, so it moves with the existing interaction.
    for item in original:
        if item.get("component") != "door" or item["floor"] != 4:
            continue
        frame = base.NODES[item["parent"] - 1]
        entrance = base.NODES[frame["parent"] - 1]
        if not entrance["name"].startswith("StairEntrance"):
            continue
        b("DoorCloserBody", item["id"], (.55, 2.12, -.065), (.23, .075, .065), "Steel", False, bevel=.012)
        b("FireDoorPushBar", item["id"], (.79, 1.02, -.09), (1.15, .045, .045), "Steel", False, bevel=.008)
        safety = s(item["id"], "FIRE DOOR\nKEEP CLOSED", (.8, 1.77, -.075), .5, .25, material="PosterBlue", size=.065)
        for x in [-.48, .48]:
            b("PushBarMount", item["id"], (.79 + x, 1.02, -.06), (.04, .1, .045), "Steel", False)

    outdoor = next(item["id"] for item in original if item["name"] == "Outdoor")
    for x in [-17, 17]:
        for z in [-15, -27]:
            bollard = n("CampusBollard", outdoor, (x, 0, z))
            b("BollardBody", bollard, (0, .45, 0), (.16, .9, .16), "Navy", True, bevel=.016)
            b("BollardDiffuser", bollard, (0, .77, -.087), (.11, .12, .012), "Glow", False)
    gateway = n("CampusDirectory", outdoor, (-6.4, 0, -14.8), yaw=-15)
    b("DirectoryPlinth", gateway, (0, .09, 0), (1.5, .18, .45), "Stone", True, bevel=.035)
    b("DirectoryBody", gateway, (0, 1.16, 0), (1.24, 2.14, .18), "Teal", True, bevel=.025)
    text(gateway, "HARBOURVIEW", (0, 2.02, -.101), .10, "White")
    text(gateway, "BLOCK A", (0, 1.65, -.101), .23, "White")
    text(gateway, "TEACHING BUILDING\n\n01 / Student services\n02 / Learning studios\n03 / Faculty offices\n04 / Teaching rooms", (0, 1.03, -.101), .085, "White")
    text(gateway, "ASSEMBLY POINT / PLAZA", (0, .32, -.101), .055, "White")
    data.update(scenePath="Assets/Scenes/TeachingBuildingCampus.unity", assetFolder=ASSETS,
                previewFolder=PREVIEWS, previewPrefix="Campus", bakeLighting=True,
                skyTexture=f"{ASSETS}/SourceTextures/kloofendal_48d_partly_cloudy_puresky_1k.hdr",
                prefabNames=data["prefabNames"] + ["CampusNoticeBoard", "CampusPlanter", "CampusDirectory"])
    return data


def preview_html(data):
    preview = copy.deepcopy(data)
    for item in preview['nodes']:
        if item.get('printTexture'):
            item['printData'] = 'data:image/png;base64,' + base64.b64encode((ROOT / item['printTexture']).read_bytes()).decode('ascii')
    for material in preview["materials"]:
        if material.get("texture"):
            # The lightweight browser renderer has 512px texture-array layers.
            resized = Image.open(ROOT / material["texture"]).convert("RGB").resize((512, 512), Image.Resampling.LANCZOS)
            buffer = io.BytesIO()
            resized.save(buffer, format="PNG")
            material["textureData"] = "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")
    template = (ROOT / "Tools/teaching_building_preview.html").read_text(encoding="utf-8")
    template = upgrade_preview(template)
    template = template.replace("四层教学楼 · 场景原型", "校园消防疏散 · 场景升级")
    template = template.replace("Harbourview · 四层教学楼原型", "Harbourview · Campus Art Pass")
    template = template.replace("SCENE BLOCKOUT / 初版布局", "CAMPUS ART PASS / 校园场景")
    template = template.replace("<div class=\"line\"></div>", '<label class="check"><input id="atmosphere" type="checkbox">火警氛围预览</label><div class="line"></div>', 1)
    template = template.replace("function visible(n){if(n.hidden)return false;", "function visible(n){if(n.atmosphere){if(!document.getElementById('atmosphere').checked)return false;}else if(n.hidden)return false;")
    template = template.replace("n.hidden=!n.active||!!parent?.hidden;", "n.hidden=!n.active||!!parent?.hidden;n.atmosphere=n.component==='campusAtmosphere'||!!parent?.atmosphere;")
    template = template.replace("window.preview={", "document.getElementById('atmosphere').addEventListener('change',()=>dirty=true);window.preview={")
    template = template.replace("document.querySelectorAll('[data-view]').forEach(b=>b.addEventListener", "document.querySelector('.modes').insertAdjacentHTML('beforeend','<button data-view=\"notice\"><span class=\"number\">07</span>校园公告栏</button>');document.querySelectorAll('[data-view]').forEach(b=>b.addEventListener", 1)
    template = template.replace("const descriptions={", "const descriptions={notice:'校园公告栏：原创学生社团、设计活动及安全提示海报，采用统一的学校配色。',")
    template = template.replace("corridor:'06 / 四楼走廊'", "corridor:'06 / 四楼走廊',notice:'07 / 校园公告栏'")
    template = template.replace("const views={", "const views={notice:{eye:[-4.35,12.45,.85],target:[-4.35,12.65,-1.34],fov:1.3},")
    template = template.replace('07</span>校园公告栏</button>', '07</span>校园公告栏</button><button data-view="window"><span class="number">08</span>外窗与隔墙</button>')
    template = template.replace("const descriptions={", "const descriptions={window:'窗洞随房间边界划分，隔墙接到实心窗间墙；玻璃在窗框内分块，室内设卷帘与窗台。',")
    template = template.replace("notice:'07 / 校园公告栏'", "notice:'07 / 校园公告栏',window:'08 / 外窗与隔墙'")
    template = template.replace("const views={", "const views={window:{eye:[-10.1,12.6,12.4],target:[-10.3,12.4,6],fov:1.13},")
    template = template.replace('08</span>外窗与隔墙</button>', '08</span>外窗与隔墙</button><button data-view="student"><span class="number">09</span>学生角色</button>')
    template = template.replace("const descriptions={", "const descriptions={student:'校园学生角色，身高约 1.73 米；Unity 中按 V 切换第三人称。',")
    template = template.replace("window:'08 / 外窗与隔墙'", "window:'08 / 外窗与隔墙',student:'09 / 学生角色'")
    template = template.replace("const views={", "const views={student:{eye:[-6.15,12.1,4.5],target:[-7.8,11.75,2.7],fov:.8},")
    template = template.replace("401 教室：课桌椅、白板、投影设备和安全告示。门扇设观察窗，门口保持通行空间。", "401 教室：软垫座椅、笔记本电脑、教学控制台、收纳柜与绿植，保留原有疏散通道。")
    template = template.replace("四楼走廊：墙裙、地砖、公告栏和消防设施，出口方向标识沿通道布置。", "四楼走廊：水磨石地面、校园活动公告栏、楼层识别和消防设施。可开启局部烟雾示意，观察标识可见性。")
    template = template.replace("document.getElementById('info').textContent=descriptions[value];", "document.getElementById('info').textContent=descriptions[value];document.querySelector('.top').style.display=['exterior','floor'].includes(value)?'block':'none';document.querySelector('.bottom').style.display=['exterior','floor'].includes(value)?'flex':'none';")
    # Open on the enhanced classroom; the remaining views are available in the panel.
    template = template.replace("setView('exterior');render();", "setView('classroom');render();")
    template = restyle_preview(template)
    template = template.replace("window:'教室外窗'})", "window:'教室外窗',student:'学生角色 · Unity 中按 V 切换第三人称'})")
    template = upgrade_modeling_preview(template)
    # Printed glyphs use the same measured dimensions as their Unity decals.
    anchor = "function textTexture(n){"
    raster = """function printedTexture(n){
const texture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,texture);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,1,1,0,gl.RGBA,gl.UNSIGNED_BYTE,new Uint8Array(4));
const bitmap=new Image();bitmap.src=n.printData;bitmap.decode().then(()=>{gl.activeTexture(gl.TEXTURE0);gl.bindTexture(gl.TEXTURE_2D,texture);gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,bitmap);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);dirty=true}).catch(console.error);
n.textTexture=texture;n.textModel=multiply(n.world,compose([0,0,0],[0,0,0,1],[n.printWidth,n.printHeight,1]));}
"""
    template = template.replace(anchor, raster + anchor + 'if(n.printData){printedTexture(n);return;}', 1)
    template = template.replace('result=vec4(tint.rgb,alpha*tint.a);', 'result=vec4(tint.rgb*letterLight,alpha*tint.a);')
    template = template.replace('uniform sampler2D glyph;uniform vec4 tint;', 'uniform sampler2D glyph;uniform vec4 tint;uniform float letterLight;')
    template = template.replace("gl.uniform4fv(gl.getUniformLocation(textProgram,'tint'),n.textColor);", "gl.uniform4fv(gl.getUniformLocation(textProgram,'tint'),n.textColor);const normal=normalize([-n.world[8],-n.world[9],-n.world[10]]);gl.uniform1f(gl.getUniformLocation(textProgram,'letterLight'),.68+.22*Math.max(dot(normal,normalize([.4,.85,.5])),0.));")
    folder = ROOT / PREVIEWS
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "TeachingBuildingCampusPreview.html").write_text(template.replace("__SCENE_DATA__", json.dumps(preview, ensure_ascii=False)), encoding="utf-8")


def main():
    source = json.loads((ROOT / "Assets/Environment/TeachingBuildingRefinedLayout.json").read_text(encoding="utf-8"))
    original_routes = copy.deepcopy(source["routes"])
    create_textures()
    pbr.prepare_scans()
    campus = art_pass(source)
    fitted_signs = prepare_signage(campus)
    prepare_walkthrough(campus)
    apply_modeling_changes(campus)
    assert campus["routes"] == original_routes, "Art pass changed route definitions"
    junctions = validate_window_junctions(campus)
    campus["facadeJunctionCount"] = junctions
    verify.validate_assets(campus)
    counts, errors = verify.validate_routes(campus)
    if errors:
        raise RuntimeError("Campus art pass obstructs an evacuation route")
    for material in campus["materials"]:
        for key in ["texture", "detailTexture"]:
            if material.get(key):
                assert (ROOT / material[key]).is_file(), material[key]
    assert len(campus["materials"]) <= 64, "Preview material limit"
    relative = "Assets/Environment/TeachingBuildingCampusLayout.json"
    (ROOT / relative).write_text(json.dumps(campus, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    base.asset_meta(relative)
    preview_html(campus)
    # Only add missing metadata; retain all existing GUIDs.
    base.ensure_metadata()
    report = f"Campus layout: {len(campus['nodes'])} nodes, {len(campus['materials'])} shared materials.\n"
    report += f"Facade wall/glass junctions: {junctions} masonry-contact and no-glass-overlap checks PASS\n"
    report += f"Printed signs fitted within physical carriers: {fitted_signs} PASS\n"
    report += "Original route definitions preserved.\n" + "\n".join(f"Route {name}: {count} geometry samples PASS" for name, count in counts.items())
    report += "\nCC0 Poly Haven scans/HDRI plus original procedural textures and preview alarm.\nHeadset performance and XR locomotion unverified.\n"
    (ROOT / PREVIEWS / "CampusGeometryValidation.txt").write_text(report, encoding="utf-8")
    print(report)


if __name__ == "__main__":
    main()
