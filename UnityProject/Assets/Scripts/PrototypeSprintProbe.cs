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
    // Explicit development probe; normal startup never enables the fixture.
    public sealed class PrototypeSprintProbe : MonoBehaviour
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        const float Dt = 1f / 30f;
        string output;
        int errors;
        PrototypeRuntime game;
        PrototypePlayer player;
        PrototypeSoldierVisual soldier;
        CharacterController controller;
        Camera camera;
        Vector3 start = new Vector3(-325f, 50.02f, -320f);
        readonly List<string> checks = new List<string>();
        readonly List<string> samples = new List<string> { "sequence,frame,shift,fire,aim,sprinting,speed,measured_speed,ammo" };
        readonly List<string> motionSamples = new List<string> {
            "sequence,frame,gait,weight,camera_x,camera_y,pitch,yaw,roll,chest_yaw,pelvis_yaw" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-ironfrontSprintProbe")
                    new GameObject("Sprint interruption verification").AddComponent<PrototypeSprintProbe>().output = Path.GetFullPath(args[i+1]);
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            Application.runInBackground = true;
            Time.captureDeltaTime = Dt;
            Application.logMessageReceived += Observe;
            IEnumerator work = Verify();
            Exception failure = null;
            while (true)
            {
                bool more = false; object current = null;
                try { more = work.MoveNext(); if (more) current = work.Current; }
                catch (Exception e) { failure = e; }
                if (!more || failure != null) break;
                yield return current;
            }
            bool pass = failure == null && errors == 0;
            File.WriteAllText(Path.Combine(output, "results.txt"), (pass ? "PASS" : "FAIL") +
                "\nVersion=" + Application.version + "\nRuntimeErrors=" + errors + "\n" + string.Join("\n", checks) +
                (failure == null ? "" : "\n" + failure));
            File.WriteAllLines(Path.Combine(output, "samples.csv"), samples);
            File.WriteAllLines(Path.Combine(output, "motion.csv"), motionSamples);
            Application.logMessageReceived -= Observe;
            Debug.Log("IRONFRONT sprint " + (pass ? "PASS" : "FAIL"));
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(pass ? 0 : 1);
            #else
            Application.Quit(pass ? 0 : 1);
            #endif
        }

        IEnumerator Verify()
        {
            for (int i=0; i<600; i++)
            {
                game=FindAnyObjectByType<PrototypeRuntime>();
                if(game != null && game.Player != null) break;
                yield return null;
            }
            Check(game != null && game.Player != null, "Runtime starts");
            game.BeginBattle(); game.Frontend.Close(); game.SetSupportMenu(false);
            player=game.Player; player.enabled=false;
            player.ApplyLoadout(new PrototypeLoadout { role=PrototypeInfantryClass.Assault,
                primary=PrototypeWeaponId.Carbine, secondary=PrototypeWeaponId.Pistol, throwable=PrototypeThrowableId.Frag });
            foreach(var b in game.Bots) { b.enabled=false; b.gameObject.SetActive(false); }
            foreach(var v in game.Vehicles.OfType<MonoBehaviour>()) v.gameObject.SetActive(false);
            soldier=player.GetComponent<PrototypeSoldierVisual>(); soldier.enabled=false;
            Set(soldier,"baseHeight",0f);
            controller=player.GetComponent<CharacterController>(); camera=player.ViewCamera; camera.enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="Sprint verification flat floor";
            floor.transform.position=new Vector3(-325,49.5f,-290);
            floor.transform.localScale=new Vector3(120,1,160);
            floor.GetComponent<Renderer>().sharedMaterial=game.GunMaterial;
            // Fixed references make millimetre-scale head motion readable in
            // first person; these exist only in the explicit probe fixture.
            for(int i=0;i<9;i++)
            {
                var stripe=GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name="Sprint floor reference";
                stripe.transform.position=new Vector3(-325,50.005f,-318+i*4);
                stripe.transform.localScale=new Vector3(12,.01f,.08f);
                stripe.GetComponent<Renderer>().sharedMaterial=game.LampMaterial;
                Destroy(stripe.GetComponent<Collider>());
                foreach(float side in new[]{-1f,1f})
                {
                    var post=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name="Sprint upright reference";
                    post.transform.position=new Vector3(-325+side*5,51,-318+i*4);
                    post.transform.localScale=new Vector3(.12f,2,.12f);
                    post.GetComponent<Renderer>().sharedMaterial=game.LampMaterial;
                    Destroy(post.GetComponent<Collider>());
                }
            }
            Reset(); yield return null;
            controller.Move(Vector3.down*.2f);
            Check(controller.isGrounded,"CharacterController grounded on flat fixture at "+player.transform.position);
            VerifyRules(); VerifyRemoteGuard();
            foreach(bool first in new[]{true,false})
                foreach(bool aimAction in new[]{false,true})
                {
                    Reset();
                    if(player.IsFirstPerson!=first) player.TogglePerspective();
                    string name=(first?"fp-":"tp-")+(aimAction?"aim-interrupt":"fire-interrupt");
                    int initialAmmo=player.Ammo;
                    float minRoll=1f,maxRoll=-1f,minChest=100f,maxChest=-100f,minY=100f,maxY=-100f;
                    for(int frame=0;frame<126;frame++)
                    {
                        bool shift=frame<78||frame>=90;
                        bool action=frame>=36&&frame<60;
                        bool fire=action&&!aimAction, aim=action&&aimAction;
                        float speed=Resolve(Vector2.up,shift,fire,aim);
                        bool expectedSprint=frame<36||frame>=90;
                        Require(player.IsSprinting==expectedSprint,name+" sprint state frame "+frame);
                        Require(player.SprintRequested==expectedSprint,name+" network sprint intent frame "+frame);
                        Require(player.IsAiming==aim,name+" aim state frame "+frame);
                        float expectedSpeed=expectedSprint?6.6f:aim?2.73f:4.2f;
                        Require(Mathf.Abs(speed-expectedSpeed)<.001f,name+" immediate speed frame "+frame);
                        Vector3 before=player.transform.position;
                        controller.Move((Vector3.forward*speed+Vector3.down*2f)*Dt);
                        Vector3 delta=player.transform.position-before; delta.y=0;
                        float measured=delta.magnitude/Dt;
                        Require(Mathf.Abs(measured-speed)<.01f,name+" actual controller displacement frame "+frame);
                        Invoke(soldier,"Update");
                        Invoke(player,"UpdateInfantryPresentation"); soldier.RefreshPose(true);
                        RecordMotion(name,frame);
                        float roll=Signed(camera.transform.localEulerAngles.z);
                        float chest=Signed(((Transform)Get(soldier,"torso")).eulerAngles.y);
                        if(frame>=12&&frame<36)
                        {
                            minRoll=Mathf.Min(minRoll,roll);maxRoll=Mathf.Max(maxRoll,roll);
                            minChest=Mathf.Min(minChest,chest);maxChest=Mathf.Max(maxChest,chest);
                            minY=Mathf.Min(minY,camera.transform.localPosition.y);maxY=Mathf.Max(maxY,camera.transform.localPosition.y);
                        }
                        if(first)
                        {
                            Require(Mathf.Abs(roll)<.31f,name+" gentle roll bound");
                            Require(Mathf.Abs(Signed(camera.transform.localEulerAngles.x))<.21f,name+" gentle pitch bound");
                            if(frame>=42&&frame<90)
                                Require(soldier.SprintMotionWeight<.0001f&&Quaternion.Angle(camera.transform.localRotation,Quaternion.identity)<.001f,
                                    name+" camera settles after combat interruption");
                        }
                        else Require(Mathf.Abs(roll)<.001f,name+" gameplay third-person camera stays level");
                        if(fire) Invoke(player,"Fire");
                        if(frame==36&&!aimAction) Check(player.Ammo==initialAmmo-1,name+" first click fires on interruption frame");
                        if(!first)
                        {
                            Vector3 focus=player.transform.position+Vector3.up*.9f;
                            camera.transform.position=focus+new Vector3(2.6f,.35f,3f);
                            camera.transform.LookAt(focus); camera.fieldOfView=46;
                        }
                        samples.Add($"{name},{frame},{shift},{fire},{aim},{player.IsSprinting},{speed:F4},{measured:F4},{player.Ammo}");
                        yield return null;
                        Capture(name+"/"+frame.ToString("D4"));
                    }
                    Check(true,name+": 126 frames, instant interruption, held-button walking, Shift release/repress re-arms");
                    Check(aimAction?player.Ammo==initialAmmo:player.Ammo<initialAmmo,name+" ammo semantics");
                    Check(maxChest-minChest>9f&&maxChest-minChest<12f,name+" gentle chest counter-rotation in both directions");
                    if(first)
                    {
                        Check(maxRoll-minRoll>.55f,name+" first-person camera sways with hidden body");
                        Check(maxY-minY>.008f&&maxY-minY<.025f,name+" subtle vertical camera bob");
                    }
                    var fp=(GameObject)Get(player,"firstPersonWeapon");
                    Check(fp.transform.Find("Weapon visual size/Stock").gameObject.activeInHierarchy==first,name+" stock retained with view visibility");
                }
            // Real capsule obstruction: holding forward/Shift against a wall
            // must not keep shaking a stationary first-person view.
            Reset(); if(!player.IsFirstPerson)player.TogglePerspective();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name="Sprint obstruction"; wall.transform.position=start+new Vector3(0,1.5f,.65f);
            wall.transform.localScale=new Vector3(4,3,.2f); Physics.SyncTransforms();
            for(int frame=0;frame<42;frame++)
            {
                float speed=Resolve(Vector2.up,true,false,false);
                controller.Move((Vector3.forward*speed+Vector3.down*2)*Dt);
                Invoke(soldier,"Update");Invoke(player,"UpdateInfantryPresentation");soldier.RefreshPose(true);
                RecordMotion("fp-blocked",frame);
                if(frame>=24)Require(soldier.SprintMotionWeight<.001f&&Quaternion.Angle(camera.transform.localRotation,Quaternion.identity)<.001f,
                    "No run bob while capsule blocked by wall");
                yield return null;
            }
            Check(true,"Forward/Shift held against a wall: camera settles without actual movement");
            Destroy(wall); Reset();
            for(int frame=0;frame<30;frame++)
            {
                Resolve(Vector2.up,true,false,false);
                controller.Move((Vector3.forward*6.6f+Vector3.down*2)*Dt);
                Invoke(soldier,"Update");Invoke(player,"UpdateInfantryPresentation");
                yield return null;
            }
            player.TogglePerspective();
            Check(!player.IsFirstPerson&&Quaternion.Angle(camera.transform.localRotation,Quaternion.identity)<.001f,
                "Switching to third person removes additive camera tilt immediately");
            player.TogglePerspective();
            for(int frame=0;frame<30;frame++)
            {
                Resolve(Vector2.zero,false,false,false);Set(player,"moveAmount",0f);
                controller.Move(Vector3.down*2*Dt);
                Invoke(soldier,"Update");Invoke(player,"UpdateInfantryPresentation");
                RecordMotion("fp-stop",frame);
                yield return null;
            }
            Check(soldier.SprintMotionWeight<.001f&&Quaternion.Angle(camera.transform.localRotation,Quaternion.identity)<.001f&&
                Mathf.Abs(camera.transform.localPosition.y-1.65f)<.001f,"Stopping restores steady eye height and view");
        }

        static float Signed(float angle)=>Mathf.DeltaAngle(0f,angle);
        void RecordMotion(string sequence,int frame)
        {
            Vector3 p=camera.transform.localPosition,e=camera.transform.localEulerAngles;
            float chest=Signed(((Transform)Get(soldier,"torso")).eulerAngles.y);
            float pelvis=Signed(((Transform)Get(soldier,"pelvis")).eulerAngles.y);
            motionSamples.Add($"{sequence},{frame},{soldier.GaitPhase:F5},{soldier.SprintMotionWeight:F5},{p.x:F5},{p.y:F5},{Signed(e.x):F5},{Signed(e.y):F5},{Signed(e.z):F5},{chest:F5},{pelvis:F5}");
        }

        void VerifyRules()
        {
            Reset(); Check(Resolve(Vector2.up,true,false,false)==6.6f,"Forward Shift starts sprint");
            Check(Resolve(Vector2.up,true,true,false)==4.2f&&!player.IsSprinting,"Trigger interrupts before movement");
            Check(!player.SprintRequested,"Combat interruption is also cleared from network intent");
            for(int i=0;i<20;i++) Require(Resolve(Vector2.up,true,true,false)==4.2f,"Held trigger cannot re-enter sprint");
            Check(Resolve(Vector2.up,true,false,false)==4.2f,"Releasing trigger alone leaves walking");
            Resolve(Vector2.up,false,false,false);
            Check(Resolve(Vector2.up,true,false,false)==6.6f,"Release/repress Shift re-arms sprint");
            Check(Mathf.Abs(Resolve(Vector2.up,true,false,true)-2.73f)<.001f&&player.IsAiming,"ADS interrupts and uses existing aim walk speed");
            Check(Resolve(Vector2.up,true,false,false)==4.2f,"Releasing ADS alone leaves walking");
            Reset(); Check(Mathf.Abs(Resolve(Vector2.up,true,true,true)-2.73f)<.001f&&!player.IsSprinting,"Simultaneous fire and aim have priority over Shift");
            Reset(); Check(Resolve(Vector2.left,true,false,false)==4.2f,"Pure strafe does not sprint");
            Check(Resolve(Vector2.down,true,false,false)==4.2f,"Backwards does not sprint");
            Reset(); Check(Resolve(new Vector2(.7f,.7f),true,false,false)==6.6f,"Forward diagonal can sprint");
            Set(player,"crouched",true); Check(Resolve(Vector2.up,true,false,false)==2f,"Crouch speed preserved");
            Set(player,"crouched",false); Set(player,"prone",true);
            Check(Mathf.Abs(Resolve(Vector2.up,true,false,false)-.85f)<.001f,"Prone speed preserved");
            Reset(); player.Slide.TryStart(6.6f,Vector3.forward,true,true);
            Resolve(Vector2.up,true,false,true); Check(!player.IsSprinting&&!player.IsAiming&&player.IsSliding,"Slide remains separate from sprint and ADS");
            Reset(); Resolve(Vector2.up,true,false,false); player.Kit.ApplyNetworkAmmo(0,player.Kit.Weapon.Reserve);
            Resolve(Vector2.up,true,true,false); Invoke(player,"Fire");
            Check(!player.IsSprinting&&player.Reloading,"Empty trigger cancels sprint and retains auto reload");
            Reset(); Resolve(Vector2.up,true,false,false); Set(player.Kit,"nextFireAt",Time.time+1f);
            int ammo=player.Ammo; Resolve(Vector2.up,true,true,false); Invoke(player,"Fire");
            Check(!player.IsSprinting&&player.Ammo==ammo,"Trigger on cooldown still cancels sprint without consuming ammo");
            Reset(); Invoke(player,"EquipSecondary",true);
            Resolve(Vector2.up,true,false,false); Resolve(Vector2.up,true,true,false); Invoke(player,"Fire");
            Check(!player.IsSprinting&&player.Ammo==player.ActiveWeapon.Magazine-1,"Pistol single shot cancels sprint");
            for(int i=0;i<10;i++) Require(Resolve(Vector2.up,true,true,false)==4.2f,"Semi-auto held trigger cannot re-enter sprint");
            Invoke(player,"EquipSecondary",false); Reset();
        }

        void VerifyRemoteGuard()
        {
            var root=new GameObject("Host sprint input guard");
            var remote=root.AddComponent<PrototypeRemotePlayer>();remote.Initialize(game,9998,PrototypeTeam.Red);remote.enabled=false;
            Vector3 origin=new Vector3(-325,PrototypeLayout.HeightAt(-325,-320)+.3f,-320);
            remote.transform.position=origin; remote.ApplyInput(1,0,0,0,true,false,.05f);
            Vector2 run=remote.MapPosition-new Vector2(origin.x,origin.z);
            remote.transform.position=origin; remote.ApplyInput(1,0,0,0,true,true,.05f);
            Vector2 fire=remote.MapPosition-new Vector2(origin.x,origin.z);
            Check(run.magnitude>.32f&&run.magnitude<.34f&&fire.magnitude<.22f&&fire.magnitude>.20f,"Host rejects simultaneous sprint/fire speed in peer input");
            Destroy(root);
        }

        void Reset()
        {
            controller.enabled=false;player.transform.SetPositionAndRotation(start,Quaternion.identity);controller.enabled=true;
            Set(player,"sprintInterrupted",false); Set(player,"sprinting",false); Set(player,"aiming",false);
            Set(player,"crouched",false); Set(player,"prone",false); player.Slide.Reset(); Set(player,"pitch",0f); Set(player,"moveAmount",1f);
            player.Kit.ResetForSpawn();player.ActiveKit.ResetForSpawn();
            Set(soldier,"runWeight",0f);Set(soldier,"motion",0f);Set(soldier,"gait",0f);Set(soldier,"sprintMotionWeight",0f);
            controller.height=1.8f;controller.center=Vector3.up*.9f;
            Physics.SyncTransforms();controller.Move(Vector3.down*.2f);
            Set(soldier,"lastPosition",player.transform.position);
            Invoke(player,"PositionInfantryCamera",1f);
        }
        float Resolve(Vector2 input,bool shift,bool fire,bool aim)
        {
            bool request=(bool)Invoke(player,"ResolveSprintInput",input,shift,fire,aim);
            return (float)Invoke(player,"ResolveMovementSpeed",request,aim);
        }
        void Capture(string name)
        {
            var rt=new RenderTexture(960,640,24);var previous=RenderTexture.active;var old=camera.targetTexture;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(960,640,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,960,640),0,0);texture.Apply();
            string path=Path.Combine(output,name+".png");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=old;rt.Release();Destroy(rt);Destroy(texture);
        }
        static void Require(bool pass,string detail) { if(!pass)throw new InvalidOperationException(detail); }
        void Check(bool pass,string detail) { Require(pass,detail);checks.Add(detail); }
        void Observe(string message,string stack,LogType kind) { if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors++; }
        static object Get(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
        static object Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden).Invoke(o,args);
    }
}
#endif
