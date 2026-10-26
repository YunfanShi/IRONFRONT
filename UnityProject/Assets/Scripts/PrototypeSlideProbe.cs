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
    public sealed class PrototypeSlideProbe : MonoBehaviour
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        string output; int errors; PrototypeRuntime game; PrototypePlayer player;
        PrototypeSoldierVisual visual; CharacterController capsule; Camera camera;
        readonly Vector3 start = new Vector3(-325,50.02f,-320);
        readonly List<string> checks = new List<string>();
        readonly List<string> samples = new List<string>{"sequence,frame,slide,time,speed,hip_y,left_ankle_y,right_ankle_y,left_grip_error,right_grip_error"};
        float maxGrip, maxBoneError, minAnkle=100f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-ironfrontSlideProbe")
                new GameObject("Slide verification").AddComponent<PrototypeSlideProbe>().output=Path.GetFullPath(args[i+1]);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.runInBackground=true;
            Time.captureDeltaTime=1f/30f; Application.logMessageReceived+=Observe;
            IEnumerator work=Verify(); Exception failure=null;
            while(true)
            {
                bool more=false; object value=null;
                try { more=work.MoveNext();if(more)value=work.Current; } catch(Exception e){failure=e;}
                if(!more||failure!=null)break;yield return value;
            }
            bool pass=failure==null&&errors==0;
            File.WriteAllText(Path.Combine(output,"results.txt"),(pass?"PASS":"FAIL")+"\nVersion="+Application.version+
                "\nRuntimeErrors="+errors+"\nMaximumGripError="+maxGrip+"\nMaximumBoneLengthError="+maxBoneError+
                "\nMinimumAnkleHeight="+minAnkle+"\n"+string.Join("\n",checks)+(failure==null?"":"\n"+failure));
            File.WriteAllLines(Path.Combine(output,"samples.csv"),samples);
            Application.logMessageReceived-=Observe;Debug.Log("IRONFRONT slide "+(pass?"PASS":"FAIL"));Application.Quit(pass?0:1);
        }
        IEnumerator Verify()
        {
            for(int i=0;i<600;i++){game=FindAnyObjectByType<PrototypeRuntime>();if(game!=null&&game.Player!=null)break;yield return null;}
            Check(game!=null&&game.Player!=null,"Runtime starts");game.BeginBattle();game.Frontend.Close();game.SetSupportMenu(false);
            player=game.Player;player.enabled=false;visual=player.GetComponent<PrototypeSoldierVisual>();visual.enabled=false;
            capsule=player.GetComponent<CharacterController>();camera=player.ViewCamera;camera.enabled=false;Set(visual,"baseHeight",-.08f);
            foreach(var bot in game.Bots){bot.enabled=false;bot.gameObject.SetActive(false);}
            foreach(var vehicle in game.Vehicles.OfType<MonoBehaviour>())vehicle.gameObject.SetActive(false);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(-325,49.5f,-290);
            floor.transform.localScale=new Vector3(120,1,160);floor.GetComponent<Renderer>().sharedMaterial=game.GunMaterial;
            for(int i=0;i<14;i++)
            {
                var stripe=GameObject.CreatePrimitive(PrimitiveType.Cube);stripe.transform.position=new Vector3(-325,50.005f,-320+i*2);
                stripe.transform.localScale=new Vector3(10,.01f,.035f);stripe.GetComponent<Renderer>().sharedMaterial=game.LampMaterial;Destroy(stripe.GetComponent<Collider>());
            }
            Reset();yield return null;Check(visual.HasSlideAnimation,"Three source clips load from runtime resources");
            Step(Vector2.zero,false,false,false,true,false,false,1f/30f);
            Check(!player.IsSliding,"Standing Alt cannot start slide");
            Reset();Step(Vector2.up,true,false,false,false,true,false,1f/30f);
            Check(player.IsCrouched&&!player.IsSliding,"C always crouches, including Shift+C");
            Reset();capsule.enabled=false;player.transform.position+=Vector3.up*3;capsule.enabled=true;
            Step(Vector2.up,true,false,false,true,false,false,1f/30f);Check(!player.IsSliding,"Airborne Alt cannot start slide");
            foreach(string sequence in new[]{"side-crouch","front-run","fp-slide","aim-cancel","wall-stop","low-ceiling"})
            {
                Reset();bool first=sequence=="fp-slide";if(player.IsFirstPerson!=first)player.TogglePerspective();
                GameObject obstacle=null;bool started=false;float slideOrigin=0,slideEnd=0;int startCount=0;bool prior=false;
                for(int frame=0;frame<100;frame++)
                {
                    bool alt=frame>=24&&frame<80;
                    bool shift=sequence=="front-run"||sequence=="low-ceiling"||frame<25;
                    bool aim=sequence=="aim-cancel"&&frame>=34;
                    if(frame==29&&(sequence=="wall-stop"||sequence=="low-ceiling"))
                    {
                        obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
                        if(sequence=="wall-stop") {obstacle.transform.position=player.transform.position+new Vector3(0,1.5f,.7f);obstacle.transform.localScale=new Vector3(4,3,.15f);}
                        else {obstacle.transform.position=player.transform.position+new Vector3(0,1.45f,2);obstacle.transform.localScale=new Vector3(5,.15f,10);}
                        Physics.SyncTransforms();
                    }
                    Step(Vector2.up,shift,false,aim,alt,false,false,1f/30f);
                    if(player.IsSliding&&!prior){started=true;startCount++;slideOrigin=player.transform.position.z;}
                    if(!player.IsSliding&&prior)slideEnd=player.transform.position.z;
                    prior=player.IsSliding;
                    if(sequence=="aim-cancel"&&frame>=35)Require(player.IsAiming,"Aim interrupts slide into aimed recovery");
                    if(sequence=="wall-stop"&&frame==34)
                        Check(player.Slide.Blocked&&player.transform.position.z<obstacle.transform.position.z-.3f,"Capsule collision stops slide before the wall");
                    Record(sequence,frame);
                    if(!first)
                    {
                        Vector3 focus=player.transform.position+Vector3.up*.85f;
                        camera.transform.position=focus+(sequence=="front-run"?new Vector3(1.4f,.25f,3.3f):new Vector3(3.3f,.35f,.6f));
                        camera.transform.LookAt(focus);camera.fieldOfView=44;
                    }
                    yield return null;
                    if(frame>=15&&frame<72)Capture(sequence,frame-15);
                }
                Check(started&&startCount==1,sequence+": one Alt press starts once; held Alt cannot chain");
                Check(!player.IsSliding,sequence+": recovery finishes");
                if(sequence=="front-run")Check(player.IsSprinting&&!player.IsCrouched,"Forward+Shift resumes sprint after recovery");
                else Check(player.IsCrouched,"Default/blocked/ADS recovery remains crouched: "+sequence);
                if(sequence=="side-crouch"||sequence=="front-run"||sequence=="fp-slide")Check(slideEnd-slideOrigin>3.8f&&slideEnd-slideOrigin<5.2f,sequence+": slide distance="+(slideEnd-slideOrigin));
                if(obstacle!=null){Destroy(obstacle);yield return null;}
            }
            // Same local input pipeline, with real capsule displacement, at three frame rates.
            foreach(int fps in new[]{30,60,120})
            {
                float dt=1f/fps;Time.captureDeltaTime=dt;Reset();bool seen=false;float origin=0,end=0;int trigger=Mathf.RoundToInt(fps*.8f);bool priorSlide=false;
                for(int i=0;i<fps*3;i++)
                {
                    bool was=priorSlide;Step(Vector2.up,i<=trigger,false,false,i>=trigger,false,false,dt);
                    if(player.IsSliding&&!was){origin=player.transform.position.z;seen=true;}
                    if(!player.IsSliding&&was)end=player.transform.position.z;
                    priorSlide=player.IsSliding;
                    yield return null;
                }
                Check(seen&&end-origin>3.8f&&end-origin<5.2f,fps+" Hz distance="+(end-origin));
            }
            Time.captureDeltaTime=1f/30f;
            Reset();
            foreach(PrototypeWeaponId weapon in Enum.GetValues(typeof(PrototypeWeaponId)))
            {
                visual.SetWeapon(weapon,game);player.Slide.TryStart(6.6f,Vector3.forward,true,true);
                player.Slide.StartedAt=Time.time-.42f;player.Slide.EndsAt=Time.time+.6f;
                visual.RefreshPose(true);Record("weapon-"+weapon,0);
                Capture("weapon-"+weapon,0);player.Slide.Reset();
            }
            visual.SetWeapon(player.ActiveWeapon.Id,game);
            Reset();
            // The host validates movement speed rather than trusting a client's slide request.
            var remoteRoot=new GameObject("Remote slide authority probe");var remote=remoteRoot.AddComponent<PrototypeRemotePlayer>();
            remote.Initialize(game,991,PrototypeTeam.Red);remote.enabled=false;
            remote.transform.position=new Vector3(-325,PrototypeLayout.HeightAt(-325,-320)+.3f,-320);
            remote.ApplyInput(0,0,0,0,true,false,.05f,true);Check(!remote.Slide.Active,"Host rejects stationary slide request");
            for(int i=0;i<8;i++){remote.ApplyInput(1,0,0,0,true,false,.05f);yield return null;}
            remote.ApplyInput(1,0,0,0,true,false,.05f,true);Check(remote.Slide.Active,"Host accepts sprinting remote slide");
            var snapshot=new PrototypeLanActor{alive=true};remote.Slide.WriteSnapshot(snapshot);
            var replicated=new PrototypeSlideState();replicated.ReadSnapshot(snapshot);
            Check(replicated.Active&&Mathf.Abs(replicated.Elapsed-remote.Slide.Elapsed)<.001f,"Snapshot carries slide phase without restarting it");
            remote.ApplyInput(1,0,0,0,false,false,.05f,false,true);Check(remote.Slide.Recovering,"Host ADS cancels remote slide");
            remote.SetConnected(false);Check(!remote.Slide.Active,"Disconnect clears slide");Destroy(remoteRoot);
            Check(minAnkle>.07f,"Blended slide feet stay above floor");
            Check(maxGrip<.025f,"Hands remain attached to actual weapon grips");
            Check(maxBoneError<.001f,"Leg segment lengths remain unchanged");
        }
        void Step(Vector2 input,bool shift,bool fire,bool aim,bool alt,bool crouch,bool prone,float dt)
        {
            Set(player,"moveAmount",input.magnitude);
            Vector3 horizontal=(Vector3)Invoke(player,"ResolveInfantryMotion",input,shift,fire,aim,crouch,prone,alt,dt);
            float height=player.IsProne?.75f:player.IsSliding?Mathf.Lerp(1.15f,player.Slide.StandExit?1.8f:1.15f,player.Slide.Recovery):player.IsCrouched?1.15f:1.8f;
            capsule.height=height;capsule.center=Vector3.up*height*.5f;
            Vector3 before=player.transform.position;CollisionFlags flags=capsule.Move((horizontal+Vector3.down*2f)*dt);
            Invoke(player,"ResolveSlideCollision",flags,before,horizontal,dt);
            Invoke(visual,"Update");Invoke(player,"UpdateInfantryPresentation");visual.RefreshPose();
        }
        void Record(string sequence,int frame)
        {
            Transform model=player.transform.Find("Armored infantry visual"),hip=model.Find("Pelvis");
            Transform l=hip.Find("Left leg"),r=hip.Find("Right leg"),lk=l.Find("Knee"),rk=r.Find("Knee"),lf=lk.Find("Ankle"),rf=rk.Find("Ankle");
            var art=visual.HeldWeapon.GetComponent<PrototypeWeaponArtRig>();
            Transform chest=hip.Find("Torso"),lh=chest.Find("Left arm/Elbow/Hand"),rh=chest.Find("Right arm/Elbow/Hand");
            float le=art!=null?Vector3.Distance(lh.position,art.SupportPosition):0,re=art!=null?Vector3.Distance(rh.position,art.RightGrip.position):0;
            maxGrip=Mathf.Max(maxGrip,Mathf.Max(le,re));
            foreach(var pair in new[]{new[]{l,lk},new[]{r,rk},new[]{lk,lf},new[]{rk,rf}})
                maxBoneError=Mathf.Max(maxBoneError,Mathf.Abs(Vector3.Distance(pair[0].position,pair[1].position)-pair[1].localPosition.magnitude));
            float ly=model.InverseTransformPoint(lf.position).y,ry=model.InverseTransformPoint(rf.position).y;minAnkle=Mathf.Min(minAnkle,Mathf.Min(ly,ry));
            samples.Add($"{sequence},{frame},{player.IsSliding},{player.Slide.Elapsed:F4},{player.Slide.Speed:F4},{model.InverseTransformPoint(hip.position).y:F4},{ly:F4},{ry:F4},{le:F5},{re:F5}");
        }
        void Reset()
        {
            capsule.enabled=false;player.transform.SetPositionAndRotation(start,Quaternion.identity);capsule.enabled=true;
            player.Slide.Reset();foreach(string field in new[]{"crouched","prone","sprinting","sprintInterrupted","aiming","slideKeyHeld","slideInputPending"})Set(player,field,false);
            Set(player,"pitch",0f);Set(player,"moveAmount",0f);player.Kit.ResetForSpawn();
            capsule.height=1.8f;capsule.center=Vector3.up*.9f;Physics.SyncTransforms();capsule.Move(Vector3.down*.2f);
            Set(visual,"lastPosition",player.transform.position);Set(visual,"gait",0f);Set(visual,"motion",0f);Set(visual,"runWeight",0f);
            visual.RefreshPose(true);Invoke(player,"PositionInfantryCamera",1f);
        }
        void Capture(string sequence,int frame)
        {
            var rt=new RenderTexture(960,640,24);RenderTexture previous=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(960,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();
            string folder=Path.Combine(output,sequence);Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,frame.ToString("D4")+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Destroy(rt);Destroy(image);
        }
        void Observe(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        static void Require(bool pass,string detail){if(!pass)throw new InvalidOperationException(detail);}
        void Check(bool pass,string detail){Require(pass,detail);checks.Add(detail);}
        static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Hidden).SetValue(obj,value);
        static object Invoke(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
    }
}
#endif
