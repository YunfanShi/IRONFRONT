# Audio sources

- UI clicks / confirmation / switch / error: Kenney Interface Sounds, CC0. https://kenney.nl/assets/interface-sounds
- Mechanical reload recordings: SpringySpringo, CC0. https://opengameart.org/content/gun-reload-sounds
- Seven weapon reports, cannon, explosion, footsteps, near-miss, impacts, cloth, landing, wind: generated specifically for IRONFRONT; no third-party recordings. Generation recipe in scripts/generate-sfx.py.
- Eleven music tracks: supplied by the user, retained locally and in the full personal ZIP. Public redistribution permission is not asserted. Audio files are excluded from the public source package and GitHub. Track mapping: docs/MUSIC_MANIFEST.json.

## 0.9.0 Recorded firearms and explosion

14 gunshot clips derived from The Free Firearm Sound Library, recorded by Ben Jaszczak, Brian Nelson, Kevin Heras and Matthew Nanney. Source: https://opengameart.org/node/21826 . CC0. Selection and original filenames: docs/RECORDED_SFX_MANIFEST.json. Trimmed first gunshot onset, mono 44.1 kHz PCM, peak normalized to 0.88, short tail fade. Seven fictional weapons each use two distinct takes; recordings do not imply those fictional weapons represent the recorded real firearm.

Explosion impact sample: EZduzziteh, Explosions, https://opengameart.org/content/explosions-4 . CC0. Converted explosion1.ogg to mono PCM and lowered gain. This is designed explosion audio, not claimed to be field-recorded live artillery. Engine, cannon launch, impact, wind and some movement sounds retain procedural layers/fallbacks. No Battlefield or Delta Force audio was extracted.
