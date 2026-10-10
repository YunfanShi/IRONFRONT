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
        private CharacterController controller;
        private Camera viewCamera;
        private AudioSource audioSource;
        private AudioClip shotSound;
        private GameObject rifle;
        private PrototypeRuntime runtime;
        private float pitch;
        private float vehicleLookYaw;
        private float verticalSpeed;
        private float respawnAt;
        private float downedUntil;
        private bool rescueCalled;
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
        public PrototypeInfantryClass Class => kit.Role;
        public PrototypeInfantryKit Kit => kit;
        public string SupportStatus { get; private set; } = "";
        public Vector2 MapPosition => CurrentVehicle != null ? CurrentVehicle.MapPosition :
            new Vector2(transform.position.x, transform.position.z);
        public Camera ViewCamera => viewCamera;
        public IPrototypeVehicle CurrentVehicle { get; private set; }
        public float RespawnRemaining => Alive ? 0f : Mathf.Max(0f, respawnAt - Time.time);
        public bool IsDowned => !Alive && downedUntil > Time.time;
        public float DownedRemaining => IsDowned ? downedUntil - Time.time : 0f;
        public bool RescueCalled => rescueCalled;

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

            var cameraObject = new GameObject("First Person Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            viewCamera = cameraObject.AddComponent<Camera>();
            viewCamera.fieldOfView = 76f;
            viewCamera.farClipPlane = 950f;
            viewCamera.backgroundColor = new Color(0.55f, 0.69f, 0.76f);
            cameraObject.AddComponent<AudioListener>();
            audioSource = gameObject.AddComponent<AudioSource>();
            shotSound = Resources.Load<AudioClip>("Audio/carbine");

            BuildWeaponVisual();
            Respawn();
        }

        private void BuildWeaponVisual()
        {
            if (rifle != null) Destroy(rifle);
            rifle = PrototypeWeaponVisual.Build(viewCamera.transform, ActiveWeapon.Id,
                runtime, true, Team);
            rifle.transform.localPosition = new Vector3(.37f, -.32f, .60f);
            rifle.transform.localScale = Vector3.one * .64f;
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
            if (viewCamera != null) BuildWeaponVisual();
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
            if (viewCamera != null) BuildWeaponVisual();
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
            Health = alive ? Mathf.Clamp(health, 0.01f, 100f) : 0f;
            downedUntil = !alive && downedRemaining > 0f ?
                Time.time + Mathf.Clamp(downedRemaining, 0f, 30f) : 0f;
            rescueCalled = !alive && rescueRequested;
            respawnAt = alive || downedUntil > 0f ? 0f :
                Time.time + Mathf.Max(0f, respawnRemaining);
            kit.ApplyNetworkAmmo(ammo, reserve);
            if (controller != null) controller.enabled = alive;
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
                if (!runtime.IsNetworkReplica && downedUntil > 0f)
                {
                    if (Input.GetKeyDown(KeyCode.H)) RequestRescue();
                    if (Input.GetKey(KeyCode.Space)) GiveUp(Time.deltaTime);
                    if (Time.time >= downedUntil) Eliminate();
                }
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }
            if (runtime.Match.Winner.HasValue) return;
            kit.Tick(Time.time);
            secondaryKit.Tick(Time.time);
            if (Time.time > supportStatusUntil) SupportStatus = "";

            if (CurrentVehicle != null)
            {
                if (Input.GetKeyDown(KeyCode.E)) { ForceExitVehicle(); return; }
                if (CurrentVehicle is PrototypeTransportVehicle transport)
                {
                    if (Input.GetKeyDown(KeyCode.F1)) transport.TrySetPlayerSeat(this, 0);
                    if (Input.GetKeyDown(KeyCode.F2)) transport.TrySetPlayerSeat(this, 1);
                }
                vehicleLookYaw = Mathf.Clamp(vehicleLookYaw +
                    Input.GetAxis("Mouse X") * runtime.MouseSensitivity, -135f, 135f);
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * runtime.MouseSensitivity, -35f, 55f);
                UpdateVehicleCamera();
                return;
            }

            transform.Rotate(0f, Input.GetAxis("Mouse X") * runtime.MouseSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * runtime.MouseSensitivity, -86f, 86f);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);
            bool sprint = Input.GetKey(KeyCode.LeftShift) && input.y > 0.1f;
            float speed = sprint ? SprintSpeed : WalkSpeed;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (!runtime.IsNetworkReplica && controller.isGrounded &&
                Input.GetKeyDown(KeyCode.Space)) verticalSpeed = 6.2f;
            verticalSpeed -= 22f * Time.deltaTime;
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
                float distance = Vector2.Distance(MapPosition, candidate.MapPosition);
                if (distance >= nearest ||
                    PrototypeLayout.LineBlocked(MapPosition, candidate.MapPosition, 0.1f)) continue;
                closest = candidate;
                nearest = distance;
            }
            if (closest == null) return;
            EnterVehicle(closest);
        }

        private bool EnterVehicle(IPrototypeVehicle closest)
        {
            if (!closest.TrySetDriver(this)) return false;
            CurrentVehicle = closest;
            controller.enabled = false;
            transform.position = closest.SeatPosition;
            transform.rotation = closest.VehicleTransform.rotation;
            verticalSpeed = 0f;
            pitch = 10f;
            vehicleLookYaw = 0f;
            rifle.SetActive(false);
            viewCamera.fieldOfView = 80f;
            UpdateVehicleCamera();
            return true;
        }

        private void UpdateVehicleCamera()
        {
            if (CurrentVehicle == null) return;
            transform.position = CurrentVehicle.SeatPosition;
            transform.rotation = CurrentVehicle.VehicleTransform.rotation;
            Quaternion look = Quaternion.Euler(pitch,
                CurrentVehicle.VehicleTransform.eulerAngles.y + vehicleLookYaw, 0f);
            viewCamera.transform.rotation = look;
            Vector3 seatEye = CurrentVehicle.SeatPosition + Vector3.up * 1.65f;
            viewCamera.transform.position = seatEye + Vector3.up * 1.15f -
                look * Vector3.forward * 5.4f;
        }

        public void ForceExitVehicle()
        {
            IPrototypeVehicle vehicle = CurrentVehicle;
            if (vehicle == null) return;
            Vector3 exit = FindSafeExit(vehicle);
            vehicle.RemoveDriver(this);
            CurrentVehicle = null;
            controller.enabled = false;
            transform.position = exit;
            transform.rotation = Quaternion.Euler(0f, vehicle.VehicleTransform.eulerAngles.y, 0f);
            pitch = 0f;
            vehicleLookYaw = 0f;
            verticalSpeed = 0f;
            viewCamera.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            viewCamera.transform.localRotation = Quaternion.identity;
            viewCamera.fieldOfView = 76f;
            rifle.SetActive(true);
            controller.enabled = true;
        }

        private Vector3 FindSafeExit(IPrototypeVehicle vehicle)
        {
            Physics.SyncTransforms();
            Vector3 proposed = vehicle.ExitPosition;
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
            if (!weaponKit.TryFire(Time.time)) return;
            if (shotSound != null) audioSource.PlayOneShot(shotSound, 0.3f);
            Vector3 origin = viewCamera.transform.position;
            Vector3 direction = (viewCamera.transform.forward +
                viewCamera.transform.right * Random.Range(-weaponKit.Weapon.HipSpread, weaponKit.Weapon.HipSpread) +
                viewCamera.transform.up * Random.Range(-weaponKit.Weapon.HipSpread, weaponKit.Weapon.HipSpread)).normalized;
            float range = weaponKit.Weapon.MaxRange;
            Vector3 end = origin + direction * range;
            // Bots move by Transform in Update; synchronize their colliders before this raycast.
            Physics.SyncTransforms();
            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, ~(1 << 2)))
            {
                end = hit.point;
                PrototypeBot bot = hit.collider.GetComponent<PrototypeBot>();
                if (bot != null && bot.Team != Team)
                    bot.TakeDamage(weaponKit.Weapon.DamageAtRange(hit.distance));
                else
                {
                    PrototypeRemotePlayer remote =
                        hit.collider.GetComponentInParent<PrototypeRemotePlayer>();
                    if (remote != null && remote.Team != Team)
                        remote.TakeDamage(weaponKit.Weapon.DamageAtRange(hit.distance));
                    else
                    {
                        IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(hit.collider);
                        if (vehicle != null && vehicle.Team != Team)
                            vehicle.TakeDamage(weaponKit.Weapon.DamageAtRange(hit.distance) * 0.55f);
                    }
                }
            }
            runtime.ShowTracer(origin + viewCamera.transform.right * 0.25f - viewCamera.transform.up * 0.15f,
                end, Team);
            if (weaponKit.Ammo == 0) weaponKit.StartReload(Time.time);
        }

        private void ThrowEquipment()
        {
            if (throwableCount <= 0 || CurrentVehicle != null) return;
            throwableCount--;
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = loadout.throwable == PrototypeThrowableId.Smoke ?
                "Smoke grenade" : "Fragmentation grenade";
            projectile.transform.position = viewCamera.transform.position +
                viewCamera.transform.forward * .75f;
            projectile.transform.localScale = Vector3.one * .20f;
            projectile.GetComponent<Renderer>().sharedMaterial = runtime.GunMaterial;
            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = viewCamera.transform.forward * 17f + Vector3.up * 5f;
            StartCoroutine(Detonate(projectile, loadout.throwable));
        }

        private void FireRocket()
        {
            if (loadout.role != PrototypeInfantryClass.Engineer || rocketCount <= 0 ||
                CurrentVehicle != null) return;
            rocketCount--;
            Vector3 origin = viewCamera.transform.position;
            Vector3 end = origin + viewCamera.transform.forward * 160f;
            if (Physics.Raycast(origin, viewCamera.transform.forward,
                out RaycastHit hit, 160f, ~(1 << 2))) end = hit.point;
            var struck = new HashSet<IPrototypeVehicle>();
            foreach (Collider nearby in Physics.OverlapSphere(end, 4.5f))
            {
                IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(nearby);
                if (vehicle != null && vehicle.Team != Team && struck.Add(vehicle))
                    vehicle.TakeDamage(210f);
            }
            runtime.ShowTracer(origin, end, Team);
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
        }

        private IEnumerator Detonate(GameObject projectile, PrototypeThrowableId kind)
        {
            yield return new WaitForSeconds(1.8f);
            if (projectile == null) yield break;
            Vector3 center = projectile.transform.position;
            Destroy(projectile);
            if (kind == PrototypeThrowableId.Smoke)
            {
                GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cloud.name = "Smoke cover";
                cloud.transform.position = center + Vector3.up * 1.4f;
                cloud.transform.localScale = new Vector3(5.5f, 3.2f, 5.5f);
                Destroy(cloud.GetComponent<Collider>());
                Material smoke = new Material(runtime.GlassMaterial);
                smoke.color = new Color(.52f, .59f, .59f, .72f);
                cloud.GetComponent<Renderer>().material = smoke;
                Destroy(cloud, 8f);
                Destroy(smoke, 8f);
                yield break;
            }
            var struckBots = new HashSet<PrototypeBot>();
            var struckPlayers = new HashSet<PrototypeRemotePlayer>();
            var struckVehicles = new HashSet<IPrototypeVehicle>();
            foreach (Collider hit in Physics.OverlapSphere(center, 5.5f))
            {
                PrototypeBot bot = hit.GetComponentInParent<PrototypeBot>();
                if (bot != null && bot.Team != Team && struckBots.Add(bot))
                    bot.TakeDamage(95f * (1f - Mathf.Clamp01(
                        Vector3.Distance(center, bot.transform.position) / 6f)));
                PrototypeRemotePlayer remote = hit.GetComponentInParent<PrototypeRemotePlayer>();
                if (remote != null && remote.Team != Team && struckPlayers.Add(remote))
                    remote.TakeDamage(80f);
                IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(hit);
                if (vehicle != null && vehicle.Team != Team && struckVehicles.Add(vehicle))
                    vehicle.TakeDamage(38f);
            }
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                runtime.ShowTracer(center, center + new Vector3(
                    Mathf.Cos(angle) * 3f, .5f, Mathf.Sin(angle) * 3f), Team);
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
            downedUntil = 0f;
            rescueCalled = false;
            Health = 50f;
            if (controller != null) controller.enabled = true;
            SupportStatus = "REVIVED BY MEDIC";
            supportStatusUntil = Time.time + 2f;
            runtime.Frontend.Close();
            return true;
        }

        private void Eliminate()
        {
            if (runtime == null || runtime.IsNetworkReplica || respawnAt > 0f) return;
            downedUntil = 0f;
            rescueCalled = false;
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
            if (vehicle != null) EnterVehicle(vehicle);
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
            if (controller != null) controller.enabled = false;
            transform.position = new Vector3(point.x,
                PrototypeLayout.HeightAt(point.x, point.y) + 0.3f, point.y);
            transform.rotation = Quaternion.Euler(0f,
                Team == PrototypeTeam.Blue ? 48f : 228f, 0f);
            pitch = 0f;
            verticalSpeed = 0f;
            Health = 100f;
            downedUntil = 0f;
            rescueCalled = false;
            respawnAt = 0f;
            kit.ResetForSpawn();
            secondaryKit.ResetForSpawn();
            throwableCount = 2;
            rocketCount = 2;
            secondaryEquipped = false;
            if (viewCamera != null) BuildWeaponVisual();
            if (controller != null) controller.enabled = true;
        }
    }
}
