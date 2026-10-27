"""Original-arm before/after player captures, synchronized by P8 reload phase.

0.66.2 audit sampled reload across 90 frames; later audits use the actual
39-frame / 1.3-second interval. Select the closest original old frame to
each current phase. Preserve original meshes, camera and every image pixel
except equal scaling; do not synthesize animation or optical-flow frames.
"""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

R = Path(__file__).resolve().parent
repo = R.parents[2]
sources = [R.parent/'weapon-fit-2026-10-26/checks/motion',
           R/'checks/pistol-pistol']
versions = ['0.66.2', '0.66.10']
titles = ['旧手臂 · 0.66.2', '旧手臂＋动作包 · 0.66.10']
subtitles = ['原程序化换弹', 'Gun_Reload02 · 四指仍为共用弯曲骨']
colors = ['#e7bd7a', '#8ddbcf']
out = R/'checks/legacy-comparison-frames'
out.mkdir(parents=True, exist_ok=True)
font_path = repo/'UnityProject/Assets/Fonts/Lobby/SourceHanSansCN-Medium.otf'
title = ImageFont.truetype(str(font_path), 30)
body = ImageFont.truetype(str(font_path), 22)
small = ImageFont.truetype(str(font_path), 20)
frames, provenance, mapping = {}, [], []
for n in range(60, 129):
    old = n if n < 75 else (75+round((n-75)*90/39) if n < 114 else 165+n-114)
    mapping.append({'timelineFrame': n-60, 'sourceFrames': [old, n],
                    'targetReloadPhase': (n-75)/39 if 75 <= n < 114 else None})
for side, source in enumerate(sources):
    report = (source/'results.txt').read_text()
    assert report.startswith('PASS\nVersion='+versions[side]+'\nRuntimeErrors=0')
    files = []
    for item in mapping:
        n = item['sourceFrames'][side]
        f = source/'motion-Pistol'/f'{n:04}.png'
        im = Image.open(f).convert('RGB')
        assert im.size == (1440, 960)
        frames[side, item['timelineFrame']] = im.resize((960, 640), Image.Resampling.LANCZOS)
        files.append({'frame': n, 'sha256': hashlib.sha256(f.read_bytes()).hexdigest()})
    provenance.append({'version': versions[side], 'directory': str(source.relative_to(repo)), 'frames': files})

index, segments = 0, []
for label, repeats in [('正常速度 1× · 第 1 遍', 1), ('正常速度 1× · 第 2 遍', 1), ('半速观察 0.5×', 2)]:
    first = index
    for t in range(69):
        im = Image.new('RGB', (1920, 800), '#141a23')
        d = ImageDraw.Draw(im)
        for side in range(2):
            x = side*960
            d.rectangle((x, 0, x+959, 4), fill=colors[side])
            d.text((x+26, 11), titles[side], font=title, fill=colors[side])
            d.text((x+26, 53), subtitles[side], font=body, fill='white')
            im.paste(frames[side, t], (x, 96))
        for x in (959,):
            d.line((x, 0, x, 735), fill='#404a58', width=2)
        elapsed = max(0, min(1.3, (t-15)/30))
        phase = '准备' if t < 15 else ('恢复持枪' if t >= 54 else f'换弹 {elapsed:.2f} / 1.30 秒')
        d.text((26, 748), label, font=body, fill='white')
        d.text((595, 748), '同一视角 · 0.66.2 逐帧画面按换弹进度同步', font=small, fill='#b8c3d0')
        d.text((1555, 748), phase, font=body, fill='white')
        d.rectangle((24, 789, 1895, 793), fill='#303b4a')
        if elapsed:
            d.rectangle((24, 789, 24+int(1871*elapsed/1.3), 793), fill='#8ddbcf')
        for _ in range(repeats):
            im.save(out/f'{index:04}.png', compress_level=2)
            index += 1
        if first == 0 and t in (36, 49):
            im.save(R/f'review/P8-legacy-comparisons-{t}.jpg', quality=95)
    segments.append({'label': label, 'startSeconds': first/30, 'endSeconds': index/30,
                     'playbackRate': 1/repeats})
assert index == 276
max_error = max(abs((m['sourceFrames'][0]-75)/90-m['targetReloadPhase'])
                for m in mapping if m['targetReloadPhase'] is not None)
record = {'output': 'legacy-arms-reload02-comparison.mp4', 'fps': 30, 'dimensions': [1920, 800],
          'frames': index, 'durationSeconds': index/30, 'gameReloadSeconds': 1.3,
          'historicalProbeCommit': 'c21faf2',
          'historicalSampling': '0.66.2 source frame 75..164 has phase (frame-75)/90; recovery starts at 165',
          'phaseMapping': 'Nearest original 0.66.2 phase to the later 39-frame reload; pre/post frames retain 30 fps',
          'maxHistoricalPhaseError': max_error, 'maxHistoricalTimeErrorSeconds': max_error*1.3,
          'composition': 'Full original views scaled equally; historical sleeves and meshes retained; no crop or generated motion',
          'slowMotion': 'Duplicate frames twice; no interpolated frames or audio',
          'segments': segments, 'frameMapping': mapping, 'sources': provenance}
(R/'legacy-comparison-manifest.json').write_text(json.dumps(record, ensure_ascii=False, indent=2)+'\n')
print(f'LEGACY_COMPARISON_READY {index} frames / {index/30} seconds; phase error {max_error:.6f}', flush=True)
