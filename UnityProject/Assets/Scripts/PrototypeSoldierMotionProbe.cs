#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Opt-in deterministic motion fixture, using the actual runtime pose/skin.
    // 30 fps samples include root travel; the camera tracks it over a fixed grid.
    public sealed class PrototypeSoldierMotionProbe : MonoBehaviour
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        string output; int errors; Camera camera; RenderTexture rt; Texture2D image;
        readonly List<string> checks=new List<string>();
        readonly List<string> samples=new List<string>{"action,frame,phase,minY,maxY,leftElbowY,rightElbowY,leftKneeY,rightKneeY,rightGripError,leftFootSlide,rightFootSlide"};
        readonly List<string> legSamples=new List<string>{"action,frame,footLaneGap,kneeLaneGap,legClearance"};
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++) if(args[i]=="-ironfrontMotionProbe")
                new GameObject("Motion proof").AddComponent<PrototypeSoldierMotionProbe>().output=Path.GetFullPath(args[i+1]);
        }
        IEnumerator Start()
        {
            Application.runInBackground=true; Directory.CreateDirectory(output); Application.logMessageReceived+=Observe;
            var work=Verify(); Exception failure=null;
            while(true)
            {
                bool more=false; object current=null;
                try { more=work.MoveNext(); if(more)current=work.Current; } catch(Exception e){failure=e;}
                if(!more||failure!=null)break;
                yield return current;
            }
            bool pass=failure==null&&errors==0;
            File.WriteAllLines(Path.Combine(output,"samples.csv"),samples);
            File.WriteAllLines(Path.Combine(output,"leg-samples.csv"),legSamples);
            File.WriteAllText(Path.Combine(output,"motion-results.txt"),(pass?"PASS\n":"FAIL\n")+"Version="+Application.version+"\nUnity="+Application.unityVersion+"\nGPU="+SystemInfo.graphicsDeviceName+"\nRuntimeErrors="+errors+"\n"+string.Join("\n",checks)+(failure==null?"":"\n"+failure));
            Debug.Log("SOLDIER_MOTION_"+(pass?"PASS":"FAIL"));
            Application.logMessageReceived-=Observe;
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
            Check(game!=null&&game.Player!=null,"Runtime starts");game.BeginBattle();game.Frontend.Close();game.SetSupportMenu(false);game.Player.enabled=false;
            foreach(var b in game.Bots){b.enabled=false;b.gameObject.SetActive(false);}
            foreach(var v in game.Vehicles.OfType<MonoBehaviour>())v.gameObject.SetActive(false);
            game.Player.GetComponent<PrototypeSoldierVisual>().SetVisible(false);
            var arguments=Environment.GetCommandLineArgs();
            bool actualPlayer=arguments.Contains("-ironfrontMotionPlayer");
            bool allowCrossedLegs=arguments.Contains("-ironfrontAllowCrossedLegs");
            var actor=actualPlayer?game.Player.gameObject:game.Bots.First(b=>b.Team==PrototypeTeam.Blue).gameObject;actor.SetActive(true);
            var visual=actor.GetComponent<PrototypeSoldierVisual>();visual.enabled=false;visual.SetWeapon(PrototypeWeaponId.Carbine,game);
            visual.SetVisible(true);visual.ArtRig.SetFirstPerson(false);
            float feetOffset=(float)Get(visual,"baseHeight");
            visual.ArtRig.GetComponent<LODGroup>().ForceLOD(0);
            Vector3 stage=new Vector3(-325,30,-320);
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=new Color(.28f,.31f,.32f);
            var plane=GameObject.CreatePrimitive(PrimitiveType.Cube);plane.name="Motion proof ground";plane.transform.position=stage+new Vector3(0,-.1f,5);plane.transform.localScale=new Vector3(60,.2f,60);plane.GetComponent<Renderer>().sharedMaterial=mat;
            var gridMat=new Material(mat);gridMat.color=new Color(.52f,.57f,.58f);
            for(int z=-20;z<30;z++)
            {var line=GameObject.CreatePrimitive(PrimitiveType.Cube);line.transform.position=stage+new Vector3(0,.001f,z);line.transform.localScale=new Vector3(60,.002f,.012f);line.GetComponent<Renderer>().sharedMaterial=gridMat;Destroy(line.GetComponent<Collider>());}
            var fill=new GameObject("Motion proof fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=1.7f;fill.transform.rotation=Quaternion.Euler(35,220,0);
            camera=new GameObject("Motion proof camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.14f,.18f);camera.nearClipPlane=.03f;camera.farClipPlane=100;
            camera.orthographic=true;camera.orthographicSize=1.25f;
            rt=new RenderTexture(960,640,24);image=new Texture2D(960,640,TextureFormat.RGB24,false);camera.targetTexture=rt;
            string[] actions={"idle","walk","run","crouch","crawl","crawl-strafe","crawl-reverse","prone-aim","strafe","reverse","transition"};
            for(int i=0;i+1<arguments.Length;i++)if(arguments[i]=="-ironfrontMotionActions")actions=arguments[i+1].Split(',');
            foreach(string action in actions)
            {
                Directory.CreateDirectory(Path.Combine(output,action));
                actor.transform.SetPositionAndRotation(stage-Vector3.up*feetOffset,Quaternion.identity);
                Set(visual,"gait",0f);Set(visual,"runWeight",action.StartsWith("run")?1f:0f);Set(visual,"motion",0f);Set(visual,"strideDirection",Vector2.up);
                typeof(PrototypeSoldierVisual).GetField("legYaw",Hidden)?.SetValue(visual,0f);
                float crouch=action.StartsWith("crouch")?1:0,prone=action.StartsWith("crawl")||action=="prone-aim"?1:0;
                float speed=action.StartsWith("run")?6.6f:action.StartsWith("crawl")?.85f:action.StartsWith("crouch")?2f:action=="idle"||action=="prone-aim"||action=="transition"?0:1.8f;
                Vector3 direction=action.EndsWith("strafe")?Vector3.right:action.EndsWith("reverse")?Vector3.back:Vector3.forward;
                bool lateral=action.Contains("-left")||action.Contains("-right");
                bool diagonal=action.Contains("diagonal");
                bool reverse=action.Contains("reverse");
                bool turning=action=="turn-left-right";
                if(lateral)
                {
                    direction=new Vector3(action.Contains("-left")?-1:1,0,diagonal?(reverse?-1:1):0).normalized;
                    speed=crouch>0?2f:diagonal&&!reverse?6.6f:4.2f;
                }
                if(actualPlayer)
                {
                    Set(game.Player,"sprinting",speed>6f);Set(game.Player,"crouched",crouch>0);Set(game.Player,"prone",prone>0);
                }
                Set(visual,"crouchWeight",crouch);Set(visual,"proneWeight",prone);Set(visual,"aimWeight",action=="prone-aim"?1f:0f);
                Vector3[] prevFeet=new Vector3[2];float[] prevU={-1,-1};float minGround=100,maxGrip=0,maxSlide=0,minLane=100,minKneeLane=100,minClearance=100;
                int frames=action=="idle"||action=="prone-aim"?45:90;
                int lastDirectionChange=0;Vector3 previousDirection=direction;
                for(int frame=0;frame<frames;frame++)
                {
                    if(action=="transition")prone=Mathf.PingPong(frame/45f,1f);
                    if(turning)direction=frame<30?Vector3.left:frame<60?Vector3.right:Vector3.forward;
                    if(Vector3.Dot(previousDirection,direction)<.999f){lastDirectionChange=frame;previousDirection=direction;}
                    Vector3 delta=direction*(speed/30f);actor.transform.position+=delta;
                    Invoke(visual,"AdvanceLocomotion",delta,1f/30f);
                    float gait=(float)Get(visual,"gait"),movement=(float)Get(visual,"motion");
                    Invoke(visual,"Pose",crouch,prone,Mathf.Sin(gait),movement);
                    yield return null;yield return null;
                    var model=((GameObject)Get(visual,"model")).transform;
                    var points=BakedPoints(visual.ArtRig);float min=points.Min(p=>p.y)-stage.y,max=points.Max(p=>p.y)-stage.y;
                    minGround=Mathf.Min(minGround,min);
                    float grip=Vector3.Distance(((Transform)Get(visual,"rightHand")).position,visual.HeldWeapon.transform.TransformPoint(new Vector3(0,-.18f,-.18f)));maxGrip=Mathf.Max(maxGrip,grip);
                    float run=(float)Get(visual,"runWeight");float duty=Mathf.Lerp(Mathf.Lerp(.60f,.24f,run),.56f,crouch);if(prone>.98f)duty=.40f;
                    float[] slides=new float[2];
                    for(int side=0;side<2;side++)
                    {
                        var foot=(Transform)Get(visual,side==0?"leftFoot":"rightFoot");float u=Mathf.Repeat((gait+(side==0?0:Mathf.PI))/(2*Mathf.PI),1f);
                        if(movement>.999f && frame-lastDirectionChange>12 && u<duty-.05f && prevU[side]>=.05f && u>prevU[side])
                        {var d=foot.position-prevFeet[side];d.y=0;slides[side]=d.magnitude;maxSlide=Mathf.Max(maxSlide,slides[side]);}
                        prevFeet[side]=foot.position;prevU[side]=u;
                        var hip=(Transform)Get(visual,side==0?"leftLeg":"rightLeg");var knee=(Transform)Get(visual,side==0?"leftKnee":"rightKnee");
                        Check(Mathf.Abs(Vector3.Distance(hip.position,knee.position)-visual.ArtRig.ThighLength)<.001f,action+" bone lengths frame="+frame+" side="+side);
                    }
                    samples.Add(string.Join(",",new[]{action,frame.ToString(),gait.ToString("F5"),min.ToString("F5"),max.ToString("F5"),Height(visual,"leftElbow",stage.y),Height(visual,"rightElbow",stage.y),Height(visual,"leftKnee",stage.y),Height(visual,"rightKnee",stage.y),grip.ToString("F5"),slides[0].ToString("F5"),slides[1].ToString("F5")}));
                    Vector3 axis=Vector3.Cross(Vector3.up,direction);if(reverse)axis=-axis;
                    Vector3 lf=((Transform)Get(visual,"leftFoot")).position,rf=((Transform)Get(visual,"rightFoot")).position;
                    Vector3 lk=((Transform)Get(visual,"leftKnee")).position,rk=((Transform)Get(visual,"rightKnee")).position;
                    Vector3 lh=((Transform)Get(visual,"leftLeg")).position,rh=((Transform)Get(visual,"rightLeg")).position;
                    float lane=Vector3.Dot(rf-lf,axis),kneeLane=Vector3.Dot(rk-lk,axis);
                    float clearance=Mathf.Min(SegmentDistance(lh,lk,rh,rk),Mathf.Min(SegmentDistance(lk,lf,rk,rf),Mathf.Min(SegmentDistance(lh,lk,rk,rf),SegmentDistance(lk,lf,rh,rk))));
                    if(frame>15){minLane=Mathf.Min(minLane,lane);minKneeLane=Mathf.Min(minKneeLane,kneeLane);minClearance=Mathf.Min(minClearance,clearance);}
                    legSamples.Add($"{action},{frame},{lane:F5},{kneeLane:F5},{clearance:F5}");
                    Vector3 focus=actor.transform.position+Vector3.up*feetOffset+new Vector3(0,prone>.9f?.35f:.88f,0);
                    Vector3 angle=action=="strafe"?new Vector3(.2f,.25f,4):new Vector3(4,.35f,1.3f);
                    if(lateral)angle=new Vector3(.3f,.35f,-4);
                    if(action.EndsWith("front"))angle=new Vector3(.2f,.25f,4);
                    if(action.EndsWith("rear"))angle=new Vector3(.5f,.25f,-4);
                    camera.transform.position=focus+angle;camera.transform.LookAt(focus);
                    SaveFrame(Path.Combine(output,action,frame.ToString("D4")+".png"));
                    if(frame==0||frame==30||frame==60)
                    {
                        camera.transform.position=focus+new Vector3(.2f,3.2f,-2f);camera.transform.LookAt(focus);
                        SaveFrame(Path.Combine(output,action+"-top-"+frame+".png"));
                    }
                    Check(points.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),action+" finite frame="+frame);
                }
                checks.RemoveAll(s=>s.StartsWith(action+" bone lengths")||s.StartsWith(action+" finite"));
                Check(maxGrip<.035f,action+" right grip max="+maxGrip.ToString("F5"));
                Check(minGround>-.015f,action+" no deep ground penetration min="+minGround.ToString("F5"));
                Check(maxSlide<.012f,action+" planted ankle travel/frame max="+maxSlide.ToString("F5"));
                checks.Add(action+" "+frames+" frames: finite skin and fixed bone lengths");
                if(lateral)
                {
                    checks.Add($"{action} foot lane={minLane:F4} knee lane={minKneeLane:F4} leg clearance={minClearance:F4}");
                    if(!allowCrossedLegs)
                    {
                        if(!turning){Check(minLane>.18f,action+" feet stay in separate travel lanes");Check(minKneeLane>.12f,action+" knees stay in separate travel lanes");}
                        Check(minClearance>.12f,action+" legs do not intersect");
                    }
                }
            }
            Destroy(plane);Destroy(fill.gameObject);rt.Release();Destroy(image);Destroy(rt);
        }
        void SaveFrame(string path)
        {camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=old;}
        static Vector3[] BakedPoints(PrototypeSoldierArtRig rig)
        {var points=new List<Vector3>();foreach(var r in rig.GetComponent<LODGroup>().GetLODs()[0].renderers.Cast<SkinnedMeshRenderer>()){var mesh=new Mesh();r.BakeMesh(mesh);points.AddRange(mesh.vertices.Select(v=>r.transform.TransformPoint(v)));Destroy(mesh);}return points.ToArray();}
        static string Height(object o,string name,float floor)=>(((Transform)Get(o,name)).position.y-floor).ToString("F5");
        // Independent closest-point distance for the four thigh/shin segment
        // pairs; this detects crossing even when both legs keep valid lengths.
        static float SegmentDistance(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            Vector3 u=b-a,v=d-c,w=a-c;float aa=Vector3.Dot(u,u),bb=Vector3.Dot(u,v),cc=Vector3.Dot(v,v),dd=Vector3.Dot(u,w),ee=Vector3.Dot(v,w);
            float denominator=aa*cc-bb*bb;
            float s=denominator>.000001f?Mathf.Clamp01((bb*ee-cc*dd)/denominator):0;
            float t=(bb*s+ee)/cc;
            if(t<0){t=0;s=Mathf.Clamp01(-dd/aa);}else if(t>1){t=1;s=Mathf.Clamp01((bb-dd)/aa);}
            return Vector3.Distance(a+u*s,c+v*t);
        }
        void Check(bool b,string message){if(!b)throw new Exception(message);checks.Add(message);}
        void Observe(string m,string t,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
        static object Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Hidden).Invoke(o,args);
    }
}
#endif
