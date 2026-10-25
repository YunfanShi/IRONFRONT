#!/usr/bin/env python3
"""Reproduce three original IRONFRONT musical loops (macOS, Swift + NumPy).

The Swift companion performs our original note score with the system-installed
Apple GM bank. No sound bank, commercial loop, song or external recording is
copied. Original synthesized percussion/bass and circular room DSP are mixed
here. Run with the bundled Python (NumPy) and grant offline CoreAudio access if
the host sandbox cannot discover Audio Units.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
import subprocess
import tempfile
import uuid
import wave
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "UnityProject/Assets/Resources/Audio/Music"
RATE = 44100
SEED = 46047
THEME = [62, 65, 67, 69]  # D4 F4 G4 A4: original ascending minor fourth/fifth motif.

# Voicings leave space for game effects: upper strings above a separate bass.
CHORDS = {
    "Dm9": (38, [50, 53, 57, 60, 64]),
    "Bbmaj7": (34, [53, 57, 58, 62]),
    "Fmaj7": (41, [53, 57, 60, 64]),
    "Csus2": (36, [55, 60, 62, 64]),
    "Gm9": (43, [53, 57, 58, 62]),
    "Am7": (33, [52, 55, 60, 64]),
    "Ebmaj7": (39, [51, 55, 58, 62]),
    "Asus4": (33, [52, 57, 62, 64]),
    "A": (33, [52, 57, 61, 64]),
}


def track(name, program, volume, pan=0):
    return dict(name=name, program=program, volume=volume, pan=pan, notes=[])


def note(t, pitch, start, duration, velocity=70):
    t["notes"].append(dict(pitch=int(pitch), velocity=int(velocity),
                           start=round(float(start), 5), duration=round(float(duration), 5)))


def theme(t, start, transpose=0, variation=0, velocity=70, half_speed=False):
    # Each phrase has a response and rests, rather than one repeating four-note cell.
    motifs = [
        [(0, 62, .7), (1, 65, .7), (2, 67, .7), (3, 69, 1.6), (5, 67, .6), (6, 65, 1.7)],
        [(0, 69, 1.5), (2, 72, .7), (3, 69, .7), (4, 67, 1.5), (6, 65, .7), (7, 62, .8)],
        [(0, 62, .7), (1, 65, .7), (2, 67, 1.6), (4, 69, .7), (5, 72, .7), (6, 74, 1.7)],
        [(0, 69, .7), (1, 67, .7), (2, 65, 1.6), (4, 64, 1.5), (6, 62, 1.7)],
    ]
    scale = 1.5 if half_speed else 1
    for i, (offset, pitch, duration) in enumerate(motifs[variation % 4]):
        note(t, pitch + transpose, start + offset * scale, duration * scale,
             velocity + [0, -7, -4, 4, -6, -10][i % 6])


def make_score(kind):
    if kind == "lobby":
        bpm, bars = 72, 16
        harmony = ["Dm9", "Bbmaj7", "Fmaj7", "Csus2", "Dm9", "Bbmaj7", "Gm9", "Am7"]
        strings = track("quiet sustained strings", 49, .32, -.23)
        piano = track("warm electric piano theme", 4, .65, .08)
        vibes = track("sparse vibraphone answer", 11, .20, .32)
        horn = track("distant horn at return", 60, .16, -.12)
        tracks = [strings, piano, vibes, horn]
        for i, symbol in enumerate(harmony):
            _, chord = CHORDS[symbol]
            for p in chord:
                note(strings, p, i * 8, 7.7, 46 + (i % 3) * 3)
            # Chord voice leading, punctuated by rests at each phrase entrance.
            for j, b in enumerate([1.5, 3, 5.5, 7]):
                note(vibes, chord[(i + j) % len(chord)] + 12, i * 8 + b, .65, 40 + j * 3)
        theme(piano, 2, variation=0, velocity=67, half_speed=True)
        theme(piano, 18, variation=1, velocity=62, half_speed=True)
        theme(piano, 34, variation=2, velocity=67, half_speed=True)
        theme(piano, 50, variation=3, velocity=61, half_speed=True)
        theme(horn, 35, transpose=-12, variation=0, velocity=46, half_speed=True)
    elif kind == "victory-near":
        bpm, bars = 128, 32
        harmony = ["Dm9", "Bbmaj7", "Fmaj7", "Csus2", "Dm9", "Bbmaj7", "Fmaj7", "Csus2",
                   "Dm9", "Bbmaj7", "Fmaj7", "Csus2", "Gm9", "Bbmaj7", "Csus2", "A"]
        strings = track("driving string section", 48, .30, -.18)
        pizz = track("pizzicato eighth-note movement", 45, .33, .28)
        horn = track("heroic horn theme", 60, .42, -.06)
        brass = track("brass section answers", 61, .19, .22)
        timpani = track("timpani phrase accents", 47, .45, -.16)
        tracks = [strings, pizz, horn, brass, timpani]
        for i, symbol in enumerate(harmony):
            root, chord = CHORDS[symbol]
            for p in chord:
                note(strings, p + (12 if i >= 8 and p < 57 else 0), i * 8, 7.72, 66 + (i % 3) * 4)
            for k in range(16):
                # Changing arpeggio order and offbeat accents follow the harmony.
                p = chord[(k * 2 + i + k // 4) % len(chord)] + 12
                note(pizz, p, i * 8 + .5 * k + (.015 if k % 2 else 0), .29,
                     65 + (8 if k % 4 == 0 else -5) + i % 4)
            if i % 2 or i >= 12:
                for b in [0, 2.5, 4, 6.5]:
                    for p in chord[:3]: note(brass, p, i * 8 + b, .6, 54 + (i % 3) * 3)
            for b in ([0, 6] if i % 4 == 0 else [0]):
                note(timpani, root, i * 8 + b, 1.2, 76 if b == 0 else 53)
        for start, variation in [(8, 0), (24, 1), (40, 0), (56, 2), (72, 0), (88, 2), (104, 1), (120, 3)]:
            theme(horn, start, variation=variation, velocity=77 + (4 if start >= 72 else 0))
    else:
        bpm, bars = 112, 32
        harmony = ["Dm9", "Ebmaj7", "Bbmaj7", "Am7", "Dm9", "Ebmaj7", "Gm9", "Am7",
                   "Dm9", "Ebmaj7", "Dm9", "Bbmaj7", "Gm9", "Ebmaj7", "Asus4", "A"]
        strings = track("low uneasy string sustain", 49, .32, -.18)
        cello = track("cello heartbeat ostinato", 42, .38, .13)
        horn = track("low fragmented theme", 60, .23, -.08)
        vibes = track("distant metallic response", 11, .18, .36)
        tracks = [strings, cello, horn, vibes]
        for i, symbol in enumerate(harmony):
            root, chord = CHORDS[symbol]
            for p in chord: note(strings, p - 12, i * 8, 7.7, 52 + i % 4 * 3)
            for b in [0, .75, 2, 2.75, 4, 4.75, 6, 6.75]:
                p = root + 12 + (7 if b in [2.75, 6.75] else 0)
                note(cello, p, i * 8 + b, .38, 61 if b % 2 == 0 else 47)
            if i % 2:
                note(vibes, chord[-1] + 12, i * 8 + 3.5, 1.0, 37)
                note(vibes, chord[-2] + 12, i * 8 + 6.5, .8, 35)
        for start, variation in [(4, 0), (28, 3), (52, 1), (76, 0), (100, 3), (120, 3)]:
            theme(horn, start, transpose=-12, variation=variation, velocity=54)
    return dict(title={"lobby": "Before the Front", "victory-near": "The Final Advance", "defeat-near": "Hold the Line"}[kind],
                id=kind, bpm=bpm, bars=bars, beats=bars * 4, sampleRate=RATE,
                harmony=harmony, tracks=tracks)


def circular_add(audio, signal, start, pan=0, gain=1):
    """All musical and effect tails wrap into the loop, never truncate or fade to silence."""
    n = len(audio)
    index = (np.arange(len(signal)) + int(round(start * RATE))) % n
    left, right = math.sqrt((1 - pan) / 2), math.sqrt((1 + pan) / 2)
    np.add.at(audio[:, 0], index, signal * gain * left)
    np.add.at(audio[:, 1], index, signal * gain * right)


def envelope(t, attack, decay):
    return (1 - np.exp(-t / attack)) * np.exp(-t / decay)


def filter_noise(length, rng, low=0, high=12000):
    n = int(length * RATE)
    white = rng.normal(0, 1, n)
    f = np.fft.rfftfreq(n, 1 / RATE)
    response = 1 / (1 + (f / high) ** 6)
    if low: response *= 1 - 1 / (1 + (f / low) ** 6)
    x = np.fft.irfft(np.fft.rfft(white) * response, n)
    return x / max(.01, np.std(x))


def kick(rng, soft=False):
    t = np.arange(int(.55 * RATE)) / RATE
    frequency = (90 if soft else 130) * np.exp(-t * 22) + (43 if soft else 48)
    phase = np.cumsum(frequency) * 2 * np.pi / RATE
    tone = np.sin(phase) * envelope(t, .0015, .13 if soft else .18)
    noise = rng.normal(0, 1, len(t)) * np.exp(-t * 90) * (.035 if soft else .09)
    return tone + noise


def snare(rng, soft=False):
    x = filter_noise(.45, rng, 750 if soft else 1100, 6400)
    t = np.arange(len(x)) / RATE
    wire = x * envelope(t, .0012, .064 if soft else .11)
    body = (np.sin(2 * np.pi * 182 * t) + .35 * np.sin(2 * np.pi * 317 * t)) * np.exp(-t * 29)
    return .33 * wire + .3 * body


def cymbal(rng, duration=.14, crash=False):
    x = filter_noise(duration, rng, 4900 if crash else 6200, 12000)
    t = np.arange(len(x)) / RATE
    return x * envelope(t, .002, .42 if crash else .035) * .13


def tom(rng):
    t = np.arange(int(.8 * RATE)) / RATE
    freq = 76 + 50 * np.exp(-t * 24)
    phase = np.cumsum(freq) * 2 * np.pi / RATE
    return (np.sin(phase) + .17 * np.sin(2.7 * phase)) * envelope(t, .001, .2)


def bass_note(pitch, duration, dark=False, airy=False):
    n = int((duration + .12) * RATE)
    t = np.arange(n) / RATE
    hz = 440 * 2 ** ((pitch - 69) / 12)
    # Warm harmonic oscillators, without a raw saw/square or piercing resonant sweep.
    x = np.sin(2 * np.pi * hz * t)
    for k, a in [(2, .26), (3, .10), (4, .04)]:
        x += a * np.sin(2 * np.pi * hz * k * t + k * .13)
    e = (1 - np.exp(-t / (.024 if airy else .004)))
    e *= np.where(t < duration, .72 + .28 * np.exp(-t * 7), np.exp(-(t - duration) * 34))
    if dark: e *= np.exp(-t * 3.8)
    return x * e


def electronic_layers(score):
    kind, bpm, beats = score["id"], score["bpm"], score["beats"]
    sec = 60 / bpm
    audio = np.zeros((round(beats * sec * RATE), 2), np.float64)
    rng = np.random.default_rng(SEED + ["lobby", "victory-near", "defeat-near"].index(kind))
    for bar in range(score["bars"]):
        root, chord = CHORDS[score["harmony"][bar // 2]]
        start = bar * 4
        if kind == "lobby":
            for b in [0, 2.5]:
                circular_add(audio, kick(rng, True), (start + b) * sec, gain=.072 if bar >= 8 else .051)
            circular_add(audio, snare(rng, True), (start + 3) * sec, pan=-.05, gain=.045)
            for b in [1, 1.5, 3.5]:
                circular_add(audio, cymbal(rng), (start + b + .012) * sec, pan=.30, gain=.19)
            for b in [0, 2]:
                circular_add(audio, bass_note(root, 1.6 * sec, airy=True), (start + b) * sec, gain=.040)
        elif kind == "victory-near":
            for b in [0, 1.5, 2, 3.5]:
                circular_add(audio, kick(rng), (start + b) * sec, gain=.19 if b in [0, 2] else .095)
            for b in [1, 3]: circular_add(audio, snare(rng), (start + b + .014) * sec, gain=.18)
            if bar % 4 == 3:
                for b in [3.25, 3.5, 3.75]:
                    circular_add(audio, snare(rng, True), (start + b) * sec, pan=-.07, gain=.065)
                circular_add(audio, tom(rng), (start + 3.5) * sec, pan=-.30, gain=.09)
            for j in range(8):
                circular_add(audio, cymbal(rng), (start + .5 * j + .008 * (j % 2)) * sec,
                             pan=.3 if j % 2 else -.2, gain=.35 if j % 2 else .22)
            if bar % 8 == 0:
                circular_add(audio, cymbal(rng, 2.2, True), start * sec, pan=.2, gain=.43)
            for j in range(8):
                p = root + (12 if j in [3, 7] else 0)
                circular_add(audio, bass_note(p, .33 * sec), (start + .5 * j) * sec, gain=.066 if j % 2 == 0 else .044)
        else:
            for b in [0, .75, 2, 2.75]:
                circular_add(audio, kick(rng, True), (start + b) * sec, gain=.16 if b % 2 == 0 else .085)
            circular_add(audio, snare(rng, True), (start + 3) * sec, pan=-.12, gain=.10)
            if bar % 4 == 3:
                circular_add(audio, tom(rng), (start + 3.5) * sec, pan=.18, gain=.085)
            for j in range(8):
                circular_add(audio, cymbal(rng), (start + .5 * j + .012) * sec,
                             pan=.35 if j % 2 else -.3, gain=.20 if j % 2 else .11)
            for j in range(8):
                circular_add(audio, bass_note(root + (7 if j == 7 else 0), .30 * sec, dark=True),
                             (start + .5 * j) * sec, gain=.072 if j % 2 == 0 else .041)
            if bar % 8 == 7:
                circular_add(audio, cymbal(rng, 1.8, True), (start + 3.5) * sec, pan=-.2, gain=.20)
    return audio


def tonal_shape(x):
    n = len(x); freq = np.fft.rfftfreq(n, 1 / RATE)
    response = (1 - 1 / (1 + (freq / 32) ** 4)) / (1 + (freq / 11500) ** 8)
    # Clear muddy lower mids and leave room for speech / weapon transients.
    response *= 1 - .23 * np.exp(-.5 * ((np.log2(np.maximum(freq, 1) / 260)) / .70) ** 2)
    response *= 1 - .09 * np.exp(-.5 * ((np.log2(np.maximum(freq, 1) / 3200)) / .65) ** 2)
    return np.fft.irfft(np.fft.rfft(x, axis=0) * response[:, None], n, axis=0)


def room(x, kind):
    # Circular delay network: the tail from the final bar feeds the first bar.
    wet = np.zeros_like(x)
    amount = .36 if kind == "lobby" else .24 if kind == "victory-near" else .30
    for j, delay in enumerate([.067, .103, .151, .219, .317, .433, .593, .797, 1.071, 1.391, 1.791, 2.191]):
        gain = amount * math.exp(-delay / .80) * .22
        wet[:, 0] += np.roll(x[:, j % 2], round(delay * RATE)) * gain
        wet[:, 1] += np.roll(x[:, 1 - j % 2], round((delay + .013) * RATE)) * gain
    freq = np.fft.rfftfreq(len(x), 1 / RATE)
    warm = 1 / (1 + (freq / 3800) ** 4)
    wet = np.fft.irfft(np.fft.rfft(wet, axis=0) * warm[:, None], len(x), axis=0)
    return x + wet


def master(x, target_db=-19.5):
    x -= np.mean(x, axis=0)
    rms = np.sqrt(np.mean(x * x))
    x *= 10 ** (target_db / 20) / max(rms, 1e-8)
    # Smooth peak compression, then repeat matching: retain phrasing and drum dynamics.
    x = .72 * np.tanh(x / .72)
    x *= 10 ** (target_db / 20) / max(np.sqrt(np.mean(x * x)), 1e-8)
    limit = 10 ** (-1.5 / 20)
    if np.max(np.abs(x)) > limit:
        x *= limit / np.max(np.abs(x))
    # The native bank's random oscillator phases differ between passes. A short
    # DC-correction ramp removes only the endpoint jump (not a silent fade).
    m = round(.015 * RATE)
    jump = x[0] - x[-1]
    taper = .5 * (1 + np.cos(np.linspace(0, np.pi, m)))
    x[:m] -= taper[:, None] * jump
    x -= np.mean(x, axis=0)
    return x


def metrics(x):
    peak = float(np.max(np.abs(x)))
    rms = float(np.sqrt(np.mean(x * x)))
    blocks = x[:len(x) // RATE * RATE].reshape(-1, RATE, 2)
    block_rms = np.sqrt(np.mean(blocks * blocks, axis=(1, 2)))
    return dict(durationSeconds=round(len(x) / RATE, 6), channels=2, sampleRate=RATE,
                bitDepth=16, peakDbfs=round(20 * math.log10(peak), 3),
                rmsDbfs=round(20 * math.log10(rms), 3),
                dcOffset=[round(float(a), 8) for a in np.mean(x, axis=0)],
                loopBoundaryJumpMax=round(float(np.max(np.abs(x[0] - x[-1]))), 9),
                minimumOneSecondRmsDbfs=round(20 * math.log10(float(np.min(block_rms))), 3),
                maximumOneSecondRmsDbfs=round(20 * math.log10(float(np.max(block_rms))), 3),
                clippedSamples=int(np.sum(np.abs(x) >= 1)),
                nonzeroFraction=round(float(np.mean(np.abs(x) > 1 / 32768)), 6))


def write_meta(path, folder=False):
    meta = Path(str(path) + ".meta")
    if meta.exists(): return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "ironfront-original-music/" + path.name).hex
    if folder:
        content = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    else:
        content = f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.8
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  ambisonic: 0
  loadInBackground: 1
  preloadAudioData: 0
  3D: 0
  userData: Original IRONFRONT score; stereo streaming music
  assetBundleName:
  assetBundleVariant:
"""
    meta.write_text(content)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--renderer", type=Path, help="precompiled render-native-instrument.swift")
    p.add_argument("--track", choices=["lobby", "victory-near", "defeat-near"], action="append")
    args = p.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    write_meta(OUT, True)
    source = ROOT / "scripts/render-native-instrument.swift"
    renderer = args.renderer or Path(tempfile.gettempdir()) / "ironfront-gm-render"
    if not args.renderer:
        subprocess.run(["swiftc", "-module-cache-path", str(Path(tempfile.gettempdir()) / "ironfront-music-swift-cache"),
                        str(source), "-o", str(renderer)], check=True)
    selected = args.track or ["lobby", "victory-near", "defeat-near"]
    result = []
    with tempfile.TemporaryDirectory(prefix="ironfront-original-music-") as tmp:
        for kind in selected:
            score = make_score(kind)
            # Score is reproducible editable source, located outside Resources to
            # avoid shipping thousands of unnecessary note-event strings.
            source_dir = ROOT / "docs/music-scores"
            source_dir.mkdir(parents=True, exist_ok=True)
            (source_dir / f"{kind}.json").write_text(json.dumps(score, ensure_ascii=False, indent=2) + "\n")
            score_file = Path(tmp) / f"{kind}.json"
            raw_file = Path(tmp) / f"{kind}.raw"
            score_file.write_text(json.dumps(score))
            subprocess.run([str(renderer), str(score_file), str(raw_file)], check=True)
            native = np.fromfile(raw_file, dtype="<f4").reshape(-1, 2).astype(np.float64)
            loop_length = round(score["beats"] * 60 / score["bpm"] * RATE)
            native = native[loop_length:2 * loop_length]
            if len(native) != loop_length: raise RuntimeError("Native renderer length mismatch")
            x = master(room(tonal_shape(native * 1.8 + electronic_layers(score)), kind))
            pcm = np.round(np.clip(x, -1, 1) * 32767).astype("<i2")
            output = OUT / f"{kind}.wav"
            with wave.open(str(output), "wb") as w:
                w.setnchannels(2); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(pcm.tobytes())
            write_meta(output)
            qa = metrics(pcm.astype(np.float64) / 32768)
            qa.update(id=kind, title=score["title"], bpm=score["bpm"], bars=score["bars"],
                      sha256=hashlib.sha256(output.read_bytes()).hexdigest())
            result.append(qa)
            print(json.dumps(qa, ensure_ascii=False))
    report_path = ROOT / "docs/UNITY_MUSIC_MANIFEST_047.json"
    if args.track and report_path.exists():
        previous = json.loads(report_path.read_text())
        result = [t for t in previous["tracks"] if t["id"] not in selected] + result
    report = dict(version="0.47.0", authorship="Original procedural compositions for IRONFRONT; no vocals, no existing song or commercial loop.",
                  sharedThemeMidi=THEME, sharedTheme="D4–F4–G4–A4 with original phrase responses and variations",
                  rendering="Apple AVAudioUnitSampler renders original note events using the host-installed macOS GM bank; bank not redistributed. Original NumPy percussion, warm harmonic bass, circular room DSP.",
                  loopMethod="Three native performance passes retain the middle pass; percussion/bass/tails and reverb wrap circularly; short endpoint correction prevents clicks without silence.",
                  sourceFiles=["scripts/generate-unity-music.py", "scripts/render-native-instrument.swift", "docs/music-scores/*.json"],
                  tracks=sorted(result, key=lambda t: ["lobby", "victory-near", "defeat-near"].index(t["id"])))
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")


if __name__ == "__main__": main()
