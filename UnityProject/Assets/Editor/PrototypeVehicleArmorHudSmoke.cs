using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeVehicleArmorHudSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Type SightFixture = typeof(PrototypeVehicleSightSmoke);
        private static PrototypeRuntime captureGame;
        private static float captureHealth;
        private static bool captureActive, requested, fixtureStarted, oldMaximized;
        private static int preparedFrame, requestedFrame;
        private static double begun;
        private static string path;
        private static EditorWindow gameView, previousFocus;

        [MenuItem("IRONFRONT/Verify Vehicle Armor HUD", true)]
        [MenuItem("IRONFRONT/Capture Vehicle Armor HUD", true)]
        private static bool CanRun() => EditorApplication.isPlaying && !captureActive;

        [MenuItem("IRONFRONT/Verify Vehicle Armor HUD")]
        public static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            PrototypePlayer oldPlayer = game?.Player;
            PrototypeScoreSystem oldScores = game?.Scores;
            float blue = game?.Match.BlueTickets ?? 0f, red = game?.Match.RedTickets ?? 0f;
            CursorLockMode cursor = Cursor.lockState; bool cursorVisible = Cursor.visible;
            string loadout = PlayerPrefs.GetString("ironfront.unity.loadout", "");
            bool hadLoadout = PlayerPrefs.HasKey("ironfront.unity.loadout");
            GameObject fixture = null; PrototypePlayer player = null; IPrototypeSeatedVehicle vehicle = null;
            bool swapped = false;
            Exception failure = null;
            try
            {
                Check(game != null && oldPlayer != null && game.MatchStarted && !game.IsNetworkReplica &&
                    !game.Match.Winner.HasValue && !game.Frontend.IsOpen && !game.PauseOpen, "active authoritative battle fixture exists");
                Set(game, "<Scores>k__BackingField", new PrototypeScoreSystem(game)); swapped = true;
                fixture = new GameObject("EDITOR ONLY vehicle armor HUD fixtures");
                var actor = new GameObject("Independent armor HUD player"); actor.transform.SetParent(fixture.transform);
                actor.AddComponent<CharacterController>(); player = actor.AddComponent<PrototypePlayer>();
                Set(game, "<Player>k__BackingField", player); player.Initialize(game); player.enabled = false;
                foreach (Camera camera in actor.GetComponentsInChildren<Camera>()) camera.enabled = false;
                foreach (AudioListener listener in actor.GetComponentsInChildren<AudioListener>()) listener.enabled = false;
                Check(!PrototypePlayerHealthHud.TryReadVehicle(player, out _), "on-foot player has no vehicle armor bar");
                var maxima = new HashSet<float>();
                foreach (string kind in new[] { "tank", "scout", "transport", "ifv", "anti-air", "attack helicopter", "jet", "motorcycle", "recon helicopter" })
                {
                    vehicle = Make(game, fixture, kind);
                    var armored = (IPrototypeArmoredVehicle)vehicle;
                    Check((bool)Invoke(player, "EnterVehicle", vehicle), kind + " accepts actual player boarding");
                    maxima.Add(armored.MaximumHealth);
                    Read(player, vehicle, 1f, kind + " driver shows its real full hull");
                    foreach (int seat in new[] { 1, vehicle.SeatCount - 1 })
                        if (seat > 0 && seat < vehicle.SeatCount)
                        { Check(player.TrySwitchVehicleSeat(seat), kind + " transfers to actual crew/passenger seat"); Read(player, vehicle, 1f, kind + " crew uses the same hull reading"); }
                    Check(player.TrySwitchVehicleSeat(0), kind + " returns driver authority");
                    float playerHealth = player.Health;
                    vehicle.TakeDamage(armored.MaximumHealth * .5f);
                    Read(player, vehicle, .5f, kind + " real damage immediately halves its own maximum");
                    Check(player.Health == playerHealth && vehicle.Repair(armored.MaximumHealth * .1f), kind + " repairs the hull without consuming infantry health");
                    Read(player, vehicle, .6f, kind + " actual repair refreshes the same hull fraction");
                    VerifyLayout(player, armored.MaximumHealth);

                    float health = vehicle.Health;
                    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, 0f })
                    { Set(vehicle, "<Health>k__BackingField", invalid); Check(!PrototypePlayerHealthHud.TryReadVehicle(player, out _), kind + " invalid/dead hull is hidden"); }
                    Set(vehicle, "<Health>k__BackingField", health);
                    vehicle.VehicleTransform.gameObject.SetActive(false);
                    Check(!PrototypePlayerHealthHud.TryReadVehicle(player, out _), kind + " inactive hull is hidden");
                    vehicle.VehicleTransform.gameObject.SetActive(true);
                    var seats = (PrototypeVehicleSeats)vehicle.GetType().GetField("seats", Private).GetValue(vehicle);
                    seats.RemovePlayer(player);
                    Check(!PrototypePlayerHealthHud.TryReadVehicle(player, out _), kind + " stale seat ownership immediately hides the bar");
                    player.ForceExitVehicle();
                    Check(!PrototypePlayerHealthHud.TryReadVehicle(player, out _) &&
                        PrototypeScorePopup.CenterX(player, 960f) == 480f, kind + " exit removes hull UI and restores centered score feedback");
                    game.Vehicles.Remove(vehicle); UnityEngine.Object.DestroyImmediate(vehicle.VehicleTransform.gameObject); vehicle = null;
                }
                Check(maxima.Count == 9 && maxima.Contains(85f) && maxima.Contains(820f), "all nine different real hull maxima were normalized independently");
                Check(game.Match.BlueTickets == blue && game.Match.RedTickets == red &&
                    PlayerPrefs.HasKey("ironfront.unity.loadout") == hadLoadout &&
                    PlayerPrefs.GetString("ironfront.unity.loadout", "") == loadout, "source tickets and saved equipment remain unchanged");
            }
            catch (Exception error) { failure = error.GetBaseException(); }
            finally
            {
                try
                {
                    if (player != null && player.CurrentVehicle != null) player.ForceExitVehicle();
                    if (game != null && vehicle != null) game.Vehicles.Remove(vehicle);
                    if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                }
                catch (Exception error) { failure = failure ?? error.GetBaseException(); }
                finally
                {
                    if (swapped && game != null) { Set(game, "<Player>k__BackingField", oldPlayer); Set(game, "<Scores>k__BackingField", oldScores); }
                    Cursor.lockState = cursor; Cursor.visible = cursorVisible;
                }
            }
            if (failure != null) Debug.LogError("IRONFRONT vehicle armor HUD FAIL: " + failure);
            else Debug.Log("IRONFRONT vehicle armor HUD PASS: actual nine-hull driver/crew boarding and maxima, damage/repair fractions independent of infantry health, live changed-vehicle reads, dead/nonfinite/inactive/stale-seat/exit hiding, bounded zero/invalid/full bars, HP/optic/status/score separation across real GUI canvas aspects; original player/accounts/tickets/preferences intact");
        }

        private static void Read(PrototypePlayer player, IPrototypeSeatedVehicle vehicle, float fraction, string message)
        {
            Check(PrototypePlayerHealthHud.TryReadVehicle(player, out var reading) && reading.VehicleName == vehicle.VehicleName &&
                Mathf.Abs(reading.Health - vehicle.Health) < .001f && reading.MaximumHealth == ((IPrototypeArmoredVehicle)vehicle).MaximumHealth &&
                reading.ArmorRating == ((IPrototypeArmoredVehicle)vehicle).ArmorRating && Mathf.Abs(reading.Fraction - fraction) < .001f, message);
        }
        private static void VerifyLayout(PrototypePlayer player, float maximum)
        {
            foreach (Vector2 screen in new[] { new Vector2(960, 600), new Vector2(1280, 720), new Vector2(1280, 800), new Vector2(1024, 768), new Vector2(587, 375) })
            {
                float scale = Mathf.Min(2f, screen.x / 960f, screen.y / 600f), width = screen.x / scale, height = screen.y / scale;
                Rect armor = PrototypePlayerHealthHud.VehiclePanel(width, height), hp = PrototypePlayerHealthHud.Panel(width, height);
                Rect status = PrototypeTankSightHud.StatusPanel(width, height);
                Check(Mathf.Abs(armor.center.x - hp.center.x) < .01f && armor.yMax <= hp.yMin - 6f &&
                    armor.xMin > 0f && armor.xMax < width && armor.yMin > height * .5f + 60f, "armor stays above HP and below the optical aiming area");
                Check(!status.Overlaps(armor) && status.yMax <= armor.yMin - 4f, "tank heading/reload line remains clear of the vehicle bar");
                Rect full = PrototypePlayerHealthHud.VehicleBar(width, height, maximum, maximum);
                Rect partial = PrototypePlayerHealthHud.VehicleBar(width, height, maximum * .6f, maximum);
                Check(full.height >= 10f && full.xMin > armor.xMin && full.xMax < armor.xMax && full.yMin > armor.yMin && full.yMax < armor.yMax &&
                    Mathf.Abs(partial.width / full.width - .6f) < .001f, "real vehicle fill is thick, contained and normalized to its own maximum");
                foreach (var pair in new[] { new Vector2(0f, maximum), new Vector2(-1f, maximum), new Vector2(float.NaN, maximum),
                    new Vector2(float.PositiveInfinity, maximum), new Vector2(10f, 0f), new Vector2(10f, float.NaN) })
                    Check(PrototypePlayerHealthHud.VehicleBar(width, height, pair.x, pair.y).width == 0f, "zero/invalid hull values produce no inverted or overflowing fill");
                Check(PrototypePlayerHealthHud.VehicleBar(width, height, maximum * 2f, maximum).width == full.width,
                    "over-max input cannot overflow the physical bar");
                Rect feedback = new Rect(PrototypeScorePopup.CenterX(player, width) - 155f, height * .63f, 310f, 85f);
                Check(!feedback.Overlaps(armor) && !feedback.Overlaps(hp) && feedback.xMin >= 0f && feedback.xMax <= width,
                    "actual mounted score-center API keeps earned-score text away from both health bars");
            }
        }

        [MenuItem("IRONFRONT/Capture Vehicle Armor HUD")]
        public static void Capture()
        {
            try
            {
                Check(!Application.isBatchMode, "actual armor HUD capture uses a native GameView");
                captureGame = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(captureGame != null && captureGame.MatchStarted && captureGame.Player.Alive && captureGame.Player.Health > 74f,
                    "fresh living actual battle fixture exists");
                captureHealth = captureGame.Player.Health; captureActive = true;
                // Reuse the existing isolated camera/actor freeze and teardown.
                fixtureStarted = true; Sight("Begin"); Sight("MakeTank");
                var tank = (PrototypeTankVehicle)SightFixture.GetField("tank", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                captureGame.Player.TakeDamage(captureGame.Player.Health - 74f);
                Sight("Enter", tank); tank.TakeDamage(tank.MaximumHealth * .45f);
                Check(PrototypePlayerHealthHud.TryReadVehicle(captureGame.Player, out var reading) &&
                    Mathf.Abs(reading.Fraction - .55f) < .001f && Mathf.Abs(captureGame.Player.Health - 74f) < .001f,
                    "actual damage prepared separate 55-percent hull and 74 infantry HP");
                previousFocus = EditorWindow.focusedWindow;
                gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                oldMaximized = gameView.maximized; gameView.Show(); gameView.Focus(); gameView.maximized = true;
                captureGame.Player.ViewCamera.ResetAspect(); Sight("CameraUpdate");
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Verification0541")); Directory.CreateDirectory(directory);
                path = Path.Combine(directory, "vehicle-armor-hud.png"); if (File.Exists(path)) File.Delete(path);
                begun = EditorApplication.timeSinceStartup; preparedFrame = Time.frameCount; requested = false;
                EditorApplication.update -= CaptureFrame; EditorApplication.update += CaptureFrame;
            }
            catch (Exception error) { EndCapture(); Debug.LogError("IRONFRONT vehicle armor HUD capture FAIL: " + error.GetBaseException()); }
        }
        private static void CaptureFrame()
        {
            try
            {
                Check(EditorApplication.isPlaying && EditorApplication.timeSinceStartup - begun < 25d, "actual GameView screenshot completes while Play continues");
                if (!requested)
                {
                    if (Time.frameCount < preparedFrame + 5) return;
                    ScreenCapture.CaptureScreenshot(path, 1); requestedFrame = Time.frameCount; requested = true; return;
                }
                if (Time.frameCount < requestedFrame + 3 || !File.Exists(path) || new FileInfo(path).Length == 0) return;
                var png = new Texture2D(2, 2);
                try
                { Check(png.LoadImage(File.ReadAllBytes(path)) && png.width > 1 && png.height > 1, "actual HUD PNG decodes"); Debug.Log("IRONFRONT vehicle armor HUD actual PNG " + png.width + "x" + png.height + ": " + path); }
                finally { UnityEngine.Object.DestroyImmediate(png); }
                EndCapture(); Debug.Log("IRONFRONT vehicle armor HUD capture PASS: actual Runtime.OnGUI/native GameView, damaged T90 hull 55% above independent player HP74; original health/camera/actors/window restored");
            }
            catch (Exception error) { EndCapture(); Debug.LogError("IRONFRONT vehicle armor HUD capture FAIL: " + error.GetBaseException()); }
        }
        private static void EndCapture()
        {
            EditorApplication.update -= CaptureFrame;
            if (captureActive && captureGame?.Player != null) Set(captureGame.Player, "<Health>k__BackingField", captureHealth);
            try { if (fixtureStarted) Sight("End"); }
            finally
            {
                if (gameView != null) gameView.maximized = oldMaximized;
                if (previousFocus != null) previousFocus.Focus();
                captureActive = fixtureStarted = requested = false; captureGame = null; gameView = previousFocus = null;
            }
        }
        private static IPrototypeSeatedVehicle Make(PrototypeRuntime game, GameObject parent, string kind)
        {
            var item = new GameObject("Actual armor HUD " + kind); item.transform.SetParent(parent.transform);
            Vector2 at = new Vector2(-325f, -320f); IPrototypeSeatedVehicle vehicle;
            if (kind == "tank") { var hull = item.AddComponent<PrototypeTankVehicle>(); hull.Initialize(game, game.Player.Team, at, 0f); vehicle = hull; }
            else if (kind == "scout") { var hull = item.AddComponent<PrototypeScoutVehicle>(); hull.Initialize(game, game.Player.Team, at, false); vehicle = hull; }
            else if (kind == "transport") { var hull = item.AddComponent<PrototypeTransportVehicle>(); hull.Initialize(game, game.Player.Team, at, false); vehicle = hull; }
            else
            {
                var hull = item.AddComponent<PrototypeCombinedArmsVehicle>();
                PrototypeVehicleKind id = kind == "ifv" ? PrototypeVehicleKind.Ifv : kind == "anti-air" ? PrototypeVehicleKind.AntiAir :
                    kind == "attack helicopter" ? PrototypeVehicleKind.Helicopter : kind == "jet" ? PrototypeVehicleKind.Jet :
                    kind == "recon helicopter" ? PrototypeVehicleKind.ReconHelicopter : PrototypeVehicleKind.Motorcycle;
                hull.Initialize(game, game.Player.Team, id, at, false); hull.RespawnEnabled = false; vehicle = hull;
            }
            ((Behaviour)vehicle).enabled = false; game.Vehicles.Add(vehicle); return vehicle;
        }
        private static object Sight(string name, params object[] values) => SightFixture.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, values);
        private static object Invoke(object obj, string name, params object[] values) => obj.GetType().GetMethod(name, Private).Invoke(obj, values);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
