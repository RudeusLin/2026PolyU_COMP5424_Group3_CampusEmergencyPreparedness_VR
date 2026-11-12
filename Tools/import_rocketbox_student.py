"""Convert the pinned MIT Rocketbox character into preview meshes and Unity textures.

Requires ufbx 0.0.5 only when reimporting the original FBX. Normal campus
generation uses the committed converted meshes and needs no FBX Python module.
"""
import argparse
import gc
import hashlib
import json
from pathlib import Path
import shutil
import sys

import numpy as np
from PIL import Image
from scipy.spatial.transform import Rotation

import generate_teaching_building as base

ROOT = Path(__file__).resolve().parents[1]
DESTINATION = 'Assets/ThirdParty/Rocketbox/MaleAdult08'
COMMIT = '0943055db6ec570bcef9f2c8b41c9e5467c808f9'


def matrix(value):
    result = np.eye(4)
    result[:3,:] = np.array(value).T
    return result


def convert(source, ufbx_path=None):
    if ufbx_path:
        sys.path.insert(0,str(ufbx_path))
    import ufbx
    # Keep the native FBX wrapper graph alive throughout conversion.
    gc.disable()
    scene = ufbx.load_file(str(source/'Male_Adult_08.fbx'),target_axes=ufbx.axes_left_handed_y_up,
                           target_unit_meters=1.0,evaluate_skinning=True)
    nodes,meshes = list(scene.nodes),list(scene.meshes)
    mesh = meshes[0]
    skins = list(mesh.skin_deformers)
    skin = skins[0]
    clusters = list(skin.clusters)
    bones = [c.bone_node for c in clusters]
    by_name = {n.name:n for n in nodes}
    pose = {n.element_id:matrix(n.node_to_world) for n in nodes}
    # Relax the imported A pose, matching the editor's shoulder adjustment.
    for side,sign in [('L',1),('R',-1)]:
        shoulder = by_name['Bip01 '+side+' UpperArm']
        elbow = by_name['Bip01 '+side+' Forearm']
        original = pose[elbow.element_id][:3,3]-pose[shoulder.element_id][:3,3]
        target = np.array([sign*.09,-1,-.07])
        original /= np.linalg.norm(original);target /= np.linalg.norm(target)
        axis = np.cross(original,target);axis /= np.linalg.norm(axis)
        delta = np.eye(4)
        delta[:3,:3] = Rotation.from_rotvec(axis*np.arccos(np.clip(np.dot(original,target),-1,1))).as_matrix()
        pivot = pose[shoulder.element_id][:3,3]
        delta[:3,3] = pivot-delta[:3,:3]@pivot
        descendants = {shoulder.element_id}
        for node in nodes:
            if node.parent and node.parent.element_id in descendants:descendants.add(node.element_id)
            if node.element_id in descendants:pose[node.element_id] = delta@pose[node.element_id]
    transforms = [pose[b.element_id]@matrix(c.geometry_to_bone) for b,c in zip(bones,clusters)]
    vertices = np.array(list(mesh.vertices))
    blended = []
    weight_errors = []
    for vertex in skin.vertices:
        weights = [skin.weights[i] for i in range(vertex.weight_begin,vertex.weight_begin+vertex.num_weights)]
        weight_errors.append(abs(sum(w.weight for w in weights)-1))
        blended.append(sum((transforms[w.cluster_index]*w.weight for w in weights),np.zeros((4,4))))
    blended = np.array(blended)
    positions = np.einsum('nij,nj->ni',blended,np.column_stack([vertices,np.ones(len(vertices))]))[:,:3]
    # The source faces -Z. Both deliverables face +Z and stand 1.74 m tall.
    facing = np.diag([-1.,1.,-1.])
    positions = positions@facing
    scale = 1.74/np.ptp(positions[:,1])
    positions *= scale
    positions[:,1] -= positions[:,1].min()
    normal_matrices = np.linalg.inv(blended[:,:3,:3]).transpose(0,2,1)
    destination = ROOT/DESTINATION
    destination.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(source/'Male_Adult_08.fbx',destination/'Male_Adult_08.fbx')
    source_hashes = {}
    for path in sorted(source.iterdir()):
        if path.suffix not in ('.tga','.fbx'):continue
        source_hashes[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
        if path.suffix == '.tga':
            with Image.open(path) as image:
                image = image.convert('RGBA' if 'opacity' in path.name else 'RGB')
                image.resize((1024,1024),Image.Resampling.LANCZOS).save(destination/(path.stem+'.png'))
    primitive_specs = []
    for material_index,name in enumerate(['Body','Head','Hair']):
        output = dict(vertices=[],normals=[],uv=[],triangles=[])
        unique = {}
        for face_index,face in enumerate(mesh.faces):
            if mesh.face_material[face_index] != material_index:continue
            assert face.num_indices == 3
            indices = []
            for corner in range(face.index_begin,face.index_begin+3):
                vertex_index = mesh.vertex_indices[corner]
                normal_index,uv_index = mesh.vertex_normal.indices[corner],mesh.vertex_uv.indices[corner]
                key = vertex_index,normal_index,uv_index
                if key not in unique:
                    normal = normal_matrices[vertex_index]@np.array(mesh.vertex_normal.values[normal_index])
                    normal = normal@facing;normal /= np.linalg.norm(normal)
                    unique[key] = len(unique)
                    output['vertices'].extend(np.round(positions[vertex_index],7).tolist())
                    output['normals'].extend(np.round(normal,7).tolist())
                    output['uv'].extend(np.round(mesh.vertex_uv.values[uv_index],7).tolist())
                indices.append(unique[key])
            # Match the derived viewer winding to the authored surface normals.
            p = np.array([output['vertices'][i*3:i*3+3] for i in indices])
            n = np.array([output['normals'][i*3:i*3+3] for i in indices]).mean(axis=0)
            if np.dot(np.cross(p[1]-p[0],p[2]-p[0]),n) < 0:
                indices[1],indices[2] = indices[2],indices[1]
            output['triangles'].extend(indices)
        filename = name+'Preview.json'
        (destination/filename).write_text(json.dumps(output,separators=(',',':')),encoding='utf-8')
        primitive_specs.append(dict(name=name,mesh=DESTINATION+'/'+filename,
                                    material='StudentImported'+name,triangles=len(output['triangles'])//3))
    manifest = dict(source='https://github.com/microsoft/Microsoft-Rocketbox',commit=COMMIT,
                    sourcePath='Assets/Avatars/Adults/Male_Adult_08',license='MIT',
                    model=DESTINATION+'/Male_Adult_08.fbx',heightMeters=1.74,primitives=primitive_specs,
                    sourceSha256=source_hashes,boneCount=len(clusters),maxSkinWeightError=max(weight_errors),
                    bones=[b.name for b in bones],textureSize=1024)
    (destination/'Manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    for path in destination.iterdir():
        if path.suffix in ('.png','.json','.fbx'):
            base.asset_meta(path.relative_to(ROOT).as_posix(),
                            'TextureImporter' if path.suffix=='.png' else 'ModelImporter' if path.suffix=='.fbx' else 'DefaultImporter')
            if path.suffix == '.png':
                metadata = Path(str(path)+'.meta')
                text = metadata.read_text(encoding='utf-8')
                if 'textureShape:' not in text:
                    metadata.write_text(text+'  textureShape: 1\n',encoding='utf-8')
    base.ensure_metadata()
    print(json.dumps({k:manifest[k] for k in ['boneCount','maxSkinWeightError','primitives']},indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',required=True,type=Path)
    parser.add_argument('--ufbx-path',type=Path)
    args = parser.parse_args()
    convert(args.source,args.ufbx_path)
