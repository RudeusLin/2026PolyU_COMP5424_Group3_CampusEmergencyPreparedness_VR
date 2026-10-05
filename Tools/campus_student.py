"""Articulated student geometry shared by Unity and the offline preview."""
import generate_teaching_building as base


def prepare_walkthrough(data):
    # Saved Refined input predates the thin treads; keep the walking surface fixed.
    for item in data['nodes']:
        if item['name'].startswith(('Step_First_', 'Step_Second_')):
            top = item['position'][1] + item['scale'][1] / 2
            item['scale'][1] = .15
            item['position'][1] = top - .075

    palette = dict(Skin=[.57, .36, .23, 1], Hair=[.045, .031, .022, 1],
                   Jacket=[.10, .22, .25, 1], Shirt=[.86, .84, .76, 1],
                   Trousers=[.11, .15, .21, 1], Shoes=[.075, .083, .087, 1],
                   Soles=[.68, .68, .64, 1], Bag=[.32, .20, .12, 1], Eyes=[.82, .79, .72, 1])
    for name, color in palette.items():
        data['materials'].append(dict(name='Student' + name, color=color, metallic=0, smoothness=.16))
    player = next(n for n in data['nodes'] if n['kind'] == 'player')
    avatar = base.node('CampusStudent', player['id'], component='studentAvatar', floor=4)
    # Offset the visible soles by the controller skin; keep its collision capsule unchanged.
    root = base.node('BodyRoot', avatar, (0, -.025, 0), floor=4)
    part_counts = {}

    def group(name, parent, position):
        return base.node(name, parent, position, floor=4)

    def shape(name, parent, position, size, material, kind='sphere', bevel=0):
        key = (parent, name)
        part_counts[key] = part_counts.get(key, 0) + 1
        if part_counts[key] > 1:
            name += '_' + str(part_counts[key])
        return base.node(name, parent, position, kind, size, 'Student' + material,
                         collision=False, bevel=bevel, floor=4)

    def rounded(name, parent, position, size, material, bevel=.025):
        return shape(name, parent, position, size, material, 'box', bevel)

    rounded('JacketTorso', root, (0, 1.205, 0), (.39, .41, .225), 'Jacket', .06)
    rounded('JacketHem', root, (0, .985, 0), (.335, .075, .225), 'Jacket', .018)
    rounded('ShirtFront', root, (0, 1.245, .114), (.145, .30, .012), 'Shirt', .005)
    for side in [-1, 1]:
        rounded('JacketSeam', root, (side * .081, 1.205, .126), (.012, .35, .009), 'Jacket', .004)
        rounded('Collar', root, (side * .063, 1.386, .095), (.073, .042, .036), 'Jacket', .013)
        rounded('Pocket', root, (side * .133, 1.095, .119), (.08, .008, .012), 'Hair', .003)
    rounded('Hips', root, (0, .925, 0), (.315, .17, .23), 'Trousers', .035)
    shape('Neck', root, (0, 1.442, 0), (.071, .048, .071), 'Skin', 'cylinder')
    head = group('Head', root, (0, 1.575, 0))
    shape('Face', head, (0, 0, 0), (.202, .259, .212), 'Skin')
    shape('HairBack', head, (0, .022, -.033), (.209, .245, .164), 'Hair')
    shape('HairCrown', head, (0, .116, -.005), (.211, .09, .209), 'Hair')
    shape('HairSweep', head, (.023, .093, .041), (.17, .073, .131), 'Hair')
    for side in [-1, 1]:
        shape('Ear', head, (side * .106, .008, 0), (.03, .051, .034), 'Skin')
        shape('Sideburn', head, (side * .093, .045, -.002), (.023, .10, .129), 'Hair')
        shape('Eye', head, (side * .039, .025, .100), (.028, .011, .012), 'Eyes')
        shape('Iris', head, (side * .039, .025, .106), (.009, .009, .006), 'Hair')
        rounded('Eyebrow', head, (side * .039, .045, .098), (.03, .007, .007), 'Hair', .002)
    shape('Nose', head, (0, -.005, .106), (.028, .043, .029), 'Skin')
    rounded('Mouth', head, (0, -.039, .099), (.034, .005, .009), 'Hair', .002)
    for side, prefix in [(-1, 'Left'), (1, 'Right')]:
        arm = group(prefix + 'Arm', root, (side * .223, 1.365, 0))
        shape('Shoulder', arm, (0, -.021, 0), (.137, .146, .153), 'Jacket')
        shape('Sleeve', arm, (0, -.15, 0), (.115, .14, .13), 'Jacket', 'cylinder')
        elbow = group(prefix + 'Forearm', arm, (0, -.285, 0))
        shape('Elbow', elbow, (0, 0, 0), (.11, .10, .12), 'Jacket')
        shape('LowerSleeve', elbow, (0, -.115, .012), (.09, .12, .108), 'Jacket', 'cylinder')
        rounded('Cuff', elbow, (0, -.225, .018), (.097, .036, .11), 'Shirt', .012)
        shape('Hand', elbow, (0, -.288, .022), (.081, .117, .061), 'Skin')
        shape('Thumb', elbow, (-side * .036, -.266, .04), (.027, .064, .031), 'Skin')
        leg = group(prefix + 'Leg', root, (side * .096, .90, 0))
        shape('Thigh', leg, (0, -.173, 0), (.137, .174, .155), 'Trousers', 'cylinder')
        knee = group(prefix + 'Shin', leg, (0, -.365, 0))
        shape('Knee', knee, (0, 0, .006), (.126, .12, .139), 'Trousers')
        shape('Calf', knee, (0, -.20075, 0), (.104, .20075, .12), 'Trousers', 'cylinder')
        rounded('TrouserCuff', knee, (0, -.3955, 0), (.112, .041, .124), 'Trousers', .012)
        rounded('Trainer', knee, (0, -.4645, .06), (.134, .105, .278), 'Shoes', .035)
        rounded('Sole', knee, (0, -.5215, .06), (.138, .027, .28), 'Soles', .01)
        rounded('ToeCap', knee, (0, -.4555, .155), (.109, .053, .067), 'Soles', .016)
        for z in [.035, .065, .095]:
            rounded('Lace', knee, (0, -.4145, z), (.065, .009, .007), 'Soles', .003)
    rounded('Backpack', root, (0, 1.195, -.196), (.29, .37, .155), 'Bag', .05)
    rounded('BackpackPocket', root, (0, 1.105, -.277), (.239, .16, .044), 'Bag', .02)
    rounded('BagZip', root, (0, 1.188, -.30), (.201, .006, .01), 'Hair', .002)
    for side in [-1, 1]:
        rounded('BackpackStrap', root, (side * .137, 1.264, .123), (.027, .265, .02), 'Bag', .008)
        shape('StrapShoulder', root, (side * .137, 1.388, -.013), (.035, .051, .259), 'Bag')
    rounded('BagHandle', root, (0, 1.415, -.181), (.10, .022, .035), 'Hair', .008)
    data['prefabNames'].append('CampusStudent')
