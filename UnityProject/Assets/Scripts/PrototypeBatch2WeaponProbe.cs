#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Runs only when explicitly requested on a development player.
    public sealed class PrototypeBatch2WeaponProbe:MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        string output;int errors,failedChecks;Camera camera;readonly List<string> checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-ironfrontBatch2Probe")
                new GameObject("Six replacement weapons integration probe").AddComponent<PrototypeBatch2WeaponProbe>().output=Path.GetFullPath(args[i+1]);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);Application.runInBackground=true;Time.captureDeltaTime=1f/30f;Application.logMessageReceived+=Observe;
            var work=Verify();Exception failure=null;
            while(true)
            {
                bool more=false;object current=null;try{more=work.MoveNext();if(more)current=work.Current;}catch(Exception e){failure=e;}
                if(!more||failure!=null)break;yield return current;
            }
            bool pass=failure==null&&errors==0&&failedChecks==0;
            File.WriteAllText(Path.Combine(output,"results.txt"),(pass?"PASS":"FAIL")+"\nVersion="+Application.version+"\nRuntimeErrors="+errors+"\n"+string.Join("\n",checks)+(failure==null?"":"\n"+failure));
            Debug.Log("BATCH2_RUNTIME_"+(pass?"PASS":"FAIL"));Application.logMessageReceived-=Observe;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(pass?0:1);
            #else
            Application.Quit(pass?0:1);
            #endif
        }
        IEnumerator Verify()
        {
            PrototypeRuntime game=null;
            for(int i=0;i<600;i++){game=FindAnyObjectByType<PrototypeRuntime>();if(game!=null&&game.Player!=null)break;yield return null;}
            Check(game!=null&&game.Player!=null,"Runtime starts");game.BeginBattle();game.Frontend.Close();game.SetSupportMenu(false);
            var player=game.Player;player.enabled=false;
            foreach(var b in game.Bots){b.enabled=false;b.gameObject.SetActive(false);}
            foreach(var v in game.Vehicles.OfType<MonoBehaviour>())v.gameObject.SetActive(false);
            Vector3 center=new Vector3(-325,PrototypeLayout.HeightAt(-325,-320)+.3f,-320);
            player.transform.SetPositionAndRotation(center,Quaternion.identity);camera=player.ViewCamera;camera.enabled=false;
            foreach(var id in new[]{PrototypeWeaponId.BattleRifle,PrototypeWeaponId.Lmg,PrototypeWeaponId.Marksman,PrototypeWeaponId.Pistol,PrototypeWeaponId.Sniper,PrototypeWeaponId.Smg})
            {
                var role=id==PrototypeWeaponId.Smg?PrototypeInfantryClass.Medic:id==PrototypeWeaponId.Lmg?PrototypeInfantryClass.Engineer:
                    id==PrototypeWeaponId.Marksman||id==PrototypeWeaponId.Sniper?PrototypeInfantryClass.Recon:PrototypeInfantryClass.Assault;
                player.ApplyLoadout(new PrototypeLoadout{role=role,primary=id==PrototypeWeaponId.Pistol?PrototypeWeaponId.Carbine:id,secondary=PrototypeWeaponId.Pistol,throwable=PrototypeThrowableId.Frag});
                if(id==PrototypeWeaponId.Pistol)Invoke(player,"EquipSecondary",true);
                var soldier=player.GetComponent<PrototypeSoldierVisual>();soldier.enabled=false;
                var fp=((GameObject)Get(player,"firstPersonWeapon")).GetComponent<PrototypeWeaponArtRig>();
                var tp=soldier.HeldWeapon.GetComponent<PrototypeWeaponArtRig>();
                Check(player.ActiveWeapon.Id==id&&fp!=null&&tp!=null,id+" inventory uses authored FP and TP prefabs");
                Check(tp.GetComponent<LODGroup>().GetLODs().Length==3,id+" world has three distance LODs");
                Check(fp.Magazine.GetComponentInChildren<MeshRenderer>()!=null,id+" moving magazine has actual geometry");
                foreach(var renderer in fp.GetComponentsInChildren<MeshRenderer>())
                    foreach(var mat in renderer.sharedMaterials)Check(mat!=null&&mat.shader.isSupported,id+" supported material "+mat.name);
                if(!player.IsFirstPerson)player.TogglePerspective();
                foreach(bool aim in new[]{false,true})
                {
                    Set(player,"aiming",aim);
                    for(int i=0;i<40;i++){Invoke(player,"UpdateInfantryPresentation");soldier.RefreshPose(true);yield return null;}
                    if(aim&&fp.UsesScope)
                    {
                        Check(fp.ScopeViewActive,id+" optical scope view active");
                        Check(!fp.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled),id+" scope view clears foreground geometry");
                        camera.enabled=true;yield return new WaitForEndOfFrame();
                        var screen=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,id+"-ads.png"),screen.EncodeToPNG());Destroy(screen);camera.enabled=false;
                    }
                    else yield return Capture(id+(aim?"-ads":"-hip"));
                    if(aim)
                    {
                        Vector3 v=camera.WorldToViewportPoint(fp.Sight.position);
                        Check(Vector2.Distance(new Vector2(v.x,v.y),Vector2.one*.5f)<.006f,id+" aim anchor centered "+v);
                        foreach(var mesh in fp.GetComponentsInChildren<MeshFilter>())
                        {
                            if(mesh.name=="Sight aiming dot"||mesh.GetComponentInParent<PrototypeSoldierArtRig>()!=null)continue;
                            var collider=mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;Physics.SyncTransforms();
                            bool blocked=collider.Raycast(new Ray(camera.transform.position,camera.transform.forward),out var hit,2f);
                            DestroyImmediate(collider);Check(!blocked,id+" ADS clear through "+mesh.transform.parent.name);
                        }
                    }
                    Check(Vector3.Distance(fp.RightGrip.position,fp.GetComponentInChildren<PrototypeSoldierArtRig>().Bindings[8].Bone.position)<.005f,id+" first person hand attached");
                }
                Set(player,"aiming",false);Set(player,"lastShotAt",-10f);
                Vector3 home=fp.Magazine.localPosition;float maxDrop=0,maxHand=0;
                player.ActiveKit.ApplyNetworkAmmo(4,player.ActiveWeapon.Reserve);
                for(int frame=0;frame<=100;frame++)
                {
                    float phase=frame/100f;
                    Set(player.ActiveKit,"reloadEndsAt",frame==100?0f:Time.time+(1-phase)*player.ActiveWeapon.ReloadSeconds);
                    Invoke(player,"UpdateInfantryPresentation");soldier.RefreshPose(true);
                    maxDrop=Mathf.Max(maxDrop,Vector3.Distance(home,fp.Magazine.localPosition));
                    maxHand=Mathf.Max(maxHand,Vector3.Distance(fp.GetComponentInChildren<PrototypeSoldierArtRig>().Bindings[7].Bone.position,fp.SupportPosition));
                    if(frame==40||frame==87)yield return Capture(id+"-reload-"+frame);else yield return null;
                }
                Check(maxDrop>.15f&&Vector3.Distance(home,fp.Magazine.localPosition)<.001f,id+" magazine detaches and returns without drift");
                Check(maxHand<.006f,id+" reload hand follows grip "+maxHand);
                for(int i=0;i<24;i++){Invoke(player,"UpdateInfantryPresentation");yield return null;}
                int ammo=player.Ammo;Vector3 muzzle=fp.Muzzle.position;Invoke(player,"Fire");
                Check(player.Ammo==ammo-1,id+" firing consumes one round");
                Check(FindObjectsByType<PrototypeBallisticProjectile>(FindObjectsSortMode.None).Any(b=>Vector3.Distance(b.transform.position,muzzle)<.002f),id+" projectile originates at authored muzzle");
                if(id==PrototypeWeaponId.Pistol)
                {
                    fp.SetPresentation(false,0,.04f);Check(fp.Bolt.localPosition.z<fp.ChargingHandle.localPosition.z-.03f,"Pistol slide recoils");yield return Capture(id+"-slide");fp.SetPresentation(false,0,10);
                }
                for(int i=0;i<12;i++){Invoke(player,"UpdateInfantryPresentation");yield return null;}
                player.TogglePerspective();Invoke(player,"UpdateInfantryPresentation");soldier.RefreshPose(true);
                camera.transform.position=center+new Vector3(2.2f,1.5f,2.1f);camera.transform.LookAt(center+Vector3.up*1.05f);camera.fieldOfView=44;
                yield return Capture(id+"-world");
                Check(Vector3.Distance(((Transform)Get(soldier,"rightHand")).position,tp.RightGrip.position)<.02f,id+" third person hand attached");
                using(var preview=new PrototypeWeaponMenuPreview(game,null))
                    File.WriteAllBytes(Path.Combine(output,id+"-menu.png"),preview.Get(id).EncodeToPNG());
            }
            var existing=PrototypeWeaponVisual.Build(null,PrototypeWeaponId.Carbine,game);
            Check(existing.GetComponent<PrototypeWeaponArtRig>()!=null&&existing.name.StartsWith("IF27"),"Existing IF27 still uses its original authored prefab");Destroy(existing);
        }
        IEnumerator Capture(string name)
        {
            yield return null;
            var rt=new RenderTexture(960,640,24);var previous=RenderTexture.active;var old=camera.targetTexture;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(960,640,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,960,640),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=old;rt.Release();Destroy(rt);Destroy(texture);
        }
        void Check(bool value,string detail){if(!value)failedChecks++;checks.Add((value?"OK ":"FAIL ")+detail);}
        void Observe(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
        static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
        static object Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Hidden).Invoke(o,args);
    }
}
#endif
