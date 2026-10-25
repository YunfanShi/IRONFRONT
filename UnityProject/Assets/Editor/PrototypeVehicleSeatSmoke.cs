using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeVehicleSeatSmoke
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("IRONFRONT/Verify All Vehicle Seats", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify All Vehicle Seats")]
        public static void Verify()
        {
            try
            {
                PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null, "SampleScene has a player");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend?.Close();
                PrototypePlayer player = game.Player;
                Check(player.Alive, "living player can verify seats");
                player.ForceExitVehicle();
                var kinds = new HashSet<string>();
                MethodInfo enter = typeof(PrototypePlayer).GetMethod("EnterVehicle", PrivateInstance);
                MethodInfo camera = typeof(PrototypePlayer).GetMethod("UpdateVehicleCamera", PrivateInstance);
                int seatsChecked = 0;
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                {
                    if (vehicle.Team != player.Team || !vehicle.Alive ||
                        !kinds.Add(vehicle.VehicleName)) continue;
                    Check(vehicle is IPrototypeSeatedVehicle, vehicle.VehicleName + " has shared seats");
                    var seated = (IPrototypeSeatedVehicle)vehicle;
                    if (vehicle is PrototypeTransportVehicle)
                        for (int rear = 2; rear < seated.SeatCount; rear++)
                            if (seated.GetSeatOccupant(rear) is PrototypeBot passenger)
                                passenger.DisembarkTransport();
                    Check(seated.SeatCount >= 1 && seated.SeatCount <= 4, "valid seat count");
                    Check((bool)enter.Invoke(player, new object[] { vehicle }),
                        vehicle.VehicleName + " accepts driver");
                    Check(seated.GetSeatOccupant(0) == player && vehicle.Driver == player,
                        "driver ledger and control owner agree");
                    Check(!player.TrySwitchVehicleSeat(-1) &&
                        !player.TrySwitchVehicleSeat(seated.SeatCount) && seated.PlayerSeat == 0 &&
                        seated.GetSeatOccupant(0) == player, "invalid transfers keep original seat");
                    Check(vehicle.TrySetDriver(player) && seated.PlayerSeat == 0,
                        "reselecting occupied own seat is idempotent");
                    foreach (IPrototypeVehicle other in game.Vehicles)
                        if (!ReferenceEquals(other, vehicle) && other.Alive)
                        {
                            Check(!other.TrySetDriver(player) && seated.PlayerSeat == 0,
                                "boarding another vehicle cannot leak the current seat");
                            break;
                        }
                    Vector3? driverHand = null;
                    for (int seat = 0; seat < seated.SeatCount; seat++)
                    {
                        if (seated.GetSeatOccupant(seat) != null &&
                            seated.GetSeatOccupant(seat) != player) continue;
                        Check(player.TrySwitchVehicleSeat(seat), vehicle.VehicleName + " F" + (seat + 1));
                        Check(seated.PlayerSeat == seat && seated.GetSeatOccupant(seat) == player &&
                            vehicle.Occupant == player, "player occupies requested seat");
                        int references = 0;
                        for (int i = 0; i < seated.SeatCount; i++)
                            if (seated.GetSeatOccupant(i) == player) references++;
                        Check(references == 1, "player never occupies two seats");
                        Check(seated.SeatCanDrive == (seat == 0) &&
                            (vehicle.Driver == player) == (seat == 0),
                            "only driver or pilot controls movement");
                        bool canFire = seat == 0 ? vehicle.DriverCanFire :
                            seat == 1 && seated.PlayerSeatRole == PrototypeVehicleSeatRole.Gunner;
                        Check(seated.SeatCanFire == canFire &&
                            ((IPrototypeWeaponAim)vehicle).PlayerControlsWeapon == canFire,
                            "weapon permission agrees with seat role");
                        if (!player.IsFirstPerson) player.TogglePerspective();
                        camera.Invoke(player, null);
                        Check(Vector3.Distance(player.transform.position,
                            seated.GetSeatPosition(seat)) < .01f &&
                            Vector3.Distance(player.ViewCamera.transform.position,
                            seated.GetSeatCameraPosition(seat)) < .01f,
                            "body and first-person camera follow seat location");
                        player.TogglePerspective();
                        Check(!player.IsFirstPerson, "seat supports third-person view");
                        if (vehicle is IPrototypeAircraftVehicle air && air.IsAircraft)
                            Check(Vector3.Distance(player.ViewCamera.transform.position,
                                seated.SeatPosition) > 10f,
                                "aircraft chase view clears the large airframe");
                        if (vehicle is PrototypeCombinedArmsVehicle motorcycle &&
                            motorcycle.Kind == PrototypeVehicleKind.Motorcycle)
                        {
                            var visual = player.GetComponent<PrototypeSoldierVisual>();
                            var body = (GameObject)typeof(PrototypeSoldierVisual)
                                .GetField("model", PrivateInstance).GetValue(visual);
                            var hand = (Transform)typeof(PrototypeSoldierVisual)
                                .GetField("leftHand", PrivateInstance).GetValue(visual);
                            Check(body.activeInHierarchy && !visual.HeldWeapon.activeInHierarchy,
                                "third-person motorcycle has a visible unarmed rider");
                            Vector3 handLocal = player.transform.InverseTransformPoint(hand.position);
                            if (seat == 0) driverHand = handLocal;
                            else Check(driverHand.HasValue &&
                                Vector3.Distance(driverHand.Value, handLocal) > .1f,
                                "passenger hands use a distinct seated pose");
                        }
                        player.TogglePerspective();
                        Check(player.IsFirstPerson && seated.PlayerSeat == seat,
                            "perspective toggling preserves the seat");
                        VerifyExistingVehicleInput(vehicle, seated, canFire);
                        seatsChecked++;
                    }
                    // Release must work when the player is in a non-driver seat.
                    player.ForceExitVehicle();
                    Check(vehicle.Occupant == null && vehicle.Driver == null &&
                        seated.PlayerSeat == -1, "exit releases every human control context");
                    for (int seat = 0; seat < seated.SeatCount; seat++)
                        Check(seated.GetSeatOccupant(seat) != player, "exit releases seat ledger");
                }
                VerifyAiPassengerReservation(game, player, enter);
                VerifyDestroyedPassengerRelease(game, player, enter);
                VerifyGroundAircraftInteraction(game, player);
                VerifyAircraftExit(game, player, enter);
                VerifyAntiAirMouseLook(game, player, enter);
                Check(kinds.Count == 9, "all eight main vehicle kinds plus reconnaissance helicopter are present");
                Debug.Log("IRONFRONT vehicle seats PASS: " + kinds.Count + " kinds, " +
                    seatsChecked + " seat transitions, exclusive occupancy, controls, cameras, " +
                    "AI reservation, exit and destruction release");
            }
            catch (Exception error)
            {
                Debug.LogError("IRONFRONT vehicle seats FAIL: " + error);
            }
        }

        private static void VerifyExistingVehicleInput(IPrototypeVehicle vehicle,
            IPrototypeSeatedVehicle seated, bool canFire)
        {
            if (!(vehicle is PrototypeTankVehicle || vehicle is PrototypeScoutVehicle ||
                vehicle is PrototypeTransportVehicle)) return;
            MethodInfo controls = vehicle.GetType().GetMethod("ApplyPlayerInput", PrivateInstance);
            Check(controls != null, "seat input method exists");
            // Reset only test cooldowns, and clean up spawned rounds before they
            // can affect a running match. This checks the real launch path.
            vehicle.GetType().GetField("nextShot", PrivateInstance)?.SetValue(vehicle, 0f);
            vehicle.GetType().GetField("nextGunnerShot", PrivateInstance)?.SetValue(vehicle, 0f);
            var existing = new HashSet<PrototypeBallisticProjectile>(
                UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>());
            Vector3 position = vehicle.VehicleTransform.position;
            Quaternion rotation = vehicle.VehicleTransform.rotation;
            object[] arguments = vehicle is PrototypeTankVehicle ?
                new object[] { 1f, 1f, true, false, .05f } :
                new object[] { 1f, 1f, true, .05f };
            controls.Invoke(vehicle, arguments);
            if (!seated.SeatCanDrive)
                Check(Vector3.Distance(position, vehicle.VehicleTransform.position) < .001f &&
                    Quaternion.Angle(rotation, vehicle.VehicleTransform.rotation) < .001f,
                    "gunner and passenger input cannot move the hull");
            int launched = 0;
            foreach (PrototypeBallisticProjectile round in
                UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>())
                if (!existing.Contains(round))
                {
                    launched++;
                    UnityEngine.Object.DestroyImmediate(round.gameObject);
                }
            Check(launched == (canFire ? 1 : 0), "authorized seat launches exactly one physical round");
            vehicle.VehicleTransform.SetPositionAndRotation(position, rotation);
        }

        private static void VerifyAiPassengerReservation(PrototypeRuntime game,
            PrototypePlayer player, MethodInfo enter)
        {
            // A live battlefield can have every friendly bot dead, embarked,
            // or a rover moving faster than the boarding limit. Use actual
            // initialized actors in an isolated stationary fixture instead.
            // Keep all real boarding/seat authority paths and guards intact.
            var vehicleObject = new GameObject("AI reservation smoke transport");
            GameObject botObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            botObject.name = "AI reservation smoke infantry";
            PrototypeBot bot = null;
            try
            {
                Vector2 bay = new Vector2(-325f, -320f);
                var transport = vehicleObject.AddComponent<PrototypeTransportVehicle>();
                transport.Initialize(game, player.Team, bay, false);
                bot = botObject.AddComponent<PrototypeBot>();
                bot.Initialize(game, game.Navigation, player.Team,
                    bay + Vector2.right * 3f, PrototypeInfantryClass.Assault);
                bot.transform.position = transport.transform.position + transport.transform.right * 3f;
                Check(transport.TryBoardPassenger(bot) && bot.IsPassenger &&
                    transport.GetSeatOccupant(2) == bot,
                    "actual AI boarding reserves the first rear seat");
                Check(!transport.TryBoardPassenger(bot) && transport.GetSeatOccupant(3) == null,
                    "already embarked AI cannot reserve a second seat");
                Check((bool)enter.Invoke(player, new object[] { transport }),
                    "driver enters the AI-occupied transport");
                Check(!player.TrySwitchVehicleSeat(2) && transport.PlayerSeat == 0 &&
                    transport.GetSeatOccupant(0) == player && transport.Driver == player &&
                    transport.GetSeatOccupant(2) == bot,
                    "AI-occupied transfer fails without releasing either occupant");
                player.ForceExitVehicle();
                Check(transport.GetSeatOccupant(0) == null && transport.GetSeatOccupant(2) == bot &&
                    bot.IsPassenger, "human exit preserves the AI reservation");
                bot.DisembarkTransport();
                Check(!bot.IsPassenger && transport.GetSeatOccupant(2) == null,
                    "actual AI disembark releases its real seat");
                Check((bool)enter.Invoke(player, new object[] { transport }) &&
                    player.TrySwitchVehicleSeat(2) && transport.GetSeatOccupant(2) == player,
                    "released AI seat is immediately available to the human");
            }
            finally
            {
                player.ForceExitVehicle();
                if (bot != null && bot.IsPassenger) bot.DisembarkTransport();
                UnityEngine.Object.DestroyImmediate(botObject);
                UnityEngine.Object.DestroyImmediate(vehicleObject);
            }
        }

        private static void VerifyDestroyedPassengerRelease(PrototypeRuntime game,
            PrototypePlayer player, MethodInfo enter)
        {
            var testObject = new GameObject("Seat destruction smoke transport");
            try
            {
                var test = testObject.AddComponent<PrototypeTransportVehicle>();
                test.Initialize(game, player.Team, PrototypeLayout.BlueBase, false);
                Check((bool)enter.Invoke(player, new object[] { test }), "test transport accepts player");
                Check(player.TrySwitchVehicleSeat(3), "test passenger seat accepts player");
                test.TakeDamage(1000f);
                Check(!test.Alive && test.Occupant == null && test.PlayerSeat == -1 &&
                    player.CurrentVehicle == null && player.Alive,
                    "destroyed transport releases passenger and restores infantry control");
            }
            finally
            {
                player.ForceExitVehicle();
                UnityEngine.Object.DestroyImmediate(testObject);
            }
        }

        private static void VerifyAircraftExit(PrototypeRuntime game,
            PrototypePlayer player, MethodInfo enter)
        {
            var testObject = new GameObject("Aircraft exit smoke helicopter");
            Vector3 original = player.transform.position;
            Quaternion rotation = player.transform.rotation;
            CharacterController controller = player.GetComponent<CharacterController>();
            try
            {
                var aircraft = testObject.AddComponent<PrototypeCombinedArmsVehicle>();
                aircraft.Initialize(game, player.Team, PrototypeVehicleKind.Helicopter,
                    PrototypeLayout.BlueBase, false);
                aircraft.transform.position += Vector3.up * 40f;
                Check((bool)enter.Invoke(player, new object[] { aircraft }), "pilot enters airborne test vehicle");
                player.ForceExitVehicle();
                float ground = PrototypeLayout.HeightAt(player.transform.position.x,
                    player.transform.position.z);
                Check(player.IsParachuting && player.CurrentVehicle == null &&
                    player.transform.position.y > ground + 30f,
                    "airborne exit keeps altitude and enables automatic parachute");
                typeof(PrototypePlayer).GetMethod("StepInfantryGravity", PrivateInstance)
                    .Invoke(player, new object[] { 1f });
                float velocity = (float)typeof(PrototypePlayer).GetField("verticalSpeed", PrivateInstance)
                    .GetValue(player);
                Check(player.IsParachuting && Mathf.Approximately(velocity, -5f),
                    "parachute limits descent to 5 m/s despite stale ground contact");
                float altitudeBefore = player.transform.position.y;
                controller.Move(Vector3.up * velocity * .25f);
                Check(player.transform.position.y >= altitudeBefore - 1.26f,
                    "controller descent respects parachute speed");
                Check((bool)enter.Invoke(player, new object[] { aircraft }) && !player.IsParachuting,
                    "reboarding clears parachute state");
                player.ForceExitVehicle();
            }
            finally
            {
                player.ForceExitVehicle();
                typeof(PrototypePlayer).GetField("parachuting", PrivateInstance).SetValue(player, false);
                typeof(PrototypePlayer).GetField("verticalSpeed", PrivateInstance).SetValue(player, 0f);
                controller.enabled = false;
                player.transform.SetPositionAndRotation(original, rotation);
                controller.enabled = true;
                UnityEngine.Object.DestroyImmediate(testObject);
            }
        }

        private static void VerifyGroundAircraftInteraction(PrototypeRuntime game,
            PrototypePlayer player)
        {
            Vector2 groundPoint = Vector2.zero;
            bool found = false;
            for (int z = -320; z <= 320 && !found; z += 40)
                for (int x = -320; x <= 320 && !found; x += 40)
                {
                    Vector2 point = new Vector2(x, z);
                    if (PrototypeLayout.Collides(point, 2f)) continue;
                    bool nearby = false;
                    foreach (IPrototypeVehicle vehicle in game.Vehicles)
                        if (vehicle.Alive && Vector2.Distance(point, vehicle.MapPosition) < 16f)
                        { nearby = true; break; }
                    if (nearby) continue;
                    groundPoint = point;
                    found = true;
                }
            Check(found, "isolated ground point exists for E aircraft interaction");
            var testObject = new GameObject("Ground E aircraft interaction smoke");
            PrototypeCombinedArmsVehicle aircraft = null;
            Vector3 original = player.transform.position;
            Quaternion rotation = player.transform.rotation;
            CharacterController controller = player.GetComponent<CharacterController>();
            try
            {
                aircraft = testObject.AddComponent<PrototypeCombinedArmsVehicle>();
                aircraft.Initialize(game, player.Team, PrototypeVehicleKind.Helicopter,
                    groundPoint, false);
                game.Vehicles.Add(aircraft);
                controller.enabled = false;
                player.transform.position = new Vector3(groundPoint.x,
                    PrototypeLayout.HeightAt(groundPoint.x, groundPoint.y) + .3f, groundPoint.y);
                controller.enabled = true;
                aircraft.transform.position += Vector3.up * 40f;
                MethodInfo interaction = typeof(PrototypePlayer).GetMethod("TryEnterVehicle", PrivateInstance);
                interaction.Invoke(player, null);
                Check(player.CurrentVehicle == null && aircraft.Occupant == null &&
                    !player.IsParachuting, "ground E refuses an aircraft 40 m overhead");
                aircraft.transform.position -= Vector3.up * 40f;
                interaction.Invoke(player, null);
                Check(ReferenceEquals(player.CurrentVehicle, aircraft) && aircraft.Driver == player,
                    "same E interaction boards the landed aircraft");
                player.ForceExitVehicle();
            }
            finally
            {
                player.ForceExitVehicle();
                if (aircraft != null) game.Vehicles.Remove(aircraft);
                controller.enabled = false;
                player.transform.SetPositionAndRotation(original, rotation);
                controller.enabled = true;
                UnityEngine.Object.DestroyImmediate(testObject);
            }
        }

        private static void VerifyAntiAirMouseLook(PrototypeRuntime game,
            PrototypePlayer player, MethodInfo enter)
        {
            var testObject = new GameObject("Anti-air mouse elevation smoke");
            Vector3 original = player.transform.position;
            Quaternion rotation = player.transform.rotation;
            CharacterController controller = player.GetComponent<CharacterController>();
            try
            {
                var aa = testObject.AddComponent<PrototypeCombinedArmsVehicle>();
                aa.Initialize(game, player.Team, PrototypeVehicleKind.AntiAir,
                    PrototypeLayout.BlueBase, false);
                Check((bool)enter.Invoke(player, new object[] { aa }), "AA driver enters vehicle");
                MethodInfo look = typeof(PrototypePlayer).GetMethod("ApplyVehicleLook", PrivateInstance);
                MethodInfo camera = typeof(PrototypePlayer).GetMethod("UpdateVehicleCamera", PrivateInstance);
                look.Invoke(player, new object[] { 0f, -100f });
                camera.Invoke(player, null);
                Check(Mathf.Abs(Mathf.DeltaAngle(0f,
                    player.ViewCamera.transform.eulerAngles.x) + 77f) < .1f,
                    "AA driver mouse look reaches 77 degree elevation");
                for (int step = 0; step < 40; step++)
                    aa.AimAt(PrototypeVehicleAim.CameraTarget(player.ViewCamera,
                        aa.transform, aa.WeaponRange), .05f);
                Check(Vector3.Angle(Vector3.ProjectOnPlane(aa.WeaponMuzzle.forward,
                    Vector3.up), aa.WeaponMuzzle.forward) > 70f,
                    "real AA barrel follows the high mouse aim target");
                Check(player.TrySwitchVehicleSeat(1), "AA gunner seat accepts player");
                look.Invoke(player, new object[] { 0f, -100f });
                camera.Invoke(player, null);
                Check(Mathf.Abs(Mathf.DeltaAngle(0f,
                    player.ViewCamera.transform.eulerAngles.x) + 32f) < .1f,
                    "AA gunner mouse elevation follows its own MG limit");
                player.ForceExitVehicle();
            }
            finally
            {
                player.ForceExitVehicle();
                controller.enabled = false;
                player.transform.SetPositionAndRotation(original, rotation);
                controller.enabled = true;
                UnityEngine.Object.DestroyImmediate(testObject);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
