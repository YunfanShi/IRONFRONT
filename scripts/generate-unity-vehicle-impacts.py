#!/usr/bin/env python3
"""Original deterministic hull impacts, no external recordings or packages."""
import hashlib
import json
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'UnityProject/Assets/Resources/Audio/VehicleImpacts'
RATE = 44100
PROFILES = {
    'Ricochet': (.18, (1680, 2693, 4117, 5813), 5411),
    'MachineGun': (.22, (379, 917, 1739, 2671, 4273), 5412),
    'HeavyImpact': (.86, (109, 213, 367, 541, 1123), 5413),
}


def samples(name, seconds, modes, seed):
    rng = random.Random(seed)
    values, low, old_low = [], 0.0, 0.0
    phases = [rng.uniform(-math.pi, math.pi) for _ in modes]
    for i in range(round(seconds * RATE)):
        t = i / RATE
        noise = rng.uniform(-1, 1)
        old_low = low
        low += (noise - low) * (.10 if name == 'HeavyImpact' else .34)
        bright = noise - old_low
        ring = sum(math.sin(2 * math.pi * hz * t + phases[k]) /
                   (1 + k * .75) * math.exp(-t * (4.5 + k * 2.1 if name == 'HeavyImpact' else 21 + k * 8))
                   for k, hz in enumerate(modes))
        if name == 'HeavyImpact':
            body = .52 * math.sin(2 * math.pi * (63 * t - 16 * t * t)) * math.exp(-t * 9)
            panel = .24 * low * math.exp(-t * 6)
            # Two small delayed rattles are internal panel flex, not a second blast.
            rattle = .19 * bright * (math.exp(-((t - .07) / .018) ** 2) +
                                     .55 * math.exp(-((t - .16) / .035) ** 2))
            value = body + .24 * ring + .28 * bright * math.exp(-t * 85) + panel + rattle
        elif name == 'MachineGun':
            body = .25 * math.sin(2 * math.pi * 132 * t) * math.exp(-t * 38)
            value = body + .30 * ring + .52 * bright * math.exp(-t * 190) + .16 * low * math.exp(-t * 55)
        else:
            slide = math.sin(2 * math.pi * (3250 * t - 2900 * t * t)) * math.exp(-t * 32)
            value = .16 * ring + .20 * slide + .35 * bright * math.exp(-t * 145)
        edge = min(1, t / .0012) * min(1, (seconds - t) / .018)
        values.append(math.tanh(value * 1.8) * edge)
    peak = max(abs(v) for v in values)
    values = [v * .78 / peak for v in values]
    values[0] = values[-1] = 0
    return values


def write_meta(path, guid, folder=False):
    if folder:
        content = f'''fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
'''
    else:
        content = f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  ambisonic: 0
  loadInBackground: 0
  preloadAudioData: 1
  3D: 1
  userData: Original procedural IRONFRONT hull impact
  assetBundleName:
  assetBundleVariant:
'''
    path.write_text(content)


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    folder_meta = DEST.with_suffix('.meta')
    if not folder_meta.exists():
        write_meta(folder_meta, hashlib.md5(b'IRONFRONT054-vehicle-impacts-folder').hexdigest(), True)
    manifest = {'license': 'Original procedural audio created for IRONFRONT; no external samples',
                'generator': 'scripts/generate-unity-vehicle-impacts.py', 'sample_rate': RATE, 'clips': []}
    pcm_by_name = {}
    for name, (duration, modes, seed) in PROFILES.items():
        data = samples(name, duration, modes, seed)
        pcm = struct.pack('<' + 'h' * len(data), *(round(v * 32767) for v in data))
        path = DEST / (name + '.wav')
        with wave.open(str(path), 'wb') as wav:
            wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(RATE); wav.writeframes(pcm)
        write_meta(path.with_suffix('.wav.meta'), hashlib.md5(('IRONFRONT054-original-' + name).encode()).hexdigest())
        rms = math.sqrt(sum(v * v for v in data) / len(data))
        assert rms > .025 and max(abs(v) for v in data) <= .781
        manifest['clips'].append({'name': name, 'duration': duration, 'samples': len(data),
                                 'modal_hz': modes, 'peak': .78, 'rms_dbfs': round(20 * math.log10(rms), 3),
                                 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
        pcm_by_name[name] = pcm
    manifest_path = DEST / 'manifest.json'
    manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
    manifest_path.with_suffix('.json.meta').write_text('fileFormatVersion: 2\nguid: ' +
        hashlib.md5(b'IRONFRONT054-vehicle-impact-manifest').hexdigest() + '\n')
    reel = bytearray(pcm_by_name['Ricochet'])
    reel.extend(b'\x00\x00' * round(.24 * RATE))
    # One metallic voice restarted at the game's 70ms interval, as on a hull.
    for _ in range(7): reel.extend(pcm_by_name['MachineGun'][:round(.07 * RATE) * 2])
    reel.extend(pcm_by_name['MachineGun'])
    reel.extend(b'\x00\x00' * round(.32 * RATE))
    reel.extend(pcm_by_name['HeavyImpact'])
    output = ROOT / 'UnityProject/Builds/Verification054'
    output.mkdir(parents=True, exist_ok=True)
    with wave.open(str(output / 'vehicle-impact-reel.wav'), 'wb') as wav:
        wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(RATE); wav.writeframes(reel)
    print('PASS: 3 original mono PCM16/44100Hz hull clips and ricochet/MG burst/heavy preview; peak .78, silent boundaries')


if __name__ == '__main__':
    main()
