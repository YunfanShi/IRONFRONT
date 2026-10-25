from pathlib import Path
import json, csv
import numpy as np
from PIL import Image, ImageDraw, ImageFont

r = Path(__file__).resolve().parent
player = r / 'player'
assert (player / 'results.txt').read_text().startswith('PASS')
assert 'FINAL PASS:' in (r / 'batch-results.txt').read_text()
rows = list(csv.DictReader((player / 'samples.csv').open()))
assert len(rows) == 504
font = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf', 20)
small = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf', 16)
checks = []
for row in rows:
    stage, frame = row['sequence'], int(row['frame'])
    p = player / stage / f'{frame:04d}.png'
    im = Image.open(p).convert('RGB')
    a = np.asarray(im)
    dark = float((a.max(2) < 8).mean())
    magenta = float(((a[:, :, 0] > 225) & (a[:, :, 1] < 35) & (a[:, :, 2] > 225)).mean())
    assert dark < .95 and magenta < .001, (p, dark, magenta)
    checks.append({'frame': str(p.relative_to(r)), 'dark_fraction': dark, 'magenta_fraction': magenta})
    phase = ('SHIFT held: SPRINT' if frame < 36 else
             ('RMB: AIM + WALK' if 'aim' in stage else 'LMB: FIRE + WALK') if frame < 60 else
             'Mouse released; SHIFT held: WALK' if frame < 78 else
             'SHIFT released: WALK' if frame < 90 else 'SHIFT pressed again: SPRINT')
    d = ImageDraw.Draw(im)
    d.rectangle((12, 53, 948, 110), fill=(22, 30, 37))
    d.text((24, 60), phase, font=font, fill=(208, 237, 242))
    d.text((24, 86), f"Speed {float(row['measured_speed']):.2f} m/s   Ammo {row['ammo']}   Frame {frame}/125", font=small, fill='white')
    target = r / 'preview-sequence' / stage
    target.mkdir(parents=True, exist_ok=True)
    im.save(target / p.name)

board = Image.new('RGB', (1440, 680), (24, 28, 33))
d = ImageDraw.Draw(board)
for i, (stage, frame, label) in enumerate([
    ('fp-fire-interrupt', 28, 'SPRINT 6.60 m/s'),
    ('fp-fire-interrupt', 48, 'FIRE -> WALK 4.20 m/s'),
    ('fp-fire-interrupt', 72, 'SHIFT STILL HELD -> WALK'),
    ('fp-aim-interrupt', 28, 'SPRINT 6.60 m/s'),
    ('fp-aim-interrupt', 56, 'AIM -> WALK 2.73 m/s'),
    ('fp-aim-interrupt', 110, 'SHIFT RELEASE / REPRESS -> SPRINT')]):
    x, y = i % 3 * 480, i // 3 * 340
    im = Image.open(player / stage / f'{frame:04d}.png').resize((480, 320))
    board.paste(im, (x, y + 20))
    d.text((x + 8, y + 3), label, font=small, fill='white')
board.save(r / 'sprint-review.png')
(r / 'image-checks.json').write_text(json.dumps(checks, indent=2) + '\n')
max_error = max(abs(float(row['speed']) - float(row['measured_speed'])) for row in rows)
(r / 'movement-summary.json').write_text(json.dumps({
    'version': '0.59.2', 'frames': len(rows), 'sequences': 4, 'fps': 30,
    'max_controller_speed_error_mps': max_error,
    'interruption_frame': 36, 'shift_release_frame': 78, 'shift_repress_frame': 90,
    'sprint_mps': 6.6, 'walk_mps': 4.2, 'aim_walk_mps': 2.73,
    'verification': 'Actual gameplay input-resolution methods and CharacterController displacement; no OS key injection.'
}, indent=2) + '\n')
print(json.dumps({'frames': len(rows), 'maximum_speed_error': max_error, 'preview_ready': True}))
