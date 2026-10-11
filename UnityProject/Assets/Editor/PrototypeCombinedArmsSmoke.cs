using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeCombinedArmsSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int preview;

        [MenuItem("IRONFRONT/Verify Five Added Vehicles", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Five Added Vehicles")]
        public static void Verify()
        {
            PrototypeRuntime game = Prepare();
            PrototypePlayer player = game.Player;
            Vector3 playerStart = player.transform.position;
            try
            {
                int checkedKinds = 0;
                foreach (PrototypeVehicleKind kind in Enum.GetValues(typeof(PrototypeVehicleKind)))
                {
                    PrototypeCombinedArmsVehicle vehicle = Find(game, player.Team, kind);
                    Check(vehicle != null, kind + " is present for this team");
                    Reset(vehicle); Place(vehicle, new Vector2(-325f, -320f));
                    Check(Enter(player, vehicle), vehicle.VehicleName + " can be entered");
                    Check(vehicle.SeatCount == (kind == PrototypeVehicleKind.Jet ? 1 :
                        kind == PrototypeVehicleKind.Motorcycle ? 2 : 4), kind + " has main's seat count");
                    float speed = vehicle.Speed;
                    Vector2 start = vehicle.MapPosition;
                    for (int step = 0; step < 60; step++)
                        vehicle.ApplyPlayerInput(1f, 0f, vehicle.IsAircraft ? 1f : 0f, false, false, .05f);
                    Check(vehicle.Speed > speed && Vector2.Distance(start, vehicle.MapPosition) > .5f,
                        kind + " starts, accelerates and moves");
                    if (vehicle.IsAircraft)
                    {
                        for (int step = 0; step < 40; step++)
                            vehicle.ApplyPlayerInput(1f, .5f, 1f, false, false, .05f);
                        Check(vehicle.Altitude > 7f && vehicle.VerticalVelocity > 0f,
                            kind + " takes off with powered lift");
                        Check(Mathf.Abs(vehicle.Roll) > .01f, kind + " banks when steering");
                        float high = vehicle.Altitude;
                        for (int step = 0; step < 60; step++)
                            vehicle.ApplyPlayerInput(0f, 0f, -1f, false, true, .05f);
                        Check(vehicle.Altitude < high, kind + " descends under control");
                        vehicle.transform.position = new Vector3(-325f,
                            PrototypeLayout.HeightAt(-325f, -320f) + 18f, -320f);
                        player.ForceExitVehicle();
                        Check(player.IsParachuting && player.transform.position.y > 10f,
                            kind + " exits with a parachute from actual altitude");
                        float abandoned = vehicle.Altitude;
                        for (int step = 0; step < 20; step++)
                            Flight(vehicle, 0f, 0f, 0f, 0f, false, false, .05f, false, null);
                        Check(vehicle.Altitude < abandoned, kind + " loses lift after pilot exits");
                        for (int step = 0; step < 100; step++)
                            Flight(vehicle, 0f, 0f, 0f, 0f, false, false, .05f, false, null);
                        Check(vehicle.Altitude < .05f && Mathf.Abs(vehicle.VerticalVelocity) < .01f,
                            kind + " lands and clamps safely to the terrain");
                        Check(Enter(player, vehicle), kind + " can be re-entered after landing");
                    }
                    else
                    {
                        float yaw = vehicle.transform.eulerAngles.y;
                        vehicle.ApplyPlayerInput(1f, 1f, 0f, false, false, .05f);
                        Check(Mathf.Abs(Mathf.DeltaAngle(yaw, vehicle.transform.eulerAngles.y)) > .01f,
                            kind + " steers");
                    }
                    if (vehicle.SeatCount > 1)
                    {
                        Check(player.TrySwitchVehicleSeat(vehicle.SeatCount - 1), kind + " reaches its last seat");
                        Vector3 idlePosition = vehicle.transform.position;
                        vehicle.ApplyPlayerInput(1f, 1f, 1f, true, false, .05f);
                        Check(vehicle.transform.position == idlePosition && !vehicle.SeatCanDrive,
                            kind + " passenger cannot drive");
                        Check(player.TrySwitchVehicleSeat(0), kind + " returns to driver/pilot");
                    }
                    if (kind == PrototypeVehicleKind.Motorcycle)
                        Check(!vehicle.SeatCanFire && !vehicle.TryFireWeapon(), "M2 remains a two-seat unarmed courier");
                    vehicle.TakeDamage(10f);
                    Check(vehicle.Repair(10f) && Mathf.Approximately(vehicle.Health, vehicle.MaxHealth),
                        kind + " can be repaired while alive");
                    player.ForceExitVehicle(); Reset(vehicle);
                    Place(vehicle, new Vector2(-325f, -320f));
                    vehicle.SetAiControlled(true); Vector2 aiStart = vehicle.MapPosition;
                    for (int step = 0; step < 40; step++)
                        typeof(PrototypeCombinedArmsVehicle).GetMethod("UpdateAi", Private).Invoke(vehicle, new object[] { .05f });
                    Check(Vector2.Distance(aiStart, vehicle.MapPosition) > .5f,
                        kind + " optional AI can move toward an objective");
                    vehicle.SetAiControlled(false); Reset(vehicle); checkedKinds++;
                }
                Check(checkedKinds == 5, "all five added kinds were exercised");
                VerifyFlightViewHeading(game, player);
                VerifyWeapons(game, player);
                VerifyLossAndRespawn(game, player);
                Debug.Log("IRONFRONT five vehicles PASS: V12/A40/H8/J20/M2 drive, seats, " +
                    "takeoff/steer/descent/landing/parachute, stable mouse heading, primary damage, AA lock/missile/flares, " +
                    "repair, crew loss, blocked spawn and respawn");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT five vehicles FAIL: " + error); }
            finally
            {
                player.ForceExitVehicle();
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                    if (vehicle is PrototypeCombinedArmsVehicle added) Reset(added);
                if (!player.Alive) Respawn(player);
                CharacterController controller = player.GetComponent<CharacterController>();
                controller.enabled = false; player.transform.position = playerStart; controller.enabled = true;
                game.Frontend?.Close();
            }
        }

        private static void VerifyFlightViewHeading(PrototypeRuntime game, PrototypePlayer player)
        {
            MethodInfo applyLook = typeof(PrototypePlayer).GetMethod("ApplyVehicleLook", Private);
            MethodInfo updateCamera = typeof(PrototypePlayer).GetMethod("UpdateVehicleCamera", Private);
            Check(applyLook != null && updateCamera != null, "pilot mouse look path is available");
            foreach (PrototypeVehicleKind kind in new[] { PrototypeVehicleKind.Helicopter, PrototypeVehicleKind.Jet })
            {
                PrototypeCombinedArmsVehicle aircraft = Find(game, player.Team, kind);
                Reset(aircraft); Place(aircraft, new Vector2(-325f, -320f));
                aircraft.transform.position += Vector3.up * 30f;
                Check(Enter(player, aircraft), kind + " pilot enters for actual camera feedback check");
                // Invoke the same look path used by the mouse; subsequent
                // iterations stop mouse input while continuing the real Player
                // camera → flight → Player camera loop. Assigning Camera yaw
                // directly would conceal a relative-heading feedback bug.
                const float heading = 30f;
                applyLook.Invoke(player, new object[] { heading, 0f });
                float settledYaw = 0f;
                for (int step = 0; step < 180; step++)
                {
                    updateCamera.Invoke(player, null);
                    aircraft.ApplyPlayerInput(kind == PrototypeVehicleKind.Jet ? 1f : 0f,
                        0f, 0f, false, false, .05f);
                    updateCamera.Invoke(player, null);
                    Check(Mathf.Abs(Mathf.DeltaAngle(player.ViewCamera.transform.eulerAngles.y, heading)) < .05f,
                        kind + " camera preserves the mouse's world heading while hull turns");
                    if (step == 159) settledYaw = aircraft.transform.eulerAngles.y;
                }
                Check(Mathf.Abs(Mathf.DeltaAngle(aircraft.transform.eulerAngles.y, heading)) < 2f,
                    kind + " hull converges toward a stopped mouse heading");
                Check(Mathf.Abs(Mathf.DeltaAngle(settledYaw, aircraft.transform.eulerAngles.y)) < .5f,
                    kind + " stops continuous circling after heading settles");
                player.ForceExitVehicle(); Reset(aircraft);
            }
        }

        private static void VerifyWeapons(PrototypeRuntime game, PrototypePlayer player)
        {
            PrototypeCombinedArmsVehicle ifv = Find(game, player.Team, PrototypeVehicleKind.Ifv);
            PrototypeTeam enemyTeam = player.Team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
            PrototypeCombinedArmsVehicle target = Find(game, enemyTeam, PrototypeVehicleKind.Ifv);
            Place(ifv, new Vector2(-325f, -320f)); Place(target, new Vector2(-325f, -260f));
            Check(Enter(player, ifv), "V12 driver enters for primary weapon check");
            ifv.AimAt(target.transform.position + Vector3.up * 1.7f, float.PositiveInfinity);
            float health = target.Health;
            Check(ifv.TryFireWeapon(), "V12 launches a 30mm round");
            Check(!ifv.TryFireWeapon(), "V12 primary cooldown prevents duplicate shot");
            PrototypeVehicleProjectile[] fired = UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>();
            string launch = " muzzle=" + ifv.WeaponMuzzle.position + " axis=" + ifv.WeaponMuzzle.forward +
                " target=" + target.transform.position + " hull=" + target.GetComponent<BoxCollider>().bounds;
            StepProjectiles(60);
            string impacts = "";
            foreach (PrototypeVehicleProjectile shot in fired)
                impacts += " round=" + shot.transform.position + " hit=" +
                    (shot.HitCollider == null ? "none" : shot.HitCollider.name);
            Check(target.Health < health, "30mm swept projectile damages an enemy hull" + launch + impacts);
            Check(player.TrySwitchVehicleSeat(1) && ifv.TryFireWeapon(), "independent V12 crew MG fires");
            player.ForceExitVehicle(); Reset(ifv); Reset(target);

            PrototypeCombinedArmsVehicle aa = Find(game, player.Team, PrototypeVehicleKind.AntiAir);
            PrototypeCombinedArmsVehicle aircraft = Find(game, enemyTeam, PrototypeVehicleKind.Helicopter);
            Place(aa, new Vector2(-325f, -320f)); Place(aircraft, new Vector2(-325f, -220f));
            Check(Enter(player, aa), "A40 driver enters for guidance check");
            Check(aa.SelectWeapon(1), "A40 selects missile mode");
            aa.AimAt(aircraft.transform.position + Vector3.up * 1.7f, float.PositiveInfinity);
            aa.UpdateLock(2f); Check(aa.LockedTarget == null, "A40 refuses a grounded target");
            aircraft.transform.position += Vector3.up * 30f;
            aa.AimAt(aircraft.transform.position + Vector3.up * 1.7f, float.PositiveInfinity);
            aa.UpdateLock(.5f);
            Check(aa.LockProgress < 1f && !aa.TryFireWeapon(), "short aim cannot fire a missile");
            aa.UpdateLock(1.1f);
            Check(aa.LockProgress == 1f && aa.TryFireWeapon(), "sustained 1.5s aim fires a guided missile");
            Check(!aa.TryFireWeapon(), "A40 missile has a seven-second cooldown");
            PrototypeVehicleProjectile guided = GuidedProjectile();
            Check(guided != null && guided.IsGuided, "missile has a seeker target");
            // Optional AI authority permits countermeasure testing on the enemy
            // aircraft without assigning the local player to the wrong team.
            aircraft.SetAiControlled(true);
            Check(aircraft.DeployCountermeasure() && !aircraft.DeployCountermeasure(), "aircraft flares deploy with cooldown");
            StepProjectiles(1);
            Check(!guided.IsGuided && aa.LockedTarget == null, "flares break seeker guidance and launcher lock");
            aircraft.SetAiControlled(false); player.ForceExitVehicle();
            Reset(aa); Reset(aircraft);
            foreach (PrototypeVehicleProjectile projectile in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                UnityEngine.Object.Destroy(projectile.gameObject);
        }

        private static void VerifyLossAndRespawn(PrototypeRuntime game, PrototypePlayer player)
        {
            PrototypeCombinedArmsVehicle bike = Find(game, player.Team, PrototypeVehicleKind.Motorcycle);
            Check(Enter(player, bike), "M2 enters for crew loss check");
            float tickets = player.Team == PrototypeTeam.Blue ? game.Match.BlueTickets : game.Match.RedTickets;
            bike.TakeDamage(1000f);
            Check(!bike.Alive && !player.Alive && !player.IsDowned && player.CurrentVehicle == null,
                "destroyed vehicle eliminates its crew and clears the seat");
            float after = player.Team == PrototypeTeam.Blue ? game.Match.BlueTickets : game.Match.RedTickets;
            Check(Mathf.Approximately(after, tickets - 1.5f), "crew and vehicle loss consume exactly 1.5 tickets");
            Check(!bike.Repair(1000f), "destroyed vehicle cannot be repaired back to life");
            PrototypeCombinedArmsVehicle blocker = Find(game, player.Team, PrototypeVehicleKind.Ifv);
            Vector3 blockerHome = blocker.transform.position;
            Vector2 spawn = (Vector2)typeof(PrototypeCombinedArmsVehicle).GetField("spawn", Private).GetValue(bike);
            Place(blocker, spawn);
            typeof(PrototypeCombinedArmsVehicle).GetField("respawnAt", Private).SetValue(bike, Time.time - 1f);
            Invoke(bike, "Update"); Check(!bike.Alive, "an occupied parking bay postpones respawn");
            blocker.transform.position = blockerHome; Invoke(bike, "Update");
            Check(bike.Alive && Mathf.Approximately(bike.Health, bike.MaxHealth) && bike.PlayerSeat == -1,
                "clear bay respawns a full-health empty vehicle");
            Respawn(player); game.Frontend?.Close();
        }

        [MenuItem("IRONFRONT/Preview Next Added Vehicle", true)]
        private static bool CanPreview() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Preview Next Added Vehicle")]
        private static void Preview()
        {
            PrototypeRuntime game = Prepare(); game.Player.ForceExitVehicle();
            PrototypeVehicleKind kind = (PrototypeVehicleKind)(preview++ % 5);
            PrototypeCombinedArmsVehicle vehicle = Find(game, game.Player.Team, kind);
            if (vehicle == null) return;
            Reset(vehicle); Enter(game.Player, vehicle);
            Debug.Log("IRONFRONT added vehicle preview: " + vehicle.VehicleName + "; V view; F1–F4 seats");
        }

        [MenuItem("IRONFRONT/Capture Added Vehicle Models", true)]
        private static bool CanCapture() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Capture Added Vehicle Models")]
        public static void Capture()
        {
            PrototypeRuntime game = Prepare();
            string folder = Path.Combine(Application.dataPath, "../Builds/Verification046");
            Directory.CreateDirectory(folder);
            Camera camera = new GameObject("Vehicle QA camera").AddComponent<Camera>();
            camera.enabled = false; camera.fieldOfView = 38f; camera.nearClipPlane = .1f;
            camera.farClipPlane = 1000f; camera.backgroundColor = new Color(.58f, .69f, .72f);
            const int previewLayer = 31;
            camera.cullingMask = 1 << previewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            RenderTexture target = new RenderTexture(1200, 800, 24);
            camera.targetTexture = target;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Temporary vehicle QA floor";
            floor.layer = previewLayer; floor.transform.localScale = new Vector3(4f, 1f, 4f);
            floor.GetComponent<Collider>().enabled = false;
            Material floorMaterial = new Material(game.ArmorMaterial);
            floorMaterial.color = new Color(.37f, .42f, .41f);
            if (floorMaterial.HasProperty("_BaseColor")) floorMaterial.SetColor("_BaseColor", floorMaterial.color);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            var savedLayers = new Dictionary<GameObject, int>();
            try
            {
                foreach (PrototypeVehicleKind kind in Enum.GetValues(typeof(PrototypeVehicleKind)))
                {
                    PrototypeCombinedArmsVehicle vehicle = Find(game, game.Player.Team, kind);
                    if (vehicle == null) continue;
                    foreach (Transform part in vehicle.GetComponentsInChildren<Transform>(true))
                    {
                        savedLayers.Add(part.gameObject, part.gameObject.layer);
                        part.gameObject.layer = previewLayer;
                    }
                    floor.transform.position = vehicle.transform.position - Vector3.up * .02f;
                    Vector3 center = vehicle.transform.position + Vector3.up * 1.6f;
                    float distance = kind == PrototypeVehicleKind.Motorcycle ? 7f :
                        vehicle.IsAircraft ? 25f : 21f;
                    camera.transform.position = center + new Vector3(-.7f, .44f, .85f).normalized * distance;
                    camera.transform.LookAt(center);
                    camera.Render();
                    RenderTexture old = RenderTexture.active; RenderTexture.active = target;
                    var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply();
                    RenderTexture.active = old;
                    File.WriteAllBytes(Path.Combine(folder, "vehicle-" + kind + ".png"), png.EncodeToPNG());
                    UnityEngine.Object.Destroy(png);
                    foreach (KeyValuePair<GameObject, int> part in savedLayers)
                        if (part.Key != null) part.Key.layer = part.Value;
                    savedLayers.Clear();
                }
                Debug.Log("IRONFRONT added vehicle model captures: " + Path.GetFullPath(folder));
            }
            finally
            {
                foreach (KeyValuePair<GameObject, int> part in savedLayers)
                    if (part.Key != null) part.Key.layer = part.Value;
                camera.targetTexture = null; target.Release();
                UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(camera.gameObject);
                UnityEngine.Object.Destroy(floor); UnityEngine.Object.Destroy(floorMaterial);
            }
        }

        private static PrototypeRuntime Prepare()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game == null || game.Player == null) throw new InvalidOperationException("Start SampleScene in Play mode.");
            if (!game.MatchStarted) game.BeginBattle();
            game.Frontend?.Close(); game.Player.ForceExitVehicle();
            if (!game.Player.Alive) Respawn(game.Player);
            return game;
        }
        private static PrototypeCombinedArmsVehicle Find(PrototypeRuntime game, PrototypeTeam team, PrototypeVehicleKind kind)
        {
            foreach (IPrototypeVehicle vehicle in game.Vehicles)
                if (vehicle is PrototypeCombinedArmsVehicle combined && combined.Team == team && combined.Kind == kind) return combined;
            return null;
        }
        private static bool Enter(PrototypePlayer player, IPrototypeVehicle vehicle) =>
            (bool)typeof(PrototypePlayer).GetMethod("EnterVehicle", Private).Invoke(player, new object[] { vehicle });
        private static void Respawn(PrototypePlayer player) => typeof(PrototypePlayer).GetMethod("Respawn", Private,
            null, Type.EmptyTypes, null).Invoke(player, null);
        private static void Reset(PrototypeCombinedArmsVehicle vehicle) => Invoke(vehicle, "ResetVehicle");
        private static void Place(PrototypeCombinedArmsVehicle vehicle, Vector2 point)
        {
            vehicle.transform.position = new Vector3(point.x, PrototypeLayout.HeightAt(point.x, point.y) + .05f, point.y);
            vehicle.transform.rotation = Quaternion.identity; Physics.SyncTransforms();
        }
        private static void Flight(PrototypeCombinedArmsVehicle vehicle, float throttle, float side, float climb,
            float pitch, bool boost, bool brake, float dt, bool powered, float? yaw) =>
            typeof(PrototypeCombinedArmsVehicle).GetMethod("StepFlight", Private).Invoke(vehicle,
                new object[] { throttle, side, climb, pitch, boost, brake, dt, powered, yaw });
        private static void Invoke(object instance, string method) => instance.GetType().GetMethod(method, Private).Invoke(instance, null);
        private static PrototypeVehicleProjectile GuidedProjectile()
        {
            foreach (PrototypeVehicleProjectile projectile in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                if (projectile.IsGuided) return projectile;
            return null;
        }
        private static void StepProjectiles(int count)
        {
            for (int step = 0; step < count; step++)
                foreach (PrototypeVehicleProjectile projectile in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>())
                    if (projectile.enabled) typeof(PrototypeVehicleProjectile).GetMethod("Step", Private).Invoke(projectile, new object[] { .02f });
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
