using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeInfantryLocomotionSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int PreviewLayer = 30;
        private const int TileWidth = 440;
        private const int TileHeight = 500;
        private static readonly MethodInfo Pose = typeof(PrototypeSoldierVisual).GetMethod("Pose", Private);
        private static GameObject gallery;

        [MenuItem("IRONFRONT/Verify Infantry Locomotion", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Infantry Locomotion")]
        public static void Verify()
        {
            GameObject fixture=null;
            try
            {
                var game=FindGame();fixture=new GameObject("Skinned motion verification");fixture.transform.position=new Vector3(0,-200,0);
                Check(Pose!=null,"Runtime pose exists");int samples=0;
                foreach(var team in new[]{PrototypeTeam.Blue,PrototypeTeam.Red})
                foreach(var weapon in new[]{PrototypeWeaponId.Carbine,PrototypeWeaponId.Smg,PrototypeWeaponId.Pistol})
                {
                    var visual=CreateActor(fixture.transform,game,weapon,team);
                    var model=visual.transform.Find("Armored infantry visual");
                    foreach(string stance in new[]{"idle","walk","run","crouch","crawl","aim"})
                    for(int i=0;i<24;i++)
                    {
                        float cycle=i*Mathf.PI*2f/24f;
                        float crouch=stance=="crouch"?1:0,prone=stance=="crawl"?1:0,moving=stance=="idle"||stance=="aim"?0:1;
                        Set(visual,"runWeight",stance=="run"?1f:0f);Set(visual,"aimWeight",stance=="aim"?1f:0f);
                        Set(visual,"sprintMotionWeight",stance=="run"?1f:0f);
                        Apply(visual,crouch,prone,cycle,moving);ValidateBones(model);
                        Transform hand=model.Find("Pelvis/Torso/Right arm/Elbow/Hand");
                        Vector3 grip=visual.HeldWeapon.transform.TransformPoint(new Vector3(0,-.18f,weapon==PrototypeWeaponId.Pistol?-.045f:-.18f));
                        Check(Vector3.Distance(hand.position,grip)<.015f,team+" "+weapon+" "+stance+" right grip "+i);
                        if(stance!="crawl")ValidateGrip(visual,weapon,stance=="aim"||stance!="run");
                        Check(model.localScale==Vector3.one,"Stances preserve body scale");
                        foreach(var binding in visual.ArtRig.Bindings)
                            Check(float.IsFinite(binding.Bone.position.y),"Finite mapped skeleton");
                        if(prone>0)Check(Mathf.Abs(Vector3.Dot(model.Find("Pelvis/Torso").up,model.up))<.25f,"Prone chest stays horizontal");
                        samples++;
                    }
                    Set(visual,"runWeight",1f);Set(visual,"sprintMotionWeight",1f);Apply(visual,0,0,0,1);
                    Vector3 before=model.Find("Pelvis/Left leg/Knee/Ankle").position;
                    Apply(visual,0,0,Mathf.PI*2f-.0001f,1);
                    Check(Vector3.Distance(before,model.Find("Pelvis/Left leg/Knee/Ankle").position)<.005f,"Cycle seam continuous");
                    visual.SetDowned(true);Check(model.Find("Pelvis").localPosition.y<.35f,"Downed remains low");
                    visual.SetDowned(false);Check(model.Find("Pelvis").localPosition.y>.8f,"Revival restores height");
                    Set(visual,"motion",1f);Set(visual,"runWeight",1f);Set(visual,"sprintMotionWeight",1f);Set(visual,"lastPosition",visual.transform.position);
                    visual.transform.position+=Vector3.right*4;
                    typeof(PrototypeSoldierVisual).GetMethod("Update",Private).Invoke(visual,null);
                    typeof(PrototypeSoldierVisual).GetMethod("LateUpdate",Private).Invoke(visual,null);
                    Check((float)Get(visual,"motion")==0&&(float)Get(visual,"runWeight")==0&&visual.SprintMotionWeight==0,"Teleport resets gait and head motion");
                    visual.SetVisible(false);visual.SetVisible(true);Check(model.gameObject.activeSelf,"Perspective restores body");
                }
                VerifyPlayerWalkSprint(fixture.transform,game);
                VerifyDirectionalLegs(fixture.transform,game);
                Debug.Log("IRONFRONT infantry locomotion PASS: "+samples+" skinned poses, both teams, rifle/SMG/pistol grips, actual bone lengths, cycle seams, prone/aim/revival, teleport/view restore, Player walk/sprint sampling and 2304 directional leg samples");
            }
            catch(Exception error){Debug.LogError("IRONFRONT infantry locomotion FAIL: "+error);}
            finally{if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);}
        }

        [MenuItem("IRONFRONT/Capture Infantry Locomotion", true)]
        private static bool CanCapture() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Capture Infantry Locomotion")]
        public static void Capture() => Capture(FindGame());

        [MenuItem("IRONFRONT/Preview Infantry Locomotion", true)]
        private static bool CanPreview() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Preview Infantry Locomotion")]
        public static void Preview()
        {
            BuildGallery(FindGame());
            for (int i = 0; i < 3; i++)
                gallery.transform.Find("Actor " + i).localPosition = new Vector3((i - 1) * 2.3f, 0f, 0f);
            Camera camera = gallery.GetComponentInChildren<Camera>();
            camera.enabled = true;
            camera.depth = 100f;
            camera.orthographicSize = 2.05f;
            camera.transform.position = gallery.transform.position + new Vector3(5f, 2.5f, 6f);
            camera.transform.LookAt(gallery.transform.position + Vector3.up * .85f);
            Debug.Log("IRONFRONT locomotion preview: rifle / SMG / pistol at the running swing pose; " +
                "Clear Infantry Locomotion Preview returns to the player view");
        }

        [MenuItem("IRONFRONT/Clear Infantry Locomotion Preview")]
        public static void ClearPreview()
        {
            if (gallery != null) UnityEngine.Object.DestroyImmediate(gallery);
            gallery = null;
        }

        private static void Capture(PrototypeRuntime game)
        {
            BuildGallery(game);
            Camera camera = gallery.GetComponentInChildren<Camera>();
            TextMesh label = gallery.GetComponentInChildren<TextMesh>();
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(TileWidth, TileHeight, 24);
            var combined = new Texture2D(TileWidth * 4, TileHeight * 3, TextureFormat.RGB24, false);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Verification052"));
            Directory.CreateDirectory(folder);
            try
            {
                camera.targetTexture = target;
                string[] names = { "rifle", "smg", "pistol" };
                string[] phases = { "left-landing", "left-recovery", "right-landing", "aiming" };
                float[] cycles = { 0f, 1.45f * Mathf.PI, Mathf.PI, .5f * Mathf.PI };
                for (int view = 0; view < 2; view++)
                {
                    for (int row = 0; row < 3; row++)
                    {
                        Transform actor = gallery.transform.Find("Actor " + row);
                        PrototypeSoldierVisual visual = actor.GetComponent<PrototypeSoldierVisual>();
                        for (int i = 0; i < 3; i++) gallery.transform.Find("Actor " + i).gameObject.SetActive(i == row);
                        for (int col = 0; col < 4; col++)
                        {
                            Set(visual, "aimWeight", col == 3 ? 1f : 0f);
                            Apply(visual, 0f, 0f, cycles[col], 1f);
                            Vector3 center = actor.position + new Vector3(0f, .87f, .05f);
                            camera.transform.position = center + (view == 0 ?
                                new Vector3(4.6f, .85f, 5.7f) : new Vector3(7f, .30f, 0f));
                            camera.transform.LookAt(center);
                            label.text = names[row].ToUpperInvariant() + " / " + phases[col].ToUpperInvariant();
                            label.transform.position = actor.position + new Vector3(0f, 1.92f, 0f);
                            label.transform.rotation = camera.transform.rotation;
                            camera.Render();
                            RenderTexture.active = target;
                            var tile = new Texture2D(TileWidth, TileHeight, TextureFormat.RGB24, false);
                            tile.ReadPixels(new Rect(0f, 0f, TileWidth, TileHeight), 0, 0);
                            tile.Apply();
                            File.WriteAllBytes(Path.Combine(folder, "infantry-" + names[row] + "-" + phases[col] +
                                (view == 1 ? "-side" : "") + ".png"), tile.EncodeToPNG());
                            combined.SetPixels(col * TileWidth, (2 - row) * TileHeight, TileWidth, TileHeight, tile.GetPixels());
                            UnityEngine.Object.DestroyImmediate(tile);
                        }
                    }
                    combined.Apply();
                    string path = Path.Combine(folder, "infantry-locomotion" + (view == 1 ? "-side" : "") + ".png");
                    File.WriteAllBytes(path, combined.EncodeToPNG());
                    Debug.Log("IRONFRONT locomotion capture: " + path);
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(combined);
                ClearPreview();
            }
        }

        private static void BuildGallery(PrototypeRuntime game)
        {
            ClearPreview();
            gallery = new GameObject("Infantry locomotion gallery");
            gallery.transform.position = new Vector3(0f, -200f, 0f);
            PrototypeWeaponId[] weapons = { PrototypeWeaponId.Carbine, PrototypeWeaponId.Smg, PrototypeWeaponId.Pistol };
            for (int i = 0; i < weapons.Length; i++)
            {
                PrototypeSoldierVisual visual = CreateActor(gallery.transform, game, weapons[i]);
                visual.name = "Actor " + i;
                Set(visual, "runWeight", 1f);
                Apply(visual, 0f, 0f, 0f, 1f);
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Ground plane";
                floor.transform.SetParent(visual.transform, false);
                floor.transform.localPosition = new Vector3(0f, -.04f, .05f);
                floor.transform.localScale = new Vector3(1.8f, .06f, 2.1f);
                floor.GetComponent<Renderer>().sharedMaterial = game.BlueTrimMaterial;
                UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            }
            var cameraObject = new GameObject("Locomotion camera");
            cameraObject.transform.SetParent(gallery.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.12f;
            camera.aspect = TileWidth / (float)TileHeight;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 30f;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.82f, .88f, .92f);
            var lightObject = new GameObject("Locomotion light");
            lightObject.transform.SetParent(gallery.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(40f, -25f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            light.cullingMask = 1 << PreviewLayer;
            var title = new GameObject("Locomotion title");
            title.transform.SetParent(gallery.transform, false);
            TextMesh text = title.AddComponent<TextMesh>();
            text.fontSize = 48;
            text.characterSize = .028f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(.13f, .20f, .24f);
            foreach (Transform item in gallery.GetComponentsInChildren<Transform>(true))
                item.gameObject.layer = PreviewLayer;
        }

        private static PrototypeSoldierVisual CreateActor(Transform parent, PrototypeRuntime game,
            PrototypeWeaponId weapon, PrototypeTeam team = PrototypeTeam.Blue)
        {
            var actor = new GameObject("Locomotion " + weapon);
            actor.transform.SetParent(parent, false);
            PrototypeSoldierVisual visual = actor.AddComponent<PrototypeSoldierVisual>();
            visual.Initialize(game, team, PrototypeInfantryClass.Assault, 0f);
            visual.SetWeapon(weapon, game);
            visual.enabled = false;
            return visual;
        }

        private static void VerifyPlayerWalkSprint(Transform parent,PrototypeRuntime game)
        {
            var actor=new GameObject("Actual Player locomotion");actor.transform.SetParent(parent,false);
            var player=actor.AddComponent<PrototypePlayer>();player.enabled=false;actor.GetComponent<CharacterController>().enabled=false;
            var visual=actor.AddComponent<PrototypeSoldierVisual>();visual.Initialize(game,PrototypeTeam.Blue,PrototypeInfantryClass.Assault,0);visual.enabled=false;
            var advance=typeof(PrototypeSoldierVisual).GetMethod("AdvanceLocomotion",Private);
            var sprint=typeof(PrototypePlayer).GetField("sprinting",Private);
            const BindingFlags constants=BindingFlags.Static|BindingFlags.NonPublic;
            float walk=(float)typeof(PrototypePlayer).GetField("WalkSpeed",constants).GetRawConstantValue();
            float run=(float)typeof(PrototypePlayer).GetField("SprintSpeed",constants).GetRawConstantValue();
            sprint.SetValue(player,false);AdvanceActorFrames(actor.transform,visual,advance,walk,60);
            float walkCycle=AdvanceActorFrames(actor.transform,visual,advance,walk,60);
            Check((float)Get(visual,"runWeight")>.60f&&(float)Get(visual,"runWeight")<.70f,"Fast walk uses tactical jog blend");
            var model=actor.transform.Find("Armored infantry visual");var chest=model.Find("Pelvis/Torso");
            float walkLean=Vector3.Angle(chest.up,model.up);
            sprint.SetValue(player,true);AdvanceActorFrames(actor.transform,visual,advance,run,60);
            float runCycle=AdvanceActorFrames(actor.transform,visual,advance,run,60);
            Check((float)Get(visual,"runWeight")>.999f,"Sprint reaches running pose");
            Check(runCycle>walkCycle,"Sprint stride cadence increases with distance");
            float runStepsPerSecond=runCycle/Mathf.PI;
            Check(runStepsPerSecond>2.8f&&runStepsPerSecond<4.5f,"Sprint cadence avoids the previous five-step-per-second shuffle");
            Check(Vector3.Angle(chest.up,model.up)>walkLean+2f,"Sprint carries weight farther forward with a stable chest");
            ValidateBones(model);ValidateGrip(visual,PrototypeWeaponId.Carbine,false);
            sprint.SetValue(player,false);
            typeof(PrototypePlayer).GetField("crouched",Private).SetValue(player,true);
            float crouchSpeed=(float)typeof(PrototypePlayer).GetField("CrouchSpeed",constants).GetRawConstantValue();
            AdvanceActorFrames(actor.transform,visual,advance,crouchSpeed,60);
            float crouchSteps=AdvanceActorFrames(actor.transform,visual,advance,crouchSpeed,60)/Mathf.PI;
            Check(crouchSteps>1.8f&&crouchSteps<3.2f,"Crouch cadence avoids the previous six-step-per-second shuffle");
        }

        private static void VerifyDirectionalLegs(Transform parent,PrototypeRuntime game)
        {
            var advance=typeof(PrototypeSoldierVisual).GetMethod("AdvanceLocomotion",Private);
            foreach(var team in new[]{PrototypeTeam.Blue,PrototypeTeam.Red})
            {
                var actor=new GameObject("Directional Player "+team);actor.transform.SetParent(parent,false);
                var player=actor.AddComponent<PrototypePlayer>();player.enabled=false;actor.GetComponent<CharacterController>().enabled=false;
                var visual=actor.AddComponent<PrototypeSoldierVisual>();visual.Initialize(game,team,PrototypeInfantryClass.Assault,0);visual.enabled=false;
                foreach(bool crouch in new[]{false,true})
                foreach(Vector3 raw in new[]{Vector3.left,Vector3.right,new Vector3(-1,0,1),new Vector3(1,0,1),new Vector3(-1,0,-1),new Vector3(1,0,-1)})
                {
                    Vector3 direction=raw.normalized,across=Vector3.Cross(Vector3.up,direction);if(direction.z<0)across=-across;
                    bool sprint=!crouch&&direction.z>.1f;
                    typeof(PrototypePlayer).GetField("sprinting",Private).SetValue(player,sprint);
                    typeof(PrototypePlayer).GetField("crouched",Private).SetValue(player,crouch);
                    float speed=crouch?2:sprint?6.6f:4.2f;
                    for(int frame=0;frame<96;frame++)
                    {
                        Vector3 delta=direction*(speed/60f);actor.transform.position+=delta;
                        advance.Invoke(visual,new object[]{delta,1f/60f});visual.RefreshPose(true);
                        if(frame<30)continue;
                        Vector3 ankles=((Transform)Get(visual,"rightFoot")).position-((Transform)Get(visual,"leftFoot")).position;
                        Vector3 knees=((Transform)Get(visual,"rightKnee")).position-((Transform)Get(visual,"leftKnee")).position;
                        Check(Vector3.Dot(ankles,across)>.18f,team+" directional feet remain in separate lanes "+raw);
                        Check(Vector3.Dot(knees,across)>.12f,team+" directional knees remain in separate lanes "+raw);
                    }
                }
            }
        }

        private static float AdvanceActorFrames(Transform actor, PrototypeSoldierVisual visual,
            MethodInfo advance, float speed, int frames)
        {
            // Move the real Transform at the configured gameplay speeds. Supply
            // 60Hz to the same runtime sampler used by Update so a slow
            // editor render frame cannot turn this fixture into a teleport.
            float beforeCycle = (float)Get(visual, "gait");
            for (int i = 0; i < frames; i++)
            {
                Vector3 before = actor.position;
                actor.position += actor.forward * (speed / 60f);
                advance.Invoke(visual, new object[] { actor.position - before, 1f / 60f });
                Set(visual, "lastPosition", actor.position);
                visual.RefreshPose(true);
            }
            return (float)Get(visual, "gait") - beforeCycle;
        }

        private static void ValidateBones(Transform model)
        {
            var rig=model.GetComponentInChildren<PrototypeSoldierArtRig>();
            Transform torso = model.Find("Pelvis/Torso");
            foreach (string name in new[] { "Left arm", "Right arm" })
            {
                Transform shoulder = torso.Find(name);
                Transform elbow = shoulder.Find("Elbow");
                Check(Mathf.Abs(Vector3.Distance(shoulder.position, elbow.position) - rig.UpperArmLength) < .001f &&
                    Mathf.Abs(Vector3.Distance(elbow.position, elbow.Find("Hand").position) - rig.ForearmLength) < .001f,
                    name + " preserves upper-arm and forearm length");
            }
            foreach (string name in new[] { "Left leg", "Right leg" })
            {
                Transform hip = model.Find("Pelvis/" + name);
                Transform knee = hip.Find("Knee");
                Check(Mathf.Abs(Vector3.Distance(hip.position, knee.position) - rig.ThighLength) < .001f &&
                    Mathf.Abs(Vector3.Distance(knee.position, knee.Find("Ankle").position) - rig.ShinLength) < .001f,
                    name + " preserves thigh and shin length");
            }
        }

        private static void ValidateGrip(PrototypeSoldierVisual visual, PrototypeWeaponId weapon, bool aiming)
        {
            Transform model = visual.transform.Find("Armored infantry visual");
            Transform weaponFrame = visual.HeldWeapon.transform;
            Vector3 rightGrip = weaponFrame.TransformPoint(new Vector3(0f, -.18f,
                weapon == PrototypeWeaponId.Pistol ? -.045f : -.18f));
            Vector3 rightHand = model.Find("Pelvis/Torso/Right arm/Elbow/Hand").position;
            Check(Vector3.Distance(rightHand, rightGrip) < .012f,
                weapon + " right hand remains on the actual rendered weapon grip");
            if (weapon == PrototypeWeaponId.Carbine || aiming)
            {
                Vector3 leftGrip = weaponFrame.TransformPoint(weapon == PrototypeWeaponId.Pistol ?
                    new Vector3(-.055f, -.16f, .015f) : new Vector3(0f, -.16f, .37f));
                var authored=weaponFrame.GetComponent<PrototypeWeaponArtRig>();
                if(authored!=null)leftGrip=authored.SupportPosition;
                Vector3 leftHand = model.Find("Pelvis/Torso/Left arm/Elbow/Hand").position;
                Check(Vector3.Distance(leftHand, leftGrip) < .012f,
                    weapon + " support hand remains on the actual weapon grip");
            }
        }

        private static void Apply(PrototypeSoldierVisual visual, float crouch, float prone, float cycle, float movement)
        {
            Set(visual, "gait", cycle);
            Pose.Invoke(visual, new object[] { crouch, prone, Mathf.Sin(cycle), movement });
        }

        private static void Set(PrototypeSoldierVisual visual, string field, object value) =>
            typeof(PrototypeSoldierVisual).GetField(field, Private).SetValue(visual, value);

        private static object Get(PrototypeSoldierVisual visual, string field) =>
            typeof(PrototypeSoldierVisual).GetField(field, Private).GetValue(visual);

        private static PrototypeRuntime FindGame()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            Check(game != null && game.Player != null, "SampleScene Play has a runtime and player");
            return game;
        }

        private static void Check(bool value, string expectation)
        {
            if (!value) throw new InvalidOperationException(expectation);
        }
    }
}
