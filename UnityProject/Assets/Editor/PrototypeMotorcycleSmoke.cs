using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Exercises the actual driver rig, seat authority and camera lifecycle.
    // Preview fixtures are Play-only and never write user settings or scenes.
    public static class PrototypeMotorcycleSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo Enter = typeof(PrototypePlayer).GetMethod("EnterVehicle", Private);
        private static readonly MethodInfo CameraUpdate = typeof(PrototypePlayer).GetMethod("UpdateVehicleCamera", Private);
        private static PrototypeRuntime previewGame;
        private static PrototypeCombinedArmsVehicle previewBike;
        private static GameObject previewFixture;
        private static readonly Dictionary<Behaviour, bool> frozen = new Dictionary<Behaviour, bool>();
        private static bool capturing, requested;
        private static string capturePath;
        private static int preparedFrame, capturedFrame;
        private static double captureStarted;

        [MenuItem("IRONFRONT/Verify Motorcycle Rider", true)]
        private static bool CanVerify() => EditorApplication.isPlaying && !capturing;

        [MenuItem("IRONFRONT/Verify Motorcycle Rider")]
        public static void Verify()
        {
            PrototypeRuntime game = null;
            PrototypeCombinedArmsVehicle bike = null;
            GameObject fixture = null;
            PrototypePlayer player = null;
            Vector3 savedPosition = Vector3.zero;
            Quaternion savedRotation = Quaternion.identity;
            bool savedVehicleFirstPerson = true;
            bool savedState = false;
            try
            {
                game = FindGame(); player = game.Player;
                Check(player.Alive && player.CurrentVehicle == null, "fresh living on-foot player required");
                savedPosition = player.transform.position; savedRotation = player.transform.rotation;
                savedVehicleFirstPerson = (bool)GetField(player, "vehicleFirstPerson"); savedState = true;
                bool infantryFirstPerson = player.IsFirstPerson;
                fixture = new GameObject("EDITOR ONLY - Motorcycle rider verification");
                bike = fixture.AddComponent<PrototypeCombinedArmsVehicle>();
                bike.Initialize(game, player.Team, PrototypeVehicleKind.Motorcycle, new Vector2(-325f, -320f), false);
                bike.RespawnEnabled = false;
                game.Vehicles.Add(bike);
                SetField(player, "vehicleFirstPerson", true);
                Check((bool)Enter.Invoke(player, new object[] { bike }), "actual driver seat accepts player");
                PrototypeSoldierVisual visual = player.GetComponent<PrototypeSoldierVisual>();
                Transform model = player.transform.Find("Armored infantry visual");
                Transform pelvis = model.Find("Pelvis");
                Transform torso = pelvis.Find("Torso");
                Transform leftHand = torso.Find("Left arm/Elbow/Hand");
                Transform rightHand = torso.Find("Right arm/Elbow/Hand");
                Transform leftFoot = pelvis.Find("Left leg/Knee/Ankle");
                Transform rightFoot = pelvis.Find("Right leg/Knee/Ankle");
                PrototypeCombinedVehicleVisual vehicleModel = (PrototypeCombinedVehicleVisual)GetField(bike, "model");
                Check(bike.SeatCount == 2 && !bike.SeatCanFire, "existing two unarmed seats preserved");
                Check(vehicleModel.Hull.size == new Vector3(.78f, 1.6f, 2.55f), "gameplay hull remains unchanged");
                foreach (Collider collider in fixture.GetComponentsInChildren<Collider>())
                    Check(collider == vehicleModel.Hull || !collider.enabled, "new detail has no gameplay colliders");
                foreach (float steering in new[] { -1f, 0f, 1f })
                {
                    bike.transform.rotation = Quaternion.Euler(0f, 67f, 0f);
                    for (int i = 0; i < 35; i++) bike.ApplyPlayerInput(.2f, steering, 0f, false, false, .05f);
                    Refresh(player, visual);
                    CheckHandsAndFeet(bike, leftHand, rightHand, leftFoot, rightFoot);
                    Check(Vector3.Distance(pelvis.position, bike.SeatPosition) < .003f, "pelvis stays on actual saddle");
                    Check(Quaternion.Angle(player.transform.rotation, bike.MotorcyclePoseRotation) < .01f,
                        "rider leans with visual chassis");
                    Check(Vector3.Distance(player.ViewCamera.transform.position, bike.CameraPosition) < .003f,
                        "first-person camera stays at the real driver eye");
                }
                Check(Quaternion.Angle(vehicleModel.MotorcycleSteering.localRotation, Quaternion.identity) > 20f,
                    "handlebar and front fork follow steering input");
                Check(Quaternion.Angle(vehicleModel.MotorcycleFrontWheel.localRotation, Quaternion.identity) > 1f &&
                    Quaternion.Angle(vehicleModel.MotorcycleRearWheel.localRotation, Quaternion.identity) > 1f,
                    "front and rear wheels rotate with traveled speed");
                Check(leftHand.Find("Glove").GetComponent<Renderer>().enabled &&
                    rightHand.Find("Glove").GetComponent<Renderer>().enabled &&
                    !torso.Find("Uniform").GetComponent<Renderer>().enabled &&
                    !torso.Find("Head/Helmet shell").GetComponent<Renderer>().enabled,
                    "first person shows exactly the real arms without body/head occlusion");
                Check(!visual.HeldWeapon.activeInHierarchy &&
                    !((GameObject)GetField(player, "firstPersonWeapon")).activeInHierarchy,
                    "both weapon models are hidden while driving");
                Vector3 leftScreen = player.ViewCamera.WorldToViewportPoint(leftHand.position);
                Vector3 rightScreen = player.ViewCamera.WorldToViewportPoint(rightHand.position);
                Debug.Log("IRONFRONT motorcycle viewport diagnostic: screen=" + Screen.width + "x" + Screen.height +
                    " aspect=" + player.ViewCamera.aspect + " fov=" + player.ViewCamera.fieldOfView +
                    " camera=" + player.ViewCamera.transform.position.ToString("F3") +
                    " look=" + player.ViewCamera.transform.eulerAngles.ToString("F3") +
                    " left=" + leftHand.position.ToString("F3") + " => " + leftScreen.ToString("F3") +
                    " right=" + rightHand.position.ToString("F3") + " => " + rightScreen.ToString("F3"));
                string diagnosticDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "Builds", "Verification052");
                Directory.CreateDirectory(diagnosticDirectory);
                RenderCameraScreenshot(player.ViewCamera, Path.Combine(diagnosticDirectory, "motorcycle-grip-diagnostic.png"));
                TextMesh instruments = vehicleModel.Instruments;
                Renderer digits = instruments.GetComponent<Renderer>();
                Debug.Log("IRONFRONT motorcycle LCD diagnostic: text=" + instruments.text.Replace("\n", " / ") +
                    " font=" + (instruments.font != null ? instruments.font.name : "NONE") +
                    " shader=" + (digits.sharedMaterial != null ? digits.sharedMaterial.shader.name : "NONE") +
                    " characterSize=" + instruments.characterSize + " fontSize=" + instruments.fontSize +
                    " localBounds=" + digits.localBounds.size.ToString("F5") +
                    " worldBounds=" + digits.bounds.size.ToString("F5"));
                Check(instruments.font != null && digits.sharedMaterial == instruments.font.material &&
                    digits.sharedMaterial.mainTexture != null && !string.IsNullOrEmpty(instruments.text),
                    "LCD has a real font atlas/material and live speed/health text");
                Check(digits.localBounds.size.x > .02f && digits.localBounds.size.y > .02f &&
                    digits.localBounds.size.x < .28f && digits.localBounds.size.y < .14f,
                    "LCD text mesh is nonempty and fits its physical panel");
                Check(instruments.transform.localPosition.z < -.03f &&
                    Vector3.Dot(-instruments.transform.forward,
                        (player.ViewCamera.transform.position - instruments.transform.position).normalized) > .7f &&
                    Vector3.Dot(instruments.transform.right, player.ViewCamera.transform.right) > .85f,
                    "LCD digits sit ahead of backing and read toward the driver without mirroring");
                Check(leftScreen.z > 0f && leftScreen.x > 0f && leftScreen.x < 1f && leftScreen.y > .01f && leftScreen.y < .65f &&
                    rightScreen.z > 0f && rightScreen.x > 0f && rightScreen.x < 1f && rightScreen.y > .01f && rightScreen.y < .65f,
                    "both held grips are visibly within the lower first-person view");
                float savedAspect = player.ViewCamera.aspect;
                foreach (float aspect in new[] { .6f, 4f / 3f, 1.6f, 16f / 9f, 21f / 9f })
                {
                    player.ViewCamera.aspect = aspect; Refresh(player, visual);
                    leftScreen = player.ViewCamera.WorldToViewportPoint(leftHand.position);
                    rightScreen = player.ViewCamera.WorldToViewportPoint(rightHand.position);
                    Check(leftScreen.z > 0f && leftScreen.x > .04f && leftScreen.x < .96f && leftScreen.y > .06f && leftScreen.y < .65f &&
                        rightScreen.z > 0f && rightScreen.x > .04f && rightScreen.x < .96f && rightScreen.y > .06f && rightScreen.y < .65f,
                        "both real grip centers have visible margin at aspect " + aspect);
                }
                player.ViewCamera.aspect = savedAspect; Refresh(player, visual);
                player.TogglePerspective(); Refresh(player, visual);
                Check(!player.IsFirstPerson && torso.Find("Uniform").GetComponent<Renderer>().enabled &&
                    torso.Find("Head/Helmet shell").GetComponent<Renderer>().enabled,
                    "third person restores the complete same rider");
                CheckHandsAndFeet(bike, leftHand, rightHand, leftFoot, rightFoot);
                Check(Vector3.Distance(player.ViewCamera.transform.position, bike.SeatPosition) > 3f,
                    "external camera follows behind the bike at riding height");
                Check(player.TrySwitchVehicleSeat(1), "second seat accepts passenger"); Refresh(player, visual);
                Check(!bike.SeatCanDrive && !bike.SeatCanFire, "passenger cannot steer or shoot");
                CheckHandsAndFeet(bike, leftHand, rightHand, leftFoot, rightFoot);
                Check(Vector3.Distance(leftHand.position, bike.MotorcycleGripTarget(true, 0).position) > .6f,
                    "passenger holds own rear rail rather than driver handlebars");
                float speed = bike.Speed; Quaternion before = bike.transform.rotation;
                bike.ApplyPlayerInput(1f, -1f, 0f, false, false, .05f);
                Check(Mathf.Abs(bike.Speed - speed) < .001f && Quaternion.Angle(before, bike.transform.rotation) < .001f,
                    "seat authority prevents passenger steering");
                player.TogglePerspective(); Refresh(player, visual);
                Check(Vector3.Distance(player.ViewCamera.transform.position, bike.GetSeatCameraPosition(1)) < .003f,
                    "passenger eye point differs from driver station");
                player.ForceExitVehicle();
                Check(player.CurrentVehicle == null && player.IsFirstPerson == infantryFirstPerson,
                    "exit restores saved infantry perspective");
                Check(torso.Find("Uniform").GetComponent<Renderer>().enabled &&
                    torso.Find("Head/Helmet shell").GetComponent<Renderer>().enabled,
                    "exit restores body renderers for later infantry views");
                Check(Vector3.Distance(model.localPosition, new Vector3(0f, -.86f, 0f)) > .1f,
                    "exit restores normal ground stance instead of the riding origin");
                Debug.Log("IRONFRONT motorcycle rider PASS: real two-bone hands/feet contact both steering extremes, " +
                    "saddle/chassis tilt, wheel/fork motion, visible single first-person arms, third-person full rider, " +
                    "distinct passenger anchors and authority, seat eye/chase views, readable font-atlas LCD geometry, exit visibility/stance restore");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT motorcycle rider FAIL: " + error); }
            finally
            {
                if (player != null && ReferenceEquals(player.CurrentVehicle, bike)) player.ForceExitVehicle();
                if (savedState && player != null)
                {
                    SetField(player, "vehicleFirstPerson", savedVehicleFirstPerson);
                    var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
                    player.transform.SetPositionAndRotation(savedPosition, savedRotation); controller.enabled = true;
                    typeof(PrototypePlayer).GetMethod("PositionInfantryCamera", Private).Invoke(player, new object[] { 1f });
                    player.GetComponent<PrototypeSoldierVisual>().RefreshPose(true);
                }
                if (game != null && bike != null) game.Vehicles.Remove(bike);
                if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        private static void CheckHandsAndFeet(PrototypeCombinedArmsVehicle bike, Transform leftHand,
            Transform rightHand, Transform leftFoot, Transform rightFoot)
        {
            int seat = bike.PlayerSeat;
            Check(Vector3.Distance(leftHand.position, bike.MotorcycleGripTarget(true, seat).position) < .012f &&
                Vector3.Distance(rightHand.position, bike.MotorcycleGripTarget(false, seat).position) < .012f,
                "hands contact actual moving grips for seat " + seat);
            Check(Vector3.Distance(leftFoot.position, bike.MotorcyclePegTarget(true, seat).position) < .012f &&
                Vector3.Distance(rightFoot.position, bike.MotorcyclePegTarget(false, seat).position) < .012f,
                "boot soles stay on actual peg targets for seat " + seat);
            Check(Quaternion.Angle(leftHand.rotation, bike.MotorcycleGripTarget(true, seat).rotation) < .01f &&
                Quaternion.Angle(rightHand.rotation, bike.MotorcycleGripTarget(false, seat).rotation) < .01f,
                "gloves rotate with actual handlebars");
        }

        [MenuItem("IRONFRONT/Preview Motorcycle First Person", true)]
        [MenuItem("IRONFRONT/Preview Motorcycle Steering First Person", true)]
        [MenuItem("IRONFRONT/Preview Motorcycle Third Person", true)]
        [MenuItem("IRONFRONT/Preview Motorcycle Passenger", true)]
        private static bool CanPreview() => EditorApplication.isPlaying && !capturing;
        [MenuItem("IRONFRONT/Preview Motorcycle First Person")]
        private static void PreviewFirstPerson() => Preview(true, 0, "motorcycle-first-person.png");
        [MenuItem("IRONFRONT/Preview Motorcycle Steering First Person")]
        private static void PreviewSteeringFirstPerson() => Preview(true, 0, "motorcycle-first-person-steering.png", true);
        [MenuItem("IRONFRONT/Preview Motorcycle Third Person")]
        private static void PreviewThirdPerson() => Preview(false, 0, "motorcycle-third-person.png");
        [MenuItem("IRONFRONT/Preview Motorcycle Passenger")]
        private static void PreviewPassenger() => Preview(false, 1, "motorcycle-passenger.png");

        public static void RenderBatchPreviews()
        {
            Check(Application.isBatchMode, "batch rendering entry point requires batch Editor");
            Preview(true, 0, "motorcycle-first-person.png");
            Preview(true, 0, "motorcycle-first-person-steering.png", true);
            Preview(false, 0, "motorcycle-third-person.png");
            Preview(false, 1, "motorcycle-passenger.png");
            previewGame?.Player.ForceExitVehicle();
            if (previewBike != null) previewGame?.Vehicles.Remove(previewBike);
            if (previewFixture != null) UnityEngine.Object.DestroyImmediate(previewFixture);
        }

        private static void Preview(bool firstPerson, int seat, string filename, bool steering = false)
        {
            try
            {
                RestoreActors();
                PrototypeRuntime game = FindGame(); PrototypePlayer player = game.Player;
                Check(player.Alive && !game.Match.Winner.HasValue, "fresh active battle required");
                EditorApplication.isPaused = false;
                if (!Application.isBatchMode) EditorApplication.ExecuteMenuItem("Window/General/Game");
                player.ForceExitVehicle();
                if (previewFixture != null)
                {
                    previewGame?.Vehicles.Remove(previewBike);
                    UnityEngine.Object.DestroyImmediate(previewFixture);
                }
                Freeze(player);
                foreach (PrototypeBot bot in game.Bots) Freeze(bot);
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                    if (vehicle is Behaviour behaviour) Freeze(behaviour);
                game.HitFeedback?.Reset(); game.ScorePopup?.Reset();
                previewGame = game;
                previewFixture = new GameObject("EDITOR ONLY - Actual motorcycle rider preview");
                previewBike = previewFixture.AddComponent<PrototypeCombinedArmsVehicle>();
                previewBike.Initialize(game, player.Team, PrototypeVehicleKind.Motorcycle, new Vector2(-325f, -320f), false);
                previewBike.RespawnEnabled = false; game.Vehicles.Add(previewBike);
                Freeze(previewBike);
                SetField(player, "vehicleFirstPerson", firstPerson);
                Check((bool)Enter.Invoke(player, new object[] { previewBike }), "player enters real bike");
                if (steering)
                    for (int i = 0; i < 30; i++) previewBike.ApplyPlayerInput(.2f, 1f, 0f, false, false, .05f);
                if (seat != 0) Check(player.TrySwitchVehicleSeat(seat), "real passenger transfer");
                if (!firstPerson)
                { SetField(player, "vehicleLookYaw", 48f); SetField(player, "pitch", 19f); }
                Refresh(player, player.GetComponent<PrototypeSoldierVisual>());
                typeof(PrototypeCombinedArmsVehicle).GetMethod("RefreshInstrumentDisplay", Private).Invoke(previewBike, null);
                string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Verification052");
                Directory.CreateDirectory(directory); capturePath = Path.Combine(directory, filename);
                if (File.Exists(capturePath)) File.Delete(capturePath);
                if (Application.isBatchMode)
                {
                    RenderCameraScreenshot(player.ViewCamera, capturePath);
                    RestoreActors();
                    Debug.Log("IRONFRONT motorcycle batch camera screenshot saved: " + capturePath);
                    return;
                }
                preparedFrame = Time.frameCount; captureStarted = EditorApplication.timeSinceStartup;
                capturing = true; requested = false;
                EditorApplication.update -= Capture; EditorApplication.update += Capture;
                Debug.Log("IRONFRONT motorcycle preview prepared: " + filename + " with actual rider rig and gameplay camera; stop Play removes fixture");
            }
            catch (Exception error)
            {
                EndCapture(false); Debug.LogError("IRONFRONT motorcycle preview FAIL: " + error);
            }
        }

        private static void Capture()
        {
            try
            {
                if (!EditorApplication.isPlaying || previewGame == null) { EndCapture(false); return; }
                if (EditorApplication.timeSinceStartup - captureStarted > 8d)
                    throw new InvalidOperationException("Game frames or screenshot did not complete");
                if (!requested)
                {
                    if (Time.frameCount < preparedFrame + 2) return;
                    ScreenCapture.CaptureScreenshot(capturePath, 2); capturedFrame = Time.frameCount; requested = true; return;
                }
                if (Time.frameCount < capturedFrame + 2 || !File.Exists(capturePath) || new FileInfo(capturePath).Length == 0) return;
                EndCapture(true); Debug.Log("IRONFRONT motorcycle screenshot saved: " + capturePath);
            }
            catch (Exception error) { EndCapture(true); Debug.LogError("IRONFRONT motorcycle screenshot FAIL: " + error); }
        }
        private static void EndCapture(bool pause)
        {
            EditorApplication.update -= Capture; capturing = false;
            if (pause && EditorApplication.isPlaying) EditorApplication.isPaused = true;
            RestoreActors();
        }
        private static void RenderCameraScreenshot(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 800, 24);
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            float previousFieldOfView = camera.fieldOfView;
            try
            {
                Physics.SyncTransforms();
                camera.targetTexture = target; camera.aspect = 1.6f;
                PrototypePlayer player = camera.GetComponentInParent<PrototypePlayer>();
                if (player != null && player.CurrentVehicle != null) CameraUpdate.Invoke(player, null);
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, 1280f, 800f), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; camera.aspect = previousAspect;
                camera.fieldOfView = previousFieldOfView;
                RenderTexture.active = previousActive;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
        }
        private static void Freeze(Behaviour actor)
        {
            if (actor == null || frozen.ContainsKey(actor)) return;
            frozen.Add(actor, actor.enabled); actor.enabled = false;
        }
        private static void RestoreActors()
        {
            foreach (KeyValuePair<Behaviour, bool> state in frozen)
                if (state.Key != null) state.Key.enabled = state.Value;
            frozen.Clear();
        }
        private static void Refresh(PrototypePlayer player, PrototypeSoldierVisual visual)
        { CameraUpdate.Invoke(player, null); visual.RefreshPose(true); }
        private static PrototypeRuntime FindGame()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            Check(game != null && game.Player != null && !game.IsNetworkReplica, "Solo/host runtime exists");
            if (!game.MatchStarted) game.BeginBattle();
            game.Frontend?.Close(); game.SetSupportMenu(false);
            if (game.PauseOpen) typeof(PrototypeRuntime).GetMethod("SetPause", Private).Invoke(game, new object[] { false });
            return game;
        }
        private static object GetField(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Check(bool value, string expectation)
        { if (!value) throw new InvalidOperationException(expectation); }
    }
}
