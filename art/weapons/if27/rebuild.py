"""Reproduce the weapon from the preserved source. Does not edit Unity files."""
import subprocess,os
from pathlib import Path
R=Path(__file__).resolve().parent
blender=os.environ.get('BLENDER_EXECUTABLE','/Applications/Blender.app/Contents/MacOS/Blender')
for script in ('prepare.py','build.py','bake_normal.py','build.py'):
    subprocess.run([blender,'--background','--factory-startup','--disable-autoexec','--python-exit-code','1','--python',str(R/script)],check=True)
