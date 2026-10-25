using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public sealed class PrototypeCombinedArmsVehicle : MonoBehaviour,
        IPrototypeSeatedVehicle, IPrototypeWeaponAim, IPrototypeAircraftVehicle, IPrototypeArmoredVehicle
    {
        private PrototypeRuntime runtime;
        private PrototypeVehicleDefinition definition;
        private PrototypeVehicleSeats seats;
        private PrototypeCombinedVehicleVisual model;
        private Vector2 spawn, goal;
        private float spawnYaw, nextShot, nextMG, nextSecondary, nextCountermeasure,
            countermeasureUntil, respawnAt, wreckUntil, nextPlan, nextDisplay, blockedUntil;
        private bool aiControlled;
        private float verticalVelocity, enginePower, roll, flightPitch;
        private float motorcycleSteering, motorcycleYawRate;
        private readonly List<Vector2> route = new List<Vector2>();
        private int routeIndex;
        private IPrototypeVehicle lockTarget;
        private readonly Dictionary<object, float> lockWarnings = new Dictionary<object, float>();
        private readonly List<object> expiredLockSources = new List<object>();
        private readonly object legacyLockSource = new object();

        public PrototypeVehicleKind Kind { get; private set; }
        public string VehicleName => definition != null ? definition.Name : "VEHICLE";
        public PrototypeTeam Team { get; private set; }
        public bool Alive => Health > 0f;
        public bool RespawnEnabled { get; set; } = true;
        public bool IsActiveThreat => Alive && (aiControlled || Driver != null || SeatCanFire);
        public bool DriverCanFire => definition != null && definition.Armed;
        public float Health { get; private set; }
        public float MaxHealth => definition != null ? definition.Health : 0f;
        public float MaximumHealth => MaxHealth;
        public int ArmorRating => definition != null ? definition.ArmorRating : 6;
        public float Speed { get; private set; }
        public float MotorcycleSteeringInput => motorcycleSteering;
        public float MotorcycleYawRateDegrees => motorcycleYawRate;
        public float Clearance => definition != null ? definition.Radius : 3.15f;
        public bool IsAircraft => definition != null && definition.Aircraft;
        public bool EngineRunning => Alive && (Driver != null || aiControlled || enginePower > .05f);
        public float EnginePower => enginePower;
        public PrototypeReconRadar ReconRadar { get; private set; }
        public bool IsDownwardMachineGun => Kind == PrototypeVehicleKind.Helicopter &&
            SeatCanFire && (PlayerSeat == 1 || WeaponSlot == 1);
        public float Altitude => IsAircraft ? Mathf.Max(0f,
            transform.position.y - PrototypeLayout.HeightAt(transform.position.x, transform.position.z) - .05f) : 0f;
        public float VerticalVelocity => verticalVelocity;
        public float Roll => roll;
        public float FlightPitch => flightPitch;
        public int WeaponSlot { get; private set; }
        public float LockProgress { get; private set; }
        public IPrototypeVehicle LockedTarget => lockTarget;
        public bool CountermeasureActive => Time.time < countermeasureUntil;
        public float LockImmunityRemaining => Mathf.Max(0f, countermeasureUntil - Time.time);
        public bool BeingLocked
        {
            get { RefreshLockWarnings(); return Alive && !CountermeasureActive && Time.time < WarningUntil; }
        }
        public bool IncomingMissile => Alive && !CountermeasureActive && Time.time < IncomingUntil;
        public float CountermeasureReadyIn => Mathf.Max(0f, nextCountermeasure - Time.time);
        public float IncomingUntil { get; private set; }
        public float WarningUntil { get; private set; }
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);
        public Transform VehicleTransform => transform;
        public PrototypePlayer Driver => seats?.Driver;
        public PrototypePlayer Occupant => seats?.Player;
        public int SeatCount => seats?.SeatCount ?? 0;
        public int PlayerSeat => seats?.PlayerSeat ?? -1;
        public PrototypeVehicleSeatRole PlayerSeatRole => seats != null ? seats.PlayerRole : PrototypeVehicleSeatRole.Passenger;
        public bool HasAuthoredArt => model != null && model.HasAuthoredArt;
        public bool SeatCanDrive => Alive && seats != null && seats.CanDrive;
        public bool SeatCanFire => Alive && seats != null && seats.CanFire;
        public bool PlayerControlsWeapon => SeatCanFire;
        public Transform WeaponMuzzle => model == null ? null : PlayerSeat == 1 || IsDownwardMachineGun ?
            model.GunnerMuzzle : model.Muzzle;
        public float WeaponRange => PlayerSeat == 1 || IsDownwardMachineGun ? 150f : definition?.Range ?? 0f;
        public Vector3 SeatPosition => GetSeatPosition(Mathf.Max(0, PlayerSeat));
        public Vector3 CameraPosition => GetSeatCameraPosition(Mathf.Max(0, PlayerSeat));
        public Quaternion MotorcyclePoseRotation => model?.MotorcycleBody != null ?
            model.MotorcycleBody.rotation : transform.rotation;
        public Transform MotorcycleGripTarget(bool left, int seat) => model == null ? null :
            seat == 0 ? (left ? model.MotorcycleLeftGrip : model.MotorcycleRightGrip) :
            (left ? model.MotorcyclePassengerLeftGrip : model.MotorcyclePassengerRightGrip);
        public Transform MotorcyclePegTarget(bool left, int seat) => model == null ? null :
            seat == 0 ? (left ? model.MotorcycleLeftPeg : model.MotorcycleRightPeg) :
            (left ? model.MotorcyclePassengerLeftPeg : model.MotorcyclePassengerRightPeg);
        public string WeaponStatus
        {
            get
            {
                if (!DriverCanFire) return "UNARMED";
                if (PlayerSeat >= 2) return "PASSENGER";
                if (IsDownwardMachineGun) return "BELLY MG  /  DOWN 15-85 DEG";
                if (PlayerSeat == 1) return "CREW MG";
                if (Kind == PrototypeVehicleKind.ReconHelicopter) return "RECON MG  /  Q RADAR";
                if (WeaponSlot == 1 && definition.Guided)
                    return "MISSILE " + (Time.time < nextSecondary ?
                        Mathf.CeilToInt(nextSecondary - Time.time) + "s" :
                        LockProgress >= 1f ? "LOCKED" : lockTarget == null ? "NO LOCK" :
                        "LOCK " + Mathf.RoundToInt(LockProgress * 100f) + "%");
                if (WeaponSlot == 1) return "COAX MG";
                return Kind == PrototypeVehicleKind.Ifv ? "30mm HE" :
                    Kind == PrototypeVehicleKind.AntiAir ? "35mm AIR" :
                    Kind == PrototypeVehicleKind.Helicopter ? "70mm HE" : "25mm AP";
            }
        }

        public Vector3 ExitPosition
        {
            get
            {
                float radius = Clearance + 2.2f;
                for (int step = 0; step < 32; step++)
                {
                    float angle = step * Mathf.PI / 8f;
                    Vector2 point = MapPosition + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) *
                        (radius + step / 16 * 3f);
                    if (PrototypeLayout.Collides(point, .6f)) continue;
                    bool blocked = false;
                    foreach (IPrototypeVehicle other in runtime.Vehicles)
                        if (!ReferenceEquals(other, this) && other.Alive &&
                            Vector2.Distance(point, other.MapPosition) < OtherRadius(other) + .7f &&
                            Mathf.Abs(other.VehicleTransform.position.y - transform.position.y) < 4f)
                        { blocked = true; break; }
                    if (blocked) continue;
                    return new Vector3(point.x, IsAircraft && Altitude > 3f ?
                        transform.position.y + .7f : PrototypeLayout.HeightAt(point.x, point.y) + .3f, point.y);
                }
                return transform.position + transform.right * radius + Vector3.up * .7f;
            }
        }

        public void Initialize(PrototypeRuntime game, PrototypeTeam team,
            PrototypeVehicleKind kind, Vector2 position, bool controlledByAi)
        {
            runtime = game; Team = team; Kind = kind;
            definition = PrototypeVehicleDefinition.Get(kind); spawn = position;
            spawnYaw = team == PrototypeTeam.Blue ? 0f : 180f;
            aiControlled = controlledByAi;
            seats = new PrototypeVehicleSeats(this, runtime, definition.Seats,
                definition.Armed, definition.Armed && kind != PrototypeVehicleKind.Jet, definition.Aircraft);
            model = new PrototypeCombinedVehicleVisual();
            model.Build(transform, runtime, team, kind); ResetVehicle();
            PrototypeVehicleEngineAudio.Attach(gameObject, runtime, this,
                kind == PrototypeVehicleKind.Jet ? PrototypeEngineSound.Jet :
                IsAircraft ? PrototypeEngineSound.Rotor : kind == PrototypeVehicleKind.Motorcycle ?
                PrototypeEngineSound.Motorcycle : PrototypeEngineSound.HeavyDiesel, definition.Forward);
            if (kind == PrototypeVehicleKind.ReconHelicopter)
            {
                ReconRadar = gameObject.AddComponent<PrototypeReconRadar>();
                ReconRadar.Initialize(runtime, this);
            }
        }

        public void ApplyNetworkState(Vector3 position, float yaw, float health, bool alive)
        {
            if (runtime == null || !runtime.IsNetworkReplica || !Finite(position.x) ||
                !Finite(position.y) || !Finite(position.z) || !Finite(yaw) || !Finite(health)) return;
            transform.position = position; transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Health = alive ? Mathf.Clamp(health, .01f, definition.Health) : 0f;
            Speed = 0f; verticalVelocity = 0f;
            SetVisible(alive); model.Hull.enabled = alive;
        }

        public MonoBehaviour GetSeatOccupant(int seat) => seats?.GetOccupant(seat);
        public string GetSeatName(int seat) => seats?.GetName(seat) ?? "NO SEAT";
        public Vector3 GetSeatPosition(int seat)
        {
            Vector3 local;
            if (Kind == PrototypeVehicleKind.Motorcycle)
                return (seat == 0 ? model.MotorcycleDriverSeat : model.MotorcyclePassengerSeat).position;
            else if (Kind == PrototypeVehicleKind.Helicopter && HasAuthoredArt)
                local = seat == 0 ? new Vector3(-.35f, 2.8f, 4.5f) :
                    seat == 1 ? new Vector3(.35f, 2.7f, 2.7f) :
                    new Vector3(seat == 2 ? -.6f : .6f, 2.4f, -.6f);
            else if (HasAuthoredArt && (Kind == PrototypeVehicleKind.ReconHelicopter || Kind == PrototypeVehicleKind.Jet))
                return model.Airframe.TransformPoint(seat == 0 ? model.ArtRig.PilotSeat : seat == 1 ? new Vector3(.65f, .5f, 1.25f) : new Vector3(.55f, 1.35f, -.1f));
            else if (IsAircraft)
                local = seat < 2 ? new Vector3(seat == 0 ? -.35f : .55f, 1.35f, 1.8f) :
                    new Vector3(seat == 2 ? -.65f : .65f, 1.15f, -1.3f);
            else if (Kind == PrototypeVehicleKind.Ifv && HasAuthoredArt)
                local = seat == 0 ? new Vector3(-.65f, 1.05f, 1.35f) :
                    seat == 1 ? new Vector3(.52f, 1.80f, 0f) :
                    new Vector3(seat == 2 ? -.75f : .75f, 1.05f, -1.65f);
            else local = seat == 0 ? new Vector3(-.65f, .6f, .85f) :
                    seat == 1 ? new Vector3(.77f, 1.6f, -.6f) :
                    new Vector3(seat == 2 ? -.75f : .75f, .95f, -1.55f);
            return transform.TransformPoint(local);
        }
        public Vector3 GetSeatCameraPosition(int seat)
        {
            if (Kind == PrototypeVehicleKind.Helicopter && (seat == 1 || seat == 0 && IsDownwardMachineGun))
                return model.GunnerMuzzle.position - model.GunnerMuzzle.forward * .28f + Vector3.up * .06f;
            if (Kind == PrototypeVehicleKind.Ifv && HasAuthoredArt && seat == 1)
                return model.GunnerBarrel.position + Vector3.up * .5f - model.GunnerBarrel.forward * .15f;
            if (seat == 1 && model.GunnerBarrel != null)
                return model.GunnerBarrel.TransformPoint(new Vector3(-.15f, .16f, .35f));
            if (Kind == PrototypeVehicleKind.Motorcycle)
                return (seat == 0 ? model.MotorcycleDriverEye : model.MotorcyclePassengerEye).position;
            if (Kind == PrototypeVehicleKind.Helicopter && HasAuthoredArt)
                return transform.TransformPoint(seat == 0 ? new Vector3(-.28f, 4.35f, 5.15f) :
                    new Vector3(seat == 2 ? -1.05f : 1.05f, 3.75f, -1.3f));
            if (HasAuthoredArt && (Kind == PrototypeVehicleKind.ReconHelicopter || Kind == PrototypeVehicleKind.Jet))
                return model.Airframe.TransformPoint(seat == 0 ? model.ArtRig.PilotEye : new Vector3(.7f, 2.6f, -.05f));
            if (Kind == PrototypeVehicleKind.Ifv && HasAuthoredArt && seat == 0)
                return model.Turret.TransformPoint(new Vector3(-.65f, 1.18f, 1.45f));
            if (seat >= 2) return transform.TransformPoint(new Vector3(seat == 2 ? -1.24f : 1.24f, 3f, -2.8f));
            if (IsAircraft) return transform.TransformPoint(new Vector3(Kind == PrototypeVehicleKind.Jet ? 0f : -.35f, 2.85f, 3.24f));
            return model.Turret.TransformPoint(new Vector3(.58f, 1.11f, .91f));
        }
        public bool TrySetDriver(PrototypePlayer player) => TrySetPlayerSeat(player, 0);
        public bool TrySetPlayerSeat(PrototypePlayer player, int seat)
        {
            int previous = PlayerSeat;
            if (seats == null || !seats.TrySetPlayerSeat(player, seat)) return false;
            if (previous == seat) return true;
            if (!IsAircraft) Speed = 0f;
            if (Kind == PrototypeVehicleKind.Motorcycle) motorcycleSteering = motorcycleYawRate = 0f;
            ClearSeekerLock(); return true;
        }
        public void RemoveDriver(PrototypePlayer player)
        {
            seats?.RemovePlayer(player); if (!IsAircraft) Speed = 0f;
            if (Kind == PrototypeVehicleKind.Motorcycle) motorcycleSteering = motorcycleYawRate = 0f;
            ClearSeekerLock(); nextPlan = 0f;
        }
        public void SetAiControlled(bool value) { aiControlled = value; nextPlan = 0f; }

        public void TakeDamage(float amount)
        {
            if (!CanSimulate || !Alive || !Finite(amount) || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount); if (Alive) return;
            runtime.Scores?.ResolveDefeat(this, Team);
            PrototypeVehicleProjectile.BreakGuidanceTo(this);
            ClearLockWarning(); IncomingUntil = 0f;
            var lostCrew = new List<MonoBehaviour>();
            for (int seat = 0; seat < SeatCount; seat++)
                if (GetSeatOccupant(seat) != null) lostCrew.Add(GetSeatOccupant(seat));
            foreach (MonoBehaviour crew in lostCrew)
            {
                if (crew is PrototypePlayer player) player.TakeDamage(1000f);
                else if (crew is PrototypeBot bot) bot.TakeDamage(1000f);
                else if (crew is PrototypeRemotePlayer remote) remote.TakeDamage(1000f);
            }
            seats.Clear(); Speed = 0f; verticalVelocity = 0f;
            ClearSeekerLock();
            model.Hull.enabled = false; wreckUntil = Time.time + 12f;
            respawnAt = Time.time + definition.RespawnDelay;
            PrototypeCombatFeedback.Explosion(runtime, transform.position + Vector3.up, Clearance + 1f,
                PrototypeCombatSound.TankExplosion);
            runtime.Match.RecordVehicleLoss(Team);
        }
        // Retire requisitioned hulls when the battle ends without treating
        // disposal as combat loss. Standard map vehicles keep normal respawn.
        public void RetireSupportVehicle()
        {
            if (RespawnEnabled) return;
            if (Occupant != null && ReferenceEquals(Occupant.CurrentVehicle, this)) Occupant.ForceExitVehicle();
            seats?.Clear(); Health = 0f; Speed = verticalVelocity = enginePower = 0f;
            ClearSeekerLock(); ClearLockWarning(); IncomingUntil = 0f;
            PrototypeVehicleProjectile.BreakGuidanceTo(this);
            SetVisible(false);
            if (model?.Hull != null) model.Hull.enabled = false;
            enabled = false; gameObject.SetActive(false);
        }

        public bool Repair(float amount)
        {
            if (!CanSimulate || !Alive || !Finite(amount) || amount <= 0f || Health >= definition.Health) return false;
            Health = Mathf.Min(definition.Health, Health + amount); return true;
        }
        private bool CanSimulate => runtime != null && !runtime.IsNetworkReplica &&
            runtime.MatchStarted && !runtime.Match.Winner.HasValue;
        private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);

        private void Update()
        {
            if (!CanSimulate) return;
            if (!Alive)
            {
                if (RespawnEnabled && Time.time >= respawnAt && SpawnClear()) ResetVehicle();
                else if (Time.time >= wreckUntil) SetVisible(false);
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (Occupant != null && !Occupant.Alive) Occupant.ForceExitVehicle();
            if (SeatCanDrive && runtime.ScoreboardOpen && !IsAircraft)
                StepGround(0f, 0f, false, dt);
            else if (SeatCanDrive && !runtime.ScoreboardOpen && Cursor.lockState == CursorLockMode.Locked)
            {
                float climb = (Input.GetKey(KeyCode.Space) ? 1f : 0f) -
                    (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? 1f : 0f);
                ApplyPlayerInput(Input.GetAxisRaw("Vertical"), Input.GetAxisRaw("Horizontal"),
                    climb, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                    IsAircraft ? climb < 0f : Input.GetKey(KeyCode.Space), dt);
            }
            else if (Occupant == null && aiControlled) UpdateAi(dt);
            else if (IsAircraft) StepFlight(0f, 0f, 0f, 0f, false, false, dt, false, null);
            else
            {
                Speed = Mathf.MoveTowards(Speed, 0f, definition.Braking * dt);
                if (Kind == PrototypeVehicleKind.Motorcycle)
                {
                    motorcycleSteering = Mathf.MoveTowards(motorcycleSteering, 0f, 3.3f * dt);
                    motorcycleYawRate = Mathf.MoveTowards(motorcycleYawRate, 0f, 160f * dt);
                    model.UpdateMotorcycle(Speed, motorcycleSteering, dt);
                }
            }

            if (SeatCanFire && !runtime.ScoreboardOpen && Cursor.lockState == CursorLockMode.Locked)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SelectWeapon(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) SelectWeapon(1);
                if (Occupant.ViewCamera != null)
                    AimAt(PrototypeVehicleAim.CameraTarget(Occupant.ViewCamera, transform,
                        definition.Guided ? 650f : WeaponRange), dt);
                UpdateLock(dt);
                if (Input.GetMouseButton(0)) TryFireWeapon();
                if (PlayerSeat == 0 && Input.GetMouseButton(1) && definition.Guided)
                    TryFireWeapon(true);
            }
            if (Occupant != null && !runtime.ScoreboardOpen && !runtime.PauseOpen && !runtime.SupportMenuOpen &&
                Cursor.lockState == CursorLockMode.Locked && PlayerSeat == 0 && Input.GetKeyDown(KeyCode.X)) DeployCountermeasure();
            if (SeatCanDrive && !runtime.ScoreboardOpen && Cursor.lockState == CursorLockMode.Locked && Input.GetKeyDown(KeyCode.Q))
                ReconRadar?.TryScan();
            UpdateVisualMotion(dt);
            if (Time.time >= nextDisplay && model.Instruments != null)
            {
                nextDisplay = Time.time + .2f;
                RefreshInstrumentDisplay();
            }
        }

        private void RefreshInstrumentDisplay()
        {
            if (model?.Instruments == null) return;
            if (Kind == PrototypeVehicleKind.Motorcycle)
                model.Instruments.text = Mathf.RoundToInt(Mathf.Abs(Speed) * 3.6f) + " KM/H\nHP " + Mathf.CeilToInt(Health);
            else
                model.Instruments.text = Mathf.RoundToInt(Mathf.Abs(Speed) * 3.6f) + " KM/H  HP " +
                    Mathf.CeilToInt(Health) + (IsAircraft ? "\nALT " + Mathf.RoundToInt(Altitude) + " M" : "\n" + WeaponStatus);
        }

        private void UpdateVisualMotion(float dt)
        {
            if (model.Airframe != null)
                model.Airframe.localRotation = Quaternion.Euler(-flightPitch * Mathf.Rad2Deg, 0f, -roll * Mathf.Rad2Deg);
            if (model.Rotor != null) model.Rotor.Rotate(0f, 34f * Mathf.Rad2Deg * enginePower * dt, 0f);
            if (model.TailRotor != null) model.TailRotor.Rotate(65f * Mathf.Rad2Deg * enginePower * dt, 0f, 0f);
            if (model.Radar != null) model.Radar.Rotate(0f, 38f * dt, 0f);
        }

        // Also used by editor smoke checks. Seat authority is checked here,
        // so a gunner/passenger cannot accidentally steer through another caller.
        public void ApplyPlayerInput(float throttle, float steering, float climb,
            bool boost, bool brake, float dt)
        {
            if (!CanSimulate || !SeatCanDrive || runtime.ScoreboardOpen || !Finite(dt) || dt <= 0f) return;
            dt = Mathf.Min(dt, .05f);
            if (IsAircraft)
            {
                Camera camera = Driver.ViewCamera;
                float pitch = camera == null || IsDownwardMachineGun ? 0f : -Mathf.DeltaAngle(0f,
                    camera.transform.eulerAngles.x) * Mathf.Deg2Rad;
                float? yaw = camera == null || IsDownwardMachineGun ? (float?)null : camera.transform.eulerAngles.y;
                StepFlight(throttle, steering, climb, pitch, boost, brake, dt, true, yaw);
            }
            else StepGround(throttle, steering, brake, dt);
        }

        private void StepGround(float throttle, float steering, bool brake, float dt)
        {
            throttle = Mathf.Clamp(throttle, -1f, 1f); steering = Mathf.Clamp(steering, -1f, 1f);
            if (Kind == PrototypeVehicleKind.Ifv)
                model.WheelSteering = Mathf.MoveTowards(model.WheelSteering, steering, dt * 4f);
            float target = brake ? 0f : throttle * (throttle < 0f ? definition.Reverse : definition.Forward);
            float rate = brake ? definition.Braking * 3f : Mathf.Abs(target) < Mathf.Abs(Speed) ?
                definition.Braking : definition.Acceleration;
            Speed = Mathf.MoveTowards(Speed, target, rate * dt);
            float turnScale = Kind == PrototypeVehicleKind.Ifv || Kind == PrototypeVehicleKind.AntiAir ?
                .65f : Mathf.Min(1f, Mathf.Abs(Speed) / 4f);
            float yawRate = steering * definition.TurnRate * Mathf.Rad2Deg * turnScale *
                (Speed < -.1f ? -1f : 1f);
            if (Kind == PrototypeVehicleKind.Motorcycle)
            {
                // Build steering over .4 seconds and release slightly faster.
                // At road speed, a held key makes a broad arc instead of the
                // previous instantaneous 149-degrees-per-second turn.
                motorcycleSteering = Mathf.MoveTowards(motorcycleSteering, steering,
                    (Mathf.Abs(steering) < .05f ? 3.3f : 2.5f) * dt);
                float speedLimit = Mathf.Lerp(1f, .40f, Mathf.InverseLerp(7f, definition.Forward, Mathf.Abs(Speed)));
                float targetYawRate = motorcycleSteering * definition.TurnRate * Mathf.Rad2Deg * turnScale *
                    speedLimit * (Speed < -.1f ? -1f : 1f);
                motorcycleYawRate = Mathf.MoveTowards(motorcycleYawRate, targetYawRate, 160f * dt);
                yawRate = motorcycleYawRate;
            }
            transform.Rotate(0f, yawRate * dt, 0f, Space.World);
            Vector2 end = MapPosition + new Vector2(transform.forward.x, transform.forward.z) * Speed * dt;
            if (!ClearMove(MapPosition, end, transform.position.y))
            {
                Speed = 0f;
                if (Kind == PrototypeVehicleKind.Motorcycle) model.UpdateMotorcycle(0f, motorcycleSteering, dt);
                return;
            }
            transform.position = GroundPosition(end);
            if (Kind == PrototypeVehicleKind.Motorcycle) model.UpdateMotorcycle(Speed, motorcycleSteering, dt);
        }

        private void StepFlight(float throttle, float side, float climb, float pitch,
            bool boost, bool brake, float dt, bool powered, float? viewYaw)
        {
            bool jet = Kind == PrototypeVehicleKind.Jet;
            float altitude = Altitude;
            enginePower = Mathf.Lerp(enginePower, powered ? 1f : 0f,
                1f - Mathf.Exp(-dt * (powered ? 3f : 1.6f)));
            if (powered)
            {
                float delta = viewYaw.HasValue ? Mathf.DeltaAngle(transform.eulerAngles.y, viewYaw.Value) * Mathf.Deg2Rad : 0f;
                if (jet)
                {
                    roll = Mathf.Lerp(roll, Mathf.Clamp(side * .95f + delta * .45f, -1.1f, 1.1f), 1f - Mathf.Exp(-dt * 5f));
                    // Space gives an explicit takeoff command. Ctrl lowers the
                    // nose and brakes; the mouse then controls climbing/diving.
                    float requestedPitch = climb > 0f ? Mathf.Max(.24f, pitch) : climb < 0f ? Mathf.Min(-.18f, pitch) : pitch;
                    flightPitch = Mathf.Lerp(flightPitch, Mathf.Clamp(requestedPitch, -.85f, .85f), 1f - Mathf.Exp(-dt * 3f));
                    transform.Rotate(0f, (roll * 1.8f + Mathf.Clamp(delta, -.5f, .5f) * .3f) * Mathf.Rad2Deg * dt, 0f, Space.World);
                    float target = brake && altitude < 3f ? 0f : brake ? 32f : boost ? 92f :
                        altitude < .5f && throttle <= 0f && climb <= 0f ? 0f : Mathf.Clamp(52f + throttle * 23f, 29f, 75f);
                    Speed = Mathf.MoveTowards(Speed, target, (target < Speed ? 22f : 18f) * dt);
                }
                else
                {
                    transform.Rotate(0f, Mathf.Clamp(viewYaw.HasValue ? delta : side * 2.2f,
                        -2.2f * dt, 2.2f * dt) * Mathf.Rad2Deg, 0f, Space.World);
                    Speed = Mathf.MoveTowards(Speed, Mathf.Clamp(throttle, -1f, 1f) * definition.Forward * (boost ? 1.26f : 1f), 22f * dt);
                    roll = Mathf.Lerp(roll, Mathf.Clamp(-side * .4f, -.4f, .4f), 1f - Mathf.Exp(-dt * 5f));
                    flightPitch = Mathf.Lerp(flightPitch, -throttle * .12f, 1f - Mathf.Exp(-dt * 4f));
                }
            }
            else Speed *= Mathf.Exp(-dt * (jet ? .14f : .5f));
            float lift = (jet ? Mathf.Min(1f, Mathf.Abs(Speed) / 32f) : 1f) * enginePower;
            if (powered && lift > .95f)
                verticalVelocity = Mathf.Lerp(verticalVelocity,
                    jet ? Mathf.Sin(flightPitch) * Speed : Mathf.Clamp(climb, -1f, 1f) * 16f,
                    1f - Mathf.Exp(-dt * 4f));
            else verticalVelocity -= 9.81f * (powered ? 1f - lift : 1f) * dt;
            verticalVelocity *= Mathf.Exp(-dt * .06f);
            Vector3 move = transform.forward * (Speed * dt * (jet ? Mathf.Cos(flightPitch) : 1f)) +
                (jet || !powered ? Vector3.zero : transform.right * (side * 19f * dt));
            Vector3 candidate = transform.position + move + Vector3.up * (verticalVelocity * dt);
            float limit = PrototypeLayout.MapSize / 2f - Clearance;
            candidate.x = Mathf.Clamp(candidate.x, -limit, limit); candidate.z = Mathf.Clamp(candidate.z, -limit, limit);
            float ground = PrototypeLayout.HeightAt(candidate.x, candidate.z) + .05f;
            candidate.y = Mathf.Clamp(candidate.y, ground, ground + 130f);
            if (!ClearMove(MapPosition, new Vector2(candidate.x, candidate.z), Mathf.Min(transform.position.y, candidate.y)))
            { Speed = 0f; verticalVelocity = Mathf.Max(0f, verticalVelocity); return; }
            transform.position = candidate;
            if (candidate.y <= ground + .001f) { verticalVelocity = 0f; if (!powered) Speed = 0f; }
            if (Altitude >= 129.9f) verticalVelocity = Mathf.Min(0f, verticalVelocity);
        }

        private bool ClearMove(Vector2 from, Vector2 to, float height)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .5f));
            for (int step = 1; step <= steps; step++)
            {
                Vector2 point = Vector2.Lerp(from, to, (float)step / steps);
                if (!IsAircraft)
                { if (PrototypeLayout.Collides(point, Clearance)) return false; }
                else foreach (PrototypeLayout.Block block in PrototypeLayout.Blocks)
                {
                    if (height > PrototypeLayout.HeightAt(point.x, point.y) + block.Height + .4f) continue;
                    float dx = point.x - Mathf.Clamp(point.x, block.Position.x - block.Width / 2f, block.Position.x + block.Width / 2f);
                    float dz = point.y - Mathf.Clamp(point.y, block.Position.y - block.Depth / 2f, block.Position.y + block.Depth / 2f);
                    if (dx * dx + dz * dz < Clearance * Clearance) return false;
                }
                foreach (IPrototypeVehicle other in runtime.Vehicles)
                    if (!ReferenceEquals(other, this) && other.Alive &&
                        Mathf.Abs(other.VehicleTransform.position.y - height) < 3f &&
                        Vector2.Distance(point, other.MapPosition) < Clearance + OtherRadius(other)) return false;
            }
            return true;
        }
        private static float OtherRadius(IPrototypeVehicle other) => other is PrototypeCombinedArmsVehicle combined ?
            combined.Clearance : other is PrototypeTankVehicle ? 3.5f : other is PrototypeScoutVehicle ? 2.1f : 1.85f;
        private static Vector3 GroundPosition(Vector2 point) =>
            new Vector3(point.x, PrototypeLayout.HeightAt(point.x, point.y) + .05f, point.y);

        public void AimAt(Vector3 point, float dt)
        {
            if (!Alive || !definition.Armed || dt < 0f) return;
            bool gunner = PlayerSeat == 1 || IsDownwardMachineGun;
            Transform turret = gunner ? model.GunnerTurret : model.Turret;
            Transform barrel = gunner ? model.GunnerBarrel : model.Barrel;
            if (turret == null || barrel == null) return;
            if (Kind == PrototypeVehicleKind.Helicopter && !gunner)
            {
                // The attack cannon is bolted to the visible airframe. Its
                // bore follows flight pitch/roll, never the free-look camera.
                turret.localRotation = Quaternion.identity;
                barrel.localRotation = Quaternion.identity;
                return;
            }
            if (IsDownwardMachineGun)
            {
                // The same limits are exposed to Player mouse look: the visible
                // camera, barrel and launched round share one restricted cone.
                Vector3 delta = point - turret.position;
                float relativeYaw = Mathf.Clamp(Mathf.DeltaAngle(transform.eulerAngles.y,
                    Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg), -75f, 75f);
                float pitch = Mathf.Clamp(-Mathf.Atan2(delta.y,
                    Mathf.Max(.1f, new Vector2(delta.x, delta.z).magnitude)) * Mathf.Rad2Deg, 15f, 85f);
                turret.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + relativeYaw, 0f);
                barrel.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                return;
            }
            if (IsAircraft && !gunner)
            {
                Vector3 flat = point - turret.position; flat.y = 0f;
                float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                float limit = Kind == PrototypeVehicleKind.Jet ? .18f : .65f;
                float clamped = transform.eulerAngles.y + Mathf.Clamp(
                    Mathf.DeltaAngle(transform.eulerAngles.y, yaw), -limit * Mathf.Rad2Deg, limit * Mathf.Rad2Deg);
                float distance = flat.magnitude;
                Vector3 forward = Quaternion.Euler(0f, clamped, 0f) * Vector3.forward;
                point = turret.position + forward * distance + Vector3.up * (point.y - turret.position.y);
            }
            PrototypeVehicleAim.Turn(turret, barrel, point, (gunner ? 160f : 109f) * dt,
                57.3f * dt, Kind == PrototypeVehicleKind.AntiAir && !gunner ? -7f : IsAircraft ? -46f : -11f,
                Kind == PrototypeVehicleKind.AntiAir && !gunner ? 77f : 32f);
        }
        public bool SelectWeapon(int slot)
        {
            if (!SeatCanFire || PlayerSeat != 0 || slot < 0 || slot > 1) return false;
            if (Kind == PrototypeVehicleKind.ReconHelicopter && slot != 0) return false;
            WeaponSlot = slot; ClearSeekerLock(); return true;
        }
        public void UpdateLock(float dt)
        {
            if (!definition.Guided || !Alive || (!SeatCanFire && !aiControlled) ||
                PlayerSeat > 0 || (WeaponSlot != 1 && !aiControlled && !Input.GetMouseButton(1)))
            { ClearSeekerLock(); return; }
            IPrototypeVehicle selected = null; float nearest = Kind == PrototypeVehicleKind.AntiAir ? 320f : 650f;
            Vector3 origin = model.Muzzle.position;
            foreach (IPrototypeVehicle other in runtime.Vehicles)
            {
                if (ReferenceEquals(other, this) || !other.Alive || other.Team == Team ||
                    other is PrototypeCombinedArmsVehicle protectedVehicle && protectedVehicle.CountermeasureActive) continue;
                if (Kind == PrototypeVehicleKind.AntiAir &&
                    (!(other is PrototypeCombinedArmsVehicle airborne) || !airborne.IsAircraft || airborne.Altitude < 5f)) continue;
                Vector3 delta = other.VehicleTransform.position + Vector3.up * 1.7f - origin;
                float distance = delta.magnitude;
                if (distance < 5f || distance >= nearest || Vector3.Dot(delta.normalized, model.Muzzle.forward) < .965f ||
                    !SightClear(origin, other.VehicleTransform.position + Vector3.up * 1.7f, other.VehicleTransform)) continue;
                selected = other; nearest = distance;
            }
            if (selected == null) { ClearSeekerLock(); return; }
            if (!ReferenceEquals(selected, lockTarget)) { ClearSeekerLock(); lockTarget = selected; }
            LockProgress = Mathf.Min(1f, LockProgress + Mathf.Max(0f, dt) / 1.5f);
            if (selected is PrototypeCombinedArmsVehicle warned) warned.WarnLock(this);
        }
        private bool SightClear(Vector3 origin, Vector3 destination, Transform target)
        {
            if (PrototypeSmokeCloud.SegmentBlocked(origin, destination)) return false;
            Physics.SyncTransforms();
            Vector3 delta = destination - origin;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) &&
                    (target == null || !hit.collider.transform.IsChildOf(target))) return false;
            return true;
        }
        public bool TryFireWeapon(bool missileShortcut = false)
        {
            if (!CanSimulate || !SeatCanFire || !definition.Armed) return false;
            bool gunner = PlayerSeat == 1;
            if (!gunner && definition.Guided && (WeaponSlot == 1 || missileShortcut)) return FireMissile();
            if (gunner || WeaponSlot == 1 || Kind == PrototypeVehicleKind.ReconHelicopter)
            {
                if (Time.time < nextMG) return false;
                Transform muzzle = gunner || IsDownwardMachineGun ? model.GunnerMuzzle : model.Muzzle;
                if (PrototypeBallisticProjectile.LaunchMountedRound(runtime, Team, transform,
                    muzzle.position, muzzle.forward, 700f, 150f,
                    Kind == PrototypeVehicleKind.ReconHelicopter ? 20f : 24f) == null) return false;
                nextMG = Time.time + (gunner || Kind == PrototypeVehicleKind.ReconHelicopter ? .12f : .16f);
                return true;
            }
            if (Time.time < nextShot) return false;
            if (LaunchPrimary(1f) == null) return false;
            nextShot = Time.time + definition.Interval; return true;
        }
        private PrototypeVehicleProjectile LaunchPrimary(float scale)
        {
            if (Kind == PrototypeVehicleKind.ReconHelicopter)
            {
                PrototypeBallisticProjectile.LaunchMountedRound(runtime, Team, transform,
                    model.Muzzle.position, model.Muzzle.forward, 700f, definition.Range, definition.Damage * scale);
                return null;
            }
            return PrototypeVehicleProjectile.Launch(runtime, Team, transform, model.Muzzle.position, model.Muzzle.forward,
                definition.ProjectileSpeed, definition.Range, definition.Damage * scale,
                definition.Gravity, definition.BlastRadius, definition.Armor);
        }
        public void ClampWeaponView(ref float relativeYaw, ref float pitch)
        {
            if (!IsDownwardMachineGun) return;
            relativeYaw = Mathf.Clamp(relativeYaw, -75f, 75f);
            pitch = Mathf.Clamp(pitch, 15f, 85f);
        }
        private bool FireMissile()
        {
            if (Time.time < nextSecondary || LockProgress < 1f || lockTarget == null || !lockTarget.Alive ||
                lockTarget is PrototypeCombinedArmsVehicle protectedVehicle && protectedVehicle.CountermeasureActive) return false;
            Vector3 direction = lockTarget.VehicleTransform.position + Vector3.up * 1.7f - model.Muzzle.position;
            if (PrototypeVehicleProjectile.Launch(runtime, Team, transform, model.Muzzle.position, direction,
                145f, Kind == PrototypeVehicleKind.AntiAir ? 360f : 850f,
                Kind == PrototypeVehicleKind.AntiAir ? 135f : 260f, 0f, 4f, 1f, lockTarget) == null) return false;
            nextSecondary = Time.time + 7f;
            if (lockTarget is PrototypeCombinedArmsVehicle warned) warned.WarnIncoming();
            return true;
        }
        private void ClearSeekerLock()
        {
            if (lockTarget is PrototypeCombinedArmsVehicle warned) warned.ClearLockWarning(this);
            lockTarget = null; LockProgress = 0f;
        }
        public void WarnLock(object source = null)
        {
            if (!Alive || CountermeasureActive) return;
            lockWarnings[source ?? legacyLockSource] = Time.time + .25f;
            RefreshLockWarnings();
        }
        public void ClearLockWarning(object source = null)
        {
            if (source == null) lockWarnings.Clear();
            else lockWarnings.Remove(source);
            RefreshLockWarnings();
        }
        private void RefreshLockWarnings()
        {
            WarningUntil = 0f; expiredLockSources.Clear();
            foreach (KeyValuePair<object, float> entry in lockWarnings)
            {
                if (entry.Value <= Time.time) expiredLockSources.Add(entry.Key);
                else WarningUntil = Mathf.Max(WarningUntil, entry.Value);
            }
            foreach (object source in expiredLockSources) lockWarnings.Remove(source);
        }
        public void WarnIncoming()
        {
            if (Alive && !CountermeasureActive) IncomingUntil = Time.time + .25f;
        }
        public bool DeployCountermeasure()
        {
            if (!CanSimulate || !Alive || Kind == PrototypeVehicleKind.Motorcycle ||
                Time.time < nextCountermeasure || (!SeatCanDrive && !aiControlled)) return false;
            countermeasureUntil = Time.time + (IsAircraft ? 10f : 5f); nextCountermeasure = Time.time + 24f;
            ClearSeekerLock(); ClearLockWarning(); IncomingUntil = 0f;
            // Countermeasures break seekers immediately, not only after a
            // projectile's next collision. Existing missiles then fly unguided.
            foreach (IPrototypeVehicle other in runtime.Vehicles)
                if (other is PrototypeCombinedArmsVehicle launcher && ReferenceEquals(launcher.lockTarget, this))
                { launcher.ClearSeekerLock(); }
            if (runtime.Player != null && ReferenceEquals(runtime.Player.AntiAirLockTarget, this))
                runtime.Player.ResetAntiAirLock();
            PrototypeVehicleProjectile.BreakGuidanceTo(this);
            PrototypeVehicleCountermeasure.Deploy(runtime, transform.position, IsAircraft);
            return true;
        }

        private void UpdateAi(float dt)
        {
            if (Time.time >= nextPlan)
            {
                nextPlan = Time.time + 4f; goal = MapPosition; float best = float.PositiveInfinity;
                // Defenders already own every future Breakthrough point.
                // Their aircraft must cover the active frontline, rather
                // than waiting for a non-friendly point at the distant base.
                if (IsAircraft && runtime.Match.ActiveObjective != null)
                    goal = runtime.Match.ActiveObjective.Definition.Position;
                else
                    foreach (PrototypeCapturePoint point in runtime.Match.Points)
                    {
                        if (point.Owner == Team) continue;
                        float distance = Vector2.Distance(MapPosition, point.Definition.Position);
                        if (distance >= best || !IsAircraft && PrototypeLayout.Collides(point.Definition.Position, Clearance)) continue;
                        best = distance; goal = point.Definition.Position;
                    }
                route.Clear(); routeIndex = 0;
                if (!IsAircraft) PrototypeVehicleRoute.Find(MapPosition, goal, Clearance, route);
            }
            if (IsAircraft)
            {
                Vector2 delta = goal - MapPosition;
                float desiredYaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
                if (Kind == PrototypeVehicleKind.Jet && delta.magnitude < 35f)
                    desiredYaw += 75f;
                float climb = Mathf.Clamp((55f - Altitude) / 12f, -1f, 1f);
                float pitch = Kind == PrototypeVehicleKind.Jet ? Mathf.Clamp((55f - Altitude) / 70f, -.3f, .5f) : 0f;
                bool evading = Time.time < WarningUntil || Time.time < IncomingUntil;
                StepFlight(delta.magnitude > 30f ? 1f : .2f, evading ? 1f : 0f,
                    climb, pitch, evading, false, dt, true, desiredYaw);
                if (evading) DeployCountermeasure();
            }
            else
            {
                while (routeIndex < route.Count && Vector2.Distance(MapPosition, route[routeIndex]) < 6f) routeIndex++;
                Vector2 destination = routeIndex < route.Count ? route[routeIndex] : goal;
                if (Vector2.Distance(MapPosition, destination) > 15f && (route.Count > 0 ||
                    !PrototypeLayout.CirclePathBlocked(MapPosition, destination, Clearance)))
                {
                    Vector2 delta = destination - MapPosition;
                    float angle = Mathf.DeltaAngle(transform.eulerAngles.y, Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg);
                    float steering = Mathf.Clamp(angle / 35f, -1f, 1f);
                    float throttle = Mathf.Abs(angle) > 65f ? .35f : 1f;
                    Vector2 before = MapPosition;
                    StepGround(Time.time < blockedUntil ? -.6f : throttle,
                        Time.time < blockedUntil ? -steering : steering, false, dt);
                    if (Vector2.Distance(before, MapPosition) < .001f && Time.time >= blockedUntil)
                    { blockedUntil = Time.time + 1f; nextPlan = Mathf.Min(nextPlan, Time.time + 1.1f); }
                }
                else StepGround(0f, 0f, true, dt);
            }
            if (!definition.Armed) return;
            Vector3? enemy = FindAiTarget();
            if (!enemy.HasValue) return;
            AimAt(enemy.Value, dt);
            UpdateLock(dt);
            if (LockProgress >= 1f) FireMissile();
            if (Time.time >= nextShot && Vector3.Dot((enemy.Value - model.Muzzle.position).normalized, model.Muzzle.forward) > .97f)
            {
                // Slow, reduced-damage support avoids adding another infantry
                // squad's lethality when a scenario enables these optional AI.
                if (Kind == PrototypeVehicleKind.ReconHelicopter)
                {
                    PrototypeBallisticProjectile.LaunchMountedRound(runtime, Team, transform,
                        model.Muzzle.position, model.Muzzle.forward, 700f, definition.Range, definition.Damage * .45f);
                    nextShot = Time.time + .35f;
                }
                else if (LaunchPrimary(.45f) != null) nextShot = Time.time + Mathf.Max(.8f, definition.Interval * 1.8f);
            }
        }
        private Vector3? FindAiTarget()
        {
            Vector3? selected = null; float best = definition.Range;
            foreach (IPrototypeVehicle other in runtime.Vehicles)
            {
                if (ReferenceEquals(other, this) || !other.Alive || other.Team == Team || !other.IsActiveThreat) continue;
                Vector3 point = other.VehicleTransform.position + Vector3.up * 1.7f;
                float distance = Vector3.Distance(model.Muzzle.position, point);
                if (distance >= best || !SightClear(model.Muzzle.position, point, other.VehicleTransform)) continue;
                selected = point; best = distance;
            }
            foreach (PrototypeBot bot in runtime.Bots)
            {
                if (!bot.Alive || bot.IsPassenger || bot.Team == Team) continue;
                Vector3 point = bot.transform.position + Vector3.up * 1.3f;
                float distance = Vector3.Distance(model.Muzzle.position, point);
                if (distance >= best || !SightClear(model.Muzzle.position, point, bot.transform)) continue;
                selected = point; best = distance;
            }
            if (runtime.Player != null && runtime.Player.Alive && runtime.Player.Team != Team && runtime.Player.CurrentVehicle == null)
            {
                Vector3 point = runtime.Player.transform.position + Vector3.up * 1.3f;
                if (Vector3.Distance(model.Muzzle.position, point) < best && SightClear(model.Muzzle.position, point, runtime.Player.transform))
                    selected = point;
            }
            return selected;
        }

        private void ResetVehicle()
        {
            seats.Clear(); Health = definition.Health;
            runtime.Scores?.BeginLife(this); Speed = verticalVelocity = enginePower = roll = flightPitch = 0f;
            motorcycleSteering = motorcycleYawRate = 0f;
            WeaponSlot = 0; ClearSeekerLock();
            nextShot = nextMG = nextSecondary = nextCountermeasure = countermeasureUntil = 0f;
            IncomingUntil = blockedUntil = 0f; ClearLockWarning();
            goal = spawn; route.Clear(); routeIndex = 0; nextPlan = 0f;
            transform.position = GroundPosition(spawn); transform.rotation = Quaternion.Euler(0f, spawnYaw, 0f);
            if (model.Airframe != null) model.Airframe.localRotation = Quaternion.identity;
            if (Kind == PrototypeVehicleKind.Motorcycle) model.ResetMotorcycle();
            if (model.Turret != null) model.Turret.localRotation = Quaternion.identity;
            if (model.Barrel != null) model.Barrel.localRotation = Quaternion.identity;
            if (model.GunnerTurret != null) model.GunnerTurret.localRotation = Quaternion.identity;
            if (model.GunnerBarrel != null) model.GunnerBarrel.localRotation = Quaternion.identity;
            if (Kind == PrototypeVehicleKind.Helicopter && model.GunnerBarrel != null)
                model.GunnerBarrel.localRotation = Quaternion.Euler(45f, 0f, 0f);
            ReconRadar?.Clear();
            if (Kind == PrototypeVehicleKind.Motorcycle) RefreshInstrumentDisplay();
            SetVisible(true); model.Hull.enabled = true;
        }
        private bool SpawnClear()
        {
            if (PrototypeLayout.Collides(spawn, Clearance)) return false;
            foreach (IPrototypeVehicle other in runtime.Vehicles)
                if (!ReferenceEquals(other, this) && other.Alive &&
                    Mathf.Abs(other.VehicleTransform.position.y - GroundPosition(spawn).y) < 3f &&
                    Vector2.Distance(spawn, other.MapPosition) < Clearance + OtherRadius(other)) return false;
            return true;
        }
        private void SetVisible(bool visible)
        {
            model?.SetVisible(visible);
        }
    }
}
