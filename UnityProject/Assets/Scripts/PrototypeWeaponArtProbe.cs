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
    public sealed class PrototypeWeaponArtProbe:MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        string output;int errors;Camera camera;
        readonly List<string> checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-ironfrontWeaponProbe")
                new GameObject("IF27 integration verification").AddComponent<PrototypeWeaponArtProbe>().output=Path.GetFullPath(args[i+1]);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);Application.runInBackground=true;Time.captureDeltaTime=1f/30f;
            Application.logMessageReceived+=Observe;
            var work=Verify();Exception failure=null;
            while(true)
            {
                bool more=false;object current=null;try{more=work.MoveNext();if(more)current=work.Current;}catch(Exception e){failure=e;}
                if(!more||failure!=null)break;yield return current;
            }
            bool pass=failure==null&&errors==0;
            File.WriteAllText(Path.Combine(output,"results.txt"),(pass?"PASS":"FAIL")+"\nVersion="+Application.version+"\nRuntimeErrors="+errors+"\n"+string.Join("\n",checks)+(failure==null?"":"\n"+failure));
            Debug.Log("IF27_RUNTIME_"+(pass?"PASS":"FAIL"));Application.logMessageReceived-=Observe;
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
            var player=game.Player;player.enabled=false;player.ApplyLoadout(new PrototypeLoadout{role=PrototypeInfantryClass.Assault,primary=PrototypeWeaponId.Carbine,secondary=PrototypeWeaponId.Pistol,throwable=PrototypeThrowableId.Frag});
            foreach(var b in game.Bots){b.enabled=false;b.gameObject.SetActive(false);}
            foreach(var v in game.Vehicles.OfType<MonoBehaviour>())v.gameObject.SetActive(false);
            Vector3 center=new Vector3(-325,PrototypeLayout.HeightAt(-325,-320)+.3f,-320);
            player.transform.SetPositionAndRotation(center,Quaternion.identity);
            var soldier=player.GetComponent<PrototypeSoldierVisual>();soldier.enabled=false;
            camera=player.ViewCamera;camera.enabled=false;
            var fp=((GameObject)Get(player,"firstPersonWeapon")).GetComponent<PrototypeWeaponArtRig>();
            var tp=soldier.HeldWeapon.GetComponent<PrototypeWeaponArtRig>();
            Check(fp!=null&&tp!=null,"Actual inventory instantiates both authored weapons");
            Check(tp.GetComponent<LODGroup>().GetLODs().Length==3,"World gun has three distance LODs");
            Check(fp.Sight.Find("Sight aiming dot").lossyScale.magnitude<.005f,"Reticle stays millimetre scale after FBX import");
            if(!player.IsFirstPerson)player.TogglePerspective();
            foreach(string pose in new[]{"hip","ads"})
            {
                Set(player,"aiming",pose=="ads");
                for(int i=0;i<36;i++){Invoke(player,"UpdateInfantryPresentation");soldier.RefreshPose(true);yield return null;}
                yield return Capture("fp-"+pose);
                if(pose=="ads")
                {
                    Vector3 v=camera.WorldToViewportPoint(fp.Sight.position);
                    Check(Vector2.Distance(new Vector2(v.x,v.y),Vector2.one*.5f)<.003f,"Sight aligned to camera center: "+v);
                    foreach(var mesh in fp.GetComponentsInChildren<MeshFilter>())
                    {
                        if(mesh.name=="Sight aiming dot")continue;
                        var collider=mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;Physics.SyncTransforms();
                        bool blocked=collider.Raycast(new Ray(camera.transform.position,camera.transform.forward),out var hit,1.2f);
                        DestroyImmediate(collider);Check(!blocked,"ADS line clear through "+mesh.transform.parent.name);
                    }
                }
                Check(Vector3.Distance(fp.RightGrip.position,fp.GetComponentInChildren<PrototypeSoldierArtRig>().Bindings[8].Bone.position)<.003f,"First-person right hand on grip");
            }
            Set(player,"aiming",false);
            foreach(bool first in new[]{true,false})
            {
                if(player.IsFirstPerson!=first)player.TogglePerspective();
                player.Kit.ApplyNetworkAmmo(12,player.Kit.Weapon.Reserve);
                int frames=Mathf.CeilToInt(player.Kit.Weapon.ReloadSeconds*30f);
                Vector3 magHome=fp.Magazine.localPosition,handleHome=fp.ChargingHandle.localPosition;
                float maxDrop=0,maxCharge=0,maxHand=0,minGripForward=float.MaxValue;
                for(int i=0;i<=frames;i++)
                {
                    float p=(float)i/frames;
                    Set(player.Kit,"reloadEndsAt",i==frames?0f:Time.time+(1-p)*player.Kit.Weapon.ReloadSeconds);
                    Invoke(player,"UpdateInfantryPresentation");soldier.RefreshPose(true);
                    if(!first){camera.transform.position=center+new Vector3(2.1f,1.35f,1.7f);camera.transform.LookAt(center+new Vector3(0,1,.12f));camera.fieldOfView=45;}
                    maxDrop=Mathf.Max(maxDrop,Vector3.Distance(fp.Magazine.localPosition,magHome));
                    maxCharge=Mathf.Max(maxCharge,Vector3.Distance(fp.ChargingHandle.localPosition,handleHome));
                    Vector3 supportHand=first?fp.GetComponentInChildren<PrototypeSoldierArtRig>().Bindings[7].Bone.position:
                        ((Transform)Get(soldier,"leftHand")).position;
                    maxHand=Mathf.Max(maxHand,Vector3.Distance(supportHand,(first?fp:tp).SupportPosition));
                    // Chest clearance uses the prop surface, not the wrist behind it.
                    if(!first)minGripForward=Mathf.Min(minGripForward,player.transform.InverseTransformPoint(tp.RightContactPosition).z);
                    yield return Capture((first?"reload-fp":"reload-tp")+"/"+i.ToString("D4"));
                }
                Check(maxDrop>.25f&&maxCharge>.08f,"Magazine and handle articulate in "+(first?"FP":"TP"));
                Check(maxHand<.005f,"Support hand follows reload targets, maximum="+maxHand);
                if(!first)Check(minGripForward>.14f,"TP reload keeps pistol grip ahead of chest: "+minGripForward);
                Check(Vector3.Distance(fp.Magazine.localPosition,magHome)<.001f,"Reload returns magazine to home");
            }
            if(player.IsFirstPerson)player.TogglePerspective();
            foreach(string pose in new[]{"standing","run","crouch","prone"})
            {
                Set(player,"crouched",pose=="crouch");Set(player,"prone",pose=="prone");Set(player,"sprinting",pose=="run");
                Set(soldier,"runWeight",pose=="run"?1f:0f);Set(soldier,"gait",1.1f);Set(soldier,"motion",pose=="run"?1f:0f);
                Invoke(player,"UpdateInfantryPresentation");soldier.RefreshPose(true);
                camera.transform.position=center+new Vector3(2.5f,1.4f,2.5f);camera.transform.LookAt(center+Vector3.up*.85f);camera.fieldOfView=46;
                yield return Capture("tp-"+pose);
                var hand=(Transform)Get(soldier,"rightHand");Check(Vector3.Distance(hand.position,tp.RightGrip.position)<.015f,"TP right grip "+pose);
            }
            Set(player,"prone",false);Set(player,"sprinting",false);Set(player,"crouched",false);
            if(!player.IsFirstPerson)player.TogglePerspective();
            for(int i=0;i<24;i++){Invoke(player,"UpdateInfantryPresentation");yield return null;}
            int ammo=player.Ammo;Vector3 muzzle=fp.Muzzle.position;
            Invoke(player,"Fire");
            Check(player.Ammo==ammo-1,"Actual firing consumes one round");
            Check(FindObjectsByType<PrototypeBallisticProjectile>(FindObjectsSortMode.None).Any(b=>Vector3.Distance(b.transform.position,muzzle)<.001f),"Actual bullet spawns at authored muzzle");
            for(int i=0;i<9;i++){Invoke(player,"UpdateInfantryPresentation");yield return Capture("fire/"+i.ToString("D4"));}
            foreach(PrototypeWeaponId id in Enum.GetValues(typeof(PrototypeWeaponId)))
            {var w=PrototypeWeaponVisual.Build(null,id,game);Check(w.GetComponent<PrototypeWeaponArtRig>()!=null,id+" authored weapon path");Destroy(w);}
            using(var preview=new PrototypeWeaponMenuPreview(game,null))
                File.WriteAllBytes(Path.Combine(output,"menu-if27.png"),preview.Get(PrototypeWeaponId.Carbine).EncodeToPNG());
        }
        IEnumerator Capture(string name)
        {
            yield return null;
            var rt=new RenderTexture(960,640,24);var previous=RenderTexture.active;var old=camera.targetTexture;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(960,640,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,960,640),0,0);texture.Apply();
            string path=Path.Combine(output,name+".png");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=old;rt.Release();Destroy(rt);Destroy(texture);
        }
        void Check(bool value,string detail){if(!value)throw new InvalidOperationException(detail);checks.Add(detail);}
        void Observe(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
        static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
        static object Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Hidden).Invoke(o,args);
    }
}
#endif
