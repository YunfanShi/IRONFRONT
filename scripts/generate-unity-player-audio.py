#!/usr/bin/env python3
"""Original deterministic action/feedback synthesis; standard library only."""
import hashlib, json, math, random, struct, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'UnityProject/Assets/Resources/Audio/PlayerCues'
SETTINGS = {
    'StepWalk': (.21, 90, .55, .18), 'StepRun': (.26, 62, .68, .24),
    'StepCrouch': (.18, 115, .27, .15), 'StepProne': (.30, 240, .08, .30),
    'Crouch': (.24, 180, .14, .30), 'Stand': (.23, 235, .18, .25),
    'Prone': (.36, 120, .20, .38), 'Rise': (.32, 200, .18, .33),
    'Board': (.32, 85, .60, .20), 'Hit': (.075, 1550, .20, .38),
    'Kill': (.28, 740, .42, .035), 'LockTick': (.065, 1020, .40, .025),
    'LockReady': (.24, 1200, .40, .02), 'LockLost': (.23, 960, .35, .025),
    'Zoom': (.13, 460, .13, .18),
}

def main():
    DEST.mkdir(parents=True, exist_ok=True)
    manifest = {'license': 'Original procedural audio created for IRONFRONT; no external samples',
                'generator': 'scripts/generate-unity-player-audio.py', 'sample_rate': 44100, 'clips': []}
    for index, (name, (duration, hz, tone, noise)) in enumerate(SETTINGS.items()):
        rng = random.Random(5100 + index)
        values, low = [], 0.0
        for i in range(round(duration * 44100)):
            t = i / 44100
            u = t / duration
            low = .62 * low + .38 * rng.uniform(-1, 1)
            env = min(1, t / .004) * (1-u)**2.8
            if name.startswith('Step') or name == 'Board':
                envelope = env + .32 * math.exp(-((t-.065)/.021)**2) * (1-u)
                signal = tone * math.sin(2*math.pi*hz*t) * math.exp(-t*24) + noise * low
                value = envelope * signal
            else:
                sweep = 1.0 + (.55*u if name in ('Kill', 'LockReady') else -.6*u if name == 'LockLost' else 0)
                signal = tone * math.sin(2*math.pi*hz*t*sweep)
                if name == 'Kill': signal += .15 * math.sin(2*math.pi*hz*1.5*t)
                value = env * (signal + noise * low)
            values.append(value)
        peak = max(abs(v) for v in values)
        gain = .78 / max(peak, .001)
        values = [v*gain for v in values]
        # Continuous silence boundaries eliminate click tails.
        values[0] = values[-1] = 0
        pcm = struct.pack('<' + 'h'*len(values), *(round(v*32767) for v in values))
        path = DEST / (name + '.wav')
        with wave.open(str(path), 'wb') as wav:
            wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(44100); wav.writeframes(pcm)
        guid = hashlib.md5(('IRONFRONT051-original-'+name).encode()).hexdigest()
        # Short feedback must be preloaded PCM, not Unity's compressed-memory
        # default. This also makes the actual imported samples inspectable.
        path.with_suffix('.wav.meta').write_text(f'''fileFormatVersion: 2
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
  3D: 0
  userData: Original procedural IRONFRONT local player cue
  assetBundleName:
  assetBundleVariant:
''')
        rms = math.sqrt(sum(v*v for v in values)/len(values))
        assert peak > 0 and max(abs(v) for v in values) < .8 and rms > .025
        manifest['clips'].append({'name': name, 'duration': duration, 'samples': len(values),
            'peak': max(abs(v) for v in values), 'rms_dbfs': round(20*math.log10(rms), 3),
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    (ROOT/'docs/UNITY_PLAYER_AUDIO_MANIFEST_051.json').write_text(json.dumps(manifest, indent=2)+'\n')
    reel = bytearray()
    for name in ('StepWalk', 'StepRun', 'Crouch', 'Board', 'LockTick', 'LockReady', 'Hit', 'Kill'):
        with wave.open(str(DEST/(name+'.wav')), 'rb') as clip: reel.extend(clip.readframes(clip.getnframes()))
        reel.extend(b'\x00\x00' * round(.07*44100))
    preview = ROOT/'UnityProject/Builds/Verification051'
    preview.mkdir(parents=True, exist_ok=True)
    with wave.open(str(preview/'player-audio-reel.wav'), 'wb') as wav:
        wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(44100); wav.writeframes(reel)
    assert len(reel)/2/44100 < 3
    print('PASS: 15 original mono PCM16/44100Hz clips; peak below .8, audible RMS, silent boundaries')

if __name__ == '__main__': main()
