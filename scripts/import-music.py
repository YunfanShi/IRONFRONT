"""Import the user's local music; never adds music files to Git."""
from pathlib import Path
import argparse,json,subprocess
p=argparse.ArgumentParser();p.add_argument('source',type=Path);a=p.parse_args()
root=Path(__file__).resolve().parents[1];tracks=json.loads((root/'docs/MUSIC_MANIFEST.json').read_text());out=root/'public/audio/music';out.mkdir(parents=True,exist_ok=True)
for t in tracks:
 source=a.source/t['original']
 if not source.exists():raise SystemExit(f"Missing source: {source.name}")
 subprocess.run(['ffmpeg','-nostdin','-v','error','-y','-i',str(source),'-map_metadata','-1','-af','loudnorm=I=-20:TP=-2:LRA=11','-ar','44100','-codec:a','libmp3lame','-b:a','160k',str(out/(t['id']+'.mp3'))],check=True)
print(f'Imported {len(tracks)} tracks locally. MP3 files are Git-ignored.')
