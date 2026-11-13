"""Derive subtle XR finishes from the project's CC0 terrazzo scan and authored tile patterns."""

from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Environment/CampusXRVisuals/Textures"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    source = ROOT / "Assets/Environment/TeachingBuildingCampus/Textures/PBR/ScannedTerrazzo.png"
    rgb = np.asarray(Image.open(source).convert("RGB"), dtype=np.float32) / 255
    gray = np.sum(rgb * np.array([0.2126, 0.7152, 0.0722]), axis=2, keepdims=True)
    stone = gray * 0.65 + 0.30 + (rgb - gray) * 0.22
    stone *= np.array([1.025, 1.015, 0.985])
    Image.fromarray(np.uint8(np.clip(stone, 0, 1) * 255)).save(OUT / "NeutralTerrazzo.png")

    # Four 600 mm tiles per repeat; mipmapped 1.5 mm joints stay quiet at VR viewing distances.
    size = 1024
    y, x = np.mgrid[:size, :size]
    rng = np.random.default_rng(5424)
    joint = ((x % 256) < 2) | ((y % 256) < 2)
    grain = rng.normal(0, 0.0025, (size, size))
    variation = np.array([[0.002, -0.006, 0.001, -0.003], [-0.004, 0.003, -0.002, 0.004],
                          [0.001, -0.003, 0.005, -0.002], [-0.005, 0.002, -0.001, 0.003]])
    value = 0.76 + variation[y // 256, x // 256] + grain
    value[joint] = 0.51
    tile = value[..., None] * np.array([1.035, 1.025, 1.0])
    Image.fromarray(np.uint8(np.clip(tile, 0, 1) * 255)).save(OUT / "RestroomTile.png")
    detail = np.empty((size, size, 3), dtype=np.uint8)
    detail[..., 0] = np.where(joint, 235, 255)
    detail[..., 1] = np.where(joint, 88, 130)
    detail[..., 2] = np.where(joint, 80, 170)
    Image.fromarray(detail).save(OUT / "RestroomTileDetail.png")
    print("XR finish textures generated; terrazzo derives from the existing Poly Haven CC0 source.")


if __name__ == "__main__":
    main()
