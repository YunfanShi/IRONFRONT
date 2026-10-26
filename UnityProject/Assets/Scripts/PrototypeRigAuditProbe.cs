#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace Ironfront.UnityPrototype
{
    // Opt-in visual and evaluated-skin audit, independent of grip-marker tests.
    public sealed class PrototypeRigAuditProbe : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        string output; int errors, failures; Camera camera;
        readonly List<string> checks=new List<string>();
        readonly List<string> samples=new List<string>{"team,weapon,pose,arm,upper_error,forearm_error,max_skin_edge_ratio,p95_skin_edge_ratio"};
        [Serializable] class Sweep { public Fit[] fits; }
        [Serializable] class Fit { public string name; public Vector3 rightOffset,rightAngles,leftOffset,leftAngles,fingerAngles; public bool isolateRight; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){var a=Environment.GetCommandLineArgs();for(int i=0;i+1<a.Length;i++)if(a[i]=="-ironfrontRigAudit")new GameObject("Evaluated rig audit").AddComponent<PrototypeRigAuditProbe>().output=Path.GetFullPath(a[i+1]);}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);Application.runInBackground=true;Time.captureDeltaTime=1f/30f;Application.logMessageReceived+=Observe;
            var work=Verify();Exception failure=null;
            while(true){bool more=false;object current=null;try{more=work.MoveNext();if(more)current=work.Current;}catch(Exception e){failure=e;}if(!more||failure!=null)break;yield return current;}
            bool pass=failure==null&&errors==0&&failures==0;
            File.WriteAllText(Path.Combine(output,"results.txt"),(pass?"PASS":"FAIL")+"\nVersion="+Application.version+"\nRuntimeErrors="+errors+"\n"+string.Join("\n",checks)+(failure==null?"":"\n"+failure));
            File.WriteAllLines(Path.Combine(output,"skin-metrics.csv"),samples);Application.logMessageReceived-=Observe;Application.Quit(pass?0:1);
        }
        IEnumerator Verify()
        {
            PrototypeRuntime game=null;for(int i=0;i<600;i++){game=FindAnyObjectByType<PrototypeRuntime>();if(game!=null&&game.Player!=null)break;yield return null;}
            Check(game!=null&&game.Player!=null,"Runtime starts");game.BeginBattle();game.Frontend.Close();game.SetSupportMenu(false);
            var player=game.Player;player.enabled=false;foreach(var b in game.Bots){b.enabled=false;b.gameObject.SetActive(false);}foreach(var v in game.Vehicles.OfType<MonoBehaviour>())v.gameObject.SetActive(false);
            Vector3 center=new Vector3(-325,50.02f,-320);var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=center+Vector3.down*.52f;floor.transform.localScale=new Vector3(40,1,40);floor.GetComponent<Renderer>().sharedMaterial=game.GunMaterial;
            player.GetComponent<CharacterController>().enabled=false;player.transform.SetPositionAndRotation(center,Quaternion.identity);camera=player.ViewCamera;camera.enabled=false;
            bool calibration=Environment.GetCommandLineArgs().Contains("-gripCalibration");
            bool motion=Environment.GetCommandLineArgs().Contains("-rigMotion");
            string[] args=Environment.GetCommandLineArgs();int sweepArg=Array.IndexOf(args,"-gripSweep");
            Sweep sweep=sweepArg>=0?JsonUtility.FromJson<Sweep>(File.ReadAllText(args[sweepArg+1])):null;
            foreach(var team in new[]{PrototypeTeam.Blue,PrototypeTeam.Red})
            foreach(PrototypeWeaponId id in Enum.GetValues(typeof(PrototypeWeaponId)))
            {
                if(calibration&&(team!=PrototypeTeam.Blue||id!=PrototypeWeaponId.Pistol))continue;
                if(motion&&(team!=PrototypeTeam.Blue||(id!=PrototypeWeaponId.Pistol&&id!=PrototypeWeaponId.Carbine)))continue;
                if(sweep!=null&&(team!=PrototypeTeam.Blue||id!=PrototypeWeaponId.Pistol))continue;
                player.SetTeam(team);
                player.GetComponent<CharacterController>().enabled=false;player.transform.SetPositionAndRotation(center,Quaternion.identity);
                var role=id==PrototypeWeaponId.Smg?PrototypeInfantryClass.Medic:id==PrototypeWeaponId.Lmg?PrototypeInfantryClass.Engineer:id==PrototypeWeaponId.Marksman||id==PrototypeWeaponId.Sniper?PrototypeInfantryClass.Recon:PrototypeInfantryClass.Assault;
                player.ApplyLoadout(new PrototypeLoadout{role=role,primary=id==PrototypeWeaponId.Pistol?PrototypeWeaponId.Carbine:id,secondary=PrototypeWeaponId.Pistol,throwable=PrototypeThrowableId.Frag});if(id==PrototypeWeaponId.Pistol)Invoke(player,"EquipSecondary",true);
                var soldier=player.GetComponent<PrototypeSoldierVisual>();soldier.enabled=false;
                var fp=((GameObject)Get(player,"firstPersonWeapon")).GetComponent<PrototypeWeaponArtRig>();var art=fp.GetComponentInChildren<PrototypeSoldierArtRig>();
                string resource="Weapons/"+(id==PrototypeWeaponId.Carbine?"IF27":"Batch2/"+id)+"View";
                var original=Resources.Load<GameObject>(resource).GetComponent<PrototypeWeaponArtRig>();
                float ratio=Vector3.Distance(fp.Muzzle.position,fp.Sight.position)/Vector3.Distance(original.Muzzle.position,original.Sight.position)/fp.transform.lossyScale.x;
                Check(Mathf.Abs(ratio-(id==PrototypeWeaponId.Pistol?1.55f:1.15f))<.001f,team+" "+id+" actual muzzle/sight size ratio="+ratio);
                Check(Mathf.Abs(art.transform.lossyScale.x-.55f/.65f)<.001f,team+" "+id+" arms retain original scale");
                if(!player.IsFirstPerson)player.TogglePerspective();
                if(sweep!=null)
                {
                    for(int i=0;i<35;i++){Invoke(player,"UpdateInfantryPresentation");yield return null;}
                    foreach(var fit in sweep.fits)
                    {
                        fp.RightGrip.position=fp.RightContactPosition+fp.transform.TransformVector(fit.rightOffset);
                        fp.RightGrip.localRotation=Quaternion.Euler(fit.rightAngles);
                        fp.LeftGrip.position=fp.RightContactPosition+fp.transform.TransformVector(fit.leftOffset);
                        fp.LeftHandRotation=Quaternion.Euler(fit.leftAngles);
                        Set(fp,"leftHome",fp.LeftGrip.localPosition);
                        fp.SetPresentation(false,0,100);
                        for(int f=0;f<art.Fingers.Length;f++)art.Fingers[f].localRotation=art.FingerRest[f]*Quaternion.Euler(fit.fingerAngles);
                        foreach(var ar in art.Arms)ar.enabled=!fit.isolateRight||ar.name.EndsWith("_ArmL");
                        yield return null;Capture(fit.name);
                        var observer=new GameObject("Fit contact camera").AddComponent<Camera>();observer.CopyFrom(camera);
                        observer.transform.position=fp.RightContactPosition+fp.transform.TransformDirection(new Vector3(.45f,.2f,.4f));observer.transform.LookAt(fp.RightContactPosition);observer.fieldOfView=30;
                        camera=observer;Capture(fit.name+"-contact");camera=player.ViewCamera;Destroy(observer.gameObject);
                    }
                    continue;
                }
                foreach(string pose in new[]{"hip","ads","reload40","reload87"})
                {
                    Set(player,"aiming",pose=="ads");Set(player,"lastShotAt",-10f);
                    for(int i=0;i<35;i++)
                    {
                        float phase=pose=="reload40"?.4f:pose=="reload87"?.87f:-1f;
                        Set(player.ActiveKit,"reloadEndsAt",phase<0?0f:Time.time+(1-phase)*player.ActiveWeapon.ReloadSeconds);
                        Invoke(player,"UpdateInfantryPresentation");soldier.RefreshPose(true);yield return null;
                    }
                    if(motion&&pose=="hip")
                    {
                        Directory.CreateDirectory(Path.Combine(output,"motion-"+id));
                        for(int f=0;f<180;f++)
                        {
                            bool aim=f>=30&&f<60;float phase=f>=75&&f<165?(f-75)/90f:-1f;
                            Set(player,"aiming",aim);Set(player.ActiveKit,"reloadEndsAt",phase<0?0f:Time.time+(1-phase)*player.ActiveWeapon.ReloadSeconds);
                            Invoke(player,"UpdateInfantryPresentation");soldier.RefreshPose(true);yield return null;
                            Capture("motion-"+id+"/"+f.ToString("D4"));
                        }
                        Set(player,"aiming",false);Set(player.ActiveKit,"reloadEndsAt",0f);
                        for(int f=0;f<35;f++){Invoke(player,"UpdateInfantryPresentation");yield return null;}
                    }
                    string name=team+"-"+id+"-"+pose;
                    if(!fp.ScopeViewActive){AuditSkin(team,id,pose,art);Capture(name);}
                    if((id==PrototypeWeaponId.Pistol||id==PrototypeWeaponId.Carbine)&&pose=="hip")
                    {
                        ExportObj(name,fp,art);
                        Vector3 saved=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
                        // Inspect the same evaluated first-person mesh from the
                        // opposite side without moving its parent weapon.
                        var observer=new GameObject("Grip detail camera").AddComponent<Camera>();observer.CopyFrom(camera);
                        Vector3 aim=fp.RightGrip.position;observer.transform.position=aim+fp.transform.TransformDirection(new Vector3(.5f,.25f,.45f));observer.transform.LookAt(aim);observer.fieldOfView=34;
                        camera=observer;Capture(name+"-contact");camera=player.ViewCamera;
                        if(calibration)
                        {
                            Quaternion rr=fp.RightGrip.localRotation,lr=fp.LeftHandRotation;
                            Vector3[] angles={Vector3.zero,new Vector3(90,0,0),new Vector3(-90,0,0),new Vector3(0,0,90),new Vector3(0,90,0),new Vector3(180,0,0)};
                            for(int a=0;a<angles.Length;a++)
                            {
                                fp.RightGrip.localRotation=rr*Quaternion.Euler(angles[a]);fp.LeftHandRotation=lr*Quaternion.Euler(angles[a]);fp.SetPresentation(false,0,100);
                                yield return null;
                                Capture("calibration-"+a+"-view");camera=observer;Capture("calibration-"+a+"-contact");camera=player.ViewCamera;
                            }
                            fp.RightGrip.localRotation=rr;fp.LeftHandRotation=lr;fp.SetPresentation(false,0,100);
                            for(int f=0;f<art.Fingers.Length;f++)art.Fingers[f].localRotation=art.FingerRest[f];
                            yield return null;
                            Capture("calibration-open-view");camera=observer;Capture("calibration-open-contact");camera=player.ViewCamera;
                        }
                        if(calibration)
                        {
                            // Isolate the anatomical right hand; identify the reflected source-side mesh.
                            foreach(var ar in art.Arms)ar.enabled=ar.name.EndsWith("_ArmL");
                            Vector3 rp=fp.RightGrip.localPosition;
                            Vector3[] variants={new Vector3(-60,0,0),new Vector3(-60,180,0),new Vector3(60,0,0),new Vector3(60,180,0),new Vector3(-90,0,0),new Vector3(90,180,0),new Vector3(-30,0,0),new Vector3(30,180,0)};
                            for(int variant=0;variant<variants.Length;variant++)
                            {
                                fp.RightGrip.localPosition=rp+new Vector3(0,-.075f,-.09f);
                                fp.RightGrip.localRotation=Quaternion.Euler(variants[variant]);
                                fp.SetPresentation(false,0,100);
                                for(int f=0;f<art.Thumbs.Length;f++)art.Thumbs[f].localRotation=art.ThumbRest[f];
                                yield return null;Capture("isolated-"+variant+"-view");camera=observer;Capture("isolated-"+variant+"-contact");camera=player.ViewCamera;
                            }
                            fp.RightGrip.localPosition=rp;

                        }
                        Destroy(observer.gameObject);
                    }
                }
                Set(player,"aiming",false);Set(player.ActiveKit,"reloadEndsAt",0f);player.Slide.Reset();player.TogglePerspective();
                foreach(string stance in new[]{"stand","crouch","prone","slide"})
                {
                    Set(player,"crouched",stance=="crouch");Set(player,"prone",stance=="prone");
                    if(stance=="slide"){player.Slide.TryStart(6.6f,Vector3.forward,true,true);player.Slide.StartedAt=Time.time-.45f;player.Slide.EndsAt=Time.time+.63f;}
                    for(int i=0;i<18;i++){soldier.RefreshPose(true);yield return null;}
                    camera.transform.position=center+new Vector3(1.9f,1.1f,2.5f);camera.transform.LookAt(center+Vector3.up*(stance=="prone"?.3f:.95f));camera.fieldOfView=40;Capture(team+"-"+id+"-"+stance);
                    player.Slide.Reset();
                }
                Set(player,"crouched",false);Set(player,"prone",false);
            }
        }
        void AuditSkin(PrototypeTeam team,PrototypeWeaponId id,string pose,PrototypeSoldierArtRig art)
        {
            for(int i=0;i<2;i++)
            {
                int a=i==0?3:4,b=i==0?5:6,c=i==0?7:8;float scale=art.transform.lossyScale.x;
                float upper=Mathf.Abs(Vector3.Distance(art.Bindings[a].Bone.position,art.Bindings[b].Bone.position)-art.UpperArmLength*scale);
                float lower=Mathf.Abs(Vector3.Distance(art.Bindings[b].Bone.position,art.Bindings[c].Bone.position)-art.ForearmLength*scale);
                Check(upper<.001f&&lower<.001f,team+" "+id+" "+pose+" "+i+" arm length preserved");
                var renderer=art.Arms[i];var rest=renderer.sharedMesh.vertices;var baked=new Mesh();renderer.BakeMesh(baked);var posed=baked.vertices;var triangles=baked.triangles;var ratios=new List<float>();
                if(pose=="ads")
                {
                    var proxy=new GameObject("Evaluated hand sight-line check");proxy.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);
                    proxy.transform.localScale=renderer.transform.lossyScale;
                    var collider=proxy.AddComponent<MeshCollider>();collider.sharedMesh=baked;Physics.SyncTransforms();
                    Check(!collider.Raycast(new Ray(camera.transform.position,camera.transform.forward),out var hit,2f),team+" "+id+" hands clear sight line "+i);
                    DestroyImmediate(proxy);
                }
                for(int t=0;t<triangles.Length;t+=3)for(int e=0;e<3;e++)
                {int v=triangles[t+e],w=triangles[t+(e+1)%3];float before=Vector3.Distance(rest[v],rest[w]);if(before>.003f)ratios.Add(Vector3.Distance(posed[v],posed[w])/before);}
                ratios.Sort();samples.Add(string.Join(",",team,id,pose,i,upper,lower,ratios.Last(),ratios[(int)(ratios.Count*.95f)]));Destroy(baked);
            }
        }
        void ExportObj(string name,PrototypeWeaponArtRig fp,PrototypeSoldierArtRig art)
        {
            var sb=new StringBuilder();int offset=1;
            Action<string,Mesh,Transform> add=(label,mesh,tr)=>{sb.AppendLine("o "+label);foreach(var v in mesh.vertices){Vector3 p=fp.transform.InverseTransformPoint(tr.TransformPoint(v));sb.AppendLine($"v {p.x} {p.y} {p.z}");}var ts=mesh.triangles;for(int i=0;i<ts.Length;i+=3)sb.AppendLine($"f {ts[i]+offset} {ts[i+1]+offset} {ts[i+2]+offset}");offset+=mesh.vertexCount;};
            foreach(var r in art.Arms.Take(2)){var m=new Mesh();r.BakeMesh(m);add(r.name,m,r.transform);Destroy(m);}
            foreach(var m in fp.GetComponentsInChildren<MeshFilter>())if(m.GetComponentInParent<PrototypeSoldierArtRig>()==null)add(m.transform.parent.name,m.sharedMesh,m.transform);
            File.WriteAllText(Path.Combine(output,name+".obj"),sb.ToString());
            var bones=art.Bindings.Select(b=>b.Bone.name+" "+fp.transform.InverseTransformPoint(b.Bone.position).ToString("F5")+" rotation "+(Quaternion.Inverse(fp.transform.rotation)*b.Bone.rotation).eulerAngles.ToString("F2"));File.WriteAllLines(Path.Combine(output,name+"-bones.txt"),bones);
        }
        void Capture(string name)
        {
            var rt=new RenderTexture(1440,960,24);var prev=RenderTexture.active;var old=camera.targetTexture;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var t=new Texture2D(1440,960,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1440,960),0,0);t.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),t.EncodeToPNG());RenderTexture.active=prev;camera.targetTexture=old;rt.Release();Destroy(rt);Destroy(t);
        }
        void Check(bool v,string label){if(!v)failures++;checks.Add((v?"OK ":"FAIL ")+label);}
        void Observe(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
        static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
        static object Invoke(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Hidden).Invoke(o,a);
    }
}
#endif
