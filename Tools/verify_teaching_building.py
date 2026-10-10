"""Check generated assets and route geometry without requiring an Editor license."""
import json
import math
import re
import subprocess
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
EDITOR = Path('E:/Unity/Unity Hub/2022.3.62f3/Editor/Data')


def matrix(item, open_doors=False):
    x, y, z, w = item['rotation']
    if open_doors and item.get('component') == 'door':
        angle = math.radians(item['openAngle']) / 2
        x, y, z, w = 0, math.sin(angle), 0, math.cos(angle)
    rotation = np.array([[1 - 2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                         [2*(x*y+z*w), 1 - 2*(x*x+z*z), 2*(y*z-x*w)],
                         [2*(x*z-y*w), 2*(y*z+x*w), 1 - 2*(x*x+y*y)]])
    result = np.eye(4)
    result[:3, :3] = rotation @ np.diag(item['scale'])
    result[:3, 3] = item['position']
    return result


def compile_scripts():
    logs = ROOT / 'Logs'
    logs.mkdir(exist_ok=True)
    api = EDITOR / 'MonoBleedingEdge/lib/mono/4.8-api'
    references = [api / name for name in ['mscorlib.dll', 'System.dll', 'System.Core.dll']]
    references += list((EDITOR / 'Managed/UnityEngine').glob('UnityEngine*.dll'))
    references += [api / 'Facades/netstandard.dll']
    mono = EDITOR / 'MonoBleedingEdge/bin/mono.exe'
    compiler = EDITOR / 'MonoBleedingEdge/lib/mono/msbuild/Current/bin/Roslyn/csc.exe'
    compiled = logs / 'Prototype.Runtime.dll'

    def run(name, sources, refs, output):
        response = logs / (name + '.rsp')
        response.write_text('\n'.join(['/nologo', '/noconfig', '/nostdlib+', '/target:library', '/langversion:latest',
                                        f'/out:"{output.as_posix()}"']
                                       + [f'/reference:"{path.as_posix()}"' for path in refs]
                                       + [f'"{path.as_posix()}"' for path in sources]), encoding='utf-8')
        result = subprocess.run([str(mono), str(compiler), '@' + str(response)], cwd=ROOT,
                                text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        (logs / (name + '.txt')).write_text(result.stdout, encoding='utf-8')
        print(name + ': ' + ('PASS' if result.returncode == 0 else 'FAIL'))
        if result.returncode:
            print(result.stdout)
        return result.returncode == 0

    runtime_ok = run('runtime-compile', list((ROOT / 'Assets/Scripts').rglob('*.cs')), references, compiled)
    editor_refs = references + [compiled, EDITOR / 'Managed/UnityEditor.dll']
    editor_refs += list((EDITOR / 'Managed/UnityEditor').glob('*.dll'))
    editor_ok = runtime_ok and run('editor-compile', list((ROOT / 'Assets/Editor').glob('*.cs')),
                                  editor_refs, logs / 'Prototype.Editor.dll')
    return runtime_ok and editor_ok


def validate_assets(data):
    nodes = data['nodes']
    assert len({item['id'] for item in nodes}) == len(nodes), 'Duplicate node IDs'
    ids = {item['id'] for item in nodes}
    for item in nodes:
        assert item['parent'] == 0 or item['parent'] in ids, 'Missing parent'
        assert all(math.isfinite(v) for key in ['position', 'rotation', 'scale'] for v in item[key]), 'Non-finite transform'
        assert min(item['scale']) > 0, 'Invalid scale'
        assert abs(sum(v*v for v in item['rotation']) - 1) < .002, 'Non-unit rotation'
    assert sum(item.get('noticeKind') == 0 for item in nodes) == 4, 'Lift panel count'
    assert sum(item.get('noticeKind') == 1 for item in nodes) == 8, 'Alarm count'
    assert sum(item.get('noticeKind') == 2 for item in nodes) == 8, 'Fire cabinet count'
    assert len([item for item in nodes if item['name'] == 'Cabin']) == 1, 'Cabin duplication'
    assert len([item for item in nodes if item['name'].startswith('Floor_')]) == 4, 'Floor count'
    text = (ROOT / 'Assets/Scenes/TeachingBuilding.unity').read_text(encoding='utf-8')
    file_ids = set(re.findall(r'^--- !u!\d+ &(\d+)', text, re.M))
    assert len(file_ids) == len(re.findall(r'^--- !u!', text, re.M)), 'Duplicate scene file IDs'
    for reference in re.findall(r'\{fileID: (\d+)\}', text):
        assert reference == '0' or reference in file_ids, 'Broken local reference: ' + reference
    guids = {re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8'), re.M).group(1)
             for path in (ROOT / 'Assets').rglob('*.meta')}
    for reference in re.findall(r'guid: ([a-f0-9]{32})', text):
        assert reference.startswith('0000000000000000') or reference in guids, 'Missing asset GUID'
    print('Asset hierarchy, scene references and facility counts: PASS')


def validate_routes(data):
    world = {}
    obstacles = []
    for item in data['nodes']:
        transform = (world[item['parent']] if item['parent'] else np.eye(4)) @ matrix(item, True)
        world[item['id']] = transform
        if not item['collision']:
            continue
        size = np.array([1, 2, 1] if item['kind'] == 'cylinder' else [1, 1, 1])
        columns = transform[:3, :3]
        scale = np.linalg.norm(columns, axis=0)
        rotation = columns / scale
        half = size * scale / 2
        extent = np.abs(rotation) @ half
        obstacles.append((item['name'], transform[:3, 3], rotation, half, extent))
    names = [record[0] for record in obstacles]
    centers = np.array([record[1] for record in obstacles])
    rotations = np.array([record[2] for record in obstacles])
    halves = np.array([record[3] for record in obstacles])
    extents = np.array([record[4] for record in obstacles])
    minimum, maximum = centers - extents, centers + extents
    inverse = rotations.transpose(0, 2, 1)
    direction = np.einsum('nij,j->ni', inverse, np.array([0, -1, 0]))
    errors = []
    counts = {}

    def check(point, route_name):
        origin = point + [0, .4, 0]
        local = np.einsum('nij,nj->ni', inverse, origin - centers)
        parallel = np.abs(direction) < 1e-9
        invalid = np.any(parallel & (np.abs(local) > halves + 1e-6), axis=1)
        divisor = np.where(parallel, 1, direction)
        a, b = (-halves - local) / divisor, (halves - local) / divisor
        near = np.max(np.where(parallel, -np.inf, np.minimum(a, b)), axis=1)
        far = np.min(np.where(parallel, np.inf, np.maximum(a, b)), axis=1)
        valid = ~invalid & (far >= np.maximum(near, 0)) & (near >= 0) & (near <= .85)
        distances = np.where(valid, near, np.inf)
        hit = float(distances.min())
        if not math.isfinite(hit) or abs(.4 - hit) > .32:
            errors.append(f'{route_name}: no floor support at {np.round(point, 2).tolist()}')
            return
        low = point + [-.27, .03, -.27]
        high = point + [.27, 1.77, .27]
        candidates = np.flatnonzero(np.all(maximum >= low, axis=1) & np.all(minimum <= high, axis=1)
                                    & (maximum[:, 1] > point[1] + .32))
        for index in candidates:
            # Sample the central segment of a 1.74 m capsule; low step faces are traversable.
            samples = np.array([point + [0, height, 0] for height in np.linspace(.3, 1.5, 13)])
            local_samples = (samples - centers[index]) @ rotations[index]
            distances = np.maximum(np.abs(local_samples) - halves[index], 0)
            if np.min(np.sum(distances * distances, axis=1)) < .265 ** 2:
                errors.append(f'{route_name}: blocked by {names[index]} at {np.round(point, 2).tolist()}')

    for route in data['routes']:
        count = 0
        for start, end in zip(route['points'], route['points'][1:]):
            start, end = np.array(start['position']), np.array(end['position'])
            samples = max(1, math.ceil(np.linalg.norm(end - start) / .2))
            for fraction in np.linspace(0, 1, samples + 1):
                check(start * (1 - fraction) + end * fraction, route['name'])
                count += 1
                if len(errors) >= 12:
                    break
            if len(errors) >= 12:
                break
        counts[route['name']] = count
        if len(errors) >= 12:
            break
    for error in errors:
        print(error)
    for name, count in counts.items():
        print(f'Route {name}: {count} samples; ' + ('PASS' if not errors else 'FAIL'))
    return counts, errors


def main():
    data = json.loads((ROOT / 'Assets/Environment/TeachingBuildingLayout.json').read_text(encoding='utf-8'))
    compiled = compile_scripts()
    validate_assets(data)
    counts, errors = validate_routes(data)
    report = ['Verification mode: offline; no Unity Editor execution',
              f'Scene objects: {len(data["nodes"])}', 'Floors: 4; lift panels: 4; fire cabinets: 8; alarm points: 8',
              'Scene hierarchy and asset references: PASS', f'C# compile against Unity 2022.3 DLLs: {"PASS" if compiled else "FAIL"}',
              'Route test: ground support and sampled capsule clearance; doors temporarily considered open',
              *[f'Route {name}: {count} samples' for name, count in counts.items()],
              'Offline geometry: ' + ('PASS' if not errors else 'FAIL'), *errors,
              'Unity execution: outside offline checks; see UnityValidation.txt and WorldTextValidation.txt',
              'VR headset performance and locomotion: NOT RUN']
    (ROOT / 'Docs/Previews/PrototypeValidation.txt').write_text('\n'.join(report) + '\n', encoding='utf-8')
    if not compiled or errors:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
