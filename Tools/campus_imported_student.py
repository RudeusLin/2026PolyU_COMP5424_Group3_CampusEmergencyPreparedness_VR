"""Use the committed Rocketbox FBX in Unity and its posed meshes in the viewer."""
import json
from pathlib import Path

import generate_teaching_building as base

ROOT = Path(__file__).resolve().parents[1]
FOLDER = 'Assets/ThirdParty/Rocketbox/MaleAdult08'


def apply_imported_student(data):
    if data.get('studentModelRevision') == 1:
        return
    manifest = json.loads((ROOT/FOLDER/'Manifest.json').read_text(encoding='utf-8'))
    nodes = data['nodes']
    base.NODES = nodes
    avatar = next(n for n in nodes if n.get('component')=='studentAvatar')
    body = next(n for n in nodes if n['parent']==avatar['id'] and n['name']=='BodyRoot')
    body['active'] = False
    avatar['model'] = manifest['model']
    visual = base.node('ImportedStudentPreview',avatar['id'],floor=4)
    texture_prefix = FOLDER+'/m014_'
    for primitive in manifest['primitives']:
        name = primitive['name']
        prefix = texture_prefix+('opacity' if name=='Hair' else name.lower())
        data['materials'].append(dict(name=primitive['material'],color=[1,1,1,1],metallic=0,
                                      smoothness=.07 if name=='Hair' else .23,
                                      texture=prefix+'_color.png',uvMapped=True,
                                      cutout=.35 if name=='Hair' else 0))
        base.node('ImportedStudent'+name,visual,kind='sphere',material=primitive['material'],
                  mesh=primitive['mesh'],floor=4,bevel=0)
    data['studentModelRevision'] = 1


def upgrade_imported_student_preview(template):
    template = template.replace('function addGeometry(name,vertices){','function addGeometry(name,vertices,uvs=null){')
    template = template.replace('const instances=gl.createBuffer();',
        '''if(uvs){const uvBuffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,uvBuffer);
        gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(uvs),gl.STATIC_DRAW);
        gl.enableVertexAttribArray(8);gl.vertexAttribPointer(8,2,gl.FLOAT,false,8,0);}
        const instances=gl.createBuffer();''',1)
    template = template.replace('addGeometry(name,vertices);}',
        'const uvs=mesh.uv?mesh.triangles.flatMap(i=>mesh.uv.slice(i*2,i*2+2)):null;addGeometry(name,vertices,uvs);}',1)
    template = template.replace('layout(location=0)in vec3 p;layout(location=1)in vec3 n;',
        'layout(location=8)in vec2 sourceUV;out vec2 meshUV;layout(location=0)in vec3 p;layout(location=1)in vec3 n;',1)
    template = template.replace('layer=int(material);}', 'layer=int(material);meshUV=sourceUV;}',1)
    template = template.replace('in vec3 normal; in vec4 tint; in vec3 worldPos; flat in int layer;',
        'in vec2 meshUV;in vec3 normal; in vec4 tint; in vec3 worldPos; flat in int layer;',1)
    template = template.replace('uniform float tileMeters[64];',
        'uniform float tileMeters[64];uniform float uvMapped[64];uniform float alphaCut[64];',1)
    template = template.replace('vec3 base=pow(max(tint.rgb*tex,vec3(0.)),vec3(2.2));',
        '''if(uvMapped[layer]>.5){vec4 mapped=texture(surfaces,vec3(meshUV.x,1.-meshUV.y,float(layer)));
        if(mapped.a<alphaCut[layer])discard;tex=mapped.rgb;}
        vec3 base=pow(max(tint.rgb*tex,vec3(0.)),vec3(2.2));''',1)
    template = template.replace("gl.uniform1fv(gl.getUniformLocation(program,'metal'),",
        "gl.uniform1fv(gl.getUniformLocation(program,'uvMapped'),new Float32Array(data.materials.map(m=>m.uvMapped?1:0)));"
        "gl.uniform1fv(gl.getUniformLocation(program,'alphaCut'),new Float32Array(data.materials.map(m=>m.cutout||0)));"
        "gl.uniform1fv(gl.getUniformLocation(program,'metal'),",1)
    template = template.replace('if(!instances.length)continue;',
        "if(!instances.length||k.endsWith('/HairPreview.json'))continue;",1)
    template = template.replace('学生角色 · 自然比例、曲面轮廓与织物细节',
        '学生角色 · Rocketbox 人物')
    return template


def main():
    path = ROOT/'Assets/Environment/TeachingBuildingCampusLayout.json'
    data = json.loads(path.read_text(encoding='utf-8'))
    apply_imported_student(data)
    path.write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    from upgrade_campus_scene import preview_html
    preview_html(data)
    base.ensure_metadata()
    print('Rocketbox character selected; Unity scene application uses Update campus student appearance.')


if __name__=='__main__':
    main()
