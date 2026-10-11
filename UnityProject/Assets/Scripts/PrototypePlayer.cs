using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayer : MonoBehaviour
    {
        private const float WalkSpeed = 8.6f;
        private const float SprintSpeed = 13.7f;
        private const float CrouchSpeed = 4.7f;
        private const float ProneSpeed = 2.3f;
        private const float SlideDuration = .78f;
        private const float DownedViewHalfAngle = 85f;
        private const float ReviveDuration = 4f;
        private CharacterController controller;
        private Camera viewCamera;
        private GameObject rifle;
        private GameObject firstPersonWeapon;
        private PrototypeSoldierVisual soldierVisual;
        private Transform magazineVisual;
        private Vector3 magazineHome;
        private Transform firstPersonMagazine;
        private Vector3 firstPersonMagazineHome;
        private PrototypeRuntime runtime;
        private float pitch;
        private float vehicleLookYaw;
        private float vehicleWorldLookYaw;
        private float verticalSpeed;
        private float respawnAt;
        private float downedUntil;
        private bool rescueCalled;
        private float downedLookYaw;
        private float reviveStartedAt;
        private float lastRevivePulseAt;
        private PrototypeBot revivingMedic;
        private PrototypeInfantryKit kit = new PrototypeInfantryKit(PrototypeInfantryClass.Assault);
        private PrototypeInfantryKit secondaryKit = new PrototypeInfantryKit(
            PrototypeInfantryClass.Assault, PrototypeWeaponId.Marksman);
        private PrototypeLoadout loadout = new PrototypeLoadout();
        private bool secondaryEquipped;
        private int throwableCount = 2;
        private int rocketCount = 2;
        private GameObject beaconMarker;
        private Vector2 beaconPosition;
        private bool beaconAvailable;
        private float supportStatusUntil;
        private bool crouched;
        private bool prone;
        private bool sprinting;
        private bool aiming;
        private float slideEndsAt;
        private Vector3 slideDirection;
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
        public bool BeaconAvailable => beaconAvailable && loadout.role == PrototypeInfantryClass.Recon;
        public Vector2 BeaconPosition => beaconPosition;
        public int Ammo => ActiveKit.Ammo;
        public int Reserve => ActiveKit.Reserve;
        public bool Reloading => ActiveKit.Reloading;
        public bool IsAiming => aiming && !Reloading && CurrentVehicle == null;
        public bool IsSprinting => sprinting;
        public bool IsSliding => slideEndsAt > Time.time;
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
        public bool IsDowned => !Alive && downedUntil > Time.time;
        public float DownedRemaining => IsDowned ? downedUntil - Time.time : 0f;
        public bool RescueCalled => rescueCalled;
        public float DownedViewYaw => downedLookYaw;
        public float ReviveProgress => IsDowned && revivingMedic != null &&
            Time.time - lastRevivePulseAt <= .25f ?
            Mathf.Clamp01((Time.time - reviveStartedAt) / ReviveDuration) : 0f;

        public void Initialize(PrototypeRuntime game)
        {
            runtime = game;
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
            Respawn();
        }

        private void BuildWeaponVisual()
        {
            if (soldierVisual == null) return;
            soldierVisual.SetWeapon(ActiveWeapon.Id, runtime);
            rifle = soldierVisual.HeldWeapon;
            magazineVisual = rifle.transform.Find("Magazine");
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
            firstPersonMagazine = firstPersonWeapon.transform.Find("Magazine");
            if (firstPersonMagazine != null)
                firstPersonMagazineHome = firstPersonMagazine.localPosition;
            ApplyPerspectiveVisibility();
        }

        private void ApplyPerspectiveVisibility()
        {
            bool onFoot = CurrentVehicle == null;
            bool motorcycleThirdPerson = Alive && !vehicleFirstPerson &&
                CurrentVehicle is PrototypeCombinedArmsVehicle motorcycle &&
                motorcycle.Kind == PrototypeVehicleKind.Motorcycle;
            bool showBody = onFoot && (IsDowned || Alive && infantryThirdPerson) ||
                motorcycleThirdPerson;
            if (soldierVisual != null) soldierVisual.SetVisible(showBody);
            if (rifle != null) rifle.SetActive(onFoot && showBody && Alive);
            if (firstPersonWeapon != null)
                firstPersonWeapon.SetActive(onFoot && Alive && !infantryThirdPerson);
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
            var selection = new PrototypeLoadout { role = selectedClass,
                primary = PrototypeInfantryRoles.DefaultWeapon(selectedClass),
                secondary = PrototypeWeaponId.Marksman,
                throwable = PrototypeThrowableId.Frag };
            ApplyLoadout(selection);
        }

        public void ApplyLoadout(PrototypeLoadout selection)
        {
            loadout = (selection ?? new PrototypeLoadout()).Copy();
            loadout.Validate();
            kit = new PrototypeInfantryKit(loadout.role, loadout.primary);
            secondaryKit = new PrototypeInfantryKit(loadout.role, loadout.secondary);
            secondaryEquipped = false;
            throwableCount = 2;
            rocketCount = 2;
            ClearBeacon();
            if (soldierVisual != null)
            {
                soldierVisual.Initialize(runtime, Team, Class, -.3f);
                BuildWeaponVisual();
            }
        }

        private void EquipSecondary(bool secondary)
        {
            if (secondaryEquipped == secondary) return;
            secondaryEquipped = secondary;
            BuildWeaponVisual();
        }

        public void SetTeam(PrototypeTeam team)
        {
            if (Team == team) return;
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
            bool rescueRequested)
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
        }

        private void Update()
        {
            if (runtime == null) return;
            if (!runtime.MatchStarted) return;
            if (runtime.PauseOpen) return;
            // This must precede cursor and match-state gates: a destroyed vehicle
            // must never leave the infantry controller disabled.
            if (CurrentVehicle != null && (!CurrentVehicle.Alive ||
                CurrentVehicle.Occupant != this || !Alive || runtime.Match.Winner.HasValue))
                ForceExitVehicle();
            if (!Alive)
            {
                if (IsDowned && Input.GetMouseButton(1))
                    AdjustDownedView(Input.GetAxis("Mouse X") * runtime.MouseSensitivity,
                        -Input.GetAxis("Mouse Y") * runtime.MouseSensitivity);
                if (!runtime.IsNetworkReplica && downedUntil > 0f)
                {
                    if (Input.GetKeyDown(KeyCode.H)) RequestRescue();
                    if (Input.GetKey(KeyCode.Space)) GiveUp(Time.deltaTime);
                    if (Time.time >= downedUntil) Eliminate();
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
            if (Input.GetKeyDown(KeyCode.LeftControl) ||
                Input.GetKeyDown(KeyCode.RightControl))
            {
                if (prone) { prone = false; crouched = true; }
                else { prone = true; crouched = false; slideEndsAt = 0f; }
            }
            bool wantsSprint = Input.GetKey(KeyCode.LeftShift) && input.y > .1f &&
                !prone && !crouched && controller.isGrounded && !Reloading;
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (wantsSprint)
                {
                    slideDirection = (transform.right * input.x + transform.forward * input.y).normalized;
                    slideEndsAt = Time.time + SlideDuration;
                    crouched = false;
                    prone = false;
                }
                else if (prone) { prone = false; crouched = true; }
                else if (crouched) { if (CanStand()) crouched = false; }
                else crouched = true;
            }
            if (slideEndsAt > 0f && Time.time >= slideEndsAt)
            {
                slideEndsAt = 0f;
                crouched = true;
            }
            aiming = Input.GetMouseButton(1) && !IsSliding && !Reloading;
            sprinting = wantsSprint && !aiming && !IsSliding;
            float speed = prone ? ProneSpeed : crouched ? CrouchSpeed :
                aiming ? WalkSpeed * .65f : sprinting ? SprintSpeed : WalkSpeed;
            Vector3 horizontal = IsSliding ? slideDirection *
                Mathf.Lerp(14f, 5f, 1f - (slideEndsAt - Time.time) / SlideDuration) :
                (transform.right * input.x + transform.forward * input.y) * speed;
            float targetHeight = prone ? .75f : crouched || IsSliding ? 1.15f : 1.8f;
            controller.height = targetHeight;
            controller.center = new Vector3(0f, targetHeight * .5f, 0f);
            controller.stepOffset = prone ? .12f : .35f;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (!runtime.IsNetworkReplica && controller.isGrounded &&
                !prone && !crouched && !IsSliding &&
                Input.GetKeyDown(KeyCode.Space)) verticalSpeed = 6.2f;
            StepInfantryGravity(Time.deltaTime);
            controller.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);

            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.E)) TryEnterVehicle();
            if (CurrentVehicle != null) return;
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.R))
                ActiveKit.StartReload(Time.time);
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.Alpha1))
                EquipSecondary(false);
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.Alpha2))
                EquipSecondary(true);
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.X)) UseSupport();
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.G)) ThrowEquipment();
            if (!runtime.IsNetworkReplica && Input.GetKeyDown(KeyCode.Z)) FireRocket();
            if (!runtime.IsNetworkReplica &&
                (ActiveWeapon.Automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0))) Fire();
        }

        private void LateUpdate()
        {
            if (CurrentVehicle != null) UpdateVehicleCamera();
            else if (runtime != null && runtime.MatchStarted && Alive)
                UpdateInfantryPresentation();
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
                (ActiveWeapon.Id == PrototypeWeaponId.Sniper ? 40f : 54f) :
                sprinting ? 82f : 76f;
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, blend);

            if (rifle == null) return;

            float reload = Reloading ? Mathf.Clamp01(1f -
                (ActiveKit.ReloadEndsAt - Time.time) / ActiveWeapon.ReloadSeconds) : 0f;
            float reloadDip = Reloading ? Mathf.Sin(reload * Mathf.PI) : 0f;
            float stride = moveAmount * (sprinting ? 1f : .45f);
            float bob = Mathf.Sin(Time.time * (sprinting ? 14f : 9f)) * .013f * stride;
            Vector3 home = new Vector3(0f, bob - reloadDip * .16f, 0f);
            if (sprinting || IsSliding) home += new Vector3(0f, -.07f, -.04f);
            rifle.transform.localPosition = Vector3.Lerp(rifle.transform.localPosition, home, blend);
            Quaternion pose = Quaternion.Euler(reloadDip * 28f +
                pitch * .55f + (sprinting ? -11f : 0f), 0f, reloadDip * 14f +
                (IsSliding ? -11f : 0f));
            rifle.transform.localRotation = Quaternion.Slerp(rifle.transform.localRotation, pose, blend);
            if (magazineVisual != null)
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
                if (sprinting || IsSliding)
                    firstHome += new Vector3(.06f, -.12f, -.07f);
                firstHome += new Vector3(0f, bob - reloadDip * .18f, 0f);
                firstPersonWeapon.transform.localPosition = Vector3.Lerp(
                    firstPersonWeapon.transform.localPosition, firstHome, blend);
                Quaternion firstPose = Quaternion.Euler(reloadDip * 28f +
                    (sprinting ? -11f : 0f), 0f, reloadDip * 14f +
                    (IsSliding ? -11f : 0f));
                firstPersonWeapon.transform.localRotation = Quaternion.Slerp(
                    firstPersonWeapon.transform.localRotation, firstPose, blend);
            }
        }

        private void PositionInfantryCamera(float blend)
        {
            float eyeHeight = prone ? .65f : crouched || IsSliding ? 1.05f : 1.65f;
            viewCamera.transform.rotation = transform.rotation * Quaternion.Euler(pitch, 0f, 0f);
            Vector3 focus = transform.position + Vector3.up * eyeHeight;
            if (!infantryThirdPerson)
            {
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
            CurrentVehicle = closest;
            parachuting = false;
            VehicleSeatStatus = "";
            controller.enabled = false;
            transform.position = closest.SeatPosition;
            transform.rotation = closest.VehicleTransform.rotation;
            verticalSpeed = 0f;
            pitch = 0f;
            vehicleLookYaw = 0f;
            vehicleWorldLookYaw = closest.VehicleTransform.eulerAngles.y;
            ApplyPerspectiveVisibility();
            aiming = false;
            sprinting = false;
            slideEndsAt = 0f;
            UpdateVehicleCamera();
            return true;
        }

        private bool UsesWorldVehicleHeading => CurrentVehicle is IPrototypeAircraftVehicle aircraft &&
            aircraft.IsAircraft && CurrentVehicle is IPrototypeSeatedVehicle seated && seated.PlayerSeat == 0;

        // Mouse look and pilot steering share a stable world heading. Updating
        // the hull must not move its own camera target farther away each frame.
        private void ApplyVehicleLook(float yawDelta, float pitchDelta)
        {
            if (UsesWorldVehicleHeading)
                vehicleWorldLookYaw = Mathf.Repeat(vehicleWorldLookYaw + yawDelta, 360f);
            else vehicleLookYaw = Mathf.Clamp(vehicleLookYaw + yawDelta, -135f, 135f);
            float minimumPitch = -35f;
            float maximumPitch = 55f;
            if (CurrentVehicle is PrototypeCombinedArmsVehicle combined)
            {
                if (combined.Kind == PrototypeVehicleKind.AntiAir && combined.PlayerSeat == 0)
                { minimumPitch = -77f; maximumPitch = 7f; }
                else if (combined.Kind == PrototypeVehicleKind.AntiAir && combined.PlayerSeat == 1)
                { minimumPitch = -32f; maximumPitch = 11f; }
                else if (combined.Kind == PrototypeVehicleKind.Jet && combined.PlayerSeat == 0)
                { minimumPitch = -49f; maximumPitch = 49f; }
            }
            pitch = Mathf.Clamp(pitch + pitchDelta, minimumPitch, maximumPitch);
        }

        private void UpdateVehicleCamera()
        {
            if (CurrentVehicle == null) return;
            transform.position = CurrentVehicle.SeatPosition;
            transform.rotation = CurrentVehicle.VehicleTransform.rotation;
            float heading = UsesWorldVehicleHeading ? vehicleWorldLookYaw :
                CurrentVehicle.VehicleTransform.eulerAngles.y + vehicleLookYaw;
            Quaternion look = Quaternion.Euler(pitch, heading, 0f);
            viewCamera.transform.rotation = look;
            viewCamera.fieldOfView = CurrentVehicle is PrototypeTankVehicle ?
                (vehicleFirstPerson ? 56f : 72f) :
                (vehicleFirstPerson ? 72f : 78f);
            if (vehicleFirstPerson)
            {
                viewCamera.transform.position = CurrentVehicle.CameraPosition;
                return;
            }
            Vector3 focus = CurrentVehicle.SeatPosition + Vector3.up * 1.65f;
            bool aircraft = CurrentVehicle is IPrototypeAircraftVehicle air && air.IsAircraft;
            float chaseDistance = aircraft ? 14f : 5.4f;
            float chaseHeight = aircraft ? 4.5f : 1.15f;
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

        private void ThrowEquipment()
        {
            if (throwableCount <= 0 || CurrentVehicle != null) return;
            throwableCount--;
            GameObject projectile = new GameObject();
            projectile.name = loadout.throwable == PrototypeThrowableId.Smoke ?
                "Smoke grenade" : "Fragmentation grenade";
            GameObject weaponVisual = infantryThirdPerson ? rifle : firstPersonWeapon;
            projectile.transform.position = weaponVisual.transform.TransformPoint(
                new Vector3(0f, .05f, .85f));
            SphereCollider grenadeCollider = projectile.AddComponent<SphereCollider>();
            grenadeCollider.radius = .13f;
            PrototypeEquipmentVisual.BuildThrowable(projectile.transform,
                loadout.throwable, runtime);
            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 target = InfantryAimPoint(viewCamera.transform.forward, 35f);
            body.linearVelocity = (target - projectile.transform.position).normalized *
                17f + Vector3.up * 5f;
            PrototypeCombatFeedback.Play(transform, PrototypeCombatSound.GrenadeThrow);
            StartCoroutine(Detonate(projectile, loadout.throwable));
        }

        private void FireRocket()
        {
            if (loadout.role != PrototypeInfantryClass.Engineer || rocketCount <= 0 ||
                CurrentVehicle != null) return;
            rocketCount--;
            GameObject launcher = PrototypeEquipmentVisual.BuildLauncher(
                infantryThirdPerson ? soldierVisual.WeaponMount :
                viewCamera.transform, runtime);
            launcher.transform.localPosition = infantryThirdPerson ?
                new Vector3(0f, .08f, .15f) : new Vector3(.27f, -.35f, .70f);
            if (!infantryThirdPerson) launcher.transform.localScale = Vector3.one * .55f;
            Destroy(launcher, .35f);
            GameObject weaponVisual = infantryThirdPerson ? rifle : firstPersonWeapon;
            Vector3 origin = weaponVisual.transform.TransformPoint(
                new Vector3(0f, .08f, 1.12f));
            Vector3 target = InfantryAimPoint(viewCamera.transform.forward, 160f);
            PrototypeBallisticProjectile.LaunchRocket(runtime, Team, transform,
                origin, (target - origin).normalized);
            SupportStatus = "ANTI-ARMOR ROCKET " + rocketCount + " LEFT";
            supportStatusUntil = Time.time + 2f;
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

        private IEnumerator Detonate(GameObject projectile, PrototypeThrowableId kind)
        {
            yield return new WaitForSeconds(1.8f);
            if (projectile == null) yield break;
            Vector3 center = projectile.transform.position;
            Destroy(projectile);
            if (kind == PrototypeThrowableId.Smoke)
            {
                PrototypeCombatFeedback.Smoke(runtime, center);
                yield break;
            }
            ApplyFragDamage(center);
            PrototypeCombatFeedback.Explosion(runtime, center, 5.5f,
                PrototypeCombatSound.FragExplosion);
        }

        private void ApplyFragDamage(Vector3 center)
        {
            const float blastRadius = 5.5f;
            Physics.SyncTransforms();
            var struckBots = new HashSet<PrototypeBot>();
            var struckPlayers = new HashSet<PrototypeRemotePlayer>();
            var struckVehicles = new HashSet<IPrototypeVehicle>();
            foreach (Collider hit in Physics.OverlapSphere(center, blastRadius))
            {
                bool closestSupported = hit is BoxCollider || hit is SphereCollider ||
                    hit is CapsuleCollider || hit is MeshCollider mesh && mesh.convex;
                float distance = Vector3.Distance(center,
                    closestSupported ? hit.ClosestPoint(center) :
                        hit.bounds.ClosestPoint(center));
                float falloff = Mathf.Clamp01(1f - distance / blastRadius);
                PrototypeBot bot = hit.GetComponentInParent<PrototypeBot>();
                if (bot != null && bot.Team != Team && struckBots.Add(bot))
                    bot.TakeDamage(95f * falloff,
                        new Vector2(center.x, center.z));
                PrototypeRemotePlayer remote = hit.GetComponentInParent<PrototypeRemotePlayer>();
                if (remote != null && remote.Team != Team && struckPlayers.Add(remote))
                    remote.TakeDamage(95f * falloff);
                IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(hit);
                if (vehicle != null && vehicle.Team != Team && struckVehicles.Add(vehicle))
                    vehicle.TakeDamage(55f * falloff);
            }
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
            SupportStatus = used ? "SUPPORT ACTIVE" : "NO VALID SUPPORT TARGET";
            if (used) PrototypeCombatFeedback.Support(runtime, transform.position,
                loadout.role);
            supportStatusUntil = now + 2f;
        }

        public void TakeDamage(float amount)
        {
            if (runtime == null || runtime.IsNetworkReplica || !Alive ||
                runtime.Match.Winner.HasValue) return;
            Health = Mathf.Max(0f, Health - kit.AbsorbDamage(amount));
            if (Alive) return;
            bool inVehicle = CurrentVehicle != null;
            ForceExitVehicle();
            if (inVehicle) { Eliminate(); return; }
            downedUntil = Time.time + 30f;
            rescueCalled = false;
            EnterDownedPose();
            runtime.Frontend.OpenDowned();
        }

        public void RequestRescue()
        {
            if (IsDowned) rescueCalled = true;
        }

        public void GiveUp(float dt)
        {
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
            if (revivingMedic != null && revivingMedic != medic &&
                revivingMedic.Alive && Time.time - lastRevivePulseAt <= .25f &&
                Vector2.Distance(MapPosition, revivingMedic.MapPosition) <= 3.5f)
                return false;
            if (revivingMedic != medic || Time.time - lastRevivePulseAt > .25f)
            {
                revivingMedic = medic;
                reviveStartedAt = Time.time;
            }
            lastRevivePulseAt = Time.time;
            if (Time.time - reviveStartedAt < ReviveDuration) return false;
            downedUntil = 0f;
            rescueCalled = false;
            revivingMedic = null;
            Health = 50f;
            if (controller != null) controller.enabled = true;
            LeaveDownedPose();
            SupportStatus = "REVIVED BY MEDIC";
            supportStatusUntil = Time.time + 2f;
            runtime.Frontend.Close();
            return true;
        }

        private void EnterDownedPose()
        {
            downedLookYaw = 0f;
            pitch = 0f;
            revivingMedic = null;
            reviveStartedAt = 0f;
            lastRevivePulseAt = 0f;
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
            revivingMedic = null;
            reviveStartedAt = 0f;
            lastRevivePulseAt = 0f;
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
            aiming = false;
            slideEndsAt = 0f;
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
            downedUntil = 0f;
            rescueCalled = false;
            revivingMedic = null;
            respawnAt = 0f;
            kit.ResetForSpawn();
            secondaryKit.ResetForSpawn();
            throwableCount = 2;
            rocketCount = 2;
            secondaryEquipped = false;
            if (viewCamera != null) BuildWeaponVisual();
            if (soldierVisual != null) soldierVisual.SetDowned(false);
            ApplyPerspectiveVisibility();
            if (controller != null) controller.enabled = true;
        }
    }
}
