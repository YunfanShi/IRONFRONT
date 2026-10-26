using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeHeldEquipment { Firearm, Throwable, Launcher, AntiAirLauncher }

    [RequireComponent(typeof(CharacterController))]
    public sealed partial class PrototypePlayer : MonoBehaviour
    {
        private const float WalkSpeed = 4.2f;
        private const float SprintSpeed = 6.6f;
        private const float CrouchSpeed = 2.0f;
        private const float ProneSpeed = .85f;
        private const float DownedViewHalfAngle = 85f;
        private const float ReviveDuration = 4f;
        private CharacterController controller;
        private Camera viewCamera;
        private GameObject rifle;
        private GameObject firstPersonWeapon;
        private PrototypeWeaponArtRig rifleArt, firstPersonArt;
        private PrototypeSoldierVisual soldierVisual;
        private Transform magazineVisual;
        private Vector3 magazineHome;
        private Transform firstPersonMagazine;
        private Vector3 firstPersonMagazineHome;
        private PrototypeRuntime runtime;
        private float pitch;
        private float vehicleLookYaw;
        private float vehicleWorldLookYaw;
        private bool vehicleWorldHeadingLast;
        private bool motorcycleCameraValid;
        private float motorcycleCameraHullYaw;
        private int motorcycleCameraFrame = -1;
        private float verticalSpeed;
        private float respawnAt;
        private float downedUntil;
        private float lastDownedClockAt;
        private bool networkRevivePaused;
        private float networkReviveProgress;
        private float networkDownedRemaining;
        private bool rescueCalled;
        private float downedLookYaw;
        private float reviveStartedAt;
        private float lastRevivePulseAt;
        private PrototypeBot revivingMedic;
        private PrototypeInfantryKit kit = new PrototypeInfantryKit(PrototypeInfantryClass.Assault);
        private PrototypeInfantryKit secondaryKit = new PrototypeInfantryKit(
            PrototypeInfantryClass.Assault, PrototypeWeaponId.Pistol);
        private PrototypeLoadout loadout = new PrototypeLoadout();
        private bool secondaryEquipped;
        private GameObject heldEquipmentVisual;
        private GameObject firstPersonEquipment;
        private PrototypeThrowableTrajectory throwableTrajectory;
        private float nextEquipmentUseAt;
        private PrototypeAntiAirLock antiAirLock;
        private float antiAirZoom = 1.5f;
        private readonly List<GameObject> thrownProjectiles = new List<GameObject>();
        public PrototypeHeldEquipment HeldEquipment { get; private set; }
        public GameObject HeldEquipmentVisual => infantryThirdPerson ? heldEquipmentVisual : firstPersonEquipment;
        public bool ThrowableTrajectoryVisible => throwableTrajectory != null && throwableTrajectory.IsVisible;
        public int TrajectoryPointCount => throwableTrajectory != null ? throwableTrajectory.PointCount : 0;
        public PrototypeThrowableTrajectory ThrowableTrajectory => throwableTrajectory;
        public bool CanUseHeldEquipment => runtime != null && runtime.MatchStarted && Alive &&
            CurrentVehicle == null && !runtime.Match.Winner.HasValue && !runtime.PauseOpen &&
            !runtime.IsNetworkReplica && !runtime.SupportMenuOpen && !runtime.ScoreboardOpen && (runtime.Frontend == null || !runtime.Frontend.IsOpen);

        private int throwableCount = 2;
        private int rocketCount = 2;
        private int antiAirMissileCount = 2;
        private GameObject beaconMarker;
        private Vector2 beaconPosition;
        private bool beaconAvailable;
        private float supportStatusUntil;
        private bool crouched;
        private bool prone;
        private bool sprinting;
        private bool sprintInterrupted;
        private bool aiming;
        private float moveAmount;
        private float lastShotAt = -10f;
        private bool infantryThirdPerson = true;
        private bool vehicleFirstPerson = true;
        private bool parachuting;
        public bool IsParachuting => parachuting;

        public bool Alive => Health > 0f;
        public PrototypeTeam Team { get; private set; } = PrototypeTeam.Blue;
        public float Health { get; private set; } = 100f;
        public PrototypeInfantryKit ActiveKit => secondaryEquipped ? secondaryKit : kit;
        public PrototypeWeaponDefinition ActiveWeapon => ActiveKit.Weapon;
        public bool SecondaryEquipped => secondaryEquipped;
        public PrototypeLoadout Loadout => loadout;
        public int ThrowableCount => throwableCount;
        public int RocketCount => rocketCount;
        public int AntiAirMissileCount => antiAirMissileCount;
        public float AntiAirLockProgress => antiAirLock?.Progress ?? 0f;
        public IPrototypeVehicle AntiAirLockTarget => antiAirLock?.Target;
        public bool AntiAirLocked => antiAirLock != null && antiAirLock.Locked;
        public float AntiAirZoom => antiAirZoom;
        public float AntiAirAimFieldOfView => FieldOfViewForZoom(antiAirZoom);
        public float AntiAirLockCompletedAt => antiAirLock?.CompletedAt ?? -10f;
        public static float FieldOfViewForZoom(float zoom) => 2f * Mathf.Atan(
            Mathf.Tan(76f * Mathf.Deg2Rad * .5f) / Mathf.Max(1f, zoom)) * Mathf.Rad2Deg;
        public bool BeaconAvailable => beaconAvailable && loadout.role == PrototypeInfantryClass.Recon;
        public Vector2 BeaconPosition => beaconPosition;
        public int Ammo => ActiveKit.Ammo;
        public int Reserve => ActiveKit.Reserve;
        public bool Reloading => HeldEquipment == PrototypeHeldEquipment.Firearm && ActiveKit.Reloading;
        public bool IsAiming => aiming && HeldEquipment != PrototypeHeldEquipment.Throwable && !Reloading && CurrentVehicle == null;
        public bool IsSprinting => sprinting;
        public bool SprintRequested { get; private set; }
        public bool IsSliding => Slide.Active;
        public bool IsCrouched => crouched && !IsSliding;
        public bool IsProne => prone;
        public float CrosshairSpread => Mathf.Clamp(
            (IsAiming ? 2.5f : 7f) + moveAmount * (sprinting ? 7f : 4f) +
            (Time.time - lastShotAt < .18f ? 5f : 0f), 2.5f, 22f);
        public PrototypeInfantryClass Class => kit.Role;
        public PrototypeInfantryKit Kit => kit;
        public string SupportStatus { get; private set; } = "";
        public Vector2 MapPosition => CurrentVehicle != null ? CurrentVehicle.MapPosition :
            new Vector2(transform.position.x, transform.position.z);
        public Camera ViewCamera => viewCamera;
        public bool IsFirstPerson => !IsDowned && (CurrentVehicle != null ?
            vehicleFirstPerson : !infantryThirdPerson);
        public IPrototypeVehicle CurrentVehicle { get; private set; }
        public string CurrentSeatName => CurrentVehicle is IPrototypeSeatedVehicle seated ?
            seated.GetSeatName(seated.PlayerSeat) : "DRIVER";
        public string VehicleSeatStatus { get; private set; } = "";
        private float vehicleSeatStatusUntil;
        public float RespawnRemaining => Alive ? 0f : Mathf.Max(0f, respawnAt - Time.time);
        private bool NetworkReviveFreeze => runtime != null && runtime.IsNetworkReplica &&
            !Alive && downedUntil > 0f && networkRevivePaused && networkDownedRemaining > 0f;
        public bool IsDowned
        {
            get { SyncDownedTimer(); return !Alive && (NetworkReviveFreeze || downedUntil > Time.time); }
        }
        public float DownedRemaining
        {
            get
            {
                SyncDownedTimer();
                return Alive ? 0f : NetworkReviveFreeze ? networkDownedRemaining :
                    Mathf.Max(0f, downedUntil - Time.time);
            }
        }
        public bool IsBeingRevived => IsDowned && (runtime != null && runtime.IsNetworkReplica ?
            networkRevivePaused : HasFreshValidMedic());
        public bool RescueCalled => rescueCalled;
        public float DownedViewYaw => downedLookYaw;
        public float ReviveProgress => IsBeingRevived ?
            runtime != null && runtime.IsNetworkReplica ? networkReviveProgress :
            Mathf.Clamp01((Time.time - reviveStartedAt) / ReviveDuration) : 0f;

        public void Initialize(PrototypeRuntime game)
        {
            runtime = game;
            antiAirLock = new PrototypeAntiAirLock(game, this);
            controller = GetComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.34f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 48f;
            gameObject.layer = 2; // Ignore Raycast: the muzzle cannot hit the player collider.

            var cameraObject = new GameObject("Player Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(.65f, 1.65f, -3.4f);
            viewCamera = cameraObject.AddComponent<Camera>();
            viewCamera.fieldOfView = 76f;
            viewCamera.nearClipPlane = .06f;
            viewCamera.farClipPlane = 950f;
            viewCamera.backgroundColor = new Color(0.55f, 0.69f, 0.76f);
            cameraObject.AddComponent<AudioListener>();

            soldierVisual = gameObject.AddComponent<PrototypeSoldierVisual>();
            soldierVisual.Initialize(runtime, Team, Class, -.3f);
            BuildWeaponVisual();
            var trajectoryObject = new GameObject("Held grenade dotted trajectory");
            trajectoryObject.transform.SetParent(transform, false);
            throwableTrajectory = trajectoryObject.AddComponent<PrototypeThrowableTrajectory>();
            throwableTrajectory.Initialize(transform, runtime.TracerMaterial(Team));
            Respawn();
        }

        private void BuildWeaponVisual()
        {
            if (soldierVisual == null) return;
            soldierVisual.SetWeapon(ActiveWeapon.Id, runtime);
            rifle = soldierVisual.HeldWeapon;
            rifleArt = rifle.GetComponent<PrototypeWeaponArtRig>();
            magazineVisual = rifleArt != null ? rifleArt.Magazine : rifle.transform.Find("Magazine");
            if (magazineVisual != null) magazineHome = magazineVisual.localPosition;
            if (firstPersonWeapon != null)
            {
                firstPersonWeapon.SetActive(false);
                Destroy(firstPersonWeapon);
            }
            firstPersonWeapon = PrototypeWeaponVisual.Build(viewCamera.transform,
                ActiveWeapon.Id, runtime, true, Team);
            firstPersonWeapon.transform.localPosition = new Vector3(.30f, -.29f, .74f);
            firstPersonWeapon.transform.localScale = Vector3.one * .55f;
            firstPersonArt = firstPersonWeapon.GetComponent<PrototypeWeaponArtRig>();
            firstPersonMagazine = firstPersonArt != null ? firstPersonArt.Magazine : firstPersonWeapon.transform.Find("Magazine");
            if (firstPersonMagazine != null)
                firstPersonMagazineHome = firstPersonMagazine.localPosition;
            ApplyPerspectiveVisibility();
        }

        private void ApplyPerspectiveVisibility()
        {
            bool onFoot = CurrentVehicle == null;
            bool motorcycleRider = Alive &&
                CurrentVehicle is PrototypeCombinedArmsVehicle motorcycle &&
                motorcycle.Kind == PrototypeVehicleKind.Motorcycle;
            bool showBody = onFoot && (IsDowned || Alive && infantryThirdPerson) ||
                motorcycleRider;
            if (soldierVisual != null)
            {
                soldierVisual.SetMotorcycleFirstPerson(motorcycleRider && vehicleFirstPerson);
                soldierVisual.SetVisible(showBody);
            }
            bool gameplay = runtime != null && runtime.MatchStarted && !runtime.PauseOpen && !runtime.SupportMenuOpen && !runtime.ScoreboardOpen &&
                (runtime.Frontend == null || !runtime.Frontend.IsOpen);
            bool firearms = HeldEquipment == PrototypeHeldEquipment.Firearm;
            if (rifle != null) rifle.SetActive(onFoot && showBody && Alive && firearms);
            if (firstPersonWeapon != null)
                firstPersonWeapon.SetActive(onFoot && Alive && !infantryThirdPerson && firearms);
            if (heldEquipmentVisual != null)
                heldEquipmentVisual.SetActive(gameplay && onFoot && showBody && Alive && !firearms);
            if (firstPersonEquipment != null)
                firstPersonEquipment.SetActive(gameplay && onFoot && Alive && !infantryThirdPerson && !firearms);
            if (throwableTrajectory != null && (!gameplay || !onFoot || !Alive ||
                HeldEquipment != PrototypeHeldEquipment.Throwable)) throwableTrajectory.SetVisible(false);
        }

        public void TogglePerspective()
        {
            if (!Alive) return;
            if (CurrentVehicle != null)
            {
                vehicleFirstPerson = !vehicleFirstPerson;
                ApplyPerspectiveVisibility();
                UpdateVehicleCamera();
            }
            else
            {
                infantryThirdPerson = !infantryThirdPerson;
                ApplyPerspectiveVisibility();
                PositionInfantryCamera(1f);
            }
        }

        public void SelectClass(PrototypeInfantryClass selectedClass)
        {
            var selection = loadout.Copy();
            selection.role = selectedClass;
            ApplyLoadout(selection);
        }

        public void ApplyLoadout(PrototypeLoadout selection)
        {
            ClearHeldEquipment();
            loadout = (selection ?? new PrototypeLoadout()).Copy();
            loadout.Validate();
            kit = new PrototypeInfantryKit(loadout.role, loadout.primary);
            secondaryKit = new PrototypeInfantryKit(loadout.role, loadout.secondary);
            secondaryEquipped = false;
            throwableCount = 2;
            rocketCount = 2;
            antiAirMissileCount = 2;
            ClearBeacon();
            if (soldierVisual != null)
            {
                soldierVisual.Initialize(runtime, Team, Class, -.3f);
                BuildWeaponVisual();
            }
        }

        private void EquipSecondary(bool secondary)
        {
            ClearHeldEquipment();
            if (secondaryEquipped == secondary) return;
            secondaryEquipped = secondary;
            BuildWeaponVisual();
        }

        public void SetTeam(PrototypeTeam team)
        {
            if (Team == team) return;
            ClearHeldEquipment();
            Team = team;
            if (soldierVisual != null)
            {
                soldierVisual.Initialize(runtime, Team, Class, -.3f);
                BuildWeaponVisual();
            }
            if (runtime != null && CurrentVehicle == null) Respawn();
        }

        // Guest snapshots are cosmetic. Health, position and respawn are decided
        // by the host; local input may still move the camera between snapshots.
        public void ApplyNetworkState(Vector3 position, float yaw, float health, bool alive,
            int ammo, int reserve, float respawnRemaining, float downedRemaining,
            bool rescueRequested, bool revivePaused = false, float reviveProgress = 0f)
        {
            if (runtime == null || !runtime.IsNetworkReplica ||
                float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) ||
                float.IsInfinity(position.z) || float.IsNaN(yaw) || float.IsInfinity(yaw) ||
                float.IsNaN(health) || float.IsInfinity(health)) return;
            if (CurrentVehicle != null) ForceExitVehicle();
            if (controller != null) controller.enabled = false;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            bool wasDowned = IsDowned;
            ResetReviveTracking();
            Health = alive ? Mathf.Clamp(health, 0.01f, 100f) : 0f;
            downedUntil = !alive && downedRemaining > 0f ?
                Time.time + Mathf.Clamp(downedRemaining, 0f, 30f) : 0f;
            rescueCalled = !alive && rescueRequested;
            respawnAt = alive || downedUntil > 0f ? 0f :
                Time.time + Mathf.Max(0f, respawnRemaining);
            kit.ApplyNetworkAmmo(ammo, reserve);
            if (controller != null) controller.enabled = alive;
            if (IsDowned && !wasDowned) EnterDownedPose();
            else if (alive && wasDowned) LeaveDownedPose();
            else ApplyPerspectiveVisibility();
            // Enter/leave poses reset local bookkeeping first; the latest host
            // snapshot is then applied so a guest never extrapolates a paused
            // bleedout timer between packets. Old snapshots default to running.
            networkRevivePaused = !alive && downedRemaining > 0f && revivePaused;
            networkDownedRemaining = !alive ? Mathf.Clamp(downedRemaining, 0f, 30f) : 0f;
            networkReviveProgress = networkRevivePaused && !float.IsNaN(reviveProgress) &&
                !float.IsInfinity(reviveProgress) ? Mathf.Clamp01(reviveProgress) : 0f;
        }

        private void Update()
        {
            if (runtime == null) return;
            if (!runtime.MatchStarted || !Alive || CurrentVehicle != null || runtime.Match.Winner.HasValue)
                ClearHeldEquipment();
            if (!CanUseHeldEquipment || Cursor.lockState != CursorLockMode.Locked) ResetAntiAirLock();
            ApplyPerspectiveVisibility();
            if (!runtime.MatchStarted) { ClearThrownProjectiles(); return; }
            if (runtime.PauseOpen || runtime.SupportMenuOpen) return;
            // This must precede cursor and match-state gates: a destroyed vehicle
            // must never leave the infantry controller disabled.
            if (CurrentVehicle != null && (!CurrentVehicle.Alive ||
                CurrentVehicle.Occupant != this || !Alive || runtime.Match.Winner.HasValue))
                ForceExitVehicle();
            if (!Alive)
            {
                Slide.Reset();
                if (!runtime.ScoreboardOpen && IsDowned && Input.GetMouseButton(1))
                    AdjustDownedView(Input.GetAxis("Mouse X") * runtime.MouseSensitivity,
                        -Input.GetAxis("Mouse Y") * runtime.MouseSensitivity);
                if (!runtime.IsNetworkReplica && downedUntil > 0f)
                {
                    if (!runtime.ScoreboardOpen && Input.GetKeyDown(KeyCode.H)) RequestRescue();
                    if (!runtime.ScoreboardOpen && Input.GetKey(KeyCode.Space)) GiveUp(Time.deltaTime);
                    SyncDownedTimer();
                    if (downedUntil > 0f && Time.time >= downedUntil) Eliminate();
                }
                return;
            }
            // Standings block manual actions, not bleedout or destroyed-seat cleanup.
            if (runtime.ScoreboardOpen)
            {
                aiming = false; sprinting = false; moveAmount = 0f;
                // Reading the board must not suspend a jumper/parachutist in
                // midair. Host snapshots still own a network guest's position.
                if (!runtime.IsNetworkReplica && CurrentVehicle == null && controller.enabled)
                {
                    if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
                    StepInfantryGravity(Time.deltaTime);
                    controller.Move(Vector3.up * verticalSpeed * Time.deltaTime);
                }
                return;
            }
            if (runtime.Match.Winner.HasValue) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }
            kit.Tick(Time.time);
            secondaryKit.Tick(Time.time);
            if (Time.time > supportStatusUntil) SupportStatus = "";
            if (Input.GetKeyDown(KeyCode.V)) TogglePerspective();

            if (CurrentVehicle != null)
            {
                aiming = false;
                sprinting = false;
                if (Input.GetKeyDown(KeyCode.E)) { ForceExitVehicle(); return; }
                if (Time.time > vehicleSeatStatusUntil) VehicleSeatStatus = "";
                if (Input.GetKeyDown(KeyCode.F1)) TrySwitchVehicleSeat(0);
                if (Input.GetKeyDown(KeyCode.F2)) TrySwitchVehicleSeat(1);
                if (Input.GetKeyDown(KeyCode.F3)) TrySwitchVehicleSeat(2);
                if (Input.GetKeyDown(KeyCode.F4)) TrySwitchVehicleSeat(3);
                ApplyVehicleLook(Input.GetAxis("Mouse X") * runtime.MouseSensitivity,
                    -Input.GetAxis("Mouse Y") * runtime.MouseSensitivity);
                UpdateVehicleCamera();
                return;
            }

            transform.Rotate(0f, Input.GetAxis("Mouse X") * runtime.MouseSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * runtime.MouseSensitivity, -86f, 86f);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);
            moveAmount = input.magnitude;
            Vector3 horizontal = ResolveInfantryMotion(input, Input.GetKey(KeyCode.LeftShift),
                Input.GetMouseButton(0), Input.GetMouseButton(1), Input.GetKeyDown(KeyCode.C),
                Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl),
                Input.GetKey(KeyCode.LeftAlt), Time.deltaTime);
            float targetHeight = prone ? .75f : IsSliding ? Mathf.Lerp(1.15f, Slide.StandExit ? 1.8f : 1.15f, Slide.Recovery) : crouched ? 1.15f : 1.8f;
            controller.height = targetHeight;
            controller.center = new Vector3(0f, targetHeight * .5f, 0f);
            controller.stepOffset = prone ? .12f : .35f;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (!runtime.IsNetworkReplica && controller.isGrounded &&
                !prone && !crouched && !IsSliding &&
                Input.GetKeyDown(KeyCode.Space)) verticalSpeed = 6.2f;
            StepInfantryGravity(Time.deltaTime);
            Vector3 beforeMove = transform.position;
            CollisionFlags moveFlags = controller.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            ResolveSlideCollision(moveFlags, beforeMove, horizontal, Time.deltaTime);

            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.E))
            {
                string supplyReason = "";
                if (runtime.Supplies != null && runtime.Supplies.TryUseNearest(this, out supplyReason))
                    ShowSupplyStatus(supplyReason);
                else TryEnterVehicle();
            }
            if (CurrentVehicle != null) return;
            if (!runtime.IsNetworkReplica && HeldEquipment == PrototypeHeldEquipment.Firearm &&
                Input.GetKeyDown(KeyCode.R)) ActiveKit.StartReload(Time.time);
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.Alpha1))
                EquipSecondary(false);
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.Alpha2))
                EquipSecondary(true);
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.X)) UseSupport();
            if (Input.GetKeyDown(KeyCode.Alpha3)) TryEquipThrowable();
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Z)) TryEquipLauncher();
            if (Input.GetKeyDown(KeyCode.Alpha5)) TryEquipAntiAirLauncher();
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.Alpha6) && runtime.Supplies != null)
            {
                runtime.Supplies.TryDeployCrate(this, out string supplyReason);
                ShowSupplyStatus(supplyReason);
            }
            if (Input.GetKeyDown(KeyCode.T)) TryToggleAntiAirZoom();
            UpdateAntiAirLock(IsAiming, Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.G) && TryEquipThrowable()) TryUseHeldEquipment();
            if (!runtime.IsNetworkReplica && HeldEquipment != PrototypeHeldEquipment.Firearm &&
                (Input.GetMouseButtonDown(0) || HeldEquipment == PrototypeHeldEquipment.AntiAirLauncher &&
                    Input.GetKeyDown(KeyCode.P))) TryUseHeldEquipment();
            else if (!runtime.IsNetworkReplica && HeldEquipment == PrototypeHeldEquipment.Firearm &&
                (ActiveWeapon.Automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0))) Fire();
        }

        private bool ResolveSprintInput(Vector2 input, bool sprintHeld, bool fireHeld, bool aimHeld)
        {
            if (!sprintHeld) sprintInterrupted = false;
            else if (fireHeld || aimHeld) InterruptSprint();
            SprintRequested = sprintHeld && !sprintInterrupted && input.y > .1f &&
                !prone && !crouched && !Reloading && !IsSliding;
            return SprintRequested && controller.isGrounded;
        }

        private void InterruptSprint()
        {
            sprinting = false;
            SprintRequested = false;
            sprintInterrupted = true;
        }

        private float ResolveMovementSpeed(bool wantsSprint, bool aimHeld)
        {
            aiming = aimHeld && HeldEquipment != PrototypeHeldEquipment.Throwable && (!IsSliding || Slide.Recovering) && !Reloading;
            sprinting = wantsSprint && !sprintInterrupted && !aiming && !IsSliding && !prone && !crouched;
            return prone ? ProneSpeed : crouched ? CrouchSpeed :
                aiming ? WalkSpeed * .65f : sprinting ? SprintSpeed : WalkSpeed;
        }

        private void LateUpdate()
        {
            if (CurrentVehicle != null) UpdateVehicleCamera();
            else if (runtime != null && runtime.MatchStarted && Alive)
                UpdateInfantryPresentation();
            RefreshHeldPresentation();
        }

        private void StepInfantryGravity(float dt)
        {
            if (parachuting && controller.isGrounded && transform.position.y <
                PrototypeLayout.HeightAt(transform.position.x, transform.position.z) + 1f)
                parachuting = false;
            verticalSpeed -= 22f * dt;
            if (parachuting) verticalSpeed = Mathf.Max(verticalSpeed, -5f);
        }

        private bool CanStand()
        {
            Vector3 bottom = transform.position + Vector3.up * .36f;
            Vector3 top = transform.position + Vector3.up * 1.44f;
            return !Physics.CheckCapsule(bottom, top, .31f, ~(1 << 2),
                QueryTriggerInteraction.Ignore);
        }

        private void UpdateInfantryPresentation()
        {
            float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
            PositionInfantryCamera(blend);
            float targetFov = IsAiming ?
                (HeldEquipment == PrototypeHeldEquipment.AntiAirLauncher ? AntiAirAimFieldOfView :
                    HeldEquipment == PrototypeHeldEquipment.Launcher ? 42f : ActiveWeapon.Id == PrototypeWeaponId.Sniper ? 40f : 54f) :
                sprinting ? 82f : 76f;
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, blend);

            if (rifle == null) return;

            float reload = Reloading ? Mathf.Clamp01(1f -
                (ActiveKit.ReloadEndsAt - Time.time) / ActiveWeapon.ReloadSeconds) : 0f;
            float reloadDip = Reloading ? Mathf.Sin(reload * Mathf.PI) : 0f;
            float stride = moveAmount * (sprinting ? 1f : .45f);
            float gait = soldierVisual != null ? soldierVisual.GaitPhase : 0f;
            float sprintMotion = soldierVisual != null ? soldierVisual.SprintMotionWeight : 0f;
            float bob = sprinting ? Mathf.Sin(gait * 2f) * .010f * sprintMotion :
                Mathf.Sin(Time.time * 9f) * .013f * stride;
            Vector3 home = new Vector3(0f, bob - reloadDip * .16f, 0f);
            if (sprinting || IsSliding) home += new Vector3(0f, -.07f, -.04f);
            rifle.transform.localPosition = Vector3.Lerp(rifle.transform.localPosition, home, blend);
            Quaternion pose = Quaternion.Euler(reloadDip * 28f +
                pitch * .55f + (sprinting ? -11f : 0f), 0f, reloadDip * 14f +
                (IsSliding ? -11f : 0f));
            rifle.transform.localRotation = Quaternion.Slerp(rifle.transform.localRotation, pose, blend);
            if (magazineVisual != null && rifleArt == null)
            {
                float magazineDrop = Reloading ?
                    Mathf.Sin(Mathf.Clamp01((reload - .18f) / .62f) * Mathf.PI) * .24f : 0f;
                magazineVisual.localPosition = magazineHome + Vector3.down * magazineDrop;
                if (firstPersonMagazine != null)
                    firstPersonMagazine.localPosition = firstPersonMagazineHome +
                        Vector3.down * magazineDrop;
            }
            if (firstPersonWeapon != null)
            {
                Vector3 firstHome = IsAiming ? new Vector3(0f, -.121f, .53f) :
                    new Vector3(.30f, -.29f, .74f);
                if(firstPersonArt!=null)firstHome=IsAiming?firstPersonArt.AimPosition(.55f):firstPersonArt.HipPosition;
                if (sprinting || IsSliding)
                    firstHome += new Vector3(.06f, -.12f, -.07f);
                firstHome += new Vector3(0f, bob - reloadDip * .18f, 0f);
                if(firstPersonArt!=null && Reloading)
                    firstHome=Vector3.Lerp(firstPersonArt.HipPosition,new Vector3(.13f,-.06f,.63f),reloadDip);
                Quaternion firstPose = Quaternion.Euler(reloadDip * 28f +
                    (sprinting ? -11f : 0f), 0f, reloadDip * 14f +
                    (IsSliding ? -11f : 0f));
                if(firstPersonArt!=null && Reloading)
                    firstPose=Quaternion.Euler(-10f*reloadDip,-22f*reloadDip,-22f*reloadDip);
                if(firstPersonArt!=null && !Reloading)
                {
                    float age=Time.time-lastShotAt;
                    float kick=age>=0&&age<.18f?Mathf.Sin(Mathf.Clamp01(age/.045f)*Mathf.PI*.5f)*Mathf.Exp(-age*22f):0f;
                    firstPose*=Quaternion.Euler(-4f*kick,0,0);
                }
                if (sprinting && !Reloading && !IsSliding)
                {
                    // Reference: the gun swings diagonally toward the lower
                    // left, leaving the centre clear instead of aiming while running.
                    float sway = Mathf.Sin(gait - .12f) * sprintMotion;
                    firstHome = new Vector3(-.20f + sway * .018f, -.36f + bob, .60f);
                    firstPose = Quaternion.Euler(-24f + sway * 2f, -48f, 12f + sway * 1.5f);
                }
                firstPersonWeapon.transform.localPosition = Vector3.Lerp(
                    firstPersonWeapon.transform.localPosition, firstHome, blend);
                firstPersonWeapon.transform.localRotation = Quaternion.Slerp(
                    firstPersonWeapon.transform.localRotation, firstPose, blend);
            }
            if(rifleArt!=null)rifleArt.SetPresentation(Reloading,reload,Time.time-lastShotAt);
            if(firstPersonArt!=null)firstPersonArt.SetPresentation(Reloading,reload,Time.time-lastShotAt);
            if(firstPersonArt!=null)firstPersonArt.SetAiming(IsAiming && !Reloading && !infantryThirdPerson);
        }

        private void PositionInfantryCamera(float blend)
        {
            float eyeHeight = prone ? .65f : crouched ? 1.05f : 1.65f;
            if (IsSliding)
            {
                float enter = Mathf.SmoothStep(0f, 1f, Slide.Elapsed / PrototypeSlideState.EnterDuration);
                eyeHeight = Mathf.Lerp(1.65f, .88f, enter);
                if (Slide.Recovering) eyeHeight = Mathf.Lerp(eyeHeight, Slide.StandExit ? 1.65f : 1.05f,
                    Mathf.SmoothStep(0f, 1f, Slide.Recovery));
            }
            viewCamera.transform.rotation = transform.rotation * Quaternion.Euler(pitch, 0f, 0f);
            Vector3 focus = transform.position + Vector3.up * eyeHeight;
            if (!infantryThirdPerson)
            {
                float phase = soldierVisual != null ? soldierVisual.GaitPhase : 0f;
                float weight = !IsSliding && soldierVisual != null ? soldierVisual.SprintMotionWeight : 0f;
                // Tiny, stride-driven head movement: two landings per cycle,
                // one side-to-side weight shift. Never add onto last frame's rotation.
                Vector3 offset = new Vector3(Mathf.Sin(phase) * .012f,
                    -Mathf.Cos(phase * 2f) * .014f, 0f) * weight;
                if (IsSliding)
                {
                    offset = Vector3.zero;
                    viewCamera.transform.rotation *= Quaternion.Euler(0f, 0f, -1.1f * Slide.Weight);
                }
                focus += transform.TransformDirection(offset);
                viewCamera.transform.rotation *= Quaternion.Euler(
                    Mathf.Sin(phase * 2f) * .20f * weight,
                    Mathf.Sin(phase) * .10f * weight,
                    -Mathf.Sin(phase) * .30f * weight);
                viewCamera.transform.position = Vector3.Lerp(
                    viewCamera.transform.position, focus, blend);
                return;
            }
            float shoulder = IsAiming ? .38f : .65f;
            float distance = IsAiming ? 1.65f : 3.4f;
            Vector3 desired = focus + transform.right * shoulder -
                viewCamera.transform.forward * distance;
            Vector3 travel = desired - focus;
            float nearest = travel.magnitude;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.SphereCastAll(focus, .18f,
                travel.normalized, nearest, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            if (nearest < travel.magnitude)
                desired = focus + travel.normalized * Mathf.Max(.20f, nearest - .08f);
            viewCamera.transform.position = Vector3.Lerp(
                viewCamera.transform.position, desired, blend);
        }

        private void TryEnterVehicle()
        {
            if (runtime == null || runtime.IsNetworkReplica || !Alive ||
                runtime.Match.Winner.HasValue) return;
            IPrototypeVehicle closest = null;
            float nearest = 5.5f;
            foreach (IPrototypeVehicle candidate in runtime.Vehicles)
            {
                if (candidate == null || !candidate.Alive || candidate.Team != Team ||
                    candidate.Occupant != null) continue;
                // Infantry interaction is measured on the map for ground hulls.
                // An aircraft overhead must not turn E into an airborne teleport.
                if (candidate is IPrototypeAircraftVehicle aircraft && aircraft.IsAircraft &&
                    aircraft.Altitude > 3f) continue;
                float distance = Vector2.Distance(MapPosition, candidate.MapPosition);
                if (distance >= nearest ||
                    PrototypeLayout.LineBlocked(MapPosition, candidate.MapPosition, 0.1f)) continue;
                closest = candidate;
                nearest = distance;
            }
            if (closest == null) return;
            EnterVehicle(closest);
        }

        public bool TrySwitchVehicleSeat(int seat)
        {
            if (!(CurrentVehicle is IPrototypeSeatedVehicle seated)) return false;
            int previousSeat = seated.PlayerSeat;
            bool accepted = seated.TrySetPlayerSeat(this, seat);
            VehicleSeatStatus = accepted ? "F" + (seat + 1) + " " + seated.GetSeatName(seat) :
                seat < 0 || seat >= seated.SeatCount ? "SEAT UNAVAILABLE" : "SEAT OCCUPIED";
            vehicleSeatStatusUntil = Time.time + 2.5f;
            if (accepted)
            {
                if (previousSeat != seat)
                {
                    pitch = 0f;
                    vehicleLookYaw = 0f;
                    vehicleWorldLookYaw = CurrentVehicle.VehicleTransform.eulerAngles.y;
                    motorcycleCameraValid = false;
                }
                UpdateVehicleCamera();
                if (soldierVisual != null) soldierVisual.RefreshPose(true);
            }
            return accepted;
        }

        private bool EnterVehicle(IPrototypeVehicle closest) => EnterVehicleSeat(closest, 0);

        private bool EnterVehicleSeat(IPrototypeVehicle closest, int seat)
        {
            if (closest is IPrototypeSeatedVehicle seated)
            {
                if (!seated.TrySetPlayerSeat(this, seat)) return false;
            }
            else if (seat != 0 || !closest.TrySetDriver(this)) return false;
            ClearHeldEquipment();
            CurrentVehicle = closest;
            parachuting = false;
            VehicleSeatStatus = "";
            controller.enabled = false;
            transform.position = closest.SeatPosition;
            transform.rotation = closest.VehicleTransform.rotation;
            verticalSpeed = 0f;
            pitch = closest is PrototypeCombinedArmsVehicle bike && bike.Kind == PrototypeVehicleKind.Motorcycle ? 15f : 0f;
            vehicleLookYaw = 0f;
            vehicleWorldLookYaw = closest.VehicleTransform.eulerAngles.y;
            motorcycleCameraValid = false;
            ApplyPerspectiveVisibility();
            aiming = false;
            sprinting = false;
            Slide.Reset();
            UpdateVehicleCamera();
            return true;
        }

        private bool UsesWorldVehicleHeading => CurrentVehicle is PrototypeTankVehicle tank && tank.PlayerSeat == 0 ||
            CurrentVehicle is IPrototypeAircraftVehicle aircraft && aircraft.IsAircraft &&
            CurrentVehicle is IPrototypeSeatedVehicle seated && seated.PlayerSeat == 0 &&
            !(CurrentVehicle is PrototypeCombinedArmsVehicle combined && combined.IsDownwardMachineGun);

        // Mouse look and pilot steering share a stable world heading. Updating
        // the hull must not move its own camera target farther away each frame.
        private void ApplyVehicleLook(float yawDelta, float pitchDelta)
        {
            if (UsesWorldVehicleHeading)
                vehicleWorldLookYaw = Mathf.Repeat(vehicleWorldLookYaw + yawDelta, 360f);
            else vehicleLookYaw = Mathf.Clamp(vehicleLookYaw + yawDelta, -135f, 135f);
            float minimumPitch = -35f;
            float maximumPitch = 55f;
            if (CurrentVehicle is PrototypeTankVehicle tank && tank.PlayerSeat == 0)
            { minimumPitch = -25f; maximumPitch = 10f; }
            if (CurrentVehicle is PrototypeCombinedArmsVehicle combined)
            {
                if (combined.Kind == PrototypeVehicleKind.AntiAir && combined.PlayerSeat == 0)
                { minimumPitch = -77f; maximumPitch = 7f; }
                else if (combined.Kind == PrototypeVehicleKind.AntiAir && combined.PlayerSeat == 1)
                { minimumPitch = -32f; maximumPitch = 11f; }
                else if (combined.Kind == PrototypeVehicleKind.Jet && combined.PlayerSeat == 0)
                { minimumPitch = -49f; maximumPitch = 49f; }
                else if (combined.IsDownwardMachineGun)
                { minimumPitch = 15f; maximumPitch = 85f; }
            }
            pitch = Mathf.Clamp(pitch + pitchDelta, minimumPitch, maximumPitch);
            if (CurrentVehicle is PrototypeCombinedArmsVehicle gunPlatform)
                gunPlatform.ClampWeaponView(ref vehicleLookYaw, ref pitch);
        }

        private void UpdateVehicleCamera()
        {
            if (CurrentVehicle == null) return;
            bool worldHeading = UsesWorldVehicleHeading;
            if (worldHeading != vehicleWorldHeadingLast)
            {
                if (worldHeading) vehicleWorldLookYaw = CurrentVehicle.VehicleTransform.eulerAngles.y + vehicleLookYaw;
                else vehicleLookYaw = Mathf.DeltaAngle(CurrentVehicle.VehicleTransform.eulerAngles.y, vehicleWorldLookYaw);
                vehicleWorldHeadingLast = worldHeading;
            }
            if (CurrentVehicle is PrototypeCombinedArmsVehicle gunPlatform)
                gunPlatform.ClampWeaponView(ref vehicleLookYaw, ref pitch);
            transform.position = CurrentVehicle.SeatPosition;
            transform.rotation = CurrentVehicle is PrototypeCombinedArmsVehicle bike && bike.Kind == PrototypeVehicleKind.Motorcycle ?
                bike.MotorcyclePoseRotation : CurrentVehicle.VehicleTransform.rotation;
            float heading = UsesWorldVehicleHeading ? vehicleWorldLookYaw :
                CurrentVehicle.VehicleTransform.eulerAngles.y + vehicleLookYaw;
            bool motorcycle = CurrentVehicle is PrototypeCombinedArmsVehicle riderBike &&
                riderBike.Kind == PrototypeVehicleKind.Motorcycle;
            if (motorcycle)
            {
                float hullYaw = CurrentVehicle.VehicleTransform.eulerAngles.y;
                if (!motorcycleCameraValid)
                { motorcycleCameraHullYaw = hullYaw; motorcycleCameraValid = true; motorcycleCameraFrame = Time.frameCount; }
                else if (motorcycleCameraFrame != Time.frameCount)
                {
                    motorcycleCameraFrame = Time.frameCount;
                    motorcycleCameraHullYaw = Mathf.LerpAngle(motorcycleCameraHullYaw, hullYaw,
                        1f - Mathf.Exp(-Mathf.Min(Time.deltaTime, .05f) * 7f));
                }
                // Only chassis turn is softened. Mouse free-look stays direct,
                // and a small lag cap preserves the visible hands and grips.
                motorcycleCameraHullYaw = hullYaw + Mathf.Clamp(Mathf.DeltaAngle(hullYaw, motorcycleCameraHullYaw), -6f, 6f);
                heading = motorcycleCameraHullYaw + vehicleLookYaw;
            }
            else motorcycleCameraValid = false;
            Quaternion look = Quaternion.Euler(pitch, heading, 0f);
            viewCamera.transform.rotation = look;
            viewCamera.fieldOfView = CurrentVehicle is PrototypeTankVehicle ?
                (vehicleFirstPerson ? 56f : 72f) :
                (vehicleFirstPerson ? 72f : 78f);
            if (motorcycle && vehicleFirstPerson)
            {
                // Preserve enough horizontal view for both real grips when a
                // narrow window would otherwise crop the driver's hands.
                float gripView = 2f * Mathf.Atan(Mathf.Tan(37.5f * Mathf.Deg2Rad) /
                    Mathf.Max(.55f, viewCamera.aspect)) * Mathf.Rad2Deg;
                viewCamera.fieldOfView = Mathf.Max(72f, gripView);
            }
            if (vehicleFirstPerson)
            {
                viewCamera.transform.position = CurrentVehicle.CameraPosition;
                return;
            }
            Vector3 focus = CurrentVehicle.SeatPosition + Vector3.up * (motorcycle ? .55f : 1.65f);
            bool aircraft = CurrentVehicle is IPrototypeAircraftVehicle air && air.IsAircraft;
            bool fullSizeH8 = CurrentVehicle is PrototypeCombinedArmsVehicle combined &&
                combined.Kind == PrototypeVehicleKind.Helicopter && combined.HasAuthoredArt;
            float chaseDistance = fullSizeH8 ? 25f : aircraft ? 14f : motorcycle ? 4.2f : 5.4f;
            float chaseHeight = fullSizeH8 ? 7f : aircraft ? 4.5f : motorcycle ? .85f : 1.15f;
            Vector3 desired = focus + Vector3.up * chaseHeight -
                look * Vector3.forward * chaseDistance;
            Vector3 travel = desired - focus;
            float nearest = travel.magnitude;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.SphereCastAll(focus, .24f,
                travel.normalized, nearest, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(CurrentVehicle.VehicleTransform) ||
                    hit.collider.transform.IsChildOf(transform)) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            viewCamera.transform.position = nearest < travel.magnitude ?
                focus + travel.normalized * Mathf.Max(.3f, nearest - .1f) : desired;
        }

        public void ForceExitVehicle()
        {
            IPrototypeVehicle vehicle = CurrentVehicle;
            if (vehicle == null) return;
            bool airborneExit = Alive && vehicle is IPrototypeAircraftVehicle aircraft &&
                aircraft.IsAircraft && aircraft.Altitude > 2f;
            Vector3 exit = FindSafeExit(vehicle);
            vehicle.RemoveDriver(this);
            CurrentVehicle = null;
            parachuting = airborneExit;
            VehicleSeatStatus = "";
            controller.enabled = false;
            transform.position = exit;
            transform.rotation = Quaternion.Euler(0f, vehicle.VehicleTransform.eulerAngles.y, 0f);
            pitch = 0f;
            vehicleLookYaw = 0f;
            motorcycleCameraValid = false;
            verticalSpeed = 0f;
            viewCamera.transform.localPosition = new Vector3(.65f, 1.65f, -3.4f);
            viewCamera.transform.localRotation = Quaternion.identity;
            viewCamera.fieldOfView = 76f;
            ApplyPerspectiveVisibility();
            crouched = false;
            prone = false;
            aiming = false;
            sprinting = false;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, .9f, 0f);
            controller.enabled = true;
            PositionInfantryCamera(1f);
            if (soldierVisual != null) soldierVisual.RefreshPose(true);
        }

        private Vector3 FindSafeExit(IPrototypeVehicle vehicle)
        {
            Physics.SyncTransforms();
            Vector3 proposed = vehicle.ExitPosition;
            if (vehicle is IPrototypeAircraftVehicle aircraft && aircraft.IsAircraft &&
                aircraft.Altitude > 2f)
            {
                // Preserve the aircraft's height. Search outward above terrain;
                // the automatic parachute handles descent after the seat release.
                foreach (float radius in new[] { 6f, 9f, 13f })
                    foreach (Vector3 direction in new[] { vehicle.VehicleTransform.right,
                        -vehicle.VehicleTransform.right, -vehicle.VehicleTransform.forward })
                    {
                        Vector3 point = vehicle.VehicleTransform.position + direction * radius;
                        point.y = Mathf.Max(vehicle.VehicleTransform.position.y,
                            PrototypeLayout.HeightAt(point.x, point.z) + 3f);
                        if (!Physics.CheckCapsule(point + Vector3.up * .34f,
                            point + Vector3.up * 1.46f, .34f, ~(1 << 2),
                            QueryTriggerInteraction.Ignore)) return point;
                    }
                return proposed;
            }
            if (ExitIsClear(proposed)) return proposed;
            Vector3 forward = vehicle.VehicleTransform.forward;
            Vector3 right = vehicle.VehicleTransform.right;
            Vector3[] directions = { right, -right, -forward, forward,
                (right - forward).normalized, (-right - forward).normalized };
            foreach (float radius in new[] { 3.5f, 5f, 6.5f })
                foreach (Vector3 direction in directions)
                {
                    Vector3 candidate = vehicle.VehicleTransform.position + direction * radius;
                    candidate.y = PrototypeLayout.HeightAt(candidate.x, candidate.z) + 0.3f;
                    if (ExitIsClear(candidate)) return candidate;
                }
            // The infantry spawn is a known clear point if the vehicle is boxed in.
            Vector2 spawn = Team == PrototypeTeam.Blue ?
                PrototypeLayout.BlueBase : PrototypeLayout.RedBase;
            return new Vector3(spawn.x,
                PrototypeLayout.HeightAt(spawn.x, spawn.y) + 0.3f, spawn.y);
        }

        private bool ExitIsClear(Vector3 point)
        {
            const float radius = 0.34f;
            if (PrototypeLayout.Collides(new Vector2(point.x, point.z), radius)) return false;
            Vector3 bottom = point + Vector3.up * radius;
            Vector3 top = point + Vector3.up * (1.8f - radius);
            return !Physics.CheckCapsule(bottom, top, radius * 0.94f,
                ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        private void Fire()
        {
            if (HeldEquipment != PrototypeHeldEquipment.Firearm) return;
            InterruptSprint();
            PrototypeInfantryKit weaponKit = ActiveKit;
            if (weaponKit.Ammo == 0) { weaponKit.StartReload(Time.time); return; }
            bool aimedShot = IsAiming;
            if (!weaponKit.TryFire(Time.time)) return;
            lastShotAt = Time.time;
            float spread = aimedShot ? weaponKit.Weapon.AdsSpread : weaponKit.Weapon.HipSpread;
            Vector3 sightDirection = (viewCamera.transform.forward +
                viewCamera.transform.right * Random.Range(-spread, spread) +
                viewCamera.transform.up * Random.Range(-spread, spread)).normalized;
            Vector3 target = InfantryAimPoint(sightDirection, weaponKit.Weapon.MaxRange);
            GameObject weaponVisual = infantryThirdPerson ? rifle : firstPersonWeapon;
            Vector3 origin = weaponVisual.transform.TransformPoint(
                new Vector3(0f, .025f, .95f));
            var firingArt=infantryThirdPerson?rifleArt:firstPersonArt;
            if(firingArt!=null)origin=firingArt.Muzzle.position;
            Vector3 direction = (target - origin).normalized;
            PrototypeBallisticProjectile.LaunchBullet(runtime, Team, transform,
                origin, direction, weaponKit.Weapon);
            if (weaponKit.Ammo == 0) weaponKit.StartReload(Time.time);
        }

        private Vector3 InfantryAimPoint(Vector3 direction, float range)
        {
            Vector3 origin = viewCamera.transform.position;
            Vector3 target = origin + direction * range;
            float nearest = range;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, range,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                target = hit.point;
            }
            return target;
        }

        public bool TryEquipThrowable()
        {
            if (!CanUseHeldEquipment) return EquipmentUnavailable();
            if (throwableCount <= 0) return false;
            return EquipHeld(PrototypeHeldEquipment.Throwable);
        }

        public bool TryEquipLauncher()
        {
            if (!CanUseHeldEquipment) return EquipmentUnavailable();
            if (Class != PrototypeInfantryClass.Engineer || rocketCount <= 0) return false;
            return EquipHeld(PrototypeHeldEquipment.Launcher);
        }

        public bool TryEquipAntiAirLauncher()
        {
            if (!CanUseHeldEquipment) return EquipmentUnavailable();
            if (Class != PrototypeInfantryClass.Engineer || antiAirMissileCount <= 0) return false;
            return EquipHeld(PrototypeHeldEquipment.AntiAirLauncher);
        }

        public void UpdateAntiAirLock(bool aimHeld, float dt)
        {
            if (antiAirLock == null) antiAirLock = new PrototypeAntiAirLock(runtime, this);
            antiAirLock.Tick(aimHeld, dt);
        }

        public void ResetAntiAirLock(bool notifyLost = true) => antiAirLock?.Reset(notifyLost);

        public bool TryToggleAntiAirZoom()
        {
            if (!CanUseHeldEquipment || HeldEquipment != PrototypeHeldEquipment.AntiAirLauncher ||
                !IsAiming || IsSliding) return false;
            antiAirZoom = antiAirZoom < 2f ? 3f : 1.5f;
            PrototypePlayerAudio.PlayCue(PrototypePlayerCue.Zoom);
            return true;
        }

        // Supply is explicit and capped: it cannot award ammo on a failed call.
        public void ResupplyExplosives()
        {
            if (!Alive || runtime == null || runtime.IsNetworkReplica) return;
            throwableCount = 2; rocketCount = 2; antiAirMissileCount = 2;
        }

        public bool ResupplyAllFromCrate()
        {
            if (!CanUseHeldEquipment) return false;
            bool changed = kit.RefillFromAmmoCrate();
            changed = secondaryKit.RefillFromAmmoCrate() || changed;
            changed = throwableCount < 2 || rocketCount < 2 || antiAirMissileCount < 2 || changed;
            if (!changed) return false;
            throwableCount = 2; rocketCount = 2; antiAirMissileCount = 2;
            return true;
        }

        private void ShowSupplyStatus(string message)
        { SupportStatus = message; supportStatusUntil = Time.time + 3f; }

        private bool EquipmentUnavailable()
        {
            if (runtime != null && runtime.IsNetworkReplica)
            {
                SupportStatus = "EQUIPMENT AVAILABLE IN SOLO / HOST";
                supportStatusUntil = Time.time + 2f;
            }
            return false;
        }

        private bool EquipHeld(PrototypeHeldEquipment kind)
        {
            if (HeldEquipment == kind) return true;
            ClearHeldEquipment();
            HeldEquipment = kind;
            heldEquipmentVisual = kind == PrototypeHeldEquipment.AntiAirLauncher ?
                PrototypeEquipmentVisual.BuildAntiAirLauncher(soldierVisual.WeaponMount, runtime) :
                kind == PrototypeHeldEquipment.Launcher ?
                PrototypeEquipmentVisual.BuildLauncher(soldierVisual.WeaponMount, runtime) :
                PrototypeEquipmentVisual.BuildThrowable(soldierVisual.WeaponMount, loadout.throwable, runtime);
            heldEquipmentVisual.transform.localPosition = Vector3.zero;
            heldEquipmentVisual.transform.localScale = Vector3.one *
                (kind == PrototypeHeldEquipment.Throwable ? 1.3f : 1f);
            firstPersonEquipment = kind == PrototypeHeldEquipment.AntiAirLauncher ?
                PrototypeEquipmentVisual.BuildAntiAirLauncher(viewCamera.transform, runtime) :
                kind == PrototypeHeldEquipment.Launcher ?
                PrototypeEquipmentVisual.BuildLauncher(viewCamera.transform, runtime) :
                PrototypeEquipmentVisual.BuildThrowable(viewCamera.transform, loadout.throwable, runtime);
            PrototypeEquipmentVisual.AddHeldHands(firstPersonEquipment.transform, kind, runtime, Team);
            PrototypeEquipmentArt.ForceNearLod(firstPersonEquipment);
            firstPersonEquipment.transform.localScale = Vector3.one *
                (kind == PrototypeHeldEquipment.Throwable ? 1f : .6f);
            firstPersonEquipment.transform.localPosition = kind != PrototypeHeldEquipment.Throwable ?
                new Vector3(.30f, -.28f, .67f) : new Vector3(.30f, -.22f, .63f);
            foreach (Transform item in firstPersonEquipment.GetComponentsInChildren<Transform>())
                item.gameObject.layer = 2;
            soldierVisual.SetHeldEquipment(heldEquipmentVisual, kind);
            ApplyPerspectiveVisibility();
            RefreshHeldPresentation();
            return true;
        }

        public void EquipFirearm(bool secondary = false) => EquipSecondary(secondary);

        public void ClearHeldEquipment()
        {
            ResetAntiAirLock();
            aiming = false;
            if (HeldEquipment == PrototypeHeldEquipment.Firearm && heldEquipmentVisual == null && firstPersonEquipment == null)
            {
                throwableTrajectory?.SetVisible(false);
                return;
            }
            HeldEquipment = PrototypeHeldEquipment.Firearm;
            if (heldEquipmentVisual != null)
            {
                heldEquipmentVisual.SetActive(false);
                Destroy(heldEquipmentVisual);
            }
            if (firstPersonEquipment != null)
            {
                firstPersonEquipment.SetActive(false);
                Destroy(firstPersonEquipment);
            }
            heldEquipmentVisual = null;
            firstPersonEquipment = null;
            soldierVisual?.SetHeldEquipment(null, PrototypeHeldEquipment.Firearm);
            throwableTrajectory?.SetVisible(false);
            ApplyPerspectiveVisibility();
        }

        public void RefreshHeldPresentation()
        {
            ApplyPerspectiveVisibility();
            bool active = runtime != null && runtime.MatchStarted && Alive &&
                CurrentVehicle == null && !runtime.Match.Winner.HasValue && !runtime.PauseOpen && !runtime.SupportMenuOpen && !runtime.ScoreboardOpen &&
                (runtime.Frontend == null || !runtime.Frontend.IsOpen);
            if (!active || HeldEquipment == PrototypeHeldEquipment.Firearm)
            {
                throwableTrajectory?.SetVisible(false);
                return;
            }
            float blend = 1f - Mathf.Exp(-16f * Mathf.Max(Time.deltaTime, .001f));
            if (heldEquipmentVisual != null)
            {
                heldEquipmentVisual.transform.localRotation = Quaternion.Euler(pitch * .7f, 0f, 0f);
                heldEquipmentVisual.transform.localPosition = Vector3.zero;
            }
            if (firstPersonEquipment != null)
            {
                Vector3 target = HeldEquipment == PrototypeHeldEquipment.AntiAirLauncher ?
                    (IsAiming ? new Vector3(.24f, -.27f, .78f) : new Vector3(.30f, -.28f, .67f)) :
                    HeldEquipment != PrototypeHeldEquipment.Throwable ?
                    (IsAiming ? new Vector3(-.09f, -.114f, .54f) : new Vector3(.30f, -.28f, .67f)) :
                    new Vector3(.30f, -.22f, .63f);
                if (sprinting || IsSliding) target += new Vector3(.03f, -.09f, -.025f);
                firstPersonEquipment.transform.localPosition = Vector3.Lerp(
                    firstPersonEquipment.transform.localPosition, target, blend);
                firstPersonEquipment.transform.localRotation = Quaternion.identity;
            }
            if (HeldEquipmentVisual != null) KeepEquipmentAboveGround(HeldEquipmentVisual);
            if (HeldEquipment == PrototypeHeldEquipment.Throwable && throwableTrajectory != null)
            {
                GetThrowableLaunch(out Vector3 origin, out Vector3 velocity);
                throwableTrajectory.UpdatePath(origin, velocity, loadout.throwable == PrototypeThrowableId.Smoke ? 14f : PrototypeThrowableTrajectory.FuseSeconds);
            }
        }

        private static void KeepEquipmentAboveGround(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            float minimum = float.PositiveInfinity;
            foreach (Renderer renderer in renderers) minimum = Mathf.Min(minimum, renderer.bounds.min.y);
            Vector3 position = model.transform.position;
            float floor = PrototypeLayout.HeightAt(position.x, position.z) + .045f;
            if (minimum < floor) model.transform.position += Vector3.up * (floor - minimum);
        }

        public void GetThrowableLaunch(out Vector3 origin, out Vector3 velocity)
        {
            Transform held = HeldEquipmentVisual != null ? HeldEquipmentVisual.transform :
                viewCamera.transform;
            origin = held.position + viewCamera.transform.forward * .26f + Vector3.up * .03f;
            origin = SafeEquipmentReleaseOrigin(origin, PrototypeThrowableTrajectory.CollisionRadius,
                out _, out _);
            Vector3 target = InfantryAimPoint(viewCamera.transform.forward, 35f);
            velocity = (target - origin).normalized * PrototypeThrowableTrajectory.ThrowSpeed +
                Vector3.up * PrototypeThrowableTrajectory.UpwardBoost;
        }

        // A camera-mounted or long shoulder model can extend through a thin
        // wall. Sweep from the player's protected eye point, then start the
        // projectile on our side of the obstruction rather than beyond cover.
        private Vector3 SafeEquipmentReleaseOrigin(Vector3 desired, float radius,
            out Vector3 contact, out bool blocked)
        {
            Vector3 start = transform.position + Vector3.up * (prone ? .65f : crouched ? 1.05f : 1.65f);
            Vector3 travel = desired - start;
            float distance = travel.magnitude;
            float nearest = distance;
            contact = desired;
            blocked = false;
            Physics.SyncTransforms();
            if (distance > .001f)
                foreach (RaycastHit hit in Physics.SphereCastAll(start, radius,
                    travel / distance, distance, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(transform) || hit.distance > nearest) continue;
                    nearest = hit.distance;
                    contact = hit.point;
                    blocked = true;
                }
            return blocked ? start + travel.normalized * Mathf.Max(0f, nearest - .02f) : desired;
        }

        public void GetRocketLaunch(out Vector3 origin, out Vector3 direction)
        {
            Transform launcher = HeldEquipmentVisual != null ? HeldEquipmentVisual.transform : viewCamera.transform;
            Vector3 desired = launcher.TransformPoint(new Vector3(0f, 0f, .76f));
            origin = SafeEquipmentReleaseOrigin(desired, .09f, out Vector3 contact, out bool blocked);
            Vector3 target = blocked ? contact : InfantryAimPoint(viewCamera.transform.forward, 160f);
            direction = (target - origin).normalized;
            if (direction.sqrMagnitude < .5f) direction = viewCamera.transform.forward;
        }

        public bool TryUseHeldEquipment()
        {
            if (!CanUseHeldEquipment || Time.time < nextEquipmentUseAt) return false;
            if (HeldEquipment == PrototypeHeldEquipment.Throwable && throwableCount > 0)
            {
                ThrowEquipment();
                nextEquipmentUseAt = Time.time + .50f;
                return true;
            }
            if (HeldEquipment == PrototypeHeldEquipment.Launcher &&
                Class == PrototypeInfantryClass.Engineer && rocketCount > 0)
            {
                FireRocket();
                nextEquipmentUseAt = Time.time + .85f;
                return true;
            }
            if (HeldEquipment == PrototypeHeldEquipment.AntiAirLauncher &&
                Class == PrototypeInfantryClass.Engineer && antiAirMissileCount > 0)
            {
                if (!FireAntiAirMissile()) return false;
                nextEquipmentUseAt = Time.time + .85f;
                return true;
            }
            return false;
        }

        private bool FireAntiAirMissile()
        {
            if (antiAirLock == null || !antiAirLock.CanFire(IsAiming))
            {
                SupportStatus = "AA MISSILE: AIM FOR 5 SECONDS TO LOCK";
                supportStatusUntil = Time.time + 2f;
                return false;
            }
            IPrototypeVehicle target = antiAirLock.Target;
            GetRocketLaunch(out Vector3 origin, out Vector3 direction);
            if (PrototypeVehicleProjectile.Launch(runtime, Team, transform, origin,
                direction, 170f, 500f, 220f, 0f, 3.5f, 1f, target) == null) return false;
            antiAirMissileCount--;
            lastShotAt = Time.time;
            if (target is PrototypeCombinedArmsVehicle warned) warned.WarnIncoming();
            ResetAntiAirLock(false);
            if (antiAirMissileCount == 0) ClearHeldEquipment();
            SupportStatus = "AA MISSILE " + antiAirMissileCount + " LEFT";
            supportStatusUntil = Time.time + 2f;
            return true;
        }

        private void ThrowEquipment()
        {
            if (HeldEquipment != PrototypeHeldEquipment.Throwable || throwableCount <= 0 ||
                CurrentVehicle != null) return;
            GetThrowableLaunch(out Vector3 origin, out Vector3 velocity);
            throwableCount--;
            var projectile = new GameObject(loadout.throwable == PrototypeThrowableId.Smoke ?
                "Smoke grenade" : "Fragmentation grenade");
            projectile.transform.position = origin;
            SphereCollider grenadeCollider = projectile.AddComponent<SphereCollider>();
            grenadeCollider.radius = PrototypeThrowableTrajectory.CollisionRadius;
            if (controller != null) Physics.IgnoreCollision(grenadeCollider, controller);
            GameObject grenadeModel = PrototypeEquipmentVisual.BuildThrowable(projectile.transform, loadout.throwable, runtime);
            PrototypeThrowableVisual.RemoveSafety(grenadeModel);
            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0f;
            body.linearVelocity = velocity;
            body.angularVelocity = transform.right * 7f;
            thrownProjectiles.Add(projectile);
            throwableTrajectory?.SetVisible(false);
            PrototypeCombatFeedback.Play(transform, PrototypeCombatSound.GrenadeThrow);
            if (loadout.throwable == PrototypeThrowableId.Smoke) ArmSmokeGrenade(projectile);
            else StartCoroutine(Detonate(projectile, loadout.throwable, Team));
            ClearHeldEquipment();
            SupportStatus = (loadout.throwable == PrototypeThrowableId.Frag ? "FRAG " : "SMOKE ") +
                throwableCount + " LEFT";
            supportStatusUntil = Time.time + 2f;
        }

        private void FireRocket()
        {
            if (HeldEquipment != PrototypeHeldEquipment.Launcher ||
                loadout.role != PrototypeInfantryClass.Engineer || rocketCount <= 0 ||
                CurrentVehicle != null) return;
            rocketCount--;
            GetRocketLaunch(out Vector3 origin, out Vector3 direction);
            PrototypeBallisticProjectile.LaunchRocket(runtime, Team, transform, origin, direction);
            lastShotAt = Time.time;
            if (rocketCount == 0) ClearHeldEquipment();
            SupportStatus = "ANTI-ARMOR ROCKET " + rocketCount + " LEFT";
            supportStatusUntil = Time.time + 2f;
        }

        private void ClearThrownProjectiles()
        {
            foreach (GameObject projectile in thrownProjectiles.ToArray())
                if (projectile != null) Destroy(projectile);
            thrownProjectiles.Clear();
        }

        private void ArmSmokeGrenade(GameObject projectile)
        {
            if (projectile == null) return;
            var fuse = projectile.GetComponent<PrototypeSmokeGrenadeFuse>();
            if (fuse == null) fuse = projectile.AddComponent<PrototypeSmokeGrenadeFuse>();
            fuse.Initialize(runtime, transform, ForgetThrownProjectile);
        }

        private void ForgetThrownProjectile(GameObject projectile) => thrownProjectiles.Remove(projectile);

        private void OnDisable()
        {
            ResetAntiAirLock();
            throwableTrajectory?.SetVisible(false);
            heldEquipmentVisual?.SetActive(false);
            firstPersonEquipment?.SetActive(false);
        }

        private void OnDestroy()
        {
            ResetAntiAirLock();
            ClearThrownProjectiles();
        }

        public void ConsumeBeacon() => ClearBeacon();

        private void ClearBeacon()
        {
            beaconAvailable = false;
            if (beaconMarker != null) Destroy(beaconMarker);
            beaconMarker = null;
        }

        private void PlaceBeacon(Vector2 position)
        {
            ClearBeacon();
            beaconPosition = position;
            beaconAvailable = true;
            beaconMarker = PrototypeEquipmentArt.Create(runtime.transform, "Beacon");
            if (beaconMarker != null)
            {
                beaconMarker.name = "Recon respawn beacon";
                beaconMarker.transform.SetPositionAndRotation(new Vector3(position.x,
                    PrototypeWorldVisual.InfantrySurfaceAt(position.x, position.y) + .025f, position.y),
                    Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                return;
            }
            beaconMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beaconMarker.name = "Recon respawn beacon";
            beaconMarker.transform.position = new Vector3(position.x,
                PrototypeLayout.HeightAt(position.x, position.y) + .4f, position.y);
            beaconMarker.transform.localScale = new Vector3(.35f, .4f, .35f);
            beaconMarker.GetComponent<Renderer>().sharedMaterial =
                Team == PrototypeTeam.Blue ? runtime.BlueTrimMaterial : runtime.RedTrimMaterial;
            Destroy(beaconMarker.GetComponent<Collider>());
            GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antenna.name = "Beacon antenna";
            antenna.transform.SetParent(beaconMarker.transform, false);
            antenna.transform.localPosition = new Vector3(0f, .95f, 0f);
            antenna.transform.localScale = new Vector3(.08f, 1.2f, .08f);
            antenna.GetComponent<Renderer>().sharedMaterial = runtime.GunMaterial;
            Destroy(antenna.GetComponent<Collider>());
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Beacon lamp";
            lamp.transform.SetParent(beaconMarker.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 1.60f, 0f);
            lamp.transform.localScale = Vector3.one * .32f;
            lamp.GetComponent<Renderer>().sharedMaterial = runtime.LampMaterial;
            Destroy(lamp.GetComponent<Collider>());
        }

        private IEnumerator Detonate(GameObject projectile, PrototypeThrowableId kind, PrototypeTeam throwerTeam)
        {
            if (kind == PrototypeThrowableId.Smoke)
            {
                ArmSmokeGrenade(projectile);
                yield break;
            }
            yield return new WaitForSeconds(PrototypeThrowableTrajectory.FuseSeconds);
            if (projectile == null) yield break;
            Vector3 center = projectile.transform.position;
            thrownProjectiles.Remove(projectile);
            Destroy(projectile);
            if (runtime == null || !runtime.MatchStarted || runtime.Match.Winner.HasValue) yield break;
            ApplyFragDamageForTeam(center, throwerTeam);
            PrototypeCombatFeedback.Explosion(runtime, center, 5.5f,
                PrototypeCombatSound.FragExplosion);
        }

        public static bool CanReleaseSmoke(GameObject projectile)
        {
            if (projectile == null) return false;
            var fuse = projectile.GetComponent<PrototypeSmokeGrenadeFuse>();
            return fuse != null && fuse.ReadyToEmit;
        }

        private void ApplyFragDamage(Vector3 center) => ApplyFragDamageForTeam(center, Team);

        private void ApplyFragDamageForTeam(Vector3 center, PrototypeTeam throwerTeam)
        {
            const float blastRadius = 5.5f;
            Physics.SyncTransforms();
            var struckBots = new HashSet<PrototypeBot>();
            var struckPlayers = new HashSet<PrototypeRemotePlayer>();
            var struckVehicles = new HashSet<IPrototypeVehicle>();
            var struckSupplies = new HashSet<PrototypeAmmoSupplyPoint>();
            foreach (Collider hit in Physics.OverlapSphere(center, blastRadius))
            {
                bool closestSupported = hit is BoxCollider || hit is SphereCollider ||
                    hit is CapsuleCollider || hit is MeshCollider mesh && mesh.convex;
                float distance = Vector3.Distance(center,
                    closestSupported ? hit.ClosestPoint(center) :
                        hit.bounds.ClosestPoint(center));
                float falloff = Mathf.Clamp01(1f - distance / blastRadius);
                PrototypeAmmoSupplyPoint supply = hit.GetComponentInParent<PrototypeAmmoSupplyPoint>();
                if (supply != null && struckSupplies.Add(supply)) supply.TakeDamage(95f * falloff);
                PrototypeBot bot = hit.GetComponentInParent<PrototypeBot>();
                if (bot != null && bot.Team != throwerTeam && struckBots.Add(bot))
                    ScoreFragDamage(bot, throwerTeam, 95f * falloff, () =>
                        bot.TakeDamage(95f * falloff, new Vector2(center.x, center.z)));
                PrototypeRemotePlayer remote = hit.GetComponentInParent<PrototypeRemotePlayer>();
                if (remote != null && remote.Team != throwerTeam && struckPlayers.Add(remote))
                    ScoreFragDamage(remote, throwerTeam, 95f * falloff, () => remote.TakeDamageFrom(95f * falloff, center));
                IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(hit);
                if (vehicle != null && vehicle.Team != throwerTeam && struckVehicles.Add(vehicle))
                {
                    if (falloff > 0f)
                        PrototypeCombatFeedback.VehicleImpact(runtime, vehicle,
                            closestSupported ? hit.ClosestPoint(center) : hit.bounds.ClosestPoint(center),
                            PrototypeDamageClass.Frag);
                    float hullDamage = PrototypeVehicleArmor.Resolve(vehicle, 55f * falloff, PrototypeDamageClass.Frag);
                    if (hullDamage > 0f) ScoreFragDamage(vehicle, throwerTeam, hullDamage, () => vehicle.TakeDamage(hullDamage));
                }
            }
        }

        private void ScoreFragDamage(object target, PrototypeTeam throwerTeam, float amount, System.Action apply)
        {
            if (runtime.Scores != null) runtime.Scores.ApplyDamage(target, this, throwerTeam, amount, apply);
            else apply();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ResetAntiAirLock();
        }

        private void UseSupport()
        {
            float now = Time.time;
            bool used = false;
            if (kit.Role == PrototypeInfantryClass.Assault)
            {
                used = kit.TryResupply(now, ActiveKit, 0f);
                if (!used)
                    foreach (PrototypeBot bot in runtime.Bots)
                    {
                        if (!bot.Alive || bot.Team != Team) continue;
                        if (kit.TryResupply(now, bot.Kit,
                            Vector2.Distance(MapPosition, bot.MapPosition))) { used = true; break; }
                    }
            }
            else if (kit.Role == PrototypeInfantryClass.Medic)
            {
                if (kit.TryHeal(now, Health, 0f, out float healed))
                {
                    Health = healed;
                    used = true;
                }
                else
                    foreach (PrototypeBot bot in runtime.Bots)
                    {
                        if (!bot.Alive || bot.Team != Team) continue;
                        if (kit.TryHeal(now, bot.Health,
                            Vector2.Distance(MapPosition, bot.MapPosition), out healed))
                        {
                            bot.HealTo(healed);
                            used = true;
                            break;
                        }
                    }
            }
            else if (kit.Role == PrototypeInfantryClass.Engineer)
            {
                IPrototypeVehicle closest = null;
                float nearest = 8f;
                foreach (IPrototypeVehicle vehicle in runtime.Vehicles)
                {
                    float distance = Vector2.Distance(MapPosition, vehicle.MapPosition);
                    if (vehicle.Team == Team && vehicle.Alive && distance < nearest)
                    { closest = vehicle; nearest = distance; }
                }
                if (closest != null) used = kit.TryRepairVehicle(now, closest, nearest);
            }
            else if (kit.Role == PrototypeInfantryClass.Recon)
            {
                Vector2 site = MapPosition;
                if (!PrototypeLayout.Collides(site, 1.5f) && kit.TryDeployBeacon(now))
                {
                    PlaceBeacon(site);
                    used = true;
                }
            }
            SupportStatus = kit.Role == PrototypeInfantryClass.Assault ?
                used ? "AMMO PACK: ORDINARY AMMO ONLY" : "AMMO PACK: NO VALID AMMO TARGET" :
                used ? "SUPPORT ACTIVE" : "NO VALID SUPPORT TARGET";
            if (used) PrototypeCombatFeedback.Support(runtime, transform.position,
                loadout.role);
            supportStatusUntil = now + 2f;
        }

        public PrototypeIncomingDamage LastIncomingDamage { get; private set; }
        public void TakeDamage(float amount) => TakeDamageFrom(amount, null);

        public void TakeDamageFrom(float amount, Vector3? sourcePosition)
        {
            if (runtime == null || runtime.IsNetworkReplica || !Alive ||
                runtime.Match.Winner.HasValue || !float.IsFinite(amount) || amount <= 0f) return;
            float previousHealth = Health;
            Health = Mathf.Max(0f, Health - kit.AbsorbDamage(amount));
            float lost = previousHealth - Health;
            if (lost > 0f)
            {
                LastIncomingDamage = PrototypeIncomingDamage.Record(LastIncomingDamage, lost, sourcePosition);
                runtime.DamageFeedback?.Notify(lost, sourcePosition);
            }
            if (Alive) return;
            runtime.Scores?.ResolveDefeat(this, Team);
            ClearHeldEquipment();
            bool inVehicle = CurrentVehicle != null;
            ForceExitVehicle();
            if (inVehicle) { Eliminate(); return; }
            downedUntil = Time.time + 30f;
            ResetReviveTracking();
            rescueCalled = false;
            EnterDownedPose();
            runtime.Frontend.OpenDowned();
        }

        private bool HasValidMedic() => revivingMedic != null && revivingMedic.Alive &&
            revivingMedic.Team == Team && revivingMedic.Class == PrototypeInfantryClass.Medic &&
            Vector2.Distance(MapPosition, revivingMedic.MapPosition) <= 3.5f;

        private bool HasFreshValidMedic() => HasValidMedic() &&
            Time.time >= lastRevivePulseAt && Time.time - lastRevivePulseAt <= .25f;

        // Integrate only the interval covered by an actual recent medic pulse.
        // Properties also synchronize so component update order cannot let a
        // near-expiry casualty bleed out during a valid four-second rescue.
        private void SyncDownedTimer()
        {
            float now = Time.time;
            if (runtime != null && runtime.IsNetworkReplica)
            { lastDownedClockAt = now; return; }
            if (Alive || downedUntil <= 0f) { ResetReviveTracking(); return; }
            if (HasValidMedic() && now >= lastDownedClockAt)
            {
                float pauseFrom = Mathf.Max(lastDownedClockAt, lastRevivePulseAt);
                float pauseUntil = Mathf.Min(now, lastRevivePulseAt + .25f);
                downedUntil += Mathf.Max(0f, pauseUntil - pauseFrom);
            }
            lastDownedClockAt = now;
            if (!HasFreshValidMedic()) ResetReviveTracking();
        }

        private void ResetReviveTracking()
        {
            revivingMedic = null; reviveStartedAt = lastRevivePulseAt = 0f;
            lastDownedClockAt = Time.time;
            networkRevivePaused = false; networkReviveProgress = networkDownedRemaining = 0f;
        }

        public void RequestRescue()
        {
            if (IsDowned) rescueCalled = true;
        }

        public void GiveUp(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            SyncDownedTimer();
            if (IsDowned) downedUntil -= Mathf.Clamp(dt, 0f, .15f) * 12f;
            if (downedUntil > 0f && Time.time >= downedUntil) Eliminate();
        }

        public void GiveUpImmediately()
        {
            if (IsDowned) Eliminate();
        }

        public bool TryRevive(PrototypeBot medic)
        {
            if (runtime == null || runtime.IsNetworkReplica || !IsDowned ||
                medic == null || !medic.Alive || medic.Team != Team ||
                medic.Class != PrototypeInfantryClass.Medic ||
                Vector2.Distance(MapPosition, medic.MapPosition) > 3.5f) return false;
            if (revivingMedic != medic && HasFreshValidMedic()) return false;
            if (revivingMedic != medic)
            {
                revivingMedic = medic;
                reviveStartedAt = Time.time;
            }
            lastRevivePulseAt = Time.time;
            lastDownedClockAt = Time.time;
            if (Time.time - reviveStartedAt < ReviveDuration) return false;
            downedUntil = 0f;
            ResetReviveTracking();
            rescueCalled = false;
            revivingMedic = null;
            Health = 50f;
            runtime.Scores?.BeginLife(this);
            if (controller != null) controller.enabled = true;
            LeaveDownedPose();
            SupportStatus = "REVIVED BY MEDIC";
            supportStatusUntil = Time.time + 2f;
            runtime.Frontend.Close();
            return true;
        }

        private void EnterDownedPose()
        {
            ClearHeldEquipment();
            downedLookYaw = 0f;
            pitch = 0f;
            ResetReviveTracking();
            aiming = false;
            sprinting = false;
            if (controller != null)
            {
                controller.height = .7f;
                controller.center = new Vector3(0f, .35f, 0f);
                controller.stepOffset = .1f;
            }
            if (viewCamera != null)
            {
                viewCamera.transform.localPosition = new Vector3(.35f, .42f, -1.3f);
                viewCamera.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                viewCamera.fieldOfView = 76f;
            }
            if (soldierVisual != null) soldierVisual.SetDowned(true);
            ApplyPerspectiveVisibility();
        }

        private void LeaveDownedPose()
        {
            downedLookYaw = 0f;
            pitch = 0f;
            ResetReviveTracking();
            crouched = false;
            prone = false;
            if (controller != null)
            {
                controller.height = 1.8f;
                controller.center = new Vector3(0f, .9f, 0f);
                controller.stepOffset = .35f;
            }
            if (viewCamera != null)
            {
                viewCamera.transform.localRotation = Quaternion.identity;
                PositionInfantryCamera(1f);
            }
            if (soldierVisual != null) soldierVisual.SetDowned(false);
            ApplyPerspectiveVisibility();
        }

        public void AdjustDownedView(float horizontal, float vertical)
        {
            if (!IsDowned || viewCamera == null) return;
            downedLookYaw = Mathf.Clamp(downedLookYaw + horizontal,
                -DownedViewHalfAngle, DownedViewHalfAngle);
            pitch = Mathf.Clamp(pitch + vertical, -35f, 45f);
            viewCamera.transform.localRotation =
                Quaternion.Euler(pitch, downedLookYaw, 12f);
        }

        private void Eliminate()
        {
            if (runtime == null || runtime.IsNetworkReplica || respawnAt > 0f) return;
            downedUntil = 0f;
            ResetReviveTracking();
            rescueCalled = false;
            ApplyPerspectiveVisibility();
            runtime.Match.RecordDeath(Team);
            respawnAt = Time.time + 4.5f;
            if (!runtime.Match.Winner.HasValue) runtime.Frontend.Open(true);
        }

        public void HealTo(float health)
        {
            if (Alive) Health = Mathf.Clamp(health, Health, 100f);
        }

        public bool TryDeploy(string locationId, PrototypeInfantryClass selectedClass)
        {
            var selection = loadout.Copy();
            selection.role = selectedClass;
            return TryDeploy(locationId, selection);
        }

        public bool TryDeploy(string locationId, PrototypeLoadout selection)
        {
            if (runtime == null || runtime.IsNetworkReplica || Alive ||
                downedUntil > 0f ||
                runtime.Match.Winner.HasValue || Time.time < respawnAt ||
                !PrototypeDeployment.TryResolve(runtime, Team, locationId, true,
                    out Vector2 point, out IPrototypeVehicle vehicle)) return false;
            ApplyLoadout(selection);
            if (locationId == "BEACON") ConsumeBeacon();
            Respawn(point);
            if (vehicle != null)
            {
                int seat = 0;
                string[] parts = locationId.Split('-');
                if (parts.Length == 3 && !int.TryParse(parts[2], out seat)) return false;
                if (!EnterVehicleSeat(vehicle, seat)) return false;
            }
            return true;
        }

        private void Respawn()
        {
            Respawn(Team == PrototypeTeam.Blue ?
                PrototypeLayout.BlueBase : PrototypeLayout.RedBase);
        }

        private void Respawn(Vector2 point)
        {
            runtime?.DamageFeedback?.Clear();
            ClearHeldEquipment();
            nextEquipmentUseAt = 0f;
            ForceExitVehicle();
            parachuting = false;
            if (controller != null) controller.enabled = false;
            transform.position = new Vector3(point.x,
                PrototypeLayout.HeightAt(point.x, point.y) + 0.3f, point.y);
            transform.rotation = Quaternion.Euler(0f,
                Team == PrototypeTeam.Blue ? 48f : 228f, 0f);
            pitch = 0f;
            downedLookYaw = 0f;
            verticalSpeed = 0f;
            crouched = false;
            prone = false;
            sprinting = false;
            sprintInterrupted = false;
            SprintRequested = false;
            aiming = false;
            Slide.Reset();
            if (controller != null)
            {
                controller.height = 1.8f;
                controller.center = new Vector3(0f, .9f, 0f);
                controller.stepOffset = .35f;
            }
            if (viewCamera != null)
            {
                viewCamera.transform.localRotation = Quaternion.identity;
                viewCamera.fieldOfView = 76f;
                PositionInfantryCamera(1f);
            }
            Health = 100f;
            runtime.Scores?.BeginLife(this);
            downedUntil = 0f;
            ResetReviveTracking();
            rescueCalled = false;
            revivingMedic = null;
            respawnAt = 0f;
            kit.ResetForSpawn();
            secondaryKit.ResetForSpawn();
            throwableCount = 2;
            rocketCount = 2;
            antiAirMissileCount = 2;
            secondaryEquipped = false;
            if (viewCamera != null) BuildWeaponVisual();
            if (soldierVisual != null) soldierVisual.SetDowned(false);
            ApplyPerspectiveVisibility();
            if (controller != null) controller.enabled = true;
        }
    }
}
