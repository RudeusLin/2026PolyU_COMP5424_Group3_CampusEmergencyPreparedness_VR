"""Human-scale printed signage on existing physical Campus sign carriers."""
import hashlib
import math
import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

import generate_teaching_building as base
import refine_teaching_building as refined

ROOT = Path(__file__).resolve().parents[1]
FOLDER = Path('Assets/Environment/TeachingBuildingCampus/Textures/Lettering')
FONTS = Path(os.environ.get('CAMPUS_FONT_DIR', '/System/Library/Fonts/Supplemental'))


def signage_font(name):
    candidates = [FONTS / name,
                  Path('C:/Windows/Fonts') / ('segoepr.ttf' if name == 'Chalkboard.ttc' else 'arial.ttf'),
                  Path('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf')]
    path = next((p for p in candidates if p.is_file()), None)
    if path is None:
        raise RuntimeError('Set CAMPUS_FONT_DIR to a folder containing Arial.ttf and Chalkboard.ttc')
    return ImageFont.truetype(str(path), 96)


def prepare_signage(data):
    nodes = data['nodes']
    lookup = {n['id']: n for n in nodes}
    children = {}
    for n in nodes:
        children.setdefault(n['parent'], []).append(n)
    # Individually pinned notices, with ordinary paper margins and slight tilt.
    posters = [n for n in nodes if n['name'].startswith('Poster_')]
    for i, poster in enumerate(posters):
        angle = math.radians([-1.3, .8, -2, 1.1, -.6][i % 5]) / 2
        poster['rotation'] = [0, 0, math.sin(angle), math.cos(angle)]
        for child in children[poster['id']]:
            if child['name'] == 'PosterPaper':
                child['scale'] = [.60, .82, .006]
                child['material'] = 'Paper'
            elif child['name'] == 'PosterHeader':
                child.update(position=[-.255, .27, -.009], scale=[.012, .12, .003])
            elif child['name'] == 'PosterRule':
                child.update(position=[0, -.27, -.010], scale=[.47, .002, .002])
            elif child['name'] == 'NoticePin':
                child['position'][:2] = [math.copysign(.265, child['position'][0]), .38]
    (ROOT / FOLDER).mkdir(parents=True, exist_ok=True)
    measured = 0
    for n in list(nodes):
        if n['kind'] != 'text':
            continue
        parent = lookup[n['parent']]
        panel = next((c for c in children.get(parent['id'], []) if c['name'] == 'SignPanel'), None)
        value = n['text']
        poster_text = parent['name'].startswith('Poster_')
        if poster_text:
            if n['position'][1] > .2:
                n.update(textSize=.043, textColor=[.10, .20, .24, 1])
                n['position'][1] = .27
            elif n['position'][1] < -.3:
                n['textSize'] = .020
                n['position'][1] = -.32
            else:
                n['textSize'] = .034
        handwriting = value.startswith('CLASSROOM ') or value.startswith('FIRE EVACUATION DRILL')
        if value == 'BLOCK A / 401':
            n['active'] = False  # Room identification belongs at the entrance.
        if panel and len(value) == 3 and value.isdigit():
            panel['scale'] = [.48, .22, .018]
            panel['material'] = 'Paper'
            parent['position'][1] = 1.6
            n.update(textSize=.095, textColor=[.10, .17, .19, 1])
        elif panel and value == 'FIRE EQUIPMENT':
            panel['scale'] = [.58, .14, .018]
            n.update(text='FIRE SERVICES', textSize=.047)
        elif panel and value == 'FIRE\nHOSE':
            panel['scale'] = [.25, .19, .012]
            n.update(text='HOSE REEL', textSize=.035)
        elif panel and value == 'FIRE ALARM':
            panel['scale'] = [.28, .10, .012]
            n.update(text='FIRE ALARM', textSize=.031)
        elif panel and value.startswith('SAFETY NOTICE'):
            panel['scale'] = [.42, .52, .012]
            panel['material'] = 'Paper'
            n.update(text='FIRE ACTION\n\nRaise the alarm\nLeave by the stairs\nFollow EXIT signs\nGo to assembly point',
                     textSize=.034, textColor=[.15, .20, .20, 1])
        elif panel and value.startswith('FIRE DRILL\n'):
            panel['scale'] = [.38, .45, .016]
            panel['material'] = 'Paper'
            n.update(text='IN CASE OF FIRE\n\nDO NOT USE LIFT\n\nUse the stairs', textSize=.039,
                     textColor=[.60, .09, .06, 1])
        elif panel and 'YOU ARE HERE' in value:
            n.update(text=f'LEVEL {n["floor"]:02}\n\nSTAIR A  ←\n→  STAIR B', textSize=.10)
            panel['scale'] = [.65, .65, .018]
        elif panel and value == 'FIRE SAFETY\nUSE STAIRS':
            parent['active'] = False  # Remove the duplicate beside the lift warning.
        elif panel and value.startswith(('A / FLOOR', 'B / FLOOR')):
            n.update(text=f'{value[0]}  /  {n["floor"]:02}', textSize=.19)
            panel['scale'] = [.70, .36, .025]
        elif value == "WHAT'S ON / TEACHING BLOCK A":
            n.update(text='STUDENT NOTICEBOARD', textSize=.045, textColor=[.12, .16, .15, 1])
        elif value == 'HARBOURVIEW / STUDENT LIFE':
            n.update(text='Student Affairs Office', textSize=.026)
        elif value == 'STUDENT SOCIETIES\n\nMeet / Learn / Connect':
            n.update(text='Photography Society\n\nWeekly photo walk\nMeet in the lobby', textSize=.041)
        elif value == 'IDEAS IN ACTION\n\nExhibition / Studio 403':
            n.update(text='OPEN STUDIO\n\nStudent work exhibition\nRoom 403', textSize=.043)
        elif value == 'Keep exits clear\n\nKnow your assembly point':
            n.update(text='FIRE EVACUATION\n\nUse the nearest safe exit\nLeave belongings behind\nMeet at the plaza', textSize=.037)
        elif value == 'HARBOURVIEW' and parent['name'] == 'LaptopScreenHinge':
            n.update(text='Lecture notes', textSize=.015)
        if poster_text and value == 'CAMPUS LIFE':
            n['text'] = 'PHOTO WALK'
        elif poster_text and value == 'DESIGN WEEK':
            n['text'] = 'DESIGN EXHIBITION'
        elif poster_text and value == 'SAFETY FIRST':
            n['text'] = 'FIRE ACTION'

        value = n['text']
        if panel:
            # Decals sit 2 mm in front of the carrier, never a centimetre away.
            n['position'][2] = -panel['scale'][2] / 2 - .002
        font_name = 'Chalkboard.ttc' if handwriting else 'Arial.ttf'
        font = signage_font(font_name)
        lines = value.split('\n')
        if handwriting:
            lines = ['Fire evacuation drill', '', '1. Raise the alarm', '2. Nearest safe exit / stairs',
                     '3. Meet at the assembly point']
            n['text'] = '\n'.join(lines)
            n['textSize'] = .13
            n['textColor'] = [.10, .22, .27, 1]
        widths = [font.getlength(line) for line in lines]
        width, height = int(max(widths)) + 16, len(lines) * 112 + 16
        image = Image.new('RGBA', (width, height))
        draw = ImageDraw.Draw(image)
        for i, line in enumerate(lines):
            x = 8 if handwriting or poster_text else (width - widths[i]) / 2
            draw.text((x, 8 + i * 112), line, font=font, fill='white', anchor='lt')
        w, h = width / 96 * n['textSize'], height / 96 * n['textSize']
        if panel:
            max_w, max_h = panel['scale'][0] * .82, panel['scale'][1] * .78
            factor = min(1, max_w / w, max_h / h)
            w, h = w * factor, h * factor
            assert w <= max_w + 1e-6 and h <= max_h + 1e-6
            measured += 1
        elif parent['name'].startswith('Poster_'):
            factor = min(1, .46 / w, (.13 if n['position'][1] > .2 else .42) / h)
            w, h = w * factor, h * factor
            n['position'][0] = -.23 + w / 2
        elif parent['name'] == 'CampusDirectory':
            factor = min(1, 1.03 / w)
            w, h = w * factor, h * factor
        # Keep high-resolution lettering independent of Unity's dynamic font atlas.
        image.thumbnail((1024, 1024), Image.Resampling.LANCZOS)
        digest = hashlib.sha256((font_name + str(poster_text) + n['text']).encode()).hexdigest()[:16]
        relative = FOLDER / (digest + '.png')
        image.save(ROOT / relative)
        refined.texture_metadata(ROOT / relative)
        meta = Path(str(ROOT / relative) + '.meta')
        meta.write_text(meta.read_text().replace('maxTextureSize: 512', 'maxTextureSize: 1024')
                        .replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1')
                        .replace('mipMapsPreserveCoverage: 0', 'mipMapsPreserveCoverage: 1')
                        .replace('alphaTestReferenceValue: 0.5', 'alphaTestReferenceValue: 0.3'))
        n.update(printTexture=str(relative), printWidth=w, printHeight=h)
    return measured
