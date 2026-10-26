import subprocess,json,sys
from pathlib import Path
root=Path(__file__).resolve().parent;repo=root.parents[2]
exe=repo/'UnityProject/Builds/WeaponFit-0.66.2/IRONFRONT.app/Contents/MacOS/IRONFRONT Unity Prototype'
selected=sys.argv[1:] or ['rig','batch2','if27','sprint','menu','equipment']
flags={'rig':'-ironfrontRigAudit','batch2':'-ironfrontBatch2Probe','if27':'-ironfrontWeaponProbe','sprint':'-ironfrontSprintProbe','menu':'-ironfrontMenuPoseProbe','equipment':'-ironfrontEquipmentArtProbe','motion':'-ironfrontRigAudit'}
results=[]
for label in selected:
 out=root/'checks'/label;out.mkdir(parents=True,exist_ok=True)
 cmd=[str(exe),flags[label],str(out),*(['-rigMotion'] if label=='motion' else []),'-screen-width','1440','-screen-height','960','-screen-fullscreen','0','-logFile',str(out.with_suffix('.log'))]
 try:
  proc=subprocess.run(cmd,timeout=240,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
  report=(out/'results.txt').read_text() if (out/'results.txt').exists() else 'MISSING RESULTS'
  results.append({'probe':label,'exitCode':proc.returncode,'report':report})
 except Exception as e:results.append({'probe':label,'exception':str(e)})
 print(label, str(results[-1].get('exitCode','exception')), results[-1].get('report',results[-1].get('exception','')).splitlines()[:3],flush=True)
 (root/('summary-'+selected[0]+'.json')).write_text(json.dumps(results,indent=2))
