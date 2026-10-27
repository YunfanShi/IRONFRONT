"""Synchronize genuine 0.66.8 / 0.66.9 player captures; preserve both full views."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

R = Path(__file__).resolve().parent
repo = R.parents[2]
sources = [R.parent/'finger-volume-2026-10-27/checks/finger-motion',
           R/'checks/pistol-pistol']
out = R/'checks/comparison-frames'
out.mkdir(parents=True, exist_ok=True)
font_path = repo/'UnityProject/Assets/Fonts/Lobby/SourceHanSansCN-Medium.otf'
title = ImageFont.truetype(str(font_path), 30)
body = ImageFont.truetype(str(font_path), 22)
small = ImageFont.truetype(str(font_path), 20)
colors = ['#e7bd7a', '#8ddbcf']
frames = {}
provenance = []
for side, source in enumerate(sources):
    expected = ['0.66.8', '0.66.9'][side]
    report = (source/'results.txt').read_text()
    assert report.startswith('PASS\nVersion='+expected+'\nRuntimeErrors=0')
    manifest = []
    for n in range(60, 144):
        f = source/'motion-Pistol'/f'{n:04}.png'
        im = Image.open(f).convert('RGB')
        assert im.size == (1440, 960)
        frames[side, n] = im.resize((960, 640), Image.Resampling.LANCZOS)
        manifest.append({'frame': n, 'sha256': hashlib.sha256(f.read_bytes()).hexdigest()})
    provenance.append({'version': expected, 'directory': str(source.relative_to(repo)), 'frames': manifest})

index = 0
segments = []
for label, repeats in [('正常速度 1× · 第 1 遍', 1),
                       ('正常速度 1× · 第 2 遍', 1),
                       ('半速观察 0.5×', 2)]:
    first = index
    for n in range(60, 144):
        im = Image.new('RGB', (1920, 800), '#141a23')
        d = ImageDraw.Draw(im)
        for side in range(2):
            x = side*960
            d.rectangle((x, 0, x+959, 4), fill=colors[side])
            d.text((x+26, 11), ['之前 · 0.66.8', '现在 · 0.66.9'][side], font=title, fill=colors[side])
            d.text((x+26, 53), ['原程序化换弹', '动作包 Gun_Reload02'][side], font=body, fill='white')
            im.paste(frames[side, n], (x, 96))
        d.line((959, 0, 959, 799), fill='#404a58', width=2)
        elapsed = max(0, min(1.3, (n-75)/30))
        phase = '准备' if n < 75 else ('恢复持枪' if n >= 114 else f'换弹 {elapsed:.2f} / 1.30 秒')
        d.text((26, 748), label, font=body, fill='white')
        d.text((765, 748), '同步起点 · 同一视角', font=small, fill='#a6b1bf')
        d.text((1555, 748), phase, font=body, fill='white')
        d.rectangle((24, 789, 1895, 793), fill='#303b4a')
        if elapsed:
            d.rectangle((24, 789, 24+int(1871*elapsed/1.3), 793), fill='#8ddbcf')
        for _ in range(repeats):
            im.save(out/f'{index:04}.png', compress_level=2)
            index += 1
        if label.startswith('正常速度') and n == 109 and first == 0:
            im.save(R/'review/P8-before-after-video-poster.jpg', quality=95)
    segments.append({'label': label, 'startSeconds': first/30, 'endSeconds': index/30,
                     'playbackRate': 1/repeats})

assert index == 336
record = {'output': 'p8-before-after-comparison.mp4', 'fps': 30, 'dimensions': [1920, 800],
          'frames': index, 'durationSeconds': index/30, 'sourceFramesInclusive': [60, 143],
          'reloadStartSourceFrame': 75, 'reloadEndSourceFrame': 114, 'gameReloadSeconds': 1.3,
          'composition': 'Full source views scaled equally; no crop, retiming difference, generated poses or audio',
          'slowMotion': 'Duplicate each input frame twice; no frame interpolation',
          'segments': segments, 'sources': provenance}
(R/'comparison-manifest.json').write_text(json.dumps(record, ensure_ascii=False, indent=2)+'\n')
print(f'COMPARISON_FRAMES_READY {index} frames / {index/30} seconds', flush=True)
