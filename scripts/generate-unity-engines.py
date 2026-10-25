#!/usr/bin/env python3
"""Original seamless mono vehicle beds, no recordings or third-party sounds.

Combustion harmonics + deterministic circular filtered noise, with distinct
idle/load layers. Frequencies quantized to 4-second FFT bins keep seams exact.
Run with the bundled Python (NumPy). Outputs are 44.1 kHz/16-bit PCM.
"""
from pathlib import Path
import hashlib, json, math, uuid, wave
import numpy as np
ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'UnityProject/Assets/Resources/Audio/Engines'
DEST.mkdir(parents=True, exist_ok=True)
RATE, SECONDS = 44100, 4
COUNT = RATE * SECONDS
TIME = np.arange(COUNT) / RATE

def noise(seed, cutoff, low=0):
    rng = np.random.default_rng(seed)
    f = np.fft.rfftfreq(COUNT, 1/RATE)
    weights = np.exp(-(f / cutoff)**2) * (1 - np.exp(-(f / max(1,low))**2) if low else 1)
    spectrum = (rng.standard_normal(len(f)) + 1j*rng.standard_normal(len(f))) * weights
    spectrum[0] = 0
    audio = np.fft.irfft(spectrum, COUNT)
    return audio / max(1e-6, np.std(audio))

def tone(freq, amp=1, phase=0):
    freq = round(freq * SECONDS) / SECONDS
    return amp * np.sin(2*math.pi*freq*TIME+phase)

manifest = {'version':'0.48.0','generator':'scripts/generate-unity-engines.py',
    'license':'Original IRONFRONT procedural sound design; no external samples', 'clips':[]}
for profile, base, seed in [('heavy-diesel',42,501),('light-diesel',63,521),
    ('motorcycle',95,541),('rotor',19,561),('jet',240,581)]:
    for load in (False, True):
        high = 1 if load else 0
        if profile.endswith('diesel') or profile == 'motorcycle':
            # A firing pulse spectrum, intake resonance, exhaust rumble,
            # restrained valve chatter and slow mechanical modulation.
            signal = sum(tone(base*h, (1/h**.8) * (.8 if h == 1 else 1), h*.61)
                for h in range(1,16 if load else 11))
            signal *= 1 + .11*tone(1.25) + .06*tone(2.75)
            signal += noise(seed+high, 950 if load else 650, 80) * (.36 if load else .25)
            signal += noise(seed+3+high, 95) * .2
            signal += tone(base*4, .12) * (tone(7.5)**2)
        elif profile == 'rotor':
            # Four-beat rotor slap, broadband blade/air turbulence and turbine
            # whine. This is a rotary engine, not a repitched ground engine.
            phase = 2*math.pi*base*TIME
            thump = (np.maximum(0, np.cos(phase))**7 - .20947)
            signal = thump*2.0 + tone(base*2,.35) + tone(base*4,.24)
            wash = noise(seed+high, 1350 if load else 950, 110)
            signal += wash * (.4 + .3*(1 + np.sin(phase)))
            signal += tone(410, .11) + tone(615, .07)
            signal += noise(seed+7+high, 90)*.12
        else:
            signal = noise(seed+high, 2800 if load else 1800, 140)*.8
            signal += noise(seed+7+high, 260)*.45
            signal += tone(base,.3) + tone(base*2,.2) + tone(base*3,.12)
            signal += tone(950,.1) + tone(1425,.06)
            signal *= 1 + .055*tone(1.5) + .03*tone(3.25)
        signal -= signal.mean()
        signal = np.tanh(signal*.68)
        # Short wrap crossfade removes broadband boundary mismatch while
        # keeping a constant steady bed. Exact matching endpoints as well.
        blend = 1102
        blend_weights = .5-.5*np.cos(np.linspace(0,math.pi,blend))
        wrap = signal[-blend:]*(1-blend_weights)+signal[:blend]*blend_weights
        signal = np.concatenate((signal[blend:-blend], wrap))
        signal[-1] = signal[0]
        signal *= .24 / np.sqrt(np.mean(signal**2))
        peak = np.max(np.abs(signal))
        if peak > .87: signal *= .87/peak
        samples = np.round(signal*32767).astype('<i2')
        name = profile + ('-load' if load else '-idle')
        target = DEST / (name+'.wav')
        with wave.open(str(target),'wb') as out:
            out.setnchannels(1); out.setsampwidth(2); out.setframerate(RATE); out.writeframes(samples.tobytes())
        meta = Path(str(target)+'.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nAudioImporter:\n  externalObjects: {}\n  serializedVersion: 8\n  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 1\n    quality: 0.8\n    conversionMode: 0\n    preloadAudioData: 1\n  platformSettingOverrides: {}\n  forceToMono: 1\n  normalize: 0\n  ambisonic: 0\n  loadInBackground: 0\n  preloadAudioData: 1\n  3D: 1\n  userData: Original looped IRONFRONT engine bed\n  assetBundleName:\n  assetBundleVariant:\n')
        manifest['clips'].append({'file':str(target.relative_to(ROOT)), 'seconds':round(len(samples)/RATE,5),
            'rmsDbFS':round(20*np.log10(np.sqrt(np.mean(signal**2))),3),
            'peakDbFS':round(20*np.log10(np.max(np.abs(signal))),3),
            'seamDeltaPCM':int(samples[-1])-int(samples[0]),'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
(ROOT/'docs/UNITY_ENGINE_AUDIO_MANIFEST_048.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps(manifest,indent=2))
