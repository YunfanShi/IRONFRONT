using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ironfront.UnityPrototype.Editor
{
    // ScreenCapture includes the real Runtime.OnGUI overlay. This deliberately
    // does not substitute an offscreen camera render for a GameView HUD image.
    public static class PrototypeScoreboardCapture
    {
        private static CaptureState state;
        private static EditorWindow gameView;
        private static EditorWindow previousFocus;
        private static object sizeGroup;
        private static PropertyInfo selectedSize;
        private static int oldSizeIndex;
        private static object originalSize;
        private static int builtinCount;
        private static bool canSwitchSizes;
        private const string SmallCaptureSizeName = "IRONFRONT scoreboard capture 960x600";
        private const string LargeCaptureSizeName = "IRONFRONT scoreboard capture 1280x800";
        private static readonly List<int> addedCustomSizes = new List<int>();
        private static readonly List<string> completed = new List<string>();
        private static int resolution;
        private static int frame;
        private static int ticks;
        private static int capturedWidth;
        private static int capturedHeight;
        private static int expectedWidth;
        private static int expectedHeight;
        private static int renderWidth;
        private static int renderHeight;
        private static int lastRenderFrame;
        private static int stableRenderFrames;
        private static double resolutionStarted;
        private static bool requested;
        private static string path;
        private static double deadline;

        [MenuItem("IRONFRONT/Capture Team Scoreboard", true)]
        private static bool CanCapture() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Capture Team Scoreboard")]
        public static void Capture()
        {
            if (state != null) { Debug.LogError("IRONFRONT team scoreboard capture FAIL: capture is already active"); return; }
            try
            {
                if (Application.isBatchMode)
                    throw new InvalidOperationException("A visible normal Editor GameView is required for actual OnGUI capture.");
                PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && game.Player.Alive && game.MatchStarted &&
                    !game.IsNetworkReplica && !game.Match.Winner.HasValue && !game.PauseOpen &&
                    !game.IsNetworkSession && !game.Frontend.IsOpen,
                    "use a fresh living solo battle with the frontend closed");
                state = new CaptureState(game);
                state.Prepare();
                previousFocus = EditorWindow.focusedWindow;
                Type viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                Check(viewType != null, "Editor GameView type is available");
                gameView = EditorWindow.GetWindow(viewType);
                gameView.Show(); gameView.Focus();
                TryPrepareSizes(viewType);
                completed.Clear(); resolution = 0;
                deadline = EditorApplication.timeSinceStartup + 60d;
                PrepareResolution();
                RenderPipelineManager.endCameraRendering += EndCameraRender;
                Camera.onPostRender += ObserveCamera;
                EditorApplication.update += Tick;
            }
            catch (Exception exception) { Finish(false, exception.GetBaseException().Message); }
        }

        private static void PrepareResolution()
        {
            requested = false; ticks = 0; frame = Time.frameCount;
            capturedWidth = capturedHeight = 0;
            renderWidth = renderHeight = stableRenderFrames = 0;
            lastRenderFrame = -1;
            resolutionStarted = EditorApplication.timeSinceStartup;
            int width = resolution == 0 ? 960 : 1280;
            int height = resolution == 0 ? 600 : 800;
            expectedWidth = width; expectedHeight = height;
            if (canSwitchSizes && sizeGroup != null && selectedSize != null)
            {
                try
                {
                    Assembly editor = typeof(EditorWindow).Assembly;
                    Type sizeType = editor.GetType("UnityEditor.GameViewSize");
                    Type kindType = editor.GetType("UnityEditor.GameViewSizeType");
                    ConstructorInfo constructor = sizeType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { kindType, typeof(int), typeof(int), typeof(string) }, null);
                    object fixedSize = constructor.Invoke(new[] { Enum.Parse(kindType, "FixedResolution"),
                        (object)width, height, "IRONFRONT scoreboard capture " + width + "x" + height });
                    Invoke(sizeGroup, "AddCustomSize", fixedSize);
                    int total = (int)Invoke(sizeGroup, "GetTotalCount");
                    addedCustomSizes.Add(total - 1);
                    selectedSize.SetValue(gameView, total - 1);
                    gameView.Repaint();
                }
                catch (Exception exception)
                {
                    Debug.Log("IRONFRONT scoreboard fixed GameView size unavailable; using actual GameView resolution: " +
                        exception.GetBaseException().Message);
                    canSwitchSizes = false;
                }
            }
        }

        private static void EndCameraRender(ScriptableRenderContext context, Camera camera) => ObserveCamera(camera);

        private static void ObserveCamera(Camera camera)
        {
            if (state == null || camera == null || camera != state.Game.Player.ViewCamera || requested) return;
            // EditorApplication.update has an Editor-window Screen context.
            // Read the real camera while it is rendering the GameView instead.
            int width = camera.pixelWidth, height = camera.pixelHeight;
            if (!canSwitchSizes || width != expectedWidth || height != expectedHeight)
            foreach (string propertyName in new[] { "renderTargetSize", "targetSize" })
            {
                try
                {
                    PropertyInfo property = gameView.GetType().GetProperty(propertyName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    object target = property?.GetValue(gameView);
                    if (target is Vector2 size && size.x > 0f && size.y > 0f &&
                        (!canSwitchSizes || Mathf.RoundToInt(size.x) == expectedWidth && Mathf.RoundToInt(size.y) == expectedHeight))
                    { width = Mathf.RoundToInt(size.x); height = Mathf.RoundToInt(size.y); break; }
                    if (target is Vector2Int pixels && pixels.x > 0 && pixels.y > 0 &&
                        (!canSwitchSizes || pixels.x == expectedWidth && pixels.y == expectedHeight))
                    { width = pixels.x; height = pixels.y; break; }
                }
                catch (Exception) { /* Camera dimensions remain a useful fallback. */ }
            }
            if (width <= 0 || height <= 0 || Time.frameCount == lastRenderFrame) return;
            stableRenderFrames = width == renderWidth && height == renderHeight ? stableRenderFrames + 1 : 1;
            renderWidth = width; renderHeight = height; lastRenderFrame = Time.frameCount;
        }

        private static void TryPrepareSizes(Type viewType)
        {
            sizeGroup = null; selectedSize = null; originalSize = null; addedCustomSizes.Clear();
            canSwitchSizes = false;
            try
            {
                Assembly editor = typeof(EditorWindow).Assembly;
                Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
                Type groupType = editor.GetType("UnityEditor.GameViewSizeGroupType");
                Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .GetValue(null);
                sizeGroup = Invoke(sizes, "GetGroup", Enum.Parse(groupType, "Standalone"));
                builtinCount = (int)Invoke(sizeGroup, "GetBuiltinCount");
                selectedSize = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Check(selectedSize != null && selectedSize.CanWrite, "GameView fixed-size selection can be restored");
                oldSizeIndex = (int)selectedSize.GetValue(gameView);
                int total = (int)Invoke(sizeGroup, "GetTotalCount");
                Check(builtinCount > 0 && oldSizeIndex >= 0 && oldSizeIndex < total,
                    "GameView's original selection is valid");
                originalSize = Invoke(sizeGroup, "GetGameViewSize", oldSizeIndex);
                if (IsReservedCaptureSize(originalSize))
                {
                    originalSize = Invoke(sizeGroup, "GetGameViewSize", 0);
                    oldSizeIndex = 0;
                    Debug.Log("IRONFRONT scoreboard capture replaced a leftover test-size selection with the first built-in preset.");
                }
                canSwitchSizes = true;
            }
            catch (Exception exception)
            {
                sizeGroup = null; selectedSize = null; originalSize = null;
                Debug.Log("IRONFRONT scoreboard capture will retain actual GameView resolution: " + exception.GetBaseException().Message);
            }
            // Keep cleanup outside the reflection fallback: a cleanup failure must fail the capture.
            if (canSwitchSizes) CleanupReservedSizes("initial");
        }

        private static bool IsReservedCaptureSize(object size)
        {
            if (size == null) return false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = size.GetType();
            string name = (string)type.GetProperty("baseText", flags).GetValue(size);
            int width = (int)type.GetProperty("width", flags).GetValue(size);
            int height = (int)type.GetProperty("height", flags).GetValue(size);
            string kind = type.GetProperty("sizeType", flags).GetValue(size).ToString();
            return kind == "FixedResolution" &&
                ((name == SmallCaptureSizeName && width == 960 && height == 600) ||
                 (name == LargeCaptureSizeName && width == 1280 && height == 800));
        }

        private static int FindOriginalSizeIndex()
        {
            int total = (int)Invoke(sizeGroup, "GetTotalCount");
            for (int i = 0; i < total; i++)
                if (ReferenceEquals(originalSize, Invoke(sizeGroup, "GetGameViewSize", i))) return i;
            return -1;
        }

        private static void CleanupReservedSizes(string phase)
        {
            int builtin = (int)Invoke(sizeGroup, "GetBuiltinCount");
            int before = (int)Invoke(sizeGroup, "GetCustomCount");
            int total = (int)Invoke(sizeGroup, "GetTotalCount");
            Check(builtin > 0 && total == builtin + before, "GameView preset counts are consistent before cleanup");
            int originalIndex = FindOriginalSizeIndex();
            Check(originalIndex >= 0, "the original GameView preset remains available before cleanup");
            selectedSize.SetValue(gameView, originalIndex);
            // Custom indices can shift while deleting lower entries. A built-in selection stays valid.
            if (originalIndex >= builtin) selectedSize.SetValue(gameView, 0);
            int removed = 0;
            for (int i = total - 1; i >= builtin; i--)
            {
                if (!IsReservedCaptureSize(Invoke(sizeGroup, "GetGameViewSize", i))) continue;
                int count = (int)Invoke(sizeGroup, "GetCustomCount");
                // Unity's RemoveCustomSize accepts the combined built-in + custom index.
                Invoke(sizeGroup, "RemoveCustomSize", i);
                Check((int)Invoke(sizeGroup, "GetCustomCount") == count - 1,
                    "GameView removed the reserved capture preset at total index " + i);
                removed++;
            }
            int after = (int)Invoke(sizeGroup, "GetCustomCount");
            int afterTotal = (int)Invoke(sizeGroup, "GetTotalCount");
            Check(after == before - removed && afterTotal == builtin + after,
                "GameView custom-preset count matches reserved capture cleanup");
            for (int i = builtin; i < afterTotal; i++)
                Check(!IsReservedCaptureSize(Invoke(sizeGroup, "GetGameViewSize", i)),
                    "no reserved scoreboard capture presets remain");
            int restoredIndex = FindOriginalSizeIndex();
            Check(restoredIndex >= 0, "cleanup preserved the original user GameView preset");
            selectedSize.SetValue(gameView, restoredIndex);
            Check((int)selectedSize.GetValue(gameView) == restoredIndex, "GameView restored its original valid selection");
            Debug.Log("IRONFRONT scoreboard GameView " + phase + " cleanup: builtin=" + builtin +
                ", custom=" + before + "->" + after + ", reservedRemoved=" + removed +
                ", restoredTotalIndex=" + restoredIndex + ", createdThisRun=" + addedCustomSizes.Count);
        }

        private static void Tick()
        {
            if (state == null) return;
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play mode ended during capture.");
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Actual GameView screenshot did not complete.");
                gameView.Repaint(); ticks++;
                if (!requested)
                {
                    if (ticks < 8 || Time.frameCount < frame + 3) return;
                    if (canSwitchSizes && (renderWidth != expectedWidth || renderHeight != expectedHeight))
                    {
                        if (EditorApplication.timeSinceStartup - resolutionStarted < 12d) return;
                        canSwitchSizes = false;
                        stableRenderFrames = 0;
                        Debug.Log("IRONFRONT scoreboard fixed-size rendering could not be confirmed; capturing one actual GameView resolution.");
                        return;
                    }
                    if (stableRenderFrames < 2) return;
                    Check(state.Game.ScoreboardOpen, "Runtime's real scoreboard remains open");
                    capturedWidth = renderWidth; capturedHeight = renderHeight;
                    Check(capturedWidth >= 480 && capturedHeight >= 300, "GameView has a usable actual resolution");
                    string actual = capturedWidth + "x" + capturedHeight;
                    if (completed.Exists(item => item.EndsWith(actual + ".png", StringComparison.Ordinal)))
                    { Finish(true, "fixed-resolution switching unavailable; retained the captured actual GameView size"); return; }
                    string folder = Path.Combine(Application.dataPath, "../Builds/Verification054");
                    Directory.CreateDirectory(folder);
                    path = Path.GetFullPath(Path.Combine(folder, "team-scoreboard-capture-pending-" + resolution + ".png"));
                    if (File.Exists(path)) File.Delete(path);
                    Debug.Log("IRONFRONT scoreboard native capture request: desired=" + expectedWidth + "x" + expectedHeight +
                        ", rendered=" + capturedWidth + "x" + capturedHeight + ", stableGameFrames=" + stableRenderFrames);
                    ScreenCapture.CaptureScreenshot(path, 1);
                    frame = Time.frameCount; requested = true; return;
                }
                if (Time.frameCount < frame + 2 || !File.Exists(path) || new FileInfo(path).Length < 4096) return;
                byte[] png;
                try { png = File.ReadAllBytes(path); }
                catch (IOException) { return; }
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!image.LoadImage(png)) return;
                    if (canSwitchSizes)
                        Check(image.width == expectedWidth && image.height == expectedHeight,
                            "native PNG dimensions equal the stabilized requested GameView render target");
                    VerifyOverlayPixels(image);
                    string actualPath = Path.Combine(Path.GetDirectoryName(path),
                        "team-scoreboard-" + image.width + "x" + image.height + ".png");
                    if (File.Exists(actualPath)) File.Delete(actualPath);
                    File.Move(path, actualPath); path = actualPath;
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                completed.Add(path);
                if (!canSwitchSizes)
                { Finish(true, "fixed-resolution measurement unavailable; verified one actual native GameView PNG resolution"); return; }
                if (++resolution >= 2) { Finish(true, "actual Runtime.OnGUI at both rendered GameView resolutions"); return; }
                PrepareResolution();
            }
            catch (Exception exception) { Finish(false, exception.GetBaseException().Message); }
        }

        private static void VerifyOverlayPixels(Texture2D image)
        {
            float scale = Mathf.Min(2f, image.width / 960f, image.height / 600f);
            float logicalWidth = image.width / scale;
            float panelWidth = Mathf.Max(560f, logicalWidth - 48f);
            float half = (panelWidth - 52f) * .5f;
            Color blue = Pixel(image, scale, 44f, 106f);
            Color red = Pixel(image, scale, 44f + half + 16f, 106f);
            Check(blue.b > blue.r + .015f && red.r > red.b + .015f,
                "the screenshot contains Runtime's distinct blue/red team header fills");
            var colors = new HashSet<uint>();
            foreach (Color32 pixel in image.GetPixels32())
            {
                colors.Add((uint)(pixel.r << 16 | pixel.g << 8 | pixel.b));
                if (colors.Count >= 24) break;
            }
            Check(colors.Count >= 24, "the real screenshot contains populated non-empty UI pixels");
        }

        private static Color Pixel(Texture2D image, float scale, float x, float y) =>
            image.GetPixel(Mathf.Clamp(Mathf.RoundToInt(x * scale), 0, image.width - 1),
                Mathf.Clamp(image.height - 1 - Mathf.RoundToInt(y * scale), 0, image.height - 1));

        private static void Finish(bool passed, string detail)
        {
            EditorApplication.update -= Tick;
            RenderPipelineManager.endCameraRendering -= EndCameraRender;
            Camera.onPostRender -= ObserveCamera;
            try
            {
                if (gameView != null && sizeGroup != null && selectedSize != null && originalSize != null)
                    CleanupReservedSizes("final");
                if (gameView != null) gameView.Repaint();
                if (previousFocus != null) previousFocus.Focus();
            }
            catch (Exception exception)
            { passed = false; detail += "; GameView cleanup: " + exception.GetBaseException().Message; }
            try { state?.Dispose(); }
            catch (Exception exception)
            { passed = false; detail += "; restoration: " + exception.GetBaseException().Message; }
            finally { state = null; }
            if (passed)
                Debug.Log("IRONFRONT team scoreboard capture PASS: " + detail + "; realResolution files: " + string.Join(", ", completed));
            else Debug.LogError("IRONFRONT team scoreboard capture FAIL: " + detail);
        }

        private sealed class CaptureState : IDisposable
        {
            public readonly PrototypeRuntime Game;
            private readonly PrototypeScoreSystem scores;
            private readonly PrototypeScoreSystem matchScores;
            private readonly PrototypeScoreboard board;
            private readonly bool boardOpen;
            private readonly bool supportOpen;
            private readonly float timeScale;
            private readonly float blueTickets;
            private readonly float redTickets;
            private readonly PrototypeGameMode mode;
            private readonly int sector;
            private readonly float elapsed;
            private readonly List<PointState> points = new List<PointState>();
            private readonly CursorLockMode cursor;
            private readonly bool cursorVisible;
            private readonly List<ActorState> actors = new List<ActorState>();
            private readonly List<EnabledState> rounds = new List<EnabledState>();
            private readonly List<GameObject> fixtures = new List<GameObject>();
            private readonly List<PrototypeBotDownNotice> feed = new List<PrototypeBotDownNotice>();
            private readonly HashSet<PrototypePulseVisual> effects;
            private readonly HashSet<AudioSource> audio;

            public CaptureState(PrototypeRuntime game)
            {
                Game = game; scores = game.Scores; board = game.Scoreboard;
                matchScores = game.Match.ScoreSystem;
                boardOpen = game.ScoreboardOpen; supportOpen = game.SupportMenuOpen;
                timeScale = Time.timeScale; cursor = Cursor.lockState; cursorVisible = Cursor.visible;
                blueTickets = game.Match.BlueTickets; redTickets = game.Match.RedTickets; mode = game.Match.Mode;
                sector = game.Match.SectorIndex; elapsed = game.Match.Elapsed;
                foreach (PrototypeCapturePoint point in game.Match.Points) points.Add(new PointState(point));
                feed.AddRange(game.BotDownFeed.Entries);
                actors.Add(new ActorState(game.Player));
                foreach (PrototypeBot bot in game.Bots) if (bot != null) actors.Add(new ActorState(bot));
                foreach (PrototypeRemotePlayer remote in game.RemotePlayers) if (remote != null) actors.Add(new ActorState(remote));
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                    if (vehicle is MonoBehaviour component) actors.Add(new ActorState(component));
                foreach (PrototypeBallisticProjectile round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                    rounds.Add(new EnabledState(round));
                foreach (PrototypeVehicleProjectile round in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                    rounds.Add(new EnabledState(round));
                effects = new HashSet<PrototypePulseVisual>(UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>());
                audio = new HashSet<AudioSource>(UnityEngine.Object.FindObjectsByType<AudioSource>());
            }

            public void Prepare()
            {
                Time.timeScale = 0f;
                foreach (ActorState actor in actors) actor.Component.enabled = false;
                foreach (EnabledState round in rounds) round.Component.enabled = false;
                var fixtureScores = new PrototypeScoreSystem(Game);
                Set(Game, "<Scores>k__BackingField", fixtureScores);
                Game.Match.ScoreSystem = fixtureScores;
                Set(Game, "<Scoreboard>k__BackingField", new PrototypeScoreboard(Game));
                PrototypeTeam own = Game.Player.Team;
                PrototypeTeam enemy = own == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                var allies = new List<PrototypeBot>(); PrototypeBot opponent = null;
                foreach (PrototypeBot bot in Game.Bots)
                    if (bot.Alive && bot.Team == own) allies.Add(bot);
                    else if (bot.Alive && bot.Team == enemy && opponent == null) opponent = bot;
                Check(allies.Count >= 2 && opponent != null, "existing live soldiers from both teams are available");
                PrototypeBot target = DamageTarget(enemy);
                Hit(fixtureScores, target, allies[0], own, 25f);
                Hit(fixtureScores, target, Game.Player, own, 75f);
                target = DamageTarget(enemy);
                Hit(fixtureScores, target, allies[1], own, 100f);
                target = DamageTarget(own);
                Hit(fixtureScores, target, opponent, enemy, 100f);
                var vehicleRoot = new GameObject("Scoreboard image real hull damage fixture");
                fixtures.Add(vehicleRoot); vehicleRoot.transform.SetParent(Game.transform);
                var hull = vehicleRoot.AddComponent<PrototypeCombinedArmsVehicle>();
                hull.Initialize(Game, enemy, PrototypeVehicleKind.Motorcycle, new Vector2(800f, 820f), false);
                hull.enabled = false; hull.transform.position += Vector3.up * 300f;
                fixtureScores.ApplyDamage(hull, allies[0], own, 25f, () => hull.TakeDamage(25f));
                fixtureScores.ApplyDamage(hull, Game.Player, own, 60f, () => hull.TakeDamage(60f));
                Check(fixtureScores.Get(Game.Player).TotalEarned == 300 &&
                    fixtureScores.TrySpend(Game.Player, 200, "呼叫 M2 侦察摩托") &&
                    fixtureScores.Get(Game.Player).Credits == 100 && fixtureScores.Get(Game.Player).TotalEarned == 300,
                    "real damage/defeat/vehicle settlement and real debit produce the displayed player score");
                foreach (IPrototypeVehicle vehicle in Game.Vehicles)
                    if (vehicle.Alive && vehicle.Team == enemy && vehicle.Occupant == null &&
                        vehicle.DriverCanFire && vehicle.IsActiveThreat)
                    {
                        target = DamageTarget(own);
                        Hit(fixtureScores, target, PrototypeScoreSystem.ActorForShooter(vehicle.VehicleTransform), enemy, 100f);
                        break;
                    }
                Check(Game.SetScoreboard(true), "Runtime opens its actual scoreboard OnGUI overlay");
            }

            private PrototypeBot DamageTarget(PrototypeTeam team)
            {
                GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.name = "Scoreboard image real damage target"; fixtures.Add(root);
                root.transform.SetParent(Game.transform);
                PrototypeBot bot = root.AddComponent<PrototypeBot>();
                bot.Initialize(Game, Game.Navigation, team, new Vector2(820f, 820f), PrototypeInfantryClass.Assault);
                bot.enabled = false; root.transform.position += Vector3.up * 300f;
                return bot;
            }

            public void Dispose()
            {
                foreach (GameObject fixture in fixtures) if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                foreach (PrototypePulseVisual effect in UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>())
                    if (!effects.Contains(effect)) UnityEngine.Object.DestroyImmediate(effect.gameObject);
                foreach (AudioSource source in UnityEngine.Object.FindObjectsByType<AudioSource>())
                    if (!audio.Contains(source)) UnityEngine.Object.DestroyImmediate(source.gameObject);
                Set(Game, "<Scores>k__BackingField", scores);
                Game.Match.ScoreSystem = matchScores;
                Set(Game, "<Scoreboard>k__BackingField", board);
                Set(Game.Match, "<Mode>k__BackingField", mode);
                Set(Game.Match, "<SectorIndex>k__BackingField", sector);
                Set(Game.Match, "<Elapsed>k__BackingField", elapsed);
                foreach (PointState point in points) point.Restore();
                Game.Match.SetNetworkTickets(blueTickets, redTickets);
                var entries = (List<PrototypeBotDownNotice>)Field(Game.BotDownFeed, "entries");
                entries.Clear(); entries.AddRange(feed);
                foreach (ActorState actor in actors) actor.Restore();
                foreach (EnabledState round in rounds) round.Restore();
                Time.timeScale = timeScale;
                Game.SetScoreboard(boardOpen);
                if (supportOpen) Game.SetSupportMenu(true);
                Cursor.lockState = cursor; Cursor.visible = cursorVisible;
            }
        }

        private sealed class ActorState : EnabledState
        {
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly PropertyInfo health;
            private readonly object value;
            public ActorState(MonoBehaviour component) : base(component)
            {
                position = component.transform.position; rotation = component.transform.rotation;
                health = component.GetType().GetProperty("Health", BindingFlags.Instance | BindingFlags.Public);
                value = health?.GetValue(component);
            }
            public override void Restore()
            {
                if (Component == null) return;
                Component.transform.position = position; Component.transform.rotation = rotation;
                if (health != null) health.SetValue(Component, value);
                base.Restore();
            }
        }
        private sealed class PointState
        {
            private readonly PrototypeCapturePoint point;
            private readonly float control;
            private readonly PrototypeTeam? owner;
            private readonly bool contested;
            private readonly int blue;
            private readonly int red;
            public PointState(PrototypeCapturePoint value)
            { point = value; control = value.Control; owner = value.Owner; contested = value.Contested; blue = value.BlueCount; red = value.RedCount; }
            public void Restore()
            { point.Control = control; point.Owner = owner; point.Contested = contested; point.BlueCount = blue; point.RedCount = red; }
        }
        private class EnabledState
        {
            public readonly MonoBehaviour Component;
            private readonly bool enabled;
            public EnabledState(MonoBehaviour component) { Component = component; enabled = component.enabled; }
            public virtual void Restore() { if (Component != null) Component.enabled = enabled; }
        }
        private static void Hit(PrototypeScoreSystem scores, PrototypeBot target, object source, PrototypeTeam team, float damage) =>
            scores.ApplyDamage(target, source, team, damage, () => target.TakeDamage(damage));
        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
        private static object Field(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Check(bool value, string detail)
        { if (!value) throw new InvalidOperationException(detail); }
    }
}
