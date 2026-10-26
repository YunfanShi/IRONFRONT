import subprocess,json
from pathlib import Path
root=Path(__file__).resolve().parent
repo=root.parents[2]
exe=repo/'UnityProject/Builds/RigFix-0.66.1/IRONFRONT.app/Contents/MacOS/IRONFRONT Unity Prototype'
results=[]
for label,flag,extra in [('rig','-ironfrontRigAudit',[]),('batch2','-ironfrontBatch2Probe',[]),('if27','-ironfrontWeaponProbe',[]),('slide','-ironfrontSlideProbe',[]),('sprint','-ironfrontSprintProbe',[]),('menu','-ironfrontMenuPoseProbe',[]),('motion','-ironfrontRigAudit',['-rigMotion'])]:
 out=root/'verified'/label;out.mkdir(parents=True,exist_ok=True)
 cmd=[str(exe),flag,str(out),*extra,'-screen-width','1440','-screen-height','960','-screen-fullscreen','0','-logFile',str(out.with_suffix('.log'))]
 try:
  proc=subprocess.run(cmd,timeout=240,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
  report=(out/'results.txt').read_text() if (out/'results.txt').exists() else 'MISSING RESULTS'
  results.append({'probe':label,'exitCode':proc.returncode,'report':report})
 except Exception as e:results.append({'probe':label,'exception':str(e)})
 print(label,results[-1],flush=True)
 (root/'verified/summary.json').write_text(json.dumps(results,indent=2))
