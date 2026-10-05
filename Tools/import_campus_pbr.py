"""Prepare bundled CC0 scans for the existing Unity architectural shader.

No network calls. Source checksums and authors are recorded in the manifest.
Detail channels are linear AO / physical relief / inverse roughness.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

import refine_teaching_building as refined

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'Assets/Environment/TeachingBuildingCampus'


def prepare_scans():
    manifest = json.loads((FOLDER / 'SourceTextures/manifest.json').read_text())
    for entry in manifest['files']:
        path = ROOT / entry['path']
        assert hashlib.md5(path.read_bytes()).hexdigest() == entry['md5'], path
    output = FOLDER / 'Textures/PBR'
    output.mkdir(parents=True, exist_ok=True)
    for asset, name in [('terrazzo_tiles', 'ScannedTerrazzo'), ('white_oak_veneer', 'ScannedOak')]:
        records = {e['map']: ROOT / e['path'] for e in manifest['files'] if e['asset'] == asset}
        Image.open(records['Diffuse']).convert('RGB').save(output / (name + '.png'))
        channels = [np.asarray(Image.open(records[k]).convert('L'), dtype=np.float32) / 255
                    for k in ['AO', 'Displacement', 'Rough']]
        channels[2] = 1 - channels[2]  # Unity uses smoothness, not roughness.
        Image.fromarray(np.round(np.stack(channels, axis=2) * 255).astype(np.uint8)).save(output / (name + 'Detail.png'))
        for suffix, linear in [('', False), ('Detail', True)]:
            path = output / (name + suffix + '.png')
            refined.texture_metadata(path, linear=linear)
            meta = Path(str(path) + '.meta')
            meta.write_text(meta.read_text().replace('maxTextureSize: 512', 'maxTextureSize: 1024').replace('aniso: 4', 'aniso: 8'))
    hdr = next(ROOT / e['path'] for e in manifest['files'] if e['map'] == 'HDRI')
    refined.texture_metadata(hdr, linear=True)
    meta = Path(str(hdr) + '.meta')
    meta.write_text(meta.read_text().replace('maxTextureSize: 512', 'maxTextureSize: 2048'))


if __name__ == '__main__':
    prepare_scans()
    print('CC0 scan checksums and packed PBR textures PASS')
