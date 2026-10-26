using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeInfantryStanceSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int PreviewLayer = 30;
        private const int TileWidth = 640;
        private const int TileHeight = 480;
        private static GameObject gallery;
        private static readonly MethodInfo Pose = typeof(PrototypeSoldierVisual).GetMethod("Pose", Private);

        [MenuItem("IRONFRONT/Verify Infantry Stances", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Infantry Stances")]
        public static void Verify()
        {
            PrototypePlayer player = null;
            bool crouchedBefore = false;
            bool proneBefore = false;
            bool firstPersonBefore = false;
            float slideBefore = 0f;
            bool stateSaved = false;
            try
            {
                PrototypeRuntime game = FindGame();
                player = game.Player;
                player.ClearHeldEquipment();
                Check(player.Alive && player.CurrentVehicle == null, "player is alive and on foot");
                PrototypeSoldierVisual visual = player.GetComponent<PrototypeSoldierVisual>();
                Check(visual != null && Pose != null, "articulated infantry rig exists");
                crouchedBefore = Flag(player, "crouched");
                proneBefore = Flag(player, "prone");
                slideBefore = player.Slide.EndsAt;
                firstPersonBefore = player.IsFirstPerson;
                stateSaved = true;
                player.Slide.EndsAt = 0f;
                if (firstPersonBefore) player.TogglePerspective();
                visual.SetDowned(false);

                SetStance(player, visual, false, false);
                Transform model = player.transform.Find("Armored infantry visual");
                Transform pelvis = model.Find("Pelvis");
                Transform torso = pelvis.Find("Torso");
                Transform head = torso.Find("Head");
                Transform leftLeg = pelvis.Find("Left leg");
                Transform knee = leftLeg.Find("Knee");
                Renderer boot = knee.Find("Ankle/Boot").GetComponent<Renderer>();
                Renderer helmet = head.Find("Helmet shell").GetComponent<Renderer>();
                float ground = model.position.y;
                float standingTop = helmet.bounds.max.y - ground;
                float standingHeadY = helmet.bounds.center.y;
                Check(standingTop > 1.7f && standingTop < 1.95f,
                    "standing soldier has full human height");
                Check(Vector3.Dot(torso.up, model.up) > .999f,
                    "standing torso is upright");
                Check(Vector3.Distance(model.localScale, Vector3.one) < .001f,
                    "standing body has original proportions");
                Check(Mathf.Abs(boot.bounds.min.y - ground) < .025f,
                    "standing boot sole touches the ground");

                SetStance(player, visual, true, false);
                Check(helmet.bounds.center.y < standingHeadY - .45f &&
                    helmet.bounds.max.y - ground < standingTop * .79f,
                    "crouch visibly lowers the head with a bent body");
                Check(Quaternion.Angle(knee.localRotation, Quaternion.identity) > 80f,
                    "crouch bends the knee rather than squashing the mesh");
                Check(Vector3.Dot(torso.up, model.up) > .75f,
                    "crouching torso stays mostly upright");
                Check(Mathf.Abs(boot.bounds.min.y - ground) < .025f &&
                    Vector3.Distance(model.localScale, Vector3.one) < .001f,
                    "crouch keeps boots planted and body proportions intact");
                Check(Vector3.Dot(visual.WeaponMount.forward, model.forward) > .999f,
                    "crouched rifle remains horizontal and forward");

                SetStance(player, visual, false, true);
                Check(Vector3.Dot(torso.up, model.forward) > .98f &&
                    Mathf.Abs(Vector3.Dot(torso.up, model.up)) < .15f,
                    "prone torso lies along the ground");
                Check(helmet.bounds.max.y - ground < .8f &&
                    pelvis.position.y - ground < .35f,
                    "prone head and hips are close to the ground");
                Check(Vector3.Dot(leftLeg.TransformDirection(Vector3.down), model.forward) < -.75f,
                    "prone legs extend backwards behind the torso");
                Check(Mathf.Abs(boot.bounds.min.y - ground) < .025f &&
                    Vector3.Distance(model.localScale, Vector3.one) < .001f,
                    "prone boots stay above the ground without vertical scaling");
                Check(Vector3.Dot(head.forward, model.forward) > .96f &&
                    Vector3.Dot(visual.WeaponMount.forward, model.forward) > .999f,
                    "prone soldier looks and aims forward");
                Check(torso.Find("Class equipment") != null &&
                    torso.Find("Backpack") != null,
                    "backpack and class gear follow the articulated torso");
                Vector3 restingFoot = boot.transform.position;
                Pose.Invoke(visual, new object[] { 0f, 1f, .85f, .5f });
                Check(Vector3.Distance(restingFoot, boot.transform.position) > .02f,
                    "crawling advances alternate legs");
                Check(boot.bounds.min.y >= model.position.y - .03f,
                    "crawl does not drag a boot below the ground");
                Pose.Invoke(visual, new object[] { 1f, 0f, .85f, .5f });
                Check(Quaternion.Angle(knee.localRotation, Quaternion.identity) > 65f &&
                    boot.bounds.min.y >= model.position.y - .03f,
                    "crouch walking retains bent knees and ground clearance");

                SetStance(player, visual, false, false);
                visual.SetDowned(true);
                Check(pelvis.position.y - model.position.y < .35f &&
                    Vector3.Distance(model.localScale, Vector3.one) < .001f,
                    "downed infantry uses a ground pose with full proportions");
                visual.SetDowned(false);
                Check(helmet.bounds.max.y - model.position.y > 1.7f &&
                    Vector3.Dot(torso.up, model.up) > .999f,
                    "revival immediately restores an upright body");
                SetStance(player, visual, false, true);
                player.TogglePerspective();
                Check(player.IsFirstPerson && !visual.HeldWeapon.activeInHierarchy,
                    "first person still hides the infantry body");
                player.TogglePerspective();
                Check(!player.IsFirstPerson && visual.HeldWeapon.activeInHierarchy &&
                    Mathf.Abs(Vector3.Dot(torso.up, model.up)) < .15f,
                    "third person restores the current prone pose");
                Capture(game);
                Debug.Log("IRONFRONT infantry stances PASS: full-height stand, bent-knee crouch, " +
                    "horizontal prone, ground contact, crawl, equipment, revival and perspective restoration");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT infantry stances FAIL: " + error); }
            finally
            {
                if (player != null && stateSaved)
                {
                    PrototypeSoldierVisual visual = player.GetComponent<PrototypeSoldierVisual>();
                    visual.SetDowned(false);
                    SetStance(player, visual, crouchedBefore, proneBefore);
                    player.Slide.EndsAt = slideBefore;
                    if (player.IsFirstPerson != firstPersonBefore) player.TogglePerspective();
                    visual.RefreshPose(true);
                }
            }
        }

        [MenuItem("IRONFRONT/Capture Infantry Stances", true)]
        private static bool CanCapture() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Capture Infantry Stances")]
        public static void Capture() => Capture(FindGame());

        [MenuItem("IRONFRONT/Preview Infantry Stances", true)]
        private static bool CanPreview() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Preview Infantry Stances")]
        public static void Preview()
        {
            PrototypeRuntime game = FindGame();
            BuildGallery(game);
            Camera camera = gallery.GetComponentInChildren<Camera>();
            camera.enabled = true;
            camera.orthographicSize = 2.05f;
            camera.transform.position = gallery.transform.position + new Vector3(5f, 2.6f, 6f);
            camera.transform.LookAt(gallery.transform.position + new Vector3(0f, .85f, 0f));
            camera.depth = 100f;
            Debug.Log("IRONFRONT stance preview: stand / crouch / prone; " +
                "use Clear Infantry Stance Preview to return to the player camera");
        }

        [MenuItem("IRONFRONT/Clear Infantry Stance Preview")]
        public static void ClearPreview()
        {
            if (gallery != null) UnityEngine.Object.DestroyImmediate(gallery);
            gallery = null;
        }

        private static void Capture(PrototypeRuntime game)
        {
            ClearPreview();
            BuildGallery(game);
            Camera camera = gallery.GetComponentInChildren<Camera>();
            TextMesh label = gallery.GetComponentInChildren<TextMesh>();
            var combined = new Texture2D(TileWidth * 3, TileHeight * 2, TextureFormat.RGB24, false);
            var target = new RenderTexture(TileWidth, TileHeight, 24);
            RenderTexture previous = RenderTexture.active;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Verification046"));
            Directory.CreateDirectory(folder);
            try
            {
                camera.enabled = false;
                camera.targetTexture = target;
                string[] stances = { "standing", "crouching", "prone" };
                for (int view = 0; view < 2; view++)
                {
                    for (int stance = 0; stance < 3; stance++)
                    {
                        Transform actor = gallery.transform.Find(stances[stance]);
                        for (int i = 0; i < 3; i++)
                            gallery.transform.Find(stances[i]).gameObject.SetActive(i == stance);
                        Vector3 center = actor.position + new Vector3(0f, .95f, .12f);
                        camera.transform.position = center + (view == 0 ? Vector3.right : Vector3.forward) * 5f;
                        camera.transform.LookAt(center);
                        label.text = stances[stance].ToUpperInvariant() + (view == 0 ? " / SIDE" : " / FRONT");
                        label.transform.position = actor.position + new Vector3(0f, 2.015f, .12f);
                        label.transform.rotation = camera.transform.rotation;
                        camera.Render();
                        RenderTexture.active = target;
                        var tile = new Texture2D(TileWidth, TileHeight, TextureFormat.RGB24, false);
                        tile.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), 0, 0);
                        tile.Apply();
                        File.WriteAllBytes(Path.Combine(folder,
                            "infantry-" + stances[stance] + (view == 0 ? "-side.png" : "-front.png")), tile.EncodeToPNG());
                        combined.SetPixels(stance * TileWidth, (1 - view) * TileHeight,
                            TileWidth, TileHeight, tile.GetPixels());
                        UnityEngine.Object.DestroyImmediate(tile);
                    }
                }
                combined.Apply();
                string path = Path.Combine(folder, "infantry-stances.png");
                File.WriteAllBytes(path, combined.EncodeToPNG());
                Debug.Log("IRONFRONT stance capture: " + path);
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
            gallery = new GameObject("Infantry stance verification gallery");
            gallery.transform.position = new Vector3(0f, -200f, 0f);
            string[] names = { "standing", "crouching", "prone" };
            for (int i = 0; i < 3; i++)
            {
                var actor = new GameObject(names[i]);
                actor.transform.SetParent(gallery.transform, false);
                actor.transform.localPosition = new Vector3((i - 1) * 2.25f, 0f, 0f);
                PrototypeSoldierVisual visual = actor.AddComponent<PrototypeSoldierVisual>();
                visual.Initialize(game, PrototypeTeam.Blue, PrototypeInfantryClass.Assault, 0f);
                visual.enabled = false;
                Pose.Invoke(visual, new object[] { i == 1 ? 1f : 0f, i == 2 ? 1f : 0f, 0f, 0f });
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Ground plane";
                floor.transform.SetParent(actor.transform, false);
                floor.transform.localPosition = new Vector3(0f, -.035f, .1f);
                floor.transform.localScale = new Vector3(2.1f, .06f, 3.1f);
                floor.GetComponent<Renderer>().sharedMaterial = game.BlueTrimMaterial;
                UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            }
            var cameraObject = new GameObject("Stance verification camera");
            cameraObject.transform.SetParent(gallery.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.22f;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 20f;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.83f, .88f, .92f);
            var lightObject = new GameObject("Stance verification light");
            lightObject.transform.SetParent(gallery.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(42f, -28f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.cullingMask = 1 << PreviewLayer;
            var title = new GameObject("Stance title");
            title.transform.SetParent(gallery.transform, false);
            TextMesh text = title.AddComponent<TextMesh>();
            text.fontSize = 48;
            text.characterSize = .035f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(.13f, .20f, .24f);
            foreach (Transform item in gallery.GetComponentsInChildren<Transform>(true))
                item.gameObject.layer = PreviewLayer;
        }

        private static PrototypeRuntime FindGame()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            Check(game != null && game.Player != null, "SampleScene Play has a player");
            if (!game.MatchStarted)
            {
                game.ConfigureSoloTeams(8);
                game.Match.ConfigureSolo(PrototypeGameMode.Conquest);
                game.BeginBattle();
            }
            game.Frontend?.Close();
            return game;
        }

        private static bool Flag(PrototypePlayer player, string field) =>
            (bool)typeof(PrototypePlayer).GetField(field, Private).GetValue(player);

        private static void SetStance(PrototypePlayer player, PrototypeSoldierVisual visual,
            bool crouched, bool prone)
        {
            typeof(PrototypePlayer).GetField("crouched", Private).SetValue(player, crouched);
            typeof(PrototypePlayer).GetField("prone", Private).SetValue(player, prone);
            visual.RefreshPose(true);
        }

        private static void Check(bool value, string expectation)
        {
            if (!value) throw new InvalidOperationException(expectation);
        }
    }
}
