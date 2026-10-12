"""Generate one shared layout, an importable Unity scene, and an offline preview."""
import json
import math
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
NODES = []
MATERIALS = {
    "Wall": [.84, .86, .82, 1], "Concrete": [.63, .68, .67, 1],
    "Floor": [.75, .79, .77, 1], "Corridor": [.56, .66, .64, 1],
    "Teal": [.035, .32, .36, 1], "Navy": [.055, .13, .18, 1],
    "White": [.94, .95, .9, 1], "Red": [.74, .095, .08, 1],
    "Wood": [.67, .46, .27, 1], "Metal": [.47, .53, .55, 1],
    "Grass": [.25, .4, .31, 1], "Paving": [.49, .57, .57, 1],
    "Leaf": [.17, .34, .27, 1], "Green": [.08, .46, .31, 1],
    "Yellow": [.94, .69, .19, 1], "Screen": [.055, .11, .14, 1],
    "Glow": [.75, .93, .82, 1], "Glass": [.37, .67, .7, .23],
    "Smoke": [.35, .38, .4, .28],
}


def quat_y(degrees):
    angle = math.radians(degrees) / 2
    return [0, math.sin(angle), 0, math.cos(angle)]


def look_rotation(position, target):
    dx, dy, dz = [b - a for a, b in zip(position, target)]
    yaw = math.atan2(dx, dz) / 2
    pitch = -math.asin(dy / math.sqrt(dx * dx + dy * dy + dz * dz)) / 2
    return [math.sin(pitch) * math.cos(yaw), math.sin(yaw) * math.cos(pitch),
            -math.sin(yaw) * math.sin(pitch), math.cos(yaw) * math.cos(pitch)]


def quat_from_up(direction):
    length = math.sqrt(sum(v * v for v in direction))
    x, y, z = [v / length for v in direction]
    values = [z, 0, -x, 1 + y]
    norm = math.sqrt(sum(v * v for v in values))
    return [v / norm for v in values]


def node(name, parent=0, position=(0, 0, 0), kind="group", scale=(1, 1, 1),
         material="Wall", yaw=0, collision=False, **extra):
    item = dict(id=len(NODES) + 1, name=name, parent=parent, kind=kind,
                position=list(position), rotation=quat_y(yaw), scale=list(scale),
                material=material, collision=collision, active=True)
    item.update(extra)
    if "floor" not in item:
        item["floor"] = NODES[parent - 1].get("floor", 0) if parent else 0
    NODES.append(item)
    return item["id"]


def box(name, parent, position, scale, material="Wall", collision=True, **extra):
    return node(name, parent, position, "box", scale, material, collision=collision, **extra)


def label(parent, value, position, size=.2, yaw=0, color=None):
    return node("Label_" + value.split("\n")[0], parent, position, "text", yaw=yaw,
                text=value, textSize=size, textColor=color or [1, 1, 1, 1])


def sign(parent, value, position, width, height, yaw=0, material="Teal", size=.2):
    group = node("Sign_" + value.split("\n")[0], parent, position, yaw=yaw)
    box("SignPanel", group, (0, 0, 0), (width, height, .06), material, False)
    label(group, value, (0, 0, -.041), size)
    return group


def light(parent, position, intensity=.8, radius=9):
    return node("InteriorLight", parent, position, "light", lightIntensity=intensity, lightRange=radius)


def beam(parent, name, start, end, material="Metal", thickness=.055):
    direction = [b - a for a, b in zip(start, end)]
    length = math.sqrt(sum(v * v for v in direction))
    return box(name, parent, [(a + b) / 2 for a, b in zip(start, end)],
               (thickness, length, thickness), material, rotation=quat_from_up(direction))


def door(parent, position, width=1.5, yaw=0, open_angle=-100, opened=True, material="Wood"):
    frame = node("DoorFrame", parent, position, yaw=yaw)
    for x in [-width / 2, width / 2]:
        box("DoorJamb", frame, (x, 1.17, 0), (.07, 2.34, .22), "Navy")
    box("DoorLintel", frame, (0, 2.36, 0), (width + .07, .07, .22), "Navy")
    hinge = node("DoorHinge", frame, (-width / 2 + .04, 0, 0),
                 yaw=open_angle if opened else 0, component="door", openAngle=open_angle, startsOpen=opened)
    panel_width = width - .08
    box("DoorPanel", hinge, (panel_width / 2, 1.15, 0), (panel_width, 2.28, .055), material)
    for z in [-.065, .065]:
        box("DoorHandle", hinge, (panel_width - .12, 1.05, z), (.14, .035, .07), "Metal", False)
    return hinge


def wall_x(parent, left, right, z):
    if right <= left:
        return
    box("Wall_Partition", parent, ((left + right) / 2, 1.7, z), (right - left, 3.4, .18))
    box("Skirting", parent, ((left + right) / 2, .16, z - .105), (right - left, .22, .025), "Teal", False)


def wall_z(parent, x, low, high):
    box("Wall_Partition", parent, (x, 1.7, (low + high) / 2), (.18, 3.4, high - low))


def room_door(parent, left, right, z, x, number, north=True):
    wall_x(parent, left, x - .75, z)
    wall_x(parent, x + .75, right, z)
    box("DoorHeader", parent, (x, 2.9, z), (1.5, 1, .18))
    door(parent, (x, 0, z), open_angle=-100 if north else 100)
    sign(parent, number, (x + 1.3, 1.85, z + (-.12 if north else .12)), .9, .45,
         yaw=0 if north else 180, size=.16)


def desk(parent, position):
    group = node("DeskAndChair", parent, position)
    box("Desktop", group, (0, .77, 0), (1.15, .07, .65), "Wood")
    for x in [-.48, .48]:
        for z in [-.24, .24]:
            box("DeskLeg", group, (x, .37, z), (.045, .74, .045), "Metal")
    box("ChairSeat", group, (0, .44, -.65), (.46, .07, .44), "Teal")
    box("ChairBack", group, (0, .7, -.86), (.46, .47, .06), "Teal")
    for x in [-.18, .18]:
        for z in [-.81, -.49]:
            box("ChairLeg", group, (x, .2, z), (.035, .4, .035), "Metal")
    return group


def classroom(parent, center, detailed):
    group = node("Classroom_Detailed" if detailed else "Classroom_Basic", parent, center)
    board = sign(group, "HARBOURVIEW UNIVERSITY\nCAMPUS EMERGENCY PREPAREDNESS", (0, 1.8, 3.54), 5, 1.5,
                 material="White", size=.17)
    NODES[board + 1]['textColor'] = MATERIALS['Teal']
    for row in range(3 if detailed else 1):
        for column in range(-1, 2):
            desk(group, (column * 2.5, 0, -.8 + row * 1.45))
    box("Lectern", group, (2.8, .55, 2.5), (.75, 1.1, .6), "Wood")
    box("CeilingLight", group, (0, 3.25, 0), (2, .05, .4), "Glow", False)
    light(group, (0, 2.7, 0), .75)


def equipment(parent, position):
    group = node("FireEquipmentPoint", parent, position)
    box("Cabinet", group, (.15, 1.05, 0), (.75, .95, .19), "Red", component="notice", noticeKind=2)
    box("CabinetInset", group, (.15, 1.05, -.105), (.62, .78, .025), "White", False)
    sign(group, "FIRE\nHOSE", (.15, 1.08, -.13), .48, .55, material="Red", size=.14)
    node("Extinguisher", group, (-.5, .46, -.02), "cylinder", (.22, .32, .22), "Red", collision=True)
    box("ExtinguisherHandle", group, (-.5, .84, -.02), (.16, .05, .1), "Navy", False)
    sign(group, "FIRE EQUIPMENT", (0, 1.78, -.13), 1.3, .3, material="Red", size=.11)
    return group


def rectangle(parent, left, right, low, high):
    box("FloorSlab", parent, ((left + right) / 2, -.1, (low + high) / 2), (right - left, .2, high - low), "Floor")


def restroom(parent):
    root = node("Restrooms", parent)
    room_door(root, 7.5, 12.8, -1.5, 10.2, "WC", False)
    for i in range(2):
        x = 8.7 + i * 2.1
        wall_z(root, x - .8, -8.8, -5.2)
        box("StallDivider", root, (x, 1.1, -5.2), (1.6, 2.2, .07), "Teal")
        node("ToiletBase", root, (x, .23, -7.7), "cylinder", (.5, .23, .55), "White", collision=True)
        box("ToiletTank", root, (x, .65, -8.13), (.52, .55, .2), "White")
    box("WashCounter", root, (12.3, .85, -3.5), (.7, .12, 2.1), "White")
    box("CounterSupport", root, (12.3, .4, -3.5), (.65, .8, 2), "Concrete")
    box("Mirror", root, (12.68, 1.55, -3.5), (.025, .95, 1.8), "Metal", False)
    light(root, (10.2, 2.7, -4), .7, 7)


def lift_landing(parent, number):
    root = node("ElevatorLanding", parent)
    wall_x(root, -2.2, -.64, 4.5)
    wall_x(root, .64, 2.2, 4.5)
    box("LiftHeader", root, (0, 2.92, 4.5), (1.28, .96, .2))
    for x in [-.31, .31]:
        box("LiftDoor", root, (x, 1.17, 4.41), (.6, 2.34, .07), "Metal")
    box("LiftDoorFrame", root, (0, 2.39, 4.36), (1.42, .1, .12), "Navy")
    sign(root, f"LIFT / {number:02}", (0, 2.66, 4.33), 1.3, .32, material="Screen", size=.16)
    box("LiftCallPanel", root, (1.05, 1.25, 4.33), (.18, .36, .06), "Navy", component="notice", noticeKind=0)
    button = node("CallButton", root, (1.05, 1.26, 4.28), "cylinder", (.07, .014, .07), "Red")
    NODES[button - 1]['rotation'] = [math.sqrt(.5), 0, 0, math.sqrt(.5)]
    sign(root, "FIRE DRILL\nLIFT CLOSED\nUSE STAIRS", (1.75, 1.85, 4.31), .75, .95, material="Red", size=.095)
    sign(root, f"FLOOR {number:02}\n\nA <--- LIFT ---> B\n\nYOU ARE HERE\nEXIT VIA STAIRS", (-1.65, 1.65, 4.31), .95, 1.3, size=.085)


def window(parent, position, width, yaw):
    root = node("WindowBay", parent, position, yaw=yaw)
    box("Wall_WindowSill", root, (0, .5, 0), (width, 1, .22))
    box("Wall_WindowHeader", root, (0, 3.1, 0), (width, .8, .22))
    for x in [-width / 2 + .15, width / 2 - .15]:
        box("WindowPier", root, (x, 1.85, 0), (.3, 1.7, .22))
    box("WindowGlass", root, (0, 1.85, 0), (width - .6, 1.7, .035), "Glass")
    for y in [1.02, 2.68]:
        box("WindowFrame", root, (0, y, -.12), (width - .52, .06, .06), "Navy", False)
    for i in [-1, 0, 1]:
        box("WindowMullion", root, (i * (width - .6) / 3, 1.85, -.12), (.055, 1.7, .06), "Navy", False)


def stairwell(parent, center, identifier):
    root = node("Stairwell_" + identifier, parent)
    for floor in range(4):
        level = node(f"Stair_{identifier}_F{floor+1:02}", root, (0, floor * 3.6, 0), floor=floor + 1)
        box("Landing", level, (center, -.1, 3), (5, .2, 2.4), "Concrete")
        sign(level, f"{identifier} / FLOOR {floor+1:02}", (center, 1.8, 4.16), 2, .65, size=.27)
        light(level, (center, 2.7, 3), .8, 8)
        beam(level, "LandingRail", (center - 2.2, 1, 4.05), (center + 2.2, 1, 4.05))
        if floor == 3:
            box("TopFloorGuard", level, (center - 1.1, .48, 1.8), (1.9, .96, .07), "Teal")
            beam(level, "TopFloorRail", (center - 2.1, 1, 1.8), (center - .2, 1, 1.8))
            continue
        box("HalfLanding", level, (center, 1.7, -3), (5, .2, 2.4), "Concrete")
        beam(level, "HalfLandingRail", (center - 2.2, 2.8, -4.05), (center + 2.2, 2.8, -4.05))
        for step in range(1, 13):
            rise = step * .15
            first_z, second_z = 1.8 - (step - .5) * .3, -1.8 + (step - .5) * .3
            # Constant tread thickness preserves headroom below stacked flights.
            box(f"Step_First_{step}", level, (center - 1.08, rise - .075, first_z), (1.65, .15, .3), "Concrete")
            box(f"Step_Second_{step}", level, (center + 1.08, 1.8 + rise - .075, second_z), (1.65, .15, .3), "Concrete")
            box("StepEdge", level, (center - 1.08, rise + .004, first_z + .14), (1.6, .008, .025), "Yellow", False)
            box("StepEdge", level, (center + 1.08, 1.8 + rise + .004, second_z - .14), (1.6, .008, .025), "Yellow", False)
        for offset, base, start, end in [(-1.95, 0, 1.8, -1.8), (-.22, 0, 1.8, -1.8),
                                         (.22, 1.8, -1.8, 1.8), (1.95, 1.8, -1.8, 1.8)]:
            beam(level, "StairHandrail", (center + offset, base + 1.1, start), (center + offset, base + 2.9, end))
            for i in range(4):
                t = i / 3
                box("HandrailPost", level, (center + offset, base + 1.8 * t + .55, start + (end - start) * t), (.045, 1.1, .045), "Metal")


def layout():
    root = node("TeachingBuilding", component="bootstrap")
    building = node("Building", root)
    exterior = node("Exterior", building)
    anchors = node("GameplayAnchors", root)
    outdoor = node("Outdoor", root)
    box("Ground", outdoor, (0, -.25, -7), (78, .3, 72), "Grass")
    box("Forecourt", outdoor, (0, -.06, -20), (48, .12, 22), "Paving")
    for side in [-1, 1]:
        box("SideExitPath", outdoor, (side * 21, -.04, 1), (5, .08, 22), "Paving")
        for z in [-25, -13, 13]:
            tree = node("CampusTree", outdoor, (side * 28, 0, z))
            node("Trunk", tree, (0, 1.4, 0), "cylinder", (.3, 1.4, .3), "Wood", collision=True)
            node("Crown", tree, (0, 3.4, 0), "sphere", (4, 4.2, 4), "Leaf")
        box("Planter", outdoor, (side * 9, .25, -22), (3.5, .5, 1.2), "Concrete")
        box("PlanterGreen", outdoor, (side * 9, .62, -22), (3.2, .28, 1), "Leaf", False)
        box("Bench", outdoor, (side * 9, .45, -23.1), (3.5, .12, .55), "Wood")
        for x in [side * 9 - 1.25, side * 9 + 1.25]:
            box("BenchFoot", outdoor, (x, .2, -23.1), (.13, .4, .45), "Navy")
    box("MainWalkway", outdoor, (0, .015, -18), (4, .03, 17), "Floor", False)
    assembly = node("AssemblyPointVisual", outdoor, (0, 0, -25))
    box("AssemblyMarker", assembly, (0, .03, 0), (4.5, .05, 4.5), "Green", False)
    box("AssemblyPost", assembly, (3.4, 1.25, 0), (.1, 2.5, .1), "Metal")
    sign(assembly, "ASSEMBLY\nPOINT", (3.4, 2.05, -.1), 2, 1.4, material="Green", size=.32)
    assembly_anchor = node("AssemblyPoint", anchors, (0, 0, -25))

    spawns = {}
    for floor in range(1, 5):
        y = (floor - 1) * 3.6
        level = node(f"Floor_{floor:02}", building, (0, y, 0), floor=floor)
        if floor == 1:
            rectangle(level, -18, 18, -9, 9)
        else:
            for bounds in [(-13, 13, -9, 4.5), (-13, -1.6, 4.5, 7.5), (1.6, 13, 4.5, 7.5), (-13, 13, 7.5, 9)]:
                rectangle(level, *bounds)
            for center in [-15.5, 15.5]:
                rectangle(level, center - 2.5, center + 2.5, 4.2, 9)
                rectangle(level, center - 2.5, center + 2.5, -9, -4.2)
        box("CorridorFinish", level, (0, .008, 0), (26, .016, 3), "Corridor", False)
        box("WayfindingStripe", level, (0, .02, -.95), (23, .014, .1), "Teal", False)
        room_door(level, -11.3, -2.2, 1.5, -7.8, f"{floor}01")
        room_door(level, 2.2, 11.3, 1.5, 7.8, f"{floor}02")
        for x in [-11.3, -2.2, 2.2, 11.3]:
            wall_z(level, x, 1.5, 8.8)
        if floor == 1:
            room_door(level, -12.8, -4, -1.5, -8, "LOUNGE", False)
            room_door(level, 4, 7.5, -1.5, 5.5, "SECURITY", False)
            wall_z(level, -4, -8.8, -1.5)
            wall_z(level, 4, -8.8, -1.5)
            sign(level, "WELCOME TO\nTEACHING BLOCK A", (-3.87, 1.8, -5.4), 2.8, 1.25, -90, size=.24)
            box("ReceptionDesk", level, (5.7, .55, -6), (1.8, 1.1, .7), "Wood")
            box("LobbyBench", level, (-2.7, .45, -5.3), (.65, .14, 2.3), "Wood")
            sign(level, "ASSEMBLY POINT\nSTRAIGHT AHEAD", (0, 2.6, -7.7), 2.4, .7, 180, "Green")
            light(level, (0, 2.7, -5), .9, 11)
        else:
            room_door(level, -12.8, 0, -1.5, -7.8, f"{floor}03", False)
            room_door(level, 0, 7.5, -1.5, 4.1, f"{floor}04", False)
            wall_z(level, 0, -8.8, -1.5)
            box("OfficeDesk", level, (3.8, .75, -5.5), (1.6, .12, .8), "Wood")
        wall_z(level, 7.5, -8.8, -1.5)
        restroom(level)
        classroom(level, (-6.75, 0, 5.15), floor in [2, 4])
        classroom(level, (6.75, 0, 5.15), False)
        lift_landing(level, floor)
        node(f"Elevator_F{floor:02}", anchors, (0, y, 3.3))

        for side in [-1, 1]:
            equipment(level, (side * 9.8, 0, 1.29))
            box("AlarmButton", level, (side * 11, 1.35, 1.34), (.2, .23, .08), "Red", component="notice", noticeKind=1)
            box("AlarmPressPanel", level, (side * 11, 1.35, 1.29), (.11, .1, .015), "White", False)
            sign(level, "FIRE ALARM", (side * 11, 1.62, 1.31), .7, .2, material="Red", size=.09)
            box("EmergencyLight", level, (side * 11.7, 2.9, .5), (.36, .13, .15), "Glow", False)
            identifier = "A" if side < 0 else "B"
            node(f"Alarm_F{floor:02}_{identifier}", anchors, (side * 11, y + 1.35, 1.34))
            node(f"FireEquipment_F{floor:02}_{identifier}", anchors, (side * 9.8, y + 1, 1.29))
            entrance = node("StairEntrance_" + identifier, level, (side * 13, 0, 3), yaw=side * 90)
            wall_x(entrance, -12 if side < 0 else -6, -.85, 0)
            wall_x(entrance, .85, 6 if side < 0 else 12, 0)
            box("DoorHeader", entrance, (0, 2.9, 0), (1.7, 1, .18))
            door(entrance, (0, 0, 0), 1.7, yaw=180 if side < 0 else 0,
                 open_angle=100 if side < 0 else -100, opened=False, material="Teal")
            sign(entrance, f"STAIR {identifier} / EXIT", (0, 2.66, -.14), 1.65, .35, material="Green", size=.15)
        sign(level, "< STAIR A     |     STAIR B >", (0, 2.8, 1.42), 4.1, .45, material="Green")
        sign(level, f"FLOOR {floor:02}", (-2.07, 2.55, 2.8), 1.45, .5, -90)
        for i in [-1, 0, 1]:
            box("CeilingLight", level, (i * 8, 3.28, 0), (2.1, .045, .35), "Glow", False)
            light(level, (i * 8, 2.9, 0), .9, 10)
            node("SmokeDetector", level, (i * 8 + 2, 3.27, 0), "cylinder", (.17, .025, .17), "White")
        if floor in [2, 4]:
            spawns[floor] = node(f"Spawn_F{floor:02}_{floor}01", anchors, (-7.8, y + .05, 2.7), yaw=180)

        facade = node(f"Facade_F{floor:02}", exterior, (0, y, 0), floor=floor)
        splits = [-18, -13, -8, -3, 3, 8, 13, 18]
        for i in range(len(splits) - 1):
            middle, width = (splits[i] + splits[i + 1]) / 2, splits[i + 1] - splits[i]
            if floor == 1 and i == 3:
                entrance = node("MainEntrance", facade, (0, 0, -9))
                wall_x(entrance, -3, -1.2, 0)
                wall_x(entrance, 1.2, 3, 0)
                box("DoorHeader", entrance, (0, 2.9, 0), (2.4, 1, .18))
                door(entrance, (0, 0, 0), width=2.4, opened=True)
                sign(entrance, "EXIT", (0, 2.65, .15), 1.3, .35, 180, "Green")
                node("Exit_Main", anchors, (0, 0, -10))
            else:
                window(facade, (middle, 0, -9), width, 0)
            window(facade, (middle, 0, 9), width, 180)
        for side in [-1, 1]:
            for z, width in [(-6.6, 4.8), (-1.2, 6), (6.6, 4.8)]:
                window(facade, (side * 18, 0, z), width, side * 90)
            if floor == 1:
                exit_group = node("StairExit_" + ("A" if side < 0 else "B"), facade, (side * 18, 0, 3), yaw=side * 90)
                wall_x(exit_group, -1.2, -.75, 0)
                wall_x(exit_group, .75, 1.2, 0)
                box("DoorHeader", exit_group, (0, 2.9, 0), (1.5, 1, .18))
                door(exit_group, (0, 0, 0), opened=False, material="Teal")
                sign(exit_group, "EXIT", (0, 2.65, -.15), 1.3, .35, material="Green")
                node("Exit_" + ("A" if side < 0 else "B"), anchors, (side * 19, 0, 3))
            else:
                window(facade, (side * 18, 0, 3), 2.4, side * 90)
            box("FacadeBandSide", facade, (side * 18.12, 3.5, 0), (.26, .18, 18), "Teal", False)
        for z in [-9.12, 9.12]:
            box("FacadeBand", facade, (0, 3.5, z), (36.3, .18, .26), "Teal", False)

    for center, identifier in [(-15.5, "A"), (15.5, "B")]:
        stairwell(building, center, identifier)
    elevator = node("Elevator", building)
    shaft = node("Shaft", elevator)
    for x in [-1.6, 1.6]:
        box("ShaftSide", shaft, (x, 7.1, 6), (.18, 14.2, 3), "Concrete")
    box("ShaftBack", shaft, (0, 7.1, 7.5), (3.2, 14.2, .18), "Concrete")
    cabin = node("Cabin", elevator, (0, 0, 5.8))
    box("CabinFloor", cabin, (0, .04, 0), (1.6, .08, 1.8), "Metal")
    box("CabinCeiling", cabin, (0, 2.45, 0), (1.6, .08, 1.8), "Metal")
    box("CabinBack", cabin, (0, 1.25, .9), (1.6, 2.4, .08), "Metal")
    for x in [-.8, .8]:
        box("CabinSide", cabin, (x, 1.25, 0), (.08, 2.4, 1.8), "Metal")

    roof = node("Roof_CutawayToggle", exterior, roof=True)
    box("RoofSlab", roof, (0, 14.3, 0), (36.4, .2, 18.4), "Concrete")
    for z in [-9.12, 9.12]:
        box("RoofParapet", roof, (0, 14.75, z), (36.4, .7, .2))
    for x in [-18.12, 18.12]:
        box("RoofParapet", roof, (x, 14.75, 0), (.2, .7, 18))
    sign(exterior, "HARBOURVIEW UNIVERSITY", (0, 13.8, -9.28), 16, 1.05, size=.55)
    box("EntranceCanopy", exterior, (0, 3.03, -10.5), (8.4, .2, 3.2), "Teal")
    for x in [-4, 4]:
        box("CanopyColumn", exterior, (x, 1.45, -11.7), (.17, 2.9, .17), "Navy")
    sign(exterior, "TEACHING BLOCK A", (0, 3.05, -12.13), 6.5, .5, size=.3)

    smoke = node("Smoke_VisualOnly_ToggleH", root, active=True)
    for i in range(9):
        node("SmokeVolume", smoke, (5.6 + i % 3 * .7, 11.5 + i // 3 * .6, i % 2 * .5 - .25),
             "sphere", (1.6, 1.5, 1.6), "Smoke", floor=4)
    node("Hazard_F04_Corridor", anchors, (6.3, 10.8, 0))
    lighting = node("Lighting", root)
    node("Sun", lighting, kind="sun", rotation=look_rotation([0, 0, 0], [.5, -.75, .35]))
    preview = node("Preview", root)
    overview = node("OverviewCamera", preview, (44, 29, -46), "camera", enabled=True,
                    rotation=look_rotation([44, 29, -46], [0, 6, -3]))
    player = node("DesktopPreviewPlayer", preview, (-7.8, 10.85, 2.7), "player", component="explorer",
                  spawnSecond=spawns[2], spawnFourth=spawns[4], assembly=assembly_anchor, roofNode=roof, smokeNode=smoke)
    walking = node("WalkCamera", player, (0, 1.65, 0), "camera", enabled=False, audio=True, tag="MainCamera")
    NODES[player - 1].update(walkCamera=walking, overviewCamera=overview)
    return dict(version=1, storeyHeight=3.6,
                materials=[dict(name=name, color=color) for name, color in MATERIALS.items()], nodes=NODES,
                routes=[dict(name=name, points=[dict(position=p) for p in evacuation_route(center)])
                        for name, center in [('A', -15.5), ('B', 15.5)]])


def evacuation_route(center):
    side = -1 if center < 0 else 1
    path = [[-7.8, 10.8, 2.7], [-7.8, 10.8, 0], [side * 12.05, 10.8, 0],
            [side * 12.05, 10.8, 3], [center + 1.08, 10.8, 3]]
    for lower in range(2, -1, -1):
        y = lower * 3.6
        path.append([center + 1.08, y + 3.6, 1.8])
        for step in range(12, 0, -1):
            path.append([center + 1.08, y + 1.8 + step * .15, -1.8 + (step - .5) * .3])
        path.extend([[center + 1.08, y + 1.8, -3], [center - 1.08, y + 1.8, -3]])
        for step in range(12, 0, -1):
            path.append([center - 1.08, y + step * .15, 1.8 - (step - .5) * .3])
        path.extend([[center - 1.08, y, 3], [center + 1.08, y, 3]])
    path.extend([[side * 21, 0, 3], [side * 21, 0, -18], [0, 0, -18], [0, 0, -25]])
    return path


def guid(relative):
    path = ROOT / relative
    meta = Path(str(path) + '.meta')
    if meta.exists():
        match = re.search(r'^guid: ([a-f0-9]{32})', meta.read_text(encoding='utf-8'), re.M)
        if match:
            return match.group(1)
    return uuid.uuid5(uuid.NAMESPACE_URL, 'campus-emergency-prototype/' + relative).hex


def asset_meta(relative, importer='DefaultImporter', main_id=0):
    path = ROOT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        extra = f'  mainObjectFileID: {main_id}\n' if main_id else ''
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid(relative)}\n{importer}:\n  externalObjects: {{}}\n{extra}  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
    return guid(relative)


def v3(values):
    return '{' + ', '.join(f'{key}: {value:.8g}' for key, value in zip('xyz', values)) + '}'


def color(values):
    return '{' + ', '.join(f'{key}: {value:.8g}' for key, value in zip('rgba', values)) + '}'


def ref(file_id=0):
    return '{fileID: ' + str(file_id) + '}'


def asset_ref(file_id, asset_guid, kind=2):
    return f'{{fileID: {file_id}, guid: {asset_guid}, type: {kind}}}'


def component_header(kind, file_id, name, go):
    return f'--- !u!{kind} &{file_id}\n{name}:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {ref(go)}\n'


def material_assets():
    folder = 'Assets/Materials/Environment/TeachingBuilding'
    for name, rgba in MATERIALS.items():
        relative = f'{folder}/{name}.mat'
        path = ROOT / relative
        asset_meta(relative, 'NativeFormatImporter', 2100000)
        transparent = rgba[3] < 1
        glow = name == 'Glow'
        emission = rgba if glow else [0, 0, 0, 1]
        floats = {'_Mode': 2 if transparent else 0, '_SrcBlend': 5 if transparent else 1,
                  '_DstBlend': 10 if transparent else 0, '_ZWrite': 0 if transparent else 1,
                  '_Metallic': .62 if name == 'Metal' else 0, '_Glossiness': .38 if name == 'Metal' else .16}
        float_text = ''.join(f'    - {key}: {value}\n' for key, value in floats.items())
        keyword = '_ALPHABLEND_ON' if transparent else '_EMISSION' if glow else ''
        path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n'
                        '  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n'
                        '  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
                        f'  m_Name: {name}\n  m_Shader: {{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}}\n'
                        f'  m_ValidKeywords: [{keyword}]\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n'
                        f'  m_EnableInstancingVariants: 1\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: {3000 if transparent else -1}\n'
                        '  stringTagMap: {}\n  disabledShaderPasses: []\n  m_SavedProperties:\n    serializedVersion: 3\n'
                        '    m_TexEnvs: []\n    m_Ints: []\n    m_Floats:\n' + float_text
                        + f'    m_Colors:\n    - _Color: {color(rgba)}\n    - _EmissionColor: {color(emission)}\n  m_BuildTextureStacks: []\n', encoding='utf-8')


def script_ref(filename):
    # Existing Unity GUIDs remain authoritative when scripts are reorganized.
    paths = {'PrototypeExplorer': 'Assets/Scripts/Player/Desktop/PrototypeExplorer.cs'}
    relative = paths.get(filename, 'Assets/Scripts/Prototype/' + filename + '.cs')
    metadata = ROOT / (relative + '.meta')
    match = re.search(r'^guid: ([a-f0-9]{32})$', metadata.read_text(encoding='utf-8'), re.M)
    if not match:
        raise ValueError('Missing script GUID: ' + relative)
    return asset_ref(11500000, match.group(1), 3)

def serialize_nodes(nodes):
    chunks = []
    ids = {item['id'] for item in nodes}
    for item in nodes:
        base = item['id'] * 100
        go, transform = base + 1, base + 2
        kind, component = item['kind'], item.get('component')
        mesh = kind in ['box', 'sphere', 'cylinder']
        pieces = [transform]
        if mesh:
            pieces += [base + 3, base + 4]
        if item['collision']:
            pieces.append(base + 5)
        if kind == 'text':
            pieces += [base + 4, base + 7]
        if kind in ['light', 'sun', 'camera']:
            pieces.append(base + 8)
        if kind == 'player':
            pieces.append(base + 9)
        if item.get('audio'):
            pieces.append(base + 10)
        if component:
            pieces.append(base + 6)
        chunks.append(f'--- !u!1 &{go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n'
                      + ''.join(f'  - component: {ref(piece)}\n' for piece in pieces)
                      + f'  m_Layer: 0\n  m_Name: {json.dumps(item["name"], ensure_ascii=False)}\n  m_TagString: {item.get("tag", "Untagged")}\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: {int(item["active"])}\n')
        q = item['rotation']
        children = [child['id'] * 100 + 2 for child in nodes if child['parent'] == item['id']]
        chunks.append(component_header(4, transform, 'Transform', go) + '  serializedVersion: 2\n'
                      + '  m_LocalRotation: {' + ', '.join(f'{k}: {v:.8g}' for k, v in zip('xyzw', q)) + '}\n'
                      + f'  m_LocalPosition: {v3(item["position"])}\n  m_LocalScale: {v3(item["scale"])}\n  m_ConstrainProportionsScale: 0\n'
                      + ('  m_Children:\n' + ''.join(f'  - {ref(child)}\n' for child in children) if children else '  m_Children: []\n')
                      + f'  m_Father: {ref(item["parent"] * 100 + 2 if item["parent"] in ids else 0)}\n  m_RootOrder: 0\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n')
        if mesh:
            mesh_id = {'box': 10202, 'sphere': 10207, 'cylinder': 10206}[kind]
            chunks.append(component_header(33, base + 3, 'MeshFilter', go)
                          + f'  m_Mesh: {{fileID: {mesh_id}, guid: 0000000000000000e000000000000000, type: 0}}\n')
        if mesh or kind == 'text':
            material = ref(0) if kind == 'text' else asset_ref(2100000, guid('Assets/Materials/Environment/TeachingBuilding/' + item['material'] + '.mat'))
            chunks.append(component_header(23, base + 4, 'MeshRenderer', go)
                          + f'  m_Enabled: 1\n  m_CastShadows: {0 if kind == "text" or item["material"] in ["Glass", "Smoke"] else 1}\n'
                          + '  m_ReceiveShadows: 1\n  m_DynamicOccludee: 1\n  m_StaticShadowCaster: 0\n  m_MotionVectors: 1\n'
                          + '  m_LightProbeUsage: 1\n  m_ReflectionProbeUsage: 1\n  m_RayTracingMode: 2\n  m_RayTraceProcedural: 0\n'
                          + f'  m_RenderingLayerMask: 1\n  m_RendererPriority: 0\n  m_Materials:\n  - {material}\n'
                          + '  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n  m_StaticBatchRoot: {fileID: 0}\n'
                          + '  m_ProbeAnchor: {fileID: 0}\n  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n'
                          + '  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n  m_IgnoreNormalsForChartDetection: 0\n  m_ImportantGI: 0\n'
                          + '  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 3\n  m_MinimumChartSize: 4\n'
                          + '  m_AutoUVMaxDistance: 0.5\n  m_AutoUVMaxAngle: 89\n  m_LightmapParameters: {fileID: 0}\n'
                          + '  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: 0\n  m_AdditionalVertexStreams: {fileID: 0}\n')
        if item['collision']:
            size = [1, 2, 1] if kind == 'cylinder' else [1, 1, 1]
            chunks.append(component_header(65, base + 5, 'BoxCollider', go)
                          + '  m_Material: {fileID: 0}\n  m_IsTrigger: 0\n  m_Enabled: 1\n  serializedVersion: 2\n'
                          + f'  m_Size: {v3(size)}\n  m_Center: {{x: 0, y: 0, z: 0}}\n')
        if kind == 'text':
            chunks.append(component_header(102, base + 7, 'TextMesh', go)
                          + f'  serializedVersion: 3\n  m_Text: {json.dumps(item["text"], ensure_ascii=False)}\n  m_OffsetZ: 0\n'
                          + f'  m_CharacterSize: {item["textSize"]}\n  m_LineSpacing: 1\n  m_Anchor: 4\n  m_Alignment: 1\n'
                          + f'  m_TabSize: 4\n  m_FontSize: 64\n  m_FontStyle: 0\n  m_RichText: 1\n  m_Font: {{fileID: 0}}\n  m_Color: {color(item["textColor"])}\n')
        if kind in ['light', 'sun']:
            chunks.append(component_header(108, base + 8, 'Light', go)
                          + f'  m_Enabled: 1\n  serializedVersion: 11\n  m_Type: {1 if kind == "sun" else 2}\n  m_Shape: 0\n'
                          + f'  m_Color: {color([1, .94, .83, 1] if kind == "sun" else [.92, .97, 1, 1])}\n'
                          + f'  m_Intensity: {1.05 if kind == "sun" else item["lightIntensity"]}\n  m_Range: {item.get("lightRange", 10)}\n'
                          + '  m_SpotAngle: 30\n  m_InnerSpotAngle: 21.8\n  m_CookieSize: 10\n  m_Shadows:\n'
                          + f'    m_Type: {2 if kind == "sun" else 0}\n    m_Resolution: -1\n    m_CustomResolution: -1\n    m_Strength: 0.65\n'
                          + '    m_Bias: 0.05\n    m_NormalBias: 0.4\n    m_NearPlane: 0.2\n    m_CullingMatrixOverride: {e00: 1, e01: 0, e02: 0, e03: 0, e10: 0, e11: 1, e12: 0, e13: 0, e20: 0, e21: 0, e22: 1, e23: 0, e30: 0, e31: 0, e32: 0, e33: 1}\n'
                          + '    m_UseCullingMatrixOverride: 0\n  m_Cookie: {fileID: 0}\n  m_DrawHalo: 0\n  m_Flare: {fileID: 0}\n'
                          + '  m_RenderMode: 0\n  m_CullingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n'
                          + '  m_RenderingLayerMask: 1\n  m_Lightmapping: 4\n  m_LightShadowCasterMode: 0\n  m_AreaSize: {x: 1, y: 1}\n'
                          + '  m_BounceIntensity: 1\n  m_ColorTemperature: 6570\n  m_UseColorTemperature: 0\n  m_BoundingSphereOverride: {x: 0, y: 0, z: 0, w: 0}\n'
                          + '  m_UseBoundingSphereOverride: 0\n  m_UseViewFrustumForShadowCasterCull: 1\n  m_ForceVisible: 0\n  m_ShadowRadius: 0\n  m_ShadowAngle: 0\n')
        if kind == 'camera':
            chunks.append(component_header(20, base + 8, 'Camera', go)
                          + f'  m_Enabled: {int(item["enabled"])}\n  serializedVersion: 2\n  m_ClearFlags: 2\n  m_BackGroundColor: {color([.7, .82, .87, 1])}\n'
                          + '  m_projectionMatrixMode: 1\n  m_GateFitMode: 2\n  m_FOVAxisMode: 0\n  m_Iso: 200\n  m_ShutterSpeed: 0.005\n'
                          + '  m_Aperture: 16\n  m_FocusDistance: 10\n  m_FocalLength: 50\n  m_BladeCount: 5\n  m_Curvature: {x: 2, y: 11}\n'
                          + '  m_BarrelClipping: 0.25\n  m_Anamorphism: 0\n  m_SensorSize: {x: 36, y: 24}\n  m_LensShift: {x: 0, y: 0}\n'
                          + '  m_NormalizedViewPortRect:\n    serializedVersion: 2\n    x: 0\n    y: 0\n    width: 1\n    height: 1\n'
                          + '  near clip plane: 0.08\n  far clip plane: 180\n  field of view: 55\n  orthographic: 0\n  orthographic size: 5\n'
                          + '  m_Depth: 0\n  m_CullingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n  m_RenderingPath: -1\n'
                          + '  m_TargetTexture: {fileID: 0}\n  m_TargetDisplay: 0\n  m_TargetEye: 3\n  m_HDR: 0\n  m_AllowMSAA: 1\n'
                          + '  m_AllowDynamicResolution: 0\n  m_ForceIntoRT: 0\n  m_OcclusionCulling: 1\n  m_StereoConvergence: 10\n  m_StereoSeparation: 0.022\n')
        if kind == 'player':
            chunks.append(component_header(143, base + 9, 'CharacterController', go)
                          + '  m_Material: {fileID: 0}\n  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n'
                          + '  m_LayerOverridePriority: 0\n  m_IsTrigger: 0\n  m_Enabled: 0\n  serializedVersion: 3\n  m_Height: 1.8\n'
                          + '  m_Radius: 0.28\n  m_SlopeLimit: 46\n  m_StepOffset: 0.28\n  m_SkinWidth: 0.025\n  m_MinMoveDistance: 0\n  m_Center: {x: 0, y: 0.9, z: 0}\n')
        if item.get('audio'):
            chunks.append(component_header(81, base + 10, 'AudioListener', go) + '  m_Enabled: 1\n')
        if component:
            filename = {'door': 'PrototypeDoor', 'notice': 'FacilityNotice', 'explorer': 'PrototypeExplorer', 'bootstrap': 'PrototypeSceneBootstrap'}[component]
            chunks.append(component_header(114, base + 6, 'MonoBehaviour', go)
                          + f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {script_ref(filename)}\n  m_Name: \n  m_EditorClassIdentifier: \n')
            if component == 'door':
                chunks.append(f'  openAngle: {item["openAngle"]}\n  startsOpen: {int(item["startsOpen"])}\n')
            elif component == 'notice':
                chunks.append(f'  kind: {item["noticeKind"]}\n')
            elif component == 'explorer':
                for field, key, offset in [('walkCamera', 'walkCamera', 8), ('overviewCamera', 'overviewCamera', 8),
                                           ('spawnSecondFloor', 'spawnSecond', 2), ('spawnFourthFloor', 'spawnFourth', 2),
                                           ('assemblyPoint', 'assembly', 2), ('roof', 'roofNode', 1), ('smokePreview', 'smokeNode', 1)]:
                    chunks.append(f'  {field}: {ref(item[key] * 100 + offset)}\n')
    return ''.join(chunks)


def write_scene():
    # Keep the Unity scene settings as a tool input, independent of example scenes.
    settings = (ROOT / 'Tools/teaching_building_scene_settings.yaml').read_text(encoding='utf-8')
    settings = re.sub(r'  m_AmbientSkyColor: .*', '  m_AmbientSkyColor: ' + color([.6, .7, .76, 1]), settings)
    settings = re.sub(r'  m_AmbientEquatorColor: .*', '  m_AmbientEquatorColor: ' + color([.42, .49, .51, 1]), settings)
    settings = re.sub(r'  m_AmbientGroundColor: .*', '  m_AmbientGroundColor: ' + color([.3, .34, .32, 1]), settings)
    settings = re.sub(r'  m_AmbientMode: .*', '  m_AmbientMode: 1', settings)
    settings = re.sub(r'  m_SkyboxMaterial: .*', '  m_SkyboxMaterial: {fileID: 0}', settings)
    sun = next(item['id'] * 100 + 8 for item in NODES if item['kind'] == 'sun')
    settings = re.sub(r'  m_Sun: .*', '  m_Sun: ' + ref(sun), settings)
    relative = 'Assets/Scenes/TeachingBuilding.unity'
    asset_meta(relative)
    (ROOT / relative).write_text(settings + serialize_nodes(NODES), encoding='utf-8')
    for prefab_name in ['DeskAndChair', 'FireEquipmentPoint']:
        root_node = next(item for item in NODES if item['name'] == prefab_name)
        descendants = {root_node['id']}
        for item in NODES:
            if item['parent'] in descendants:
                descendants.add(item['id'])
        items = [dict(item) for item in NODES if item['id'] in descendants]
        items[0].update(parent=0, position=[0, 0, 0])
        relative = f'Assets/Prefabs/Environment/TeachingBuilding/{prefab_name}.prefab'
        asset_meta(relative, 'PrefabImporter')
        (ROOT / relative).write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + serialize_nodes(items), encoding='utf-8')


def ensure_metadata():
    for path in ASSETS.rglob('*'):
        if path.name.endswith('.meta'):
            continue
        relative = path.relative_to(ROOT).as_posix()
        if path.is_dir() and not Path(str(path) + '.meta').exists():
            Path(str(path) + '.meta').write_text(f'fileFormatVersion: 2\nguid: {guid(relative)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
        elif path.suffix == '.cs' and not Path(str(path) + '.meta').exists():
            asset_meta(relative, 'MonoImporter')
        elif path.suffix == '.json' and not Path(str(path) + '.meta').exists():
            asset_meta(relative)


def main():
    data = layout()
    ensure_metadata()
    material_assets()
    write_scene()
    relative = 'Assets/Environment/TeachingBuildingLayout.json'
    asset_meta(relative)
    (ROOT / relative).write_text(json.dumps(data, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    preview_folder = ROOT / 'Docs/Previews'
    preview_folder.mkdir(parents=True, exist_ok=True)
    template = (ROOT / 'Tools/teaching_building_preview.html').read_text(encoding='utf-8')
    (preview_folder / 'TeachingBuildingPreview.html').write_text(template.replace('__SCENE_DATA__', json.dumps(data, ensure_ascii=False)), encoding='utf-8')
    ensure_metadata()
    print(f'Generated {len(NODES)} scene objects, {len(MATERIALS)} materials, 2 prefabs, Unity scene and offline preview.')


if __name__ == '__main__':
    main()
