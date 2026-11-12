"""Author smooth student surfaces for the existing articulated rig and preview."""
import json
import math
from pathlib import Path

import numpy as np

import generate_teaching_building as base

ROOT = Path(__file__).resolve().parents[1]
SOURCES = "Assets/Environment/TeachingBuildingCampus/SourceMeshes/Student"
REVISION = 2


def surface(vertices, triangles):
    vertices = np.asarray(vertices, dtype=float)
    triangles = np.asarray(triangles, dtype=int).reshape(-1, 3)
    normals = np.zeros_like(vertices)
    for a, b, c in triangles:
        normal = np.cross(vertices[b] - vertices[a], vertices[c] - vertices[a])
        for index in (a, b, c):
            normals[index] += normal
    lengths = np.linalg.norm(normals, axis=1)
    assert np.all(lengths > 1e-12), "Degenerate student surface"
    normals /= lengths[:, None]
    return dict(vertices=vertices.round(7).flatten().tolist(),
                normals=normals.round(7).flatten().tolist(), triangles=triangles.flatten().tolist())


def loft(rings, sides=24, subdivisions=2, deform=None):
    """Closed elliptical sections; ring entries are y, half-width, half-depth, z."""
    rings = np.asarray(rings, dtype=float)
    samples = []
    for index in range(len(rings) - 1):
        a, b = rings[index], rings[index + 1]
        p, q = rings[max(0, index - 1)], rings[min(len(rings) - 1, index + 2)]
        for step in range(subdivisions):
            t = step / subdivisions
            value = .5 * ((2 * a) + (-p + b) * t + (2*p - 5*a + 4*b - q)*t*t
                          + (-p + 3*a - 3*b + q)*t*t*t)
            value[1:3] = np.maximum(value[1:3], .0005)
            samples.append(value)
    samples.append(rings[-1])
    vertices, triangles = [], []
    for y, width, depth, center in samples:
        for side in range(sides):
            angle = side * math.tau / sides
            point = [width * math.sin(angle), y, center + depth * math.cos(angle)]
            vertices.append(deform(point, angle) if deform else point)
    for row in range(len(samples) - 1):
        for side in range(sides):
            a, b = row * sides + side, row * sides + (side + 1) % sides
            triangles.extend([a, b, b + sides, a, b + sides, a + sides])
    for row, bottom in [(0, True), (len(samples) - 1, False)]:
        center = len(vertices)
        vertices.append([0, samples[row][0], samples[row][3]])
        for side in range(sides):
            a, b = row * sides + side, row * sides + (side + 1) % sides
            triangles.extend([center, b, a] if bottom else [center, a, b])
    return surface(vertices, triangles)


def merge(parts):
    vertices, triangles = [], []
    for part in parts:
        offset = len(vertices) // 3
        vertices.extend(part['vertices'])
        triangles.extend(i + offset for i in part['triangles'])
    return surface(np.array(vertices).reshape(-1, 3), triangles)


def translated(mesh, offset):
    vertices = np.array(mesh['vertices']).reshape(-1, 3) + offset
    return surface(vertices, mesh['triangles'])


def curve_strip(points, radius=.0015, sides=8, aspect=1):
    """A narrow curved seam, lip edge or brow, with smoothly varying thickness."""
    vertices, triangles = [], []
    points = np.asarray(points,dtype=float)
    frame = None
    for index, point in enumerate(points):
        tangent = points[min(index+1,len(points)-1)] - points[max(index-1,0)]
        tangent /= np.linalg.norm(tangent)
        if frame is None:
            reference = np.array([0.,0.,1.]) if abs(tangent[2]) < .9 else np.array([0.,1.,0.])
            frame = np.cross(reference,tangent)
        frame -= tangent*np.dot(frame,tangent)
        frame /= np.linalg.norm(frame)
        binormal = np.cross(tangent,frame)
        thickness = radius * (.45 + .55 * math.sin(math.pi * (index + .5) / len(points)))
        for side in range(sides):
            angle = math.tau * side / sides
            vertices.append(point + thickness * (frame*math.cos(angle) + binormal*math.sin(angle)*aspect))
    for row in range(len(points) - 1):
        for side in range(sides):
            a, b = row*sides + side, row*sides + (side+1) % sides
            triangles.extend([a, b+sides, a+sides, a, b, b+sides])
    return surface(vertices, triangles)


def author_surfaces():
    def face_detail(point, angle):
        x, y, z = point
        front = max(math.cos(angle), 0) ** 6
        cheeks = math.exp(-((abs(x)-.047)/.021)**2 - ((y+.024)/.027)**2)
        sockets = math.exp(-((abs(x)-.032)/.017)**2 - ((y-.028)/.012)**2)
        return [x, y, z + front * (.004*cheeks - .003*sockets)]

    head_rings = [(-.114,.025,.032,.010),(-.104,.044,.048,.006),(-.086,.061,.064,.002),
                  (-.055,.071,.077,0),(-.020,.077,.084,-.001),(.025,.077,.085,-.002),
                  (.060,.077,.087,-.004),(.092,.069,.080,-.006),(.116,.046,.057,-.007),
                  (.127,.012,.018,-.007)]
    meshes = {'Head': loft(head_rings, 40, 3, face_detail)}
    meshes['Nose'] = loft([(-.029,.008,.006,.093),(-.022,.014,.009,.101),
                           (-.010,.013,.015,.105),(.011,.008,.010,.093),
                           (.040,.005,.004,.082)], 20, 3)
    # A lifted, swept-back cap exposes the forehead and tapers at the nape.
    vertices, triangles = [], []
    for row in range(19):
        fraction = row / 18
        for side in range(48):
            angle = side * math.tau / 48
            front = max(math.cos(angle), 0)
            back = max(-math.cos(angle), 0)
            low = .030 + .057 * front**1.5 - .072 * back**.6
            scalp_y = .127 * (1-fraction) + low * fraction
            y = scalp_y + .032*(1-fraction)**.8*(.75+.25*front)
            width = np.interp(scalp_y, [r[0] for r in head_rings], [r[1] for r in head_rings])
            depth = np.interp(scalp_y, [r[0] for r in head_rings], [r[2] for r in head_rings])
            bulge = .005 + .002 * math.sin(angle*8 + fraction*3) ** 2
            center = np.interp(scalp_y, [r[0] for r in head_rings], [r[3] for r in head_rings])
            center -= .018*(1-fraction)
            vertices.append([(width+bulge)*math.sin(angle),
                             y, center + (depth+bulge)*math.cos(angle)])
    # Keep a tiny crown ring instead of collapsed pole triangles.
    for side in range(48):
        vertices[side][0] = .001 * math.sin(side*math.tau/48)
        vertices[side][1] = .157
        vertices[side][2] = -.025 + .001 * math.cos(side*math.tau/48)
    for row in range(18):
        for side in range(48):
            a, b = row*48+side, row*48+(side+1)%48
            triangles.extend([a,b+48,b,a,a+48,b+48])
    meshes['Hair'] = surface(vertices, triangles)
    # Fine raised strands run from the front hairline towards the back.
    faces = np.array(vertices)[np.array(triangles).reshape(-1,3)]
    a,b,c = faces[:,0],faces[:,1],faces[:,2]
    u,v = b[:,[0,2]]-a[:,[0,2]],c[:,[0,2]]-a[:,[0,2]]
    denominator = u[:,0]*v[:,1]-u[:,1]*v[:,0]
    valid = abs(denominator)>1e-12
    strands = []
    for x in np.linspace(-.060,.060,9):
        points = []
        for z in np.linspace(.070,-.078,29):
            d = np.array([x,z])-a[:,[0,2]]
            s = np.divide(d[:,0]*v[:,1]-d[:,1]*v[:,0],denominator,
                          out=np.zeros_like(denominator),where=valid)
            t = np.divide(u[:,0]*d[:,1]-u[:,1]*d[:,0],denominator,
                          out=np.zeros_like(denominator),where=valid)
            inside = valid & (s>=0) & (t>=0) & (s+t<=1)
            if inside.any():
                height = (a[:,1]+s*(b[:,1]-a[:,1])+t*(c[:,1]-a[:,1]))[inside].max()
                points.append([x,height+.0006,z])
        if len(points)>2: strands.append(curve_strip(points,.0007,6))
    meshes['HairFlow'] = merge(strands)
    torso_rings = [(-.25,.148,.095,0),(-.185,.154,.101,0),(-.08,.169,.111,0),
                            (.04,.183,.119,-.002),(.12,.204,.112,-.008),(.175,.198,.096,-.011),
                            (.205,.128,.077,-.006),(.216,.060,.065,0)]
    meshes['Torso'] = loft(torso_rings, 32, 3)
    # The shirt follows the jacket surface rather than floating as a flat plaque.
    vertices, triangles = [], []
    shirt_edges = []
    for row in range(25):
        y = .050 + row / 24 * .155
        half_width = np.interp(y, [.050,.17,.205], [.002,.042,.034])
        width, depth, center = [np.interp(y, [r[0] for r in torso_rings],
                                        [r[column] for r in torso_rings]) for column in (1,2,3)]
        for col in range(9):
            x = half_width * (col / 4 - 1)
            vertices.append([x,y,center+depth*math.sqrt(1-(x/width)**2)+.007])
        shirt_edges.append([half_width+.004,y,center+depth*math.sqrt(1-(half_width/width)**2)+.009])
    for row in range(24):
        for col in range(8):
            a = row*9+col
            triangles.extend([a,a+1,a+10,a,a+10,a+9])
    meshes['ShirtFront'] = surface(vertices,triangles)
    for side,prefix in [(-1,'Left'),(1,'Right')]:
        meshes[prefix+'Collar'] = curve_strip([[side*x,y,z] for x,y,z in shirt_edges],.006)
    zipper = [[0,y,np.interp(y,[r[0] for r in torso_rings],[r[2]+r[3] for r in torso_rings])+.006]
              for y in np.linspace(-.24,.05,32)]
    meshes['Zipper'] = curve_strip(zipper,.0013)
    for side,prefix in [(-1,'Left'),(1,'Right')]:
        strap = []
        for y in np.linspace(-.11,.17,24):
            width,depth,center = [np.interp(y,[r[0] for r in torso_rings],
                                          [r[column] for r in torso_rings]) for column in (1,2,3)]
            strap.append([side*.137,y,center+depth*math.sqrt(1-(.137/width)**2)+.010])
        strap += [[side*.137,y,z] for y,z in [(.185,.058),(.195,.030),(.198,-.010),
                                              (.195,-.050),(.180,-.105),(.140,-.135)]]
        meshes[prefix+'Strap'] = curve_strip(strap,.013,12,aspect=.35)
    meshes['Hips'] = loft([(-.055,.116,.074,0),(-.018,.144,.085,-.004),
                           (.045,.150,.088,0),(.09,.144,.086,0)], 28, 3)
    meshes['UpperSleeve'] = loft([(-.282,.045,.050,.004),(-.23,.051,.058,.004),
                                  (-.13,.061,.066,0),(-.05,.066,.070,-.004),
                                  (.015,.060,.066,-.004),(.04,.027,.035,0)], 24, 3)
    meshes['LowerSleeve'] = loft([(-.226,.039,.043,.018),(-.18,.042,.048,.014),
                                  (-.095,.051,.054,.007),(-.015,.049,.053,.002),
                                  (.025,.043,.049,0)], 24, 3)
    meshes['Thigh'] = loft([(-.37,.054,.059,.003),(-.30,.063,.070,0),
                            (-.19,.077,.084,-.005),(-.07,.081,.089,-.007),
                            (.025,.074,.077,-.005)], 24, 3)
    meshes['Calf'] = loft([(-.411,.039,.044,0),(-.355,.043,.049,-.004),
                           (-.235,.058,.067,-.009),(-.14,.061,.070,-.008),
                           (-.04,.054,.059,0),(.035,.052,.058,.005)], 24, 3)
    meshes['Trainer'] = loft([(-.048,.052,.112,.025),(-.032,.060,.126,.020),
                              (.002,.058,.124,.020),(.029,.050,.109,.008),
                              (.045,.039,.058,-.030),(.057,.035,.042,-.045)], 28, 3)
    meshes['Palm'] = loft([(-.36,.33,.24,.04),(-.20,.46,.31,.015),
                           (.10,.47,.33,0),(.34,.35,.27,-.015)], 20, 3)
    for side, prefix in [(-1,'Left'),(1,'Right')]:
        finger_parts = []
        for index, length in enumerate([.056,.066,.061,.048]):
            x = (index - 1.5) * .017
            finger_parts.append(translated(loft([
                (-length,.0045,.005,.020),(-length+.009,.006,.006,.022),
                (-length*.52,.007,.007,.012),(-.010,.0075,.008,.003),
                (0,.007,.007,0)], 12, 2), [x,-.292,.025]))
        meshes[prefix+'Fingers'] = merge(finger_parts)
        meshes[prefix+'Thumb'] = loft([(-.326,.006,.007,.052),(-.306,.010,.011,.047),
                                       (-.279,.012,.013,.032),(-.254,.012,.013,.025)], 16, 3,
                                      lambda p,a,s=side:[p[0]-s*(.028+(-p[1]-.254)*.25),p[1],p[2]])
        # Ears have a flatter attachment, rolled rim and a recessed inner bowl.
        vertices, triangles = [], []
        for row in range(4):
            for index in range(24):
                a = index*math.tau/24
                radius = [1,.83,.58,.12][row]
                vertices.append([side*(.079+[0,.008,.006,-.001][row]),
                                 .006+.025*math.cos(a)*radius,
                                 -.001+.015*math.sin(a)*radius])
        for row in range(3):
            for index in range(24):
                a,b=row*24+index,row*24+(index+1)%24
                triangles.extend([a,b,b+24,a,b+24,a+24] if side>0 else [a,b+24,b,a,a+24,b+24])
        meshes[prefix+'Ear'] = surface(vertices, triangles)
        brow = [[side*.032+x,.050+.004*(1-(x/.019)**2),.081-.012*abs(x/.019)]
                for x in np.linspace(-.019,.019,15)]
        meshes[prefix+'Brow'] = curve_strip(brow,.0018)
        lid = [[side*.032+x,.027+.0055*math.sin(math.pi*(x+.014)/.028),
                .081+.004*math.sin(math.pi*(x+.014)/.028)] for x in np.linspace(-.014,.014,19)]
        meshes[prefix+'Lid'] = curve_strip(lid,.0012)
    for name, y, width, radius in [('UpperLip',-.048,.024,.0021),('LowerLip',-.052,.023,.0026),
                                   ('LipLine',-.050,.023,.00065)]:
        points = [[x,y+.0018*math.cos(x/width*math.pi),.078+.006*(1-(x/width)**2)]
                  for x in np.linspace(-width,width,23)]
        meshes[name] = curve_strip(points,radius)
    return meshes


def apply_student_realism(data):
    # Refresh authored resources even when the layout revision is already current.
    meshes = author_surfaces()
    folder = ROOT / SOURCES
    folder.mkdir(parents=True, exist_ok=True)
    for name, mesh in meshes.items():
        path = f'{SOURCES}/{name}.json'
        (ROOT / path).write_text(json.dumps(mesh,separators=(',',':')),encoding='utf-8')
        base.asset_meta(path)
    if data.get('studentAppearanceRevision') == REVISION:
        return
    nodes = data['nodes']
    base.NODES = nodes
    by_id = {n['id']: n for n in nodes}
    avatar = next(n for n in nodes if n.get('component') == 'studentAvatar')
    descendants = {avatar['id']}
    for item in nodes:
        if item['parent'] in descendants:
            descendants.add(item['id'])
    parts = [n for n in nodes if n['id'] in descendants]
    head = next(n for n in parts if n['name'] == 'Head')
    head['position'][1] = 1.60
    # A small palette keeps skin, hair, lips and cloth distinct without per-part materials.
    additions = dict(Lips=[.43,.235,.19,1], Crease=[.235,.12,.085,1], Iris=[.075,.045,.025,1])
    for name,color in additions.items():
        if not any(m['name']=='Student'+name for m in data['materials']):
            data['materials'].append(dict(name='Student'+name,color=color,metallic=0,smoothness=.25))
    for material in data['materials']:
        name = material['name']
        if name in ('StudentJacket','StudentTrousers','StudentBag'):
            material.update(texture='Assets/Environment/TeachingBuildingCampus/Textures/Fabric.png',
                            detailTexture='Assets/Environment/TeachingBuildingCampus/Textures/FabricDetail.png',
                            tileMeters=.22,bumpMeters=.00012,smoothness=.10)
        if name == 'StudentSkin': material.update(color=[.87,.72,.59,1],smoothness=.25)
        if name == 'StudentLips': material['color'] = [.58,.34,.29,1]
        if name == 'StudentCrease': material['color'] = [.34,.20,.16,1]
        if name == 'StudentEyes': material['smoothness'] = .60
        if name == 'StudentHair': material['smoothness'] = .07
        if name == 'StudentSoles': material['color'] = [.50,.51,.48,1]

    def authored(item, mesh, position=None, scale=None):
        item.update(mesh=f'{SOURCES}/{mesh}.json',bevel=0,
                    position=position if position is not None else [0,0,0],
                    scale=scale if scale is not None else [1,1,1])

    hidden = {'HairCrown','Sideburn','Sideburn_2','JacketHem','JacketSeam_2',
              'Shoulder','Elbow','Knee','ToeCap','ToeCap_2','StrapShoulder','StrapShoulder_2',
              'Backpack','BackpackPocket','BackpackStrap','BackpackStrap_2','BagZip','BagHandle'}
    ears, brows, eyes, irises, collars, straps = 0,0,0,0,0,0
    for item in parts:
        name = item['name']
        parent = by_id.get(item['parent'],{})
        if name in hidden: item['active'] = False
        if name == 'JacketTorso': authored(item,'Torso',[0,1.205,0])
        elif name == 'Hips': authored(item,'Hips',[0,.925,0])
        elif name == 'Face': authored(item,'Head')
        elif name == 'Nose': authored(item,'Nose')
        elif name == 'HairBack': authored(item,'Hair')
        elif name == 'HairSweep':
            authored(item,'HairFlow'); item['active'] = True
        elif name.startswith('Ear'):
            authored(item,('Left' if ears == 0 else 'Right')+'Ear'); ears += 1
        elif name.startswith('Eyebrow'):
            authored(item,('Left' if brows == 0 else 'Right')+'Brow'); brows += 1
        elif name.startswith('Eye'):
            side = -1 if eyes == 0 else 1
            item.update(position=[side*.032,.028,.0805],scale=[.026,.009,.009]); eyes += 1
        elif name.startswith('Iris'):
            side = -1 if irises == 0 else 1
            item.update(position=[side*.032,.028,.085],scale=[.008,.008,.003],material='StudentIris'); irises += 1
        elif name == 'Mouth':
            authored(item,'UpperLip'); item['material'] = 'StudentLips'
        elif name == 'Neck': item['scale'] = [.086,.050,.086]
        elif name == 'ShirtFront':
            authored(item,'ShirtFront',[0,1.205,0])
        elif name.startswith('Collar'):
            authored(item,('Left' if collars==0 else 'Right')+'Collar',[0,1.205,0])
            collars += 1
        elif name == 'JacketSeam':
            authored(item,'Zipper',[0,1.205,0]); item['material'] = 'StudentHair'
        elif name.startswith('Pocket'):
            item['position'][2] = .081
        elif name in ('LeftArm','RightArm'):
            item['position'][0] = (-1 if name=='LeftArm' else 1)*.201
        elif name == 'Sleeve': authored(item,'UpperSleeve')
        elif name == 'LowerSleeve': authored(item,'LowerSleeve')
        elif name == 'Thigh': authored(item,'Thigh')
        elif name == 'Calf': authored(item,'Calf')
        elif name == 'Hand': authored(item,'Palm',[0,-.27,.022],[.075,.1,.06])
        elif name == 'Thumb': authored(item,('Left' if parent['name']=='LeftForearm' else 'Right')+'Thumb')
        elif name == 'TrouserCuff': item['scale'] = [.084,.032,.091]
        elif name == 'Cuff': item.update(scale=[.086,.022,.095],material='StudentJacket')
        elif name == 'Trainer':
            authored(item,'Trainer',item['position'])
        elif name == 'Sole': item.update(scale=[.124,.025,.259],bevel=.012)
        elif name == 'Backpack': item.update(scale=[.277,.365,.136],bevel=.06)
        elif name == 'BackpackPocket': item.update(scale=[.226,.151,.035],bevel=.027)
        elif name.startswith('BackpackStrap'):
            authored(item,('Left' if straps==0 else 'Right')+'Strap',[0,1.205,0]); straps += 1
        elif name == 'BagHandle': item.update(position=[0,1.378,-.196],scale=[.08,.02,.03])
        elif name == 'BagZip': item.update(position=[0,1.174,-.296],scale=[.19,.004,.006])

    def extra(name,parent,mesh,material):
        existing = next((n for n in parts if n['parent']==parent and n['name']==name),None)
        if existing is not None:
            authored(existing,mesh); existing['material'] = 'Student'+material
            return existing['id']
        return base.node(name,parent,kind='sphere',material='Student'+material,
                         mesh=f'{SOURCES}/{mesh}.json',floor=4,bevel=0)
    for name in ['LowerLip','LipLine']:
        extra(name,head['id'],name,'Lips' if name=='LowerLip' else 'Crease')
    for prefix in ['Left','Right']:
        extra(prefix+'Eyelid',head['id'],prefix+'Lid','Skin')
        forearm = next(n for n in parts if n['name']==prefix+'Forearm')
        existing = next((n for n in parts if n['parent']==forearm['id'] and n['name']=='HandDetails'),None)
        details = existing['id'] if existing else base.node('HandDetails',forearm['id'],floor=4)
        extra('AnatomicalFingers',details,prefix+'Fingers','Skin')
        if not any(n['parent']==forearm['id'] and n['name']=='HandHoldPoint' for n in parts):
            base.node('HandHoldPoint',forearm['id'],(0,-.29,.055),floor=4,
                      rotation=[math.sqrt(.5),0,0,math.sqrt(.5)])
    # Source coordinates keep knees, elbows and towel attachment points compatible.
    data['studentAppearanceRevision'] = REVISION


def upgrade_student_preview(template):
    loader = """for(const [name,mesh] of Object.entries(data.studentMeshData||{})){
const vertices=[];for(const i of mesh.triangles)vertices.push(...mesh.vertices.slice(i*3,i*3+3),...mesh.normals.slice(i*3,i*3+3));addGeometry(name,vertices);}
"""
    anchor = 'for(let n of data.nodes){n.geometry=n.kind;'
    assert anchor in template
    template = template.replace(anchor,loader+'for(let n of data.nodes){n.geometry=n.mesh||n.kind;',1)
    template = template.replace("if(n.kind==='box'&&n.bevel)","if(!n.mesh&&n.kind==='box'&&n.bevel)")
    template = template.replace("student:{eye:[-6.15,12.1,4.5],target:[-7.8,11.75,2.7],fov:.8}",
                                "student:{eye:[-6.5,12.0,5.05],target:[-7.8,11.68,2.7],fov:.72}")
    template = template.replace('<button data-view="student">学生角色</button>',
                                '<button data-view="student">学生角色</button><button data-view="studentFace">人物近景</button>')
    template = template.replace('const views={',
                                'const views={studentFace:{eye:[-7.48,12.48,3.65],target:[-7.8,12.25,2.7],fov:.68},',1)
    template = template.replace("student:'学生角色 · Unity 中按 V 切换第三人称'",
                                "student:'学生角色 · 自然比例、曲面轮廓与织物细节',studentFace:'面部与衣着细节'")
    template = template.replace("student:'学生角色'", "student:'学生角色',studentFace:'人物近景'")
    template = template.replace('function visible(n){',
                                "function visible(n){if(['student','studentFace'].includes(mode))return !n.hidden&&!!n.studentVisual;")
    template = template.replace("key=[wide,mode==='floor'?floor:0,cutaway]",
                                "key=[wide,mode==='floor'?floor:0,cutaway,['student','studentFace'].includes(mode)]")
    template = template.replace('uniform vec3 cameraEye;', 'uniform bool studentView;uniform vec3 cameraEye;')
    template = template.replace('vec3 lit=base*(ambient+vec3(1.,.96,.89)*ndl*shadow*.9)+spec;',
                                '''vec3 lit=base*(ambient+vec3(1.,.96,.89)*ndl*shadow*.9)+spec;
    if(studentView){float key=max(dot(norm,normalize(vec3(-.6,.8,.9))),0.);
    float fill=max(dot(norm,normalize(vec3(.8,.2,.45))),0.);
    float rim=max(dot(norm,normalize(vec3(.2,.7,-.8))),0.);
    lit=base*(.30+.68*key+.23*fill)+vec3(.13,.15,.17)*rim*.28+spec*.5;}''')
    template = template.replace("gl.uniform3fv(gl.getUniformLocation(program,'cameraEye'),eye);",
                                "gl.uniform3fv(gl.getUniformLocation(program,'cameraEye'),eye);gl.uniform1i(gl.getUniformLocation(program,'studentView'),['student','studentFace'].includes(mode)?1:0);")
    template = template.replace('gl.clearColor(.80,.88,.89,1);',
                                "gl.clearColor(...(['student','studentFace'].includes(mode)?[.82,.84,.85,1]:[.80,.88,.89,1]));")
    template = template.replace('}drawSunShadows(batches,width,height,eye);',
                                "}if(['student','studentFace'].includes(mode)){const p=data.studentOrigin;batches.box.push(...compose([p[0],p[1]-.035,p[2]],[0,0,0,1],[1.8,.02,1.8]),.65,.68,.69,1,materialIndices.Paving);}drawSunShadows(batches,width,height,eye);")
    template = template.replace("['exterior','floor'].includes(mode)?18:2", "['exterior','floor'].includes(mode)?18:['student','studentFace'].includes(mode)?.4:2")
    return template


def main():
    path = ROOT / 'Assets/Environment/TeachingBuildingCampusLayout.json'
    data = json.loads(path.read_text(encoding='utf-8'))
    apply_student_realism(data)
    from campus_imported_student import apply_imported_student
    apply_imported_student(data)
    path.write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    from upgrade_campus_scene import preview_html
    preview_html(data)
    base.ensure_metadata()
    print('Student appearance updated; saved Unity scene awaits the student appearance menu command.')


if __name__ == '__main__':
    main()
