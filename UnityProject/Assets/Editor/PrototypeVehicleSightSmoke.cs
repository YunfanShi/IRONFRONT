using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeVehicleSightSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static PrototypeRuntime game;
        private static PrototypePlayer player;
        private static PrototypeTankVehicle tank;
        private static PrototypeCombinedArmsVehicle bike;
        private static GameObject fixture;
        private static Vector3 originalPosition;
        private static Quaternion originalRotation;
        private static bool originalVehicleFirstPerson, saved;
        private static float originalPitch, originalAspect;
        private static CursorLockMode originalLock;
        private static bool originalCursor;
        private static readonly Dictionary<Behaviour, bool> frozen = new Dictionary<Behaviour, bool>();
        private static readonly HashSet<PrototypeBallisticProjectile> originalRounds = new HashSet<PrototypeBallisticProjectile>();
        private static bool capturing, requested;
        private static int captureFrame, preparedFrame, capturePhase;
        private static double started;
        private static string capturePath;
        private static EditorWindow captureWindow, previousFocusedWindow;
        private static bool previousMaximized, savedWindowState;

        [MenuItem("IRONFRONT/Verify Vehicle Steering and Tank Sight", true)]
        [MenuItem("IRONFRONT/Capture Tank Sight", true)]
        private static bool CanRun() => EditorApplication.isPlaying && !saved;

        [MenuItem("IRONFRONT/Verify Vehicle Steering and Tank Sight")]
        public static void Verify()
        {
            try
            {
                Begin();
                var bikeObject = new GameObject("Actual steering test motorcycle"); bikeObject.transform.SetParent(fixture.transform);
                bike = bikeObject.AddComponent<PrototypeCombinedArmsVehicle>();
                bike.Initialize(game, player.Team, PrototypeVehicleKind.Motorcycle, new Vector2(-325f, -320f), false);
                bike.RespawnEnabled = false; game.Vehicles.Add(bike); Freeze(bike);
                Enter(bike);
                StepBike(6f, 1f);
                Check(bike.MotorcycleSteeringInput > 0f && bike.MotorcycleSteeringInput < .2f &&
                    bike.MotorcycleYawRateDegrees > 0f && bike.MotorcycleYawRateDegrees <= 8.01f,
                    "first steering frame ramps in without an instantaneous yaw impulse");
                for (int i = 0; i < 25; i++) StepBike(6f, 1f);
                float lowSpeedTurn = bike.MotorcycleYawRateDegrees;
                Check(lowSpeedTurn > 50f && lowSpeedTurn < 61f, "settled moderate-speed yaw is about sixty degrees per second");
                for (int i = 0; i < 25; i++) StepBike(33f, 1f);
                Check(bike.MotorcycleYawRateDegrees > 15f && bike.MotorcycleYawRateDegrees < 26f &&
                    bike.MotorcycleYawRateDegrees < lowSpeedTurn * .5f, "road speed limits turn rate to a broad stable arc");
                StepBike(33f, -1f);
                Check(bike.MotorcycleSteeringInput > .7f && bike.MotorcycleYawRateDegrees > 0f,
                    "opposite input cannot reverse yaw direction in one frame");
                for (int i = 0; i < 20; i++) StepBike(6f, -1f);
                Check(bike.MotorcycleYawRateDegrees < -50f, "a sustained opposite input still turns both ways");
                for (int i = 0; i < 12; i++) StepBike(6f, 0f);
                Check(Mathf.Abs(bike.MotorcycleSteeringInput) < .001f && Mathf.Abs(bike.MotorcycleYawRateDegrees) < .001f,
                    "released steering recenters smoothly without continuing to rotate");
                Set(player, "vehicleLookYaw", 0f); Set(player, "motorcycleCameraValid", false); CameraUpdate();
                float cameraYaw = player.ViewCamera.transform.eulerAngles.y;
                bike.transform.Rotate(0f, 5f, 0f, Space.World); CameraUpdate();
                Check(Mathf.Abs(Mathf.DeltaAngle(cameraYaw, player.ViewCamera.transform.eulerAngles.y)) < 4f &&
                    Mathf.Abs(Mathf.DeltaAngle(bike.transform.eulerAngles.y, player.ViewCamera.transform.eulerAngles.y)) <= 6.01f &&
                    Mathf.Abs(Mathf.DeltaAngle(0f, player.ViewCamera.transform.eulerAngles.z)) < .01f,
                    "bike camera softens hull yaw, bounds lag and keeps a level horizon");
                float beforeLook = player.ViewCamera.transform.eulerAngles.y;
                Invoke(player, "ApplyVehicleLook", 10f, 0f); CameraUpdate();
                Check(Mathf.Abs(Mathf.DeltaAngle(beforeLook, player.ViewCamera.transform.eulerAngles.y) - 10f) < .01f,
                    "mouse free-look stays direct independently of chassis damping");
                Check(player.TrySwitchVehicleSeat(1), "passenger transfers to the unchanged second seat");
                Quaternion before = bike.transform.rotation; float speed = bike.Speed;
                bike.ApplyPlayerInput(1f, 1f, 0f, false, false, .05f);
                Check(Quaternion.Angle(before, bike.transform.rotation) < .001f && speed == bike.Speed,
                    "passenger remains unable to steer or accelerate");
                player.ForceExitVehicle();

                MakeTank(); Enter(tank);
                Camera camera = player.ViewCamera;
                camera.aspect = 1.6f; CameraUpdate();
                PrototypeTankVisual model = tank.GetComponent<PrototypeTankVisual>();
                Check(Vector3.Distance(camera.transform.position, model.DriverOptic.position) < .001f,
                    "driver uses the actual unobstructed optical anchor");
                Vector3 muzzleScreen = camera.WorldToViewportPoint(tank.WeaponMuzzle.position);
                Check(muzzleScreen.z > 0f && muzzleScreen.x > .52f && muzzleScreen.x < .96f &&
                    muzzleScreen.y > .08f && muzzleScreen.y < .48f,
                    "the actual modeled cannon muzzle is visible in the lower right");
                Quaternion stableView = camera.transform.rotation;
                tank.transform.Rotate(0f, 22f, 0f, Space.World); CameraUpdate();
                Check(Quaternion.Angle(stableView, camera.transform.rotation) < .01f,
                    "driving hull yaw does not jerk the stabilized world observation heading");
                Set(player, "vehicleWorldLookYaw", tank.transform.eulerAngles.y); Set(player, "pitch", -12f);
                CameraUpdate(); AimTank();
                Physics.SyncTransforms();
                Check(PrototypeTankSightHud.TryRead(game, out PrototypeTankSightHud.Reading first) &&
                    Mathf.Abs(first.Elevation - 12f) < 1.5f && first.Range == tank.WeaponRange,
                    "sight elevation reads the real elevated muzzle rather than the requested camera angle");
                Vector3 origin = camera.transform.position;
                GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube); target.name = "Measured physical range target";
                target.transform.SetParent(fixture.transform); target.transform.localScale = new Vector3(5f, 5f, 1f);
                target.transform.position = origin + camera.transform.forward * 90f;
                target.transform.rotation = camera.transform.rotation;
                target.GetComponent<Renderer>().sharedMaterial = game.ArmorMaterial;
                Physics.SyncTransforms();
                Check(PrototypeTankSightHud.TryRead(game, out first) && first.HasRange && Mathf.Abs(first.Distance - 89.5f) < .15f,
                    "optical range uses a real world collider and excludes the own hull");
                target.transform.position = origin + camera.transform.forward * 120f; Physics.SyncTransforms();
                Set(tank, "<Speed>k__BackingField", -3f);
                Check(PrototypeTankSightHud.TryRead(game, out PrototypeTankSightHud.Reading second) &&
                    second.HasRange && Mathf.Abs(second.Distance - 119.5f) < .15f && Mathf.Abs(second.SpeedKph + 10.8f) < .001f,
                    "distance and signed speed update from moving physical target and actual vehicle speed");
                Vector3 muzzle = tank.WeaponMuzzle.position, bore = tank.WeaponMuzzle.forward;
                Set(tank, "nextShot", 0f); Invoke(tank, "FireCannon");
                PrototypeBallisticProjectile shell = null;
                foreach (PrototypeBallisticProjectile item in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    if (!originalRounds.Contains(item)) shell = item;
                Check(shell != null && Vector3.Distance(shell.transform.position, muzzle) < .001f &&
                    Vector3.Angle((Vector3)Get(shell, "velocity"), bore) < .01f && Vector3.Distance(muzzle, camera.transform.position) > 2f,
                    "the cannon still launches at the actual bore origin and axis, independent of the observation camera");
                Check(PrototypeTankSightHud.TryRead(game, out second) && second.ReadyIn > 2f,
                    "sight reload indicator reads the fired cannon cooldown");
                player.TogglePerspective();
                Check(!PrototypeTankSightHud.TryRead(game, out _), "external view hides first-person-only sight readings");
                player.TogglePerspective();
                Check(PrototypeTankSightHud.TryRead(game, out _), "V restores the same actual optical camera");
                Check(player.TrySwitchVehicleSeat(1) && !PrototypeTankSightHud.TryRead(game, out _),
                    "crew machine-gun seat does not borrow the main-gun optic HUD");
                player.ForceExitVehicle();
                Check(!PrototypeTankSightHud.TryRead(game, out _), "exit removes the tank observation HUD");
                End();
                Debug.Log("IRONFRONT vehicle steering and tank sight PASS: gradual speed-limited motorcycle yaw/recenter/reversal, level damped chassis camera/direct mouse look/passenger authority; real right-lower cannon geometry, world-stabilized optic, physical moving-target range/signed speed/muzzle elevation/reload, unchanged launched bore, V/seat/exit lifecycle; isolated state restored");
            }
            catch (Exception error) { End(); Debug.LogError("IRONFRONT vehicle steering and tank sight FAIL: " + error); }
        }

        [MenuItem("IRONFRONT/Capture Tank Sight")]
        public static void Capture()
        {
            try
            {
                Begin(); MakeTank(); Enter(tank);
                if (!Application.isBatchMode)
                {
                    previousFocusedWindow = EditorWindow.focusedWindow;
                    Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                    captureWindow = EditorWindow.GetWindow(gameView);
                    previousMaximized = captureWindow.maximized; savedWindowState = true;
                    captureWindow.Show(); captureWindow.Focus(); captureWindow.maximized = true;
                }
                started = EditorApplication.timeSinceStartup; capturePhase = 0; capturing = true;
                PrepareCapture(); EditorApplication.update -= CaptureFrame; EditorApplication.update += CaptureFrame;
            }
            catch (Exception error) { End(); Debug.LogError("IRONFRONT tank sight capture FAIL: " + error); }
        }
        private static void PrepareCapture()
        {
            Set(player, "vehicleWorldLookYaw", tank.transform.eulerAngles.y + (capturePhase == 0 ? 0f : 28f));
            Set(player, "pitch", capturePhase == 0 ? 0f : -8f);
            Set(tank, "<Speed>k__BackingField", capturePhase == 0 ? 0f : 4f);
            CameraUpdate(); AimTank(); CameraUpdate();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Verification054"));
            Directory.CreateDirectory(directory);
            string name = capturePhase == 0 ? "tank-sight-level" : "tank-sight-traverse";
            RenderCamera(Path.Combine(directory, name + "-camera.png"));
            if (!Application.isBatchMode) { player.ViewCamera.ResetAspect(); CameraUpdate(); }
            capturePath = Path.Combine(directory, name + "-hud.png");
            if (File.Exists(capturePath)) File.Delete(capturePath);
            preparedFrame = Time.frameCount; requested = false;
        }
        private static void CaptureFrame()
        {
            try
            {
                if (!EditorApplication.isPlaying) { End(); return; }
                if (EditorApplication.timeSinceStartup - started > 25d) throw new InvalidOperationException("tank sight capture timed out");
                if (Time.frameCount < preparedFrame + 3) return;
                // Batch Camera.Render supplies the real mechanical image.
                // Native GameView additionally captures Runtime's actual HUD.
                if (!Application.isBatchMode)
                {
                    if (!requested) { ScreenCapture.CaptureScreenshot(capturePath, 1); captureFrame = Time.frameCount; requested = true; return; }
                    if (Time.frameCount < captureFrame + 3 || !File.Exists(capturePath) || new FileInfo(capturePath).Length == 0) return;
                    var png = new Texture2D(2, 2);
                    try
                    {
                        Check(png.LoadImage(File.ReadAllBytes(capturePath)) && png.width > 1 && png.height > 1,
                            "actual GameView screenshot decodes as a populated PNG");
                        Debug.Log("IRONFRONT tank sight actual GameView HUD capture: PNG " + png.width + "x" + png.height + "; " + capturePath);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(png); }
                }
                if (++capturePhase < 2) { PrepareCapture(); return; }
                End();
                Debug.Log("IRONFRONT tank sight capture PASS: level and traversed actual camera geometry; native GameView additionally captures Runtime optic HUD");
            }
            catch (Exception error) { End(); Debug.LogError("IRONFRONT tank sight capture FAIL: " + error); }
        }

        private static void Begin()
        {
            game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>(); player = game?.Player;
            Check(game != null && player != null && !game.IsNetworkReplica && player.Alive && player.CurrentVehicle == null,
                "fresh living on-foot authoritative Play fixture required");
            originalPosition = player.transform.position; originalRotation = player.transform.rotation;
            originalVehicleFirstPerson = (bool)Get(player, "vehicleFirstPerson"); originalPitch = (float)Get(player, "pitch");
            originalAspect = player.ViewCamera.aspect; originalLock = Cursor.lockState; originalCursor = Cursor.visible; saved = true;
            originalRounds.Clear(); foreach (var round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>()) originalRounds.Add(round);
            if (!game.MatchStarted) game.BeginBattle();
            game.Frontend?.Close(); game.SetSupportMenu(false);
            Check(!game.PauseOpen && !game.ScoreboardOpen && !game.Match.Winner.HasValue, "active unobstructed battle fixture required");
            Freeze(player); foreach (var bot in game.Bots) Freeze(bot);
            foreach (var vehicle in game.Vehicles) if (vehicle is Behaviour behaviour) Freeze(behaviour);
            fixture = new GameObject("EDITOR ONLY steering and tank optic fixture");
            Set(player, "vehicleFirstPerson", true);
        }
        private static void MakeTank()
        {
            var item = new GameObject("Actual T90 observation fixture"); item.transform.SetParent(fixture.transform);
            tank = item.AddComponent<PrototypeTankVehicle>();
            tank.Initialize(game, player.Team, new Vector2(-325f, -320f), 0f);
            game.Vehicles.Add(tank); Freeze(tank);
        }
        private static void Enter(IPrototypeVehicle vehicle)
        {
            Check((bool)Invoke(player, "EnterVehicle", vehicle), "real driver seat accepts player");
            CameraUpdate();
        }
        private static void StepBike(float speed, float steering)
        {
            Set(bike, "<Speed>k__BackingField", speed);
            Vector3 position = bike.transform.position;
            bike.ApplyPlayerInput(speed < 0f ? -1f : 1f, steering, 0f, false, false, .05f);
            bike.transform.position = position;
        }
        private static void AimTank()
        { for (int i = 0; i < 100; i++) Invoke(tank, "AimCannon", .05f); }
        private static void CameraUpdate() => Invoke(player, "UpdateVehicleCamera");
        private static void RenderCamera(string path)
        {
            Camera camera = player.ViewCamera;
            var target = new RenderTexture(1280, 800, 24); target.Create();
            var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active, previousTarget = camera.targetTexture;
            float aspect = camera.aspect;
            try
            {
                camera.aspect = 1.6f; camera.targetTexture = target; CameraUpdate(); Physics.SyncTransforms(); camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0f, 0f, 1280f, 800f), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG()); Debug.Log("IRONFRONT tank sight actual camera capture: " + path);
            }
            finally
            { camera.targetTexture = previousTarget; camera.aspect = aspect; RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); }
        }
        private static void End()
        {
            EditorApplication.update -= CaptureFrame; capturing = requested = false;
            if (savedWindowState)
            {
                if (captureWindow != null) captureWindow.maximized = previousMaximized;
                if (previousFocusedWindow != null) previousFocusedWindow.Focus();
            }
            savedWindowState = false; captureWindow = previousFocusedWindow = null;
            if (player != null && player.CurrentVehicle != null &&
                (ReferenceEquals(player.CurrentVehicle, tank) || ReferenceEquals(player.CurrentVehicle, bike))) player.ForceExitVehicle();
            if (game != null) { if (bike != null) game.Vehicles.Remove(bike); if (tank != null) game.Vehicles.Remove(tank); }
            if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            foreach (var state in frozen) if (state.Key != null) state.Key.enabled = state.Value; frozen.Clear();
            if (saved && player != null)
            {
                foreach (var round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    if (!originalRounds.Contains(round)) UnityEngine.Object.DestroyImmediate(round.gameObject);
                Set(player, "vehicleFirstPerson", originalVehicleFirstPerson); Set(player, "pitch", originalPitch);
                CharacterController controller = player.GetComponent<CharacterController>(); controller.enabled = false;
                player.transform.SetPositionAndRotation(originalPosition, originalRotation); controller.enabled = true;
                player.ViewCamera.aspect = originalAspect;
                Invoke(player, "PositionInfantryCamera", 1f); player.GetComponent<PrototypeSoldierVisual>().RefreshPose(true);
                Cursor.lockState = originalLock; Cursor.visible = originalCursor;
            }
            saved = false; game = null; player = null; fixture = null; tank = null; bike = null; originalRounds.Clear();
        }
        private static void Freeze(Behaviour actor)
        { if (actor == null || frozen.ContainsKey(actor)) return; frozen.Add(actor, actor.enabled); actor.enabled = false; }
        private static object Get(object obj, string name) => obj.GetType().GetField(name, Private).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        private static object Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
        private static void Check(bool value, string description) { if (!value) throw new InvalidOperationException(description); }
    }
}
