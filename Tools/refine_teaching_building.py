"""Refine the shared building layout without replacing the original scene."""
import base64
import copy
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

import generate_teaching_building as base

ROOT = Path(__file__).resolve().parents[1]
ASSET_FOLDER = "Assets/Environment/TeachingBuildingRefined"
PREVIEW_FOLDER = "Docs/Previews/Refined"


def texture_metadata(path, linear=False):
    relative = path.relative_to(ROOT).as_posix()
    asset_guid = base.guid(relative)
    Path(str(path) + ".meta").write_text(f"""fileFormatVersion: 2
guid: {asset_guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 9
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {0 if linear else 1}
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 512
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 4
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 512
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
  spritePackingTag:
  userData:
  assetBundleName:
  assetBundleVariant:
""", encoding="utf-8")


def textures():
    """Small, seamless original textures; all sampling uses world meters."""
    size = 512
    y, x = np.mgrid[:size, :size]
    rng = np.random.default_rng(5424)
    noise = rng.normal(0, 1, (size, size))
    # Integer-frequency noise remains periodic; varying phases avoid regular stripes.
    coarse = np.zeros((size, size))
    for frequency, amplitude in [(2, 1), (5, .5), (11, .22), (23, .1)]:
        for _ in range(4):
            fx, fy = rng.integers(1, frequency + 1, 2)
            coarse += np.sin((x * fx + y * fy) * math.tau / size + rng.uniform(0, math.tau)) * amplitude
    coarse /= np.std(coarse)
    grout = ((x % 256 < 2) | (y % 256 < 2)).astype(float)
    checker = ((x // 256 + y // 256) % 2) * 3
    flecks = (rng.random((size, size)) > .96) * rng.uniform(4, 13, (size, size))
    tiles = 243 + checker + noise * .65 + coarse * 1.2 - grout * 52 - flecks
    brick_x = (x + (y // 64 % 2) * 64) % 128
    mortar = ((brick_x < 3) | (y % 64 < 3)).astype(float)
    brick_variation = rng.uniform(-9, 9, (8, 4))[y // 64, (brick_x + x // 128 * 128) // 128 % 4]
    brick = 225 + brick_variation + noise * 3 + coarse * 5 - mortar * 56
    grain = np.zeros((size, size))
    warp = np.sin(y * math.tau / size) * .8 + coarse * .16
    for frequency, amplitude in [(7, 5), (17, 3.5), (37, 2.2), (83, .8)]:
        grain += np.sin(x * math.tau / size * frequency + warp * (frequency / 8) + rng.uniform(0, math.tau)) * amplitude
    # A restrained, distorted knot breaks up the repeated growth rings.
    knot = np.exp(-((x / size - .63) ** 2 / .012 + (y / size - .44) ** 2 / .08))
    grain += knot * np.sin(np.sqrt((x - 322) ** 2 + ((y - 225) * .4) ** 2) * .16) * 7
    surfaces = {
        "Plaster": 248 + noise * 1.2 + coarse * .5,
        "Tiles": tiles,
        "Brick": brick,
        "WoodGrain": 235 + grain + coarse * 3 + noise * .5,
        "BrushedSteel": 246 + np.sin(y * math.tau / size * 95) * 3 + noise * .9,
        "Concrete": 239 + noise * 2 + coarse * 2,
        "Paving": 235 + noise * 3 - (((x % 128 < 3) | (y % 128 < 3)) * 30),
        "Foliage": 218 + coarse * 13 + noise * 5,
        "Grass": 226 + coarse * 10 + noise * 9,
        "Bark": 220 + coarse * 7 + grain * 1.4,
    }
    for name, surface in surfaces.items():
        path = ROOT / ASSET_FOLDER / "Textures" / (name + ".png")
        path.parent.mkdir(parents=True, exist_ok=True)
        gray = np.clip(surface, 0, 255).astype(np.uint8)
        Image.fromarray(np.repeat(gray[:, :, None], 3, axis=2)).save(path)
        texture_metadata(path)
        joints = grout if name == "Tiles" else mortar if name == "Brick" else (((x % 128 < 3) | (y % 128 < 3)).astype(float)) if name == "Paving" else np.zeros_like(noise)
        height = np.clip(.65 + coarse * .06 + noise * .008 - joints * .5, 0, 1)
        smoothness = np.clip(.85 + coarse * .04 - joints * .6, 0, 1)
        ao = np.clip(1 - joints * .13, 0, 1)
        detail_path = path.with_name(name + "Detail.png")
        Image.fromarray((np.stack([ao, height, smoothness], axis=2) * 255).astype(np.uint8)).save(detail_path)
        texture_metadata(detail_path, linear=True)
    return surfaces


def decorate(data):
    base.NODES = copy.deepcopy(data["nodes"])
    original = list(base.NODES)
    colors = {material["name"]: material["color"] for material in data["materials"]}
    colors.update(Wall=[.84, .83, .79, 1], Floor=[.74, .73, .69, 1],
                  Corridor=[.7, .74, .72, 1], Concrete=[.68, .7, .68, 1],
                  Metal=[.65, .7, .72, 1], Wood=[.66, .48, .31, 1],
                  Ceiling=[.87, .88, .85, 1],
                  WallLower=[.67, .77, .74, 1], Brick=[.67, .42, .29, 1],
                  Stone=[.84, .86, .82, 1], Steel=[.82, .85, .86, 1],
                  Paper=[.95, .93, .85, 1], AccentBlue=[.17, .36, .55, 1],
                  AccentGold=[.83, .62, .23, 1], Glow=[1, .98, .93, 1],
                  Leaf=[.25, .39, .17, 1], LeafLight=[.35, .47, .21, 1],
                  Grass=[.4, .48, .27, 1], Bark=[.37, .29, .19, 1],
                  Glass=[.83, .91, .94, .12])
    base.MATERIALS = colors
    b, n, s = base.box, base.node, base.sign
    levels = [item for item in original if item["name"].startswith("Floor_")]
    roof = next(item["id"] for item in original if item["name"] == "Roof_CutawayToggle")
    exterior = next(item["id"] for item in original if item["name"] == "Exterior")

    rounded = {"Desktop": .018, "ChairSeat": .018, "ChairBack": .018,
               "Lectern": .035, "ReceptionDesk": .035, "OfficeDesk": .018,
               "LiftDoor": .01, "Cabinet": .025, "DoorJamb": .014,
               "DoorLintel": .014, "WindowFrame": .012, "WindowMullion": .01,
               "SignPanel": .015, "Bench": .02, "Planter": .03, "WashCounter": .018}
    for item in original:
        if item["name"] == "Ground":
            item["scale"] = [250, .3, 250]
            item["position"][2] = 0
        if item["name"] == "Bench":
            px, py, pz = item["position"]
            item["scale"][2] = .13
            item["position"][2] -= .18
            for offset in [0, .18]:
                b("BenchSeatSlat", item["parent"], (px, py, pz + offset), (3.5, .1, .13), "Wood", bevel=.018)
            for height in [.75, .98]:
                b("BenchBackSlat", item["parent"], (px, height, pz + .36), (3.5, .14, .09), "Wood", False, bevel=.018)
            for offset in [-1.25, 1.25]:
                b("BenchBackSupport", item["parent"], (px + offset, .67, pz + .38), (.055, .76, .055), "Navy", False)
        if item["kind"] == "light":
            item["position"][1] = 3.1
            item["lightRange"] = min(item["lightRange"], 7)
            item["lightIntensity"] = 1.65
        if item["name"] == "CeilingLight":
            px, py, pz = item["position"]
            width, height, depth = item["scale"]
            item["bevel"] = .012
            b("LuminaireHousing", item["parent"], (px, py + .035, pz),
              (width + .07, .065, depth + .07), "White", False, bevel=.015)
            for side in [-1, 1]:
                b("LuminaireEndCap", item["parent"], (px + side * (width / 2 + .015), py - .01, pz),
                  (.045, .045, depth + .05), "Metal", False, bevel=.008)
        if item["name"] == "CampusTree":
            tree = item["id"]
            for index, (px, py, pz, diameter) in enumerate([
                    (-1.05, 3.35, -.35, 2.8), (.9, 3.65, .65, 2.5),
                    (-.4, 4.6, .25, 2.8), (.75, 4.8, -.45, 2.45),
                    (-.2, 5.5, -.1, 2.3), (-1.25, 4.5, -.65, 2.15)]):
                n("FoliageCluster", tree, (px, py, pz), "sphere", (diameter, diameter * .9, diameter),
                  "LeafLight" if index % 3 == 0 else "Leaf")
                base.beam(tree, "TreeBranch", (0, 2.4, 0), (px, py - .5, pz), "Bark", .14)
        if item["name"] == "Crown":
            item["scale"] = [2.75, 2.7, 2.6]
        if item["name"] == "Trunk":
            item["material"] = "Bark"
        if item["name"] == "FireEquipmentPoint":
            group = item["id"]
            for px in [-.22, .52]:
                b("HoseCabinetDoorRim", group, (px, 1.05, -.12), (.023, .91, .025), "Steel", False)
            b("HoseCabinetLatch", group, (.42, 1.05, -.15), (.033, .13, .036), "Steel", False, bevel=.007)
            for py in [.74, 1.34]:
                b("HoseCabinetHinge", group, (-.22, py, -.116), (.035, .055, .032), "Steel", False)
            n("ExtinguisherShoulder", group, (-.5, .76, -.02), "sphere", (.21, .13, .21), "Red")
            n("ExtinguisherValve", group, (-.5, .81, -.02), "cylinder", (.062, .045, .062), "Steel")
            n("ExtinguisherFoot", group, (-.5, .15, -.02), "cylinder", (.225, .032, .225), "Navy")
            n("PressureGauge", group, (-.5, .8, -.09), "cylinder", (.055, .008, .055), "White",
              rotation=[math.sqrt(.5), 0, 0, math.sqrt(.5)])
            hose_points = [(-.46, .83, 0), (-.36, .8, 0), (-.32, .66, -.02), (-.32, .35, -.05)]
            for start, end in zip(hose_points, hose_points[1:]):
                base.beam(group, "ExtinguisherHose", start, end, "Navy", .026)
            s(group, "ABC\nFIRE", (-.5, .48, -.137), .145, .2, material="Paper", size=.04)
            for label in base.NODES[-2:]:
                if label["kind"] == "text": label["textColor"] = colors["Red"]
        if item["name"] in rounded:
            item["bevel"] = rounded[item["name"]]
        if item["name"] == "Wall_WindowSill":
            item["material"] = "Brick"
        if item["name"] == "DoorPanel":
            # Separate a genuine vision opening from the moving door leaf.
            hinge, width = item["parent"], item["scale"][0]
            item["position"] = [width / 2, .62, 0]
            item["scale"] = [width, 1.24, .055]
            item["bevel"] = .01
            for name, pos, scale in [
                ("DoorUpperLeaf", (width / 2, 2.12, 0), (width, .32, .055)),
                ("DoorLeafLeft", (.09, 1.6, 0), (.18, .72, .055)),
                ("DoorLeafRight", ((.58 + width) / 2, 1.6, 0), (width - .58, .72, .055)),
                ("DoorVisionGlass", (.38, 1.6, 0), (.4, .72, .025)),
            ]:
                b(name, hinge, pos, scale, "Glass" if "Glass" in name else item["material"], bevel=.005)
            for face in [-.035, .035]:
                for x in [.18, .58]:
                    b("VisionWindowFrame", hinge, (x, 1.6, face), (.025, .76, .025), "Metal", False, bevel=.006)
                for y in [1.24, 1.96]:
                    b("VisionWindowFrame", hinge, (.38, y, face), (.425, .025, .025), "Metal", False, bevel=.006)
                b("DoorKickPlate", hinge, (width / 2, .16, face), (width - .12, .25, .014), "Steel", False)
        if item["name"] == "Wall_Partition":
            px, py, pz = item["position"]
            width, height, depth = item["scale"]
            for side in [-1, 1]:
                if depth < .3:
                    b("WallDado", item["parent"], (px, .5, pz + side * (depth / 2 + .011)),
                      (width, .94, .018), "WallLower", False)
                    b("DadoCap", item["parent"], (px, 1, pz + side * (depth / 2 + .021)),
                      (width, .025, .025), "White", False)
                elif width < .3:
                    b("WallDado", item["parent"], (px + side * (width / 2 + .011), .5, pz),
                      (.018, .94, depth), "WallLower", False)
        if item["name"] == "WindowBay":
            width = next(child["scale"][0] for child in original if child["parent"] == item["id"] and child["name"] == "Wall_WindowSill")
            for face in [-.18, .18]:
                b("WindowSillShelf", item["id"], (0, 1.015, face), (width - .4, .055, .22), "Stone", False, bevel=.012)
                b("WindowCrossbar", item["id"], (0, 1.9, face * .67), (width - .6, .04, .05), "Metal", False, bevel=.008)
        if item["name"] == "ElevatorLanding":
            landing = item["id"]
            for x in [-.715, .715]:
                b("LiftFrameSide", landing, (x, 1.18, 4.35), (.08, 2.44, .13), "Steel", False, bevel=.015)
            b("LiftThreshold", landing, (0, .019, 4.3), (1.43, .025, .35), "Steel", False)
            b("LiftCenterSeam", landing, (0, 1.17, 4.364), (.016, 2.3, .012), "Navy", False)
            s(landing, f"{item['floor']:02}", (.47, 2.66, 4.27), .27, .24, material="Screen", size=.16)
            b("CallPanelRim", landing, (1.05, 1.25, 4.343), (.225, .41, .035), "Steel", False, bevel=.02)
            s(landing, "FIRE SAFETY\nUSE STAIRS", (-1.62, 2.59, 4.32), .95, .44, material="Green", size=.095)
        if item["name"].startswith("Classroom_"):
            group = item["id"]
            floor = item["floor"]
            room = floor * 100 + (1 if item["position"][0] < 0 else 2)
            board_text = next(child for child in original if child["kind"] == "text" and child["parent"] in
                              [sign["id"] for sign in original if sign["parent"] == group and sign["name"].startswith("Sign_")])
            board_text["text"] = f"CLASSROOM {room}\nCAMPUS EMERGENCY PREPAREDNESS"
            board_text["textColor"] = colors["Navy"]
            teaching_board = base.NODES[board_text["parent"] - 1]
            teaching_board["position"] = [-4.43, 1.9, 1]
            teaching_board["rotation"] = base.quat_y(-90)
            desks = [child for child in original if child["parent"] == group and child["name"] == "DeskAndChair"]
            for index, furniture in enumerate(desks):
                furniture["position"] = [-1.4 + index // 3 * 1.8, 0, -1.4 + index % 3 * 1.7]
                furniture["rotation"] = base.quat_y(-90)
            lectern = next(child for child in original if child["parent"] == group and child["name"] == "Lectern")
            lectern["position"] = [-3.45, .55, 1]
            b("BoardTray", group, (-4.36, 1.15, 1), (.12, .045, 5.1), "Metal", False)
            b("ProjectorBody", group, (0, 3, 1), (.4, .16, .29), "White", False, bevel=.025)
            b("ProjectorMount", group, (0, 3.19, 1), (.035, .22, .035), "Metal", False)
            n("ProjectorLens", group, (-.21, 2.99, 1), "cylinder", (.095, .02, .095), "Screen",
              rotation=[0, 0, math.sqrt(.5), math.sqrt(.5)])
            s(group, "SAFETY NOTICE\n\nRaise the alarm\nFollow EXIT signs\nUse the stairs", (4.43, 1.75, -.8), 1.35, 1.4,
              yaw=90, material="Teal", size=.105)
            for furniture in desks[:3]:
                b("Notebook", furniture["id"], (.24, .819, .07), (.22, .018, .29), "Paper", False, bevel=.005)
                b("NotebookSpine", furniture["id"], (.12, .826, .07), (.02, .018, .29), "AccentBlue", False)
            for x in [-3.6, 3.6]:
                b("AcousticCeilingPanel", group, (x, 3.3, .45), (1.45, .03, 3.3), "Ceiling", False)
            for x in [-2.7, 2.7]:
                b("LuminaireHousing", group, (x, 3.27, .45), (1.85, .08, .38), "White", False, bevel=.015)
                b("CeilingDiffuser", group, (x, 3.22, .45), (1.75, .022, .29), "Glow", False, bevel=.007)
                base.light(group, (x, 3.1, .45), 1.15, 6.5)
            for x in [-3.6, -2.4, -1.2, 0, 1.2, 2.4, 3.6]:
                b("CeilingTGrid", group, (x, 3.348, 0), (.012, .018, 7.05), "White", False)
            for z in [-3, -1.8, -.6, .6, 1.8, 3]:
                b("CeilingTGrid", group, (0, 3.348, z), (8.75, .018, .012), "White", False)
            for x in [1.8, 3]:
                b("CeilingVent", group, (x, 3.335, -2.6), (.62, .018, .32), "Metal", False)
                for z in [-2.72, -2.66, -2.6, -2.54, -2.48]:
                    b("VentSlot", group, (x, 3.32, z), (.53, .008, .017), "Navy", False)
            for z in [1.05, 1.32, 1.59]:
                b("BoardMarker", group, (-4.32, 1.2, z), (.02, .02, .16), "AccentBlue", False)
        if item["kind"] == "text" and item["text"].startswith("LIFT /"):
            item["text"] = "LIFT"
            item["position"][0] = -.16

    # Ceiling panels follow the floor footprint, retaining stair and shaft voids.
    for level in levels:
        ceiling = n("Ceiling_F%02d" % level["floor"], roof, (0, level["position"][1], 0), floor=level["floor"], roof=True)
        template_level = levels[1] if level["floor"] == 1 else level
        for slab in [item for item in original if item["parent"] == template_level["id"] and item["name"] == "FloorSlab"]:
            b("CeilingPanel", ceiling, (slab["position"][0], 3.38, slab["position"][2]),
              (slab["scale"][0], .035, slab["scale"][2]), "Ceiling", False)
        f = level["floor"]
        for x in range(-12, 13, 2):
            b("CorridorCeilingGrid", ceiling, (x, 3.351, 0), (.012, .016, 2.98), "White", False)
        for z in [-.75, .75]:
            b("CorridorCeilingGrid", ceiling, (0, 3.351, z), (25.8, .016, .012), "White", False)
        b("LiftLobbyDiffuser", level["id"], (0, 3.22, 3), (.8, .025, .48), "Glow", False)
        base.light(level["id"], (0, 3.1, 3), 1.4, 4.8)
        accent = "AccentBlue" if f in [1, 3] else "Teal"
        s(level["id"], f"{f:02}\nTEACHING FLOOR", (2.09, 2.2, 3.25), 1.45, .9, yaw=90, material=accent, size=.13)
        for x in [-4.5, 4.5]:
            s(level["id"], "CAMPUS NOTICE\n\nKeep exits clear\nAssembly point outside", (x, 1.8, -1.37), 1.5, 1.05,
              yaw=180, material=accent, size=.11)
        for x in [-12.7, 12.7]:
            b("CorridorBorder", level["id"], (x, .02, 0), (.065, .02, 2.96), "Teal", False)
        if f > 1:
            for x in [-10.5, -5.3, 2]:
                b("StudyTable", level["id"], (x, .75, -5.5), (1.7, .08, .85), "Wood", bevel=.02)
                for offset in [-.65, .65]:
                    b("StudyTableLeg", level["id"], (x + offset, .35, -5.5), (.05, .7, .7), "Metal")

    for item in [item for item in original if item["name"].startswith("Stair_") and item["kind"] == "group"]:
        center = -15.5 if "Stair_A" in item["name"] else 15.5
        identifier = "A" if center < 0 else "B"
        if item["floor"] < 4:
            for x in [-1.91, -.25]:
                base.beam(item["id"], "StairStringer", (center + x, -.06, 1.8), (center + x, 1.74, -1.8), "Stone", .14)
            for x in [.25, 1.91]:
                base.beam(item["id"], "StairStringer", (center + x, 1.74, -1.8), (center + x, 3.54, 1.8), "Stone", .14)
        s(item["id"], "EXIT VIA STAIRS\nGROUND FLOOR", (center - 2.33, 1.6, 3), 1.5, .55,
          yaw=-90, material="Green", size=.12)

    for x in [-17.9, 17.9]:
        for z in [-9.13, 9.13]:
            b("FacadeCornerPier", exterior, (x, 7.15, z), (.2, 14.3, .12), "Stone", False, bevel=.02)
    for f in range(1, 5):
        s(exterior, f"{f:02}", (-17.2, (f - 1) * 3.6 + 2.15, -9.18), .8, .8, material="Teal", size=.4)
    for x in [-1.7, -1, -.3, .4, 1.1, 1.8]:
        b("EntranceCanopySlat", exterior, (x, 2.58, -10.6), (.09, .16, 3.05), "Wood", False, bevel=.018)
    for x in [-17.8, 17.8]:
        n("RainwaterPipe", exterior, (x, 7.1, 9.24), "cylinder", (.11, 7.1, .11), "Metal")
    outdoor = next(item["id"] for item in original if item["name"] == "Outdoor")
    for x in [-24, 24]:
        b("ForecourtKerb", outdoor, (x, .01, -20), (.16, .18, 22), "Stone", False, bevel=.02)
    b("ForecourtKerb", outdoor, (0, .01, -31), (48, .18, .16), "Stone", False, bevel=.02)
    for x in [-8, 8]:
        b("RooftopServiceUnit", roof, (x, 14.85, 4.3), (2.2, .75, 1.5), "White", False, bevel=.06)
        for z in [3.83, 4.15, 4.47, 4.79]:
            b("ServiceUnitVent", roof, (x, 15.235, z), (1.8, .015, .12), "Metal", False)

    texture_for = {"Wall": "Plaster", "WallLower": "Plaster", "Ceiling": "Plaster", "Floor": "Tiles",
                   "Corridor": "Tiles", "Brick": "Brick", "Wood": "WoodGrain", "Metal": "BrushedSteel",
                   "Steel": "BrushedSteel", "Concrete": "Concrete", "Stone": "Concrete", "Paving": "Paving",
                   "Leaf": "Foliage", "LeafLight": "Foliage", "Grass": "Grass", "Bark": "Bark"}
    materials = []
    for name, color in colors.items():
        material = dict(name=name, color=color, metallic=.78 if name in ["Metal", "Steel"] else 0,
                        smoothness=.55 if name in ["Metal", "Steel"] else .38 if name in ["Floor", "Corridor"] else .26 if name == "Wood" else .06 if name in ["Leaf", "LeafLight", "Grass", "Bark"] else .18)
        if name == "Glass": material["smoothness"] = .82
        if name in texture_for:
            material.update(texture=f"{ASSET_FOLDER}/Textures/{texture_for[name]}.png",
                            detailTexture=f"{ASSET_FOLDER}/Textures/{texture_for[name]}Detail.png",
                            bumpMeters=.006 if name == "Brick" else .003 if name in ["Paving", "Concrete", "Bark"] else .0015,
                            tileMeters=1.2 if name in ["Floor", "Corridor"] else 1)
        materials.append(material)
    data.update(version=2, scenePath="Assets/Scenes/TeachingBuildingRefined.unity", assetFolder=ASSET_FOLDER,
                previewFolder=PREVIEW_FOLDER, previewPrefix="Refined", bakeLighting=True, materials=materials, nodes=base.NODES,
                prefabNames=["DeskAndChair", "FireEquipmentPoint", "WindowBay", "ElevatorLanding",
                             "Classroom_Detailed", "Stairwell_A", "Stairwell_B"])
    ceiling_details = {"CeilingLight", "CeilingDiffuser", "LuminaireHousing", "LuminaireEndCap",
                       "CeilingTGrid", "AcousticCeilingPanel", "CeilingVent", "VentSlot",
                       "ProjectorBody", "ProjectorMount", "ProjectorLens", "SmokeDetector", "LiftLobbyDiffuser"}
    for item in data["nodes"]:
        if item["name"] in ceiling_details:
            item["roof"] = True
    return data


def main():
    data = json.loads((ROOT / "Assets/Environment/TeachingBuildingLayout.json").read_text(encoding="utf-8"))
    surfaces = textures()
    refined = decorate(data)
    relative = "Assets/Environment/TeachingBuildingRefinedLayout.json"
    base.asset_meta(relative)
    (ROOT / relative).write_text(json.dumps(refined, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    preview = copy.deepcopy(refined)
    for material in preview["materials"]:
        if material.get("texture"):
            image = (ROOT / material["texture"]).read_bytes()
            material["textureData"] = "data:image/png;base64," + base64.b64encode(image).decode("ascii")
    template = (ROOT / "Tools/teaching_building_preview.html").read_text(encoding="utf-8")
    template = template.replace("四层教学楼 · 场景原型", "四层教学楼 · 细化场景").replace("SCENE BLOCKOUT / 初版布局", "MODULAR ENVIRONMENT / 模型与材质细化")
    folder = ROOT / PREVIEW_FOLDER
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "TeachingBuildingRefinedPreview.html").write_text(template.replace("__SCENE_DATA__", json.dumps(preview, ensure_ascii=False)), encoding="utf-8")
    base.ensure_metadata()
    print(f"Refined layout: {len(refined['nodes'])} objects, {len(refined['materials'])} materials, {len(surfaces)} original texture/detail pairs.")


if __name__ == "__main__":
    main()
